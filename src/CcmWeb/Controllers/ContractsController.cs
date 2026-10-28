using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

[Authorize]
public class ContractsController(
    IContractService contracts,
    ILogger<ContractsController> logger) : Controller
{
    [HttpGet("/contracts")]
    public async Task<IActionResult> Index(
        string? q,
        string? status,
        int? staff,
        DateOnly? from,
        DateOnly? to,
        bool expiring = false,
        string? sort = null,
        bool desc = false,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default)
    {
        var filter = new ContractFilter
        {
            Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            Status = WorkConstants.ContractStatus.All.Contains(status ?? string.Empty) ? status : null,
            Staff = staff,
            From = from,
            To = to,
            Expiring = expiring,
            Sort = Utils.SanitiseSort(sort, ContractFilter.SortFields, ContractFilter.SortBySignDate),
            Desc = desc,
            Page = Utils.SanitisePage(page),
            PageSize = pageSize is null
                ? WorkConstants.Settings.DefaultPageSize
                : Utils.SanitisePageSize(pageSize),
        };

        try
        {
            var result = await contracts.SearchAsync(filter, ct);
            return View(new ContractListViewModel
            {
                Filter = filter,
                Rows = result.Rows,
                TotalRows = result.TotalRows,
                Staff = await contracts.GetStaffAsync(ct),
            });
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The contract list could not be loaded.");
            return View(new ContractListViewModel
            {
                Filter = filter,
                ErrorKey = ex.MessageKey,
                Staff = await contracts.GetStaffAsync(ct),
            });
        }
    }

    [HttpGet("/contracts/create")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}")]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        View(new ContractFormViewModel
        {
            Customers = await contracts.GetCustomersAsync(ct),
            Staff = await contracts.GetStaffAsync(ct),
            Products = await contracts.GetProductsAsync(ct),
            EmployeeId = User.GetEmployeeID(),
            SignDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
        });

    [HttpPost("/contracts/create")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}")]
    public async Task<IActionResult> Create(ContractFormViewModel model, CancellationToken ct)
    {
        // Parse the lines from the hidden JSON field.
        model.Lines = ContractRules.ParseLines(model.LinesJson);

        if (model.Lines.Count == 0)
        {
            ModelState.AddModelError("LinesJson", "Error_ContractHasNoLines");
        }
        else if (ContractRules.HasDuplicateProduct(model.Lines))
        {
            ModelState.AddModelError("LinesJson", "Error_ProductDuplicated");
        }

        if (!ModelState.IsValid)
        {
            await BackToFormAsync(model, ct);
            return View(model);
        }

        try
        {
            var id = await contracts.CreateAsync(model, ct);
            logger.LogInformation("Employee {EmployeeID} created contract {ContractID}.",
                User.GetEmployeeID(), id);
            TempData["MessageKey"] = "Contract_Created";
            TempData["MessageOk"] = true;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Creating a contract failed.");
            ModelState.AddModelError(string.Empty, ex.MessageKey);
            await BackToFormAsync(model, ct);
            return View(model);
        }
    }

    [HttpGet("/contracts/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var detail = await contracts.GetDetailAsync(id, ct);
        return detail is null ? NotFound() : View(detail);
    }

    [HttpPost("/contracts/{id:int}/sign")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}")]
    public async Task<IActionResult> Sign(int id, DateOnly? signDate, CancellationToken ct)
    {
        try
        {
            await contracts.SignAsync(id, signDate, ct);
            TempData["MessageKey"] = "Contract_Signed";
            TempData["MessageOk"] = true;
        }
        catch (BusinessException ex)
        {
            TempData["MessageKey"] = ex.MessageKey;
            TempData["MessageOk"] = false;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("/contracts/{id:int}/renew")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}")]
    public async Task<IActionResult> Renew(int id, DateOnly newEndDate, CancellationToken ct)
    {
        try
        {
            await contracts.RenewAsync(id, newEndDate, ct);
            TempData["MessageKey"] = "Contract_Renewed";
            TempData["MessageOk"] = true;
        }
        catch (BusinessException ex)
        {
            TempData["MessageKey"] = ex.MessageKey;
            TempData["MessageOk"] = false;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("/contracts/{id:int}/cancel")]
    [Authorize(Roles = $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}")]
    public async Task<IActionResult> Cancel(int id, string? reason, CancellationToken ct)
    {
        try
        {
            await contracts.CancelAsync(id, reason, ct);
            TempData["MessageKey"] = "Contract_Cancelled";
            TempData["MessageOk"] = true;
        }
        catch (BusinessException ex)
        {
            TempData["MessageKey"] = ex.MessageKey;
            TempData["MessageOk"] = false;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task BackToFormAsync(ContractFormViewModel model, CancellationToken ct)
    {
        model.Customers = await contracts.GetCustomersAsync(ct);
        model.Staff = await contracts.GetStaffAsync(ct);
        model.Products = await contracts.GetProductsAsync(ct);
    }

}
