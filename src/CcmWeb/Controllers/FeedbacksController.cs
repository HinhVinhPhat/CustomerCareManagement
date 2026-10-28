using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>
/// The feedback screens (design.md 6.11). Only the Manager and Support may open them; Sales has no
/// menu entry and a direct address is refused here, not merely hidden. Assign, status and note go
/// through the two procedures of the database.
/// </summary>
[Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Support}")]
public class FeedbacksController(
    IFeedbackService feedback,
    ISettingService settings,
    ILogger<FeedbacksController> logger) : Controller
{
    [HttpGet("/feedbacks")]
    public async Task<IActionResult> Index(
        string? q,
        string? status,
        byte? priority,
        string? type,
        bool overdue = false,
        string? sort = null,
        bool desc = false,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default)
    {
        var filter = new FeedbackFilter
        {
            Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            // No status means "open" - exactly what vw_OpenFeedback returns.
            Status = WorkConstants.FeedbackStatuses.All.Contains(status ?? string.Empty) ? status : null,
            Priority = priority is { } p && WorkConstants.FeedbackPriorities.All.Contains(p) ? p : null,
            Type = WorkConstants.FeedbackTypes.All.Contains(type ?? string.Empty) ? type : null,
            Overdue = overdue,
            Sort = Utils.SanitiseSort(sort, FeedbackFilter.SortFields, FeedbackFilter.SortByPriority),
            Desc = desc,
            Page = Utils.SanitisePage(page),
            PageSize = pageSize is null
                ? await settings.GetPageSizeAsync(ct)
                : Utils.SanitisePageSize(pageSize)
        };

        try
        {
            var result = await feedback.SearchAsync(filter, ct);

            return View(new FeedbackListViewModel
            {
                Filter = filter,
                Rows = result.Rows,
                TotalRows = result.TotalRows,
                Counters = await feedback.GetCountersAsync(ct),
                Staff = await feedback.GetStaffAsync(ct)
            });
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The feedback list could not be loaded.");

            return View(new FeedbackListViewModel { Filter = filter, ErrorKey = ex.MessageKey });
        }
    }

    [HttpGet("/feedbacks/create")]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        View(new FeedbackCreateViewModel { Customers = await feedback.GetCustomersAsync(ct) });

    [HttpPost("/feedbacks/create")]
    public async Task<IActionResult> Create(FeedbackCreateViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            model.Customers = await feedback.GetCustomersAsync(ct);
            return View(model);
        }

        try
        {
            var feedbackId = await feedback.CreateAsync(model, ct);
            logger.LogInformation("Employee {EmployeeID} created feedback {FeedbackID}.",
                User.GetEmployeeID(), feedbackId);

            TempData["MessageKey"] = "Feedback_Created";
            TempData["MessageOk"] = true;

            return RedirectToAction(nameof(Details), new { id = feedbackId });
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Creating a feedback item failed.");
            AddBusinessError(ex.MessageKey);
            model.Customers = await feedback.GetCustomersAsync(ct);
            return View(model);
        }
    }

    [HttpGet("/feedbacks/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var model = await feedback.GetDetailAsync(id, ct);

        return model is null ? NotFound() : View(model);
    }

    [HttpPost("/feedbacks/{id:int}/assign")]
    public async Task<IActionResult> Assign(
        int id, int employeeId, string? returnUrl, CancellationToken ct)
    {
        if (employeeId <= 0)
        {
            TempData["MessageKey"] = "Error_EmployeeInvalid";
            TempData["MessageOk"] = false;

            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await feedback.AssignAsync(id, employeeId, ct);
            logger.LogInformation("Employee {EmployeeID} assigned feedback {FeedbackID} to {Assignee}.",
                User.GetEmployeeID(), id, employeeId);

            TempData["MessageKey"] = "Feedback_Assigned";
            TempData["MessageOk"] = true;
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Assigning feedback {FeedbackID} failed.", id);
            TempData["MessageKey"] = ex.MessageKey;
            TempData["MessageOk"] = false;

            return RedirectToAction(nameof(Details), new { id });
        }

        return RedirectLocal(returnUrl, Url.Action(nameof(Details), new { id })!);
    }

    [HttpPost("/feedbacks/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id, string? status, string? note, CancellationToken ct)
    {
        try
        {
            await feedback.UpdateStatusAsync(id, status ?? string.Empty, note, ct);
            logger.LogInformation("Employee {EmployeeID} changed feedback {FeedbackID} to {Status}.",
                User.GetEmployeeID(), id, status);

            TempData["MessageKey"] = "Feedback_StatusUpdated";
            TempData["MessageOk"] = true;
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Changing feedback {FeedbackID} failed.", id);
            TempData["MessageKey"] = ex.MessageKey;
            TempData["MessageOk"] = false;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("/feedbacks/{id:int}/note")]
    public async Task<IActionResult> AddNote(int id, string? note, CancellationToken ct)
    {
        try
        {
            await feedback.AppendNoteAsync(id, note ?? string.Empty, ct);
            logger.LogInformation("Employee {EmployeeID} added a note to feedback {FeedbackID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Feedback_NoteAdded";
            TempData["MessageOk"] = true;
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Adding a note to feedback {FeedbackID} failed.", id);
            TempData["MessageKey"] = ex.MessageKey;
            TempData["MessageOk"] = false;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private void AddBusinessError(string messageKey) =>
        ModelState.AddModelError(
            messageKey switch
            {
                "Validation_FeedbackDueDateInvalid" or "Error_FeedbackDueDateInvalid"
                    => nameof(FeedbackCreateViewModel.DueDate),
                "Validation_FeedbackPriorityInvalid" => nameof(FeedbackCreateViewModel.Priority),
                "Validation_FeedbackTypeInvalid" => nameof(FeedbackCreateViewModel.FeedbackType),
                "Error_CustomerNotFound" or "Error_CustomerNotActive"
                    => nameof(FeedbackCreateViewModel.CustomerId),
                _ => string.Empty
            },
            messageKey);

    /// <summary>Only a local address is accepted, so a crafted returnUrl cannot leave the site.</summary>
    private IActionResult RedirectLocal(string? returnUrl, string fallback) =>
        returnUrl is not null && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : Redirect(fallback);
}
