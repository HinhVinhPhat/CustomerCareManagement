using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>
/// The care screens (design.md 6.10): the schedule tab and the care log. Everyone may look; only the
/// Manager and Support may add a care record, checked here as well as in the view. The care date is
/// the server's today and the employee comes from the claim.
/// </summary>
[Authorize]
public class CareController(
    ICareService care,
    ISettingService settings,
    ILogger<CareController> logger) : Controller
{
    private const string ScheduleTab = "schedule";
    private const string LogTab = "log";

    [HttpGet("/care")]
    public async Task<IActionResult> Index(
        string? tab,
        string? q,
        int? staff,
        string? type,
        DateOnly? from,
        DateOnly? to,
        string? sort = null,
        bool desc = true,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default)
    {
        var openTab = tab == LogTab ? LogTab : ScheduleTab;

        var schedule = new CareScheduleViewModel();
        string? errorKey = null;

        try
        {
            schedule = new CareScheduleViewModel { Rows = await care.GetScheduleAsync(ct) };
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The care schedule could not be loaded.");
            errorKey = ex.MessageKey;
        }

        if (openTab != LogTab)
        {
            return View(new CarePageViewModel
            {
                Tab = ScheduleTab,
                Schedule = schedule,
                ErrorKey = errorKey
            });
        }

        var filter = new CareFilter
        {
            Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            Staff = staff,
            CareType = WorkConstants.CareTypes.All.Contains(type ?? string.Empty) ? type : null,
            From = from,
            To = to,
            Sort = Utils.SanitiseSort(sort, CareFilter.SortFields, CareFilter.SortByCareDate),
            Desc = desc,
            Page = Utils.SanitisePage(page),
            PageSize = pageSize is null
                ? await settings.GetPageSizeAsync(ct)
                : Utils.SanitisePageSize(pageSize)
        };

        return View(new CarePageViewModel
        {
            Tab = LogTab,
            Schedule = schedule,
            ErrorKey = errorKey,
            Log = await LogViewAsync(filter, ct)
        });
    }

    [HttpGet("/care/create")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Support}")]
    public async Task<IActionResult> Create(int? customerId, string? returnUrl, CancellationToken ct) =>
        View(new CareFormViewModel
        {
            CustomerId = customerId,
            ReturnUrl = returnUrl,
            Customers = await care.GetCustomersAsync(ct)
        });

    [HttpPost("/care/create")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Support}")]
    public async Task<IActionResult> Create(CareFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            model.Customers = await care.GetCustomersAsync(ct);
            return View(model);
        }

        try
        {
            var careId = await care.CreateAsync(model, User.GetEmployeeID() ?? -1, ct);
            logger.LogInformation("Employee {EmployeeID} recorded care {CareID}.",
                User.GetEmployeeID(), careId);

            TempData["MessageKey"] = "Care_Created";
            TempData["MessageOk"] = true;

            return RedirectLocal(model.ReturnUrl, "/care?tab=log");
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Recording a care visit failed.");
            AddBusinessError(ex.MessageKey);
            model.Customers = await care.GetCustomersAsync(ct);
            return View(model);
        }
    }

    private async Task<CareLogViewModel> LogViewAsync(CareFilter filter, CancellationToken ct)
    {
        try
        {
            var result = await care.SearchLogAsync(filter, ct);

            return new CareLogViewModel
            {
                Filter = filter,
                Rows = result.Rows,
                TotalRows = result.TotalRows,
                Staff = await care.GetStaffAsync(ct)
            };
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The care log could not be loaded.");

            return new CareLogViewModel { Filter = filter, ErrorKey = ex.MessageKey };
        }
    }

    private void AddBusinessError(string messageKey) =>
        ModelState.AddModelError(
            messageKey switch
            {
                "Error_NextAppointmentBeforeCare" => nameof(CareFormViewModel.NextAppointmentDate),
                "Error_SurveyScoreOnly" => nameof(CareFormViewModel.SatisfactionScore),
                "Error_CustomerNotFound" or "Error_CustomerNotActive" => nameof(CareFormViewModel.CustomerId),
                _ => string.Empty
            },
            messageKey);

    /// <summary>Only a local address is accepted, so a crafted returnUrl cannot leave the site.</summary>
    private IActionResult RedirectLocal(string? returnUrl, string fallback) =>
        returnUrl is not null && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : Redirect(fallback);
}
