using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>
/// The customer screens (design.md 6.3-6.5). Manager and Sales may look and change; Support may only
/// look - enforced here with <see cref="AuthorizeAttribute"/>, not by hiding a button. The controller
/// checks the input, calls the service and chooses a view; no SQL lives here.
/// </summary>
[Authorize]
public class CustomersController(
    ICustomerService customers,
    ISettingService settings,
    ILogger<CustomersController> logger) : Controller
{
    /// <summary>
    /// The customer list. Every filter, the sort and the page arrive in the query string and are
    /// written back into the links, so the address can be copied into another tab (design.md 3.4).
    /// </summary>
    [HttpGet("/customers")]
    public async Task<IActionResult> Index(
        string? q,
        string? tier,
        int? owner,
        bool mine = false,
        bool inactive = false,
        string? sort = null,
        bool desc = false,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default)
    {
        var filter = new CustomerFilter
        {
            Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            // An unknown tier would leave the page showing "all tiers" over an empty list, so it is
            // dropped instead of passed on.
            Tier = WorkConstants.CustomerTiers.All.Contains(tier ?? string.Empty) ? tier : null,
            Owner = owner,
            OnlyMine = mine,
            IncludeInactive = inactive,
            Sort = Utils.SanitiseSort(sort, CustomerFilter.SortFields, CustomerFilter.SortByName),
            Desc = desc,
            Page = Utils.SanitisePage(page),
            PageSize = pageSize is null
                ? await settings.GetPageSizeAsync(ct)
                : Utils.SanitisePageSize(pageSize)
        };

        // "Only my customers" is the employee from the claim. A value in the address is ignored, so
        // editing the query string cannot show somebody else's book.
        var ownerEmployeeId = filter.OnlyMine ? User.GetEmployeeID() ?? -1 : filter.Owner;

        try
        {
            var result = await customers.SearchAsync(filter, ownerEmployeeId, ct);

            return View(new CustomerListViewModel
            {
                Filter = filter,
                Rows = result.Rows,
                TotalRows = result.TotalRows,
                Owners = await customers.GetOwnersAsync(ct)
            });
        }
        catch (BusinessException ex)
        {
            // A list that could not be read is not an empty list: the page says so instead of
            // showing "0 customers".
            logger.LogWarning(ex, "The customer list could not be loaded.");

            return View(new CustomerListViewModel { Filter = filter, ErrorKey = ex.MessageKey });
        }
    }

    [HttpGet("/customers/suggest")]
    [Produces("application/json")]
    public async Task<IActionResult> Suggest(string? q, CancellationToken ct)
    {
        // Đọc thuần túy nên không cần anti-forgery; chỉ ba trường an toàn rời khỏi máy chủ.
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < WorkConstants.Search.MinKeywordLength)
        {
            return Json(Array.Empty<CustomerSuggestion>());
        }

        try
        {
            return Json(await customers.SuggestAsync(q, WorkConstants.Search.SuggestionCount, ct));
        }
        catch (BusinessException ex)
        {
            // Ô gợi ý không được làm hỏng trang đang mở: trả danh sách rỗng và ghi log.
            logger.LogWarning(ex, "The quick search failed.");
            return Json(Array.Empty<CustomerSuggestion>());
        }
    }

    [HttpGet("/customers/create")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}")]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        View(new CustomerFormViewModel
        {
            Owners = await customers.GetOwnersAsync(ct),
            // Whoever adds the customer owns it until somebody changes the owner.
            OwnerEmployeeId = User.GetEmployeeID()
        });

    [HttpPost("/customers/create")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}")]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await BackToFormAsync(model, ct);
        }

        try
        {
            var customerId = await customers.CreateAsync(model, ct);
            logger.LogInformation("Employee {EmployeeID} created customer {CustomerID}.",
                User.GetEmployeeID(), customerId);

            return RedirectToDetails(customerId, "Customer_Created");
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Creating a customer failed.");
            AddBusinessError(ex.MessageKey);
            return await BackToFormAsync(model, ct);
        }
    }

    /// <summary>The tabs of the detail page. The open one lives in the address (?tab=contracts).</summary>
    private static readonly string[] Tabs = ["overview", "contracts", "history", "feedback"];

    [HttpGet("/customers/{id:int}")]
    public async Task<IActionResult> Details(int id, string? tab = null, CancellationToken ct = default)
    {
        var customer = await customers.GetDetailAsync(id, ct);

        if (customer is null)
        {
            // A missing customer is a 404; the styled error page arrives in M07.
            return NotFound();
        }

        ViewData["Tab"] = tab is not null && Tabs.Contains(tab) ? tab : Tabs[0];

        return View(customer);
    }

    [HttpGet("/customers/{id:int}/edit")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var model = await customers.GetForEditAsync(id, ct);

        return model is null ? NotFound() : View(model);
    }

    [HttpPost("/customers/{id:int}/edit")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}")]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model, CancellationToken ct)
    {
        // The id comes from the address, never from the form, so a crafted field cannot move the
        // change onto another customer.
        model.CustomerId = id;

        if (!ModelState.IsValid)
        {
            return await BackToFormAsync(model, ct);
        }

        try
        {
            await customers.UpdateAsync(id, model, ct);
            logger.LogInformation("Employee {EmployeeID} updated customer {CustomerID}.",
                User.GetEmployeeID(), id);

            return RedirectToDetails(id, "Customer_Updated");
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Updating customer {CustomerID} failed.", id);
            AddBusinessError(ex.MessageKey);
            return await BackToFormAsync(model, ct);
        }
    }

    /// <summary>
    /// Deactivate or reactivate. A dangerous action, so the page asks for confirmation first and this
    /// action only accepts a POST; the soft delete itself is <c>sp_SetCustomerStatus</c>.
    /// </summary>
    [HttpPost("/customers/{id:int}/status")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}")]
    public async Task<IActionResult> SetStatus(int id, bool isActive, CancellationToken ct)
    {
        try
        {
            await customers.SetStatusAsync(id, isActive, ct);
            logger.LogInformation("Employee {EmployeeID} set customer {CustomerID} to IsActive = {IsActive}.",
                User.GetEmployeeID(), id, isActive);

            TempData["MessageKey"] = isActive ? "Customer_Reactivated" : "Customer_Deactivated";
            TempData["MessageOk"] = true;
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Changing the status of customer {CustomerID} failed.", id);
            TempData["MessageKey"] = ex.MessageKey;
            TempData["MessageOk"] = false;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>The form again, with the owner dropdown and the entered values still in place.</summary>
    private async Task<IActionResult> BackToFormAsync(CustomerFormViewModel model, CancellationToken ct)
    {
        model.Owners = await customers.GetOwnersAsync(ct);

        return View(model);
    }

    private IActionResult RedirectToDetails(int customerId, string messageKey)
    {
        TempData["MessageKey"] = messageKey;
        TempData["MessageOk"] = true;

        return RedirectToAction(nameof(Details), new { id = customerId });
    }

    /// <summary>
    /// Puts a message that came from the database next to the field the user has to change, instead
    /// of in a general error box. The controller still carries the resource KEY: the
    /// <see cref="ModelStateLocalizationFilter"/> renders it in the language being read.
    /// </summary>
    private void AddBusinessError(string messageKey) =>
        ModelState.AddModelError(
            messageKey switch
            {
                "Error_CustomerEmailTaken" or "Error_CustomerEmailInvalid" or "Error_CustomerNeedsContact"
                    => nameof(CustomerFormViewModel.Email),
                "Error_CustomerPhoneInvalid" => nameof(CustomerFormViewModel.Phone),
                "Error_CustomerBirthDateInvalid" or "Error_BusinessCustomerNoGender"
                    => nameof(CustomerFormViewModel.DateOfBirth),
                _ => string.Empty
            },
            messageKey);
}
