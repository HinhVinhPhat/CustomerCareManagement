using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>
/// The product catalogue (design.md 6.13). Every role reads it - Sales and Support need the prices
/// when they talk to a customer - but only a Manager changes it.
/// </summary>
[Authorize]
public class AdminProductsController(
    IProductService products,
    ISettingService settings,
    ILogger<AdminProductsController> logger) : Controller
{
    [HttpGet("/admin/products")]
    public async Task<IActionResult> Index(
        string? q,
        string? type,
        string? sort = null,
        bool desc = false,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default)
    {
        var filter = new ProductFilter
        {
            Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            // An unknown type would show an empty list over the whole catalogue, so it is dropped.
            Type = WorkConstants.ProductTypes.All.Contains(type ?? string.Empty) ? type : null,
            Sort = Utils.SanitiseSort(sort, ProductFilter.SortFields, ProductFilter.SortByName),
            Desc = desc,
            Page = Utils.SanitisePage(page),
            PageSize = pageSize is null
                ? await settings.GetPageSizeAsync(ct)
                : Utils.SanitisePageSize(pageSize)
        };

        try
        {
            var result = await products.SearchAsync(filter, ct);

            return AdminView("Products", new ProductListViewModel
            {
                Filter = filter,
                Rows = result.Rows,
                TotalRows = result.TotalRows,
                CanEdit = User.IsInRole(WorkConstants.Roles.Manager)
            });
        }
        catch (BusinessException ex)
        {
            // A list that could not be read is not an empty list.
            logger.LogWarning(ex, "The product list could not be loaded.");

            return AdminView("Products", new ProductListViewModel
            {
                Filter = filter,
                CanEdit = User.IsInRole(WorkConstants.Roles.Manager),
                ErrorKey = ex.MessageKey
            });
        }
    }

    [HttpGet("/admin/products/create")]
    [Authorize(Roles = WorkConstants.Roles.Manager)]
    public IActionResult Create() => AdminView("ProductCreate", new ProductFormViewModel());

    [HttpPost("/admin/products/create")]
    [Authorize(Roles = WorkConstants.Roles.Manager)]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return AdminView("ProductCreate", model);
        }

        try
        {
            var id = await products.CreateAsync(model, ct);
            logger.LogInformation("Employee {EmployeeID} created product {ProductID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Product_Created";
            TempData["MessageOk"] = true;

            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Creating a product failed.");
            AddBusinessError(ex.MessageKey);
            return AdminView("ProductCreate", model);
        }
    }

    [HttpGet("/admin/products/{id:int}/edit")]
    [Authorize(Roles = WorkConstants.Roles.Manager)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await products.GetAsync(id, ct);

        if (form is null)
        {
            return NotFound();
        }

        return AdminView("ProductEdit", form);
    }

    [HttpPost("/admin/products/{id:int}/edit")]
    [Authorize(Roles = WorkConstants.Roles.Manager)]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken ct)
    {
        model = CopyWithId(model, id);

        if (!ModelState.IsValid)
        {
            return AdminView("ProductEdit", model);
        }

        try
        {
            await products.UpdateAsync(id, model, ct);
            logger.LogInformation("Employee {EmployeeID} updated product {ProductID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Product_Saved";
            TempData["MessageOk"] = true;

            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Updating product {ProductID} failed.", id);
            AddBusinessError(ex.MessageKey);
            return AdminView("ProductEdit", model);
        }
    }

    [HttpPost("/admin/products/{id:int}/delete")]
    [Authorize(Roles = WorkConstants.Roles.Manager)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await products.DeleteAsync(id, ct);
            logger.LogInformation("Employee {EmployeeID} deleted product {ProductID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Product_Deleted";
            TempData["MessageOk"] = true;
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Deleting product {ProductID} failed.", id);
            TempData["MessageKey"] = ex.MessageKey;
            TempData["MessageOk"] = false;
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// The four administration screens live together under <c>Views/Admin</c>, next to the partials
    /// they share, so the view is named by its path instead of by the controller folder.
    /// </summary>
    private ViewResult AdminView(string name, object model) =>
        View($"~/Views/Admin/{name}.cshtml", model);

    /// <summary>The id comes from the route, so a posted hidden field cannot move the edit elsewhere.</summary>
    private static ProductFormViewModel CopyWithId(ProductFormViewModel model, int id) => new()
    {
        ProductId = id,
        ProductName = model.ProductName,
        ProductType = model.ProductType,
        UnitPrice = model.UnitPrice,
        Description = model.Description
    };

    /// <summary>Puts the message next to the field that caused it; the filter renders the key.</summary>
    private void AddBusinessError(string messageKey) =>
        ModelState.AddModelError(
            messageKey switch
            {
                "Error_ProductNameTaken" or "Error_Required" => nameof(ProductFormViewModel.ProductName),
                "Error_ProductTypeInvalid" => nameof(ProductFormViewModel.ProductType),
                "Error_ProductPriceInvalid" => nameof(ProductFormViewModel.UnitPrice),
                "Error_TooLong" => nameof(ProductFormViewModel.ProductName),
                _ => string.Empty
            },
            messageKey);
}
