using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>
/// The contact log (design.md 6.9). All three roles may look and record a contact; a contact is
/// history and has no edit or delete action. The employee and the time come from the claim and the
/// server, never from the form.
/// </summary>
[Authorize]
public class ContactsController(
    IContactService contacts,
    ISettingService settings,
    ILogger<ContactsController> logger) : Controller
{
    [HttpGet("/contacts")]
    public async Task<IActionResult> Index(
        string? q,
        int? staff,
        string? channel,
        DateOnly? from,
        DateOnly? to,
        string? sort = null,
        bool desc = true,
        int? page = null,
        int? pageSize = null,
        bool create = false,
        int? customerId = null,
        string? returnUrl = null,
        CancellationToken ct = default)
    {
        var filter = new ContactFilter
        {
            Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            Staff = staff,
            // An unknown channel would show an empty list over the whole log, so it is dropped.
            Channel = WorkConstants.ContactChannels.All.Contains(channel ?? string.Empty) ? channel : null,
            From = from,
            To = to,
            Sort = Utils.SanitiseSort(sort, ContactFilter.SortFields, ContactFilter.SortByTime),
            Desc = desc,
            Page = Utils.SanitisePage(page),
            PageSize = pageSize is null
                ? await settings.GetPageSizeAsync(ct)
                : Utils.SanitisePageSize(pageSize)
        };

        var form = new ContactFormViewModel
        {
            CustomerId = customerId,
            ReturnUrl = returnUrl
        };

        return await ListViewAsync(filter, form, create, ct);
    }

    [HttpPost("/contacts")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales},{WorkConstants.Roles.Support}")]
    public async Task<IActionResult> Create(ContactFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await ListViewAsync(DefaultFilter(), model, openDialog: true, ct);
        }

        try
        {
            var contactId = await contacts.CreateAsync(model, User.GetEmployeeID() ?? -1, ct);
            logger.LogInformation("Employee {EmployeeID} recorded contact {ContactID}.",
                User.GetEmployeeID(), contactId);

            TempData["MessageKey"] = "Contact_Recorded";
            TempData["MessageOk"] = true;

            return RedirectLocal(model.ReturnUrl, "/contacts");
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Recording a contact failed.");
            AddBusinessError(ex.MessageKey);
            return await ListViewAsync(DefaultFilter(), model, openDialog: true, ct);
        }
    }

    /// <summary>
    /// The list plus the dialog. The dialog is rendered on the same page so a failed POST comes back
    /// with the typed values and the error next to the field.
    /// </summary>
    private async Task<IActionResult> ListViewAsync(
        ContactFilter filter, ContactFormViewModel form, bool openDialog, CancellationToken ct)
    {
        try
        {
            var result = await contacts.SearchAsync(filter, ct);
            var customers = await contacts.GetCustomersAsync(ct);

            form.Customers = customers;

            return View(nameof(Index), new ContactListViewModel
            {
                Filter = filter,
                Rows = result.Rows,
                TotalRows = result.TotalRows,
                Staff = await contacts.GetStaffAsync(ct),
                Customers = customers,
                Form = form,
                OpenDialog = openDialog
            });
        }
        catch (BusinessException ex)
        {
            // A list that could not be read is not an empty list.
            logger.LogWarning(ex, "The contact list could not be loaded.");

            form.Customers = [];
            return View(nameof(Index), new ContactListViewModel
            {
                Filter = filter,
                Form = form,
                OpenDialog = openDialog,
                ErrorKey = ex.MessageKey
            });
        }
    }

    private static ContactFilter DefaultFilter() => new();

    /// <summary>
    /// Puts the message next to the field that caused it. The controller carries the resource KEY;
    /// the localization filter renders it in the language being read.
    /// </summary>
    private void AddBusinessError(string messageKey) =>
        ModelState.AddModelError(
            messageKey switch
            {
                "Error_CustomerNotFound" or "Error_CustomerNotActive" => nameof(ContactFormViewModel.CustomerId),
                _ => string.Empty
            },
            messageKey);

    /// <summary>Only a local address is accepted, so a crafted returnUrl cannot leave the site.</summary>
    private IActionResult RedirectLocal(string? returnUrl, string fallback) =>
        returnUrl is not null && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : Redirect(fallback);
}
