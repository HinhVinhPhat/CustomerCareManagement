using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>
/// Employees and departments (design.md 6.13). A person is edited, never deleted: contracts, care,
/// feedback and the audit log point at them, so the way to take someone out of the system is to lock
/// their account on the account screen.
/// </summary>
[Authorize(Roles = WorkConstants.Roles.Manager)]
public class AdminEmployeesController(
    IEmployeeService employees,
    ISettingService settings,
    ILogger<AdminEmployeesController> logger) : Controller
{
    [HttpGet("/admin/employees")]
    public async Task<IActionResult> Index(
        string? q,
        int? department,
        string? position,
        string? sort = null,
        bool desc = false,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default)
    {
        var filter = new EmployeeFilter
        {
            Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            Department = department is > 0 ? department : null,
            Position = WorkConstants.Positions.All.Contains(position ?? string.Empty) ? position : null,
            Sort = Utils.SanitiseSort(sort, EmployeeFilter.SortFields, EmployeeFilter.SortByName),
            Desc = desc,
            Page = Utils.SanitisePage(page),
            PageSize = pageSize is null
                ? await settings.GetPageSizeAsync(ct)
                : Utils.SanitisePageSize(pageSize)
        };

        var model = new EmployeeListViewModel { Filter = filter };

        try
        {
            var result = await employees.SearchAsync(filter, ct);
            model = new EmployeeListViewModel
            {
                Filter = filter,
                Rows = result.Rows,
                TotalRows = result.TotalRows
            };
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The employee list could not be loaded.");
            model = new EmployeeListViewModel { Filter = filter, ErrorKey = ex.MessageKey };
        }

        try
        {
            var departments = await employees.GetDepartmentsAsync(ct);
            var rows = await employees.GetDepartmentRowsAsync(ct);

            return AdminView("Employees", new EmployeeListViewModel
            {
                Filter = model.Filter,
                Rows = model.Rows,
                TotalRows = model.TotalRows,
                ErrorKey = model.ErrorKey,
                Departments = departments,
                DepartmentRows = rows
            });
        }
        catch (BusinessException ex)
        {
            // The department card fails on its own; the list above it still shows.
            logger.LogWarning(ex, "The department card could not be loaded.");

            return AdminView("Employees", new EmployeeListViewModel
            {
                Filter = model.Filter,
                Rows = model.Rows,
                TotalRows = model.TotalRows,
                ErrorKey = model.ErrorKey,
                DepartmentErrorKey = ex.MessageKey
            });
        }
    }

    [HttpGet("/admin/employees/create")]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        AdminView("EmployeeCreate", new EmployeeFormViewModel
        {
            Departments = await employees.GetDepartmentsAsync(ct)
        });

    [HttpPost("/admin/employees/create")]
    public async Task<IActionResult> Create(EmployeeFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await FormViewAsync("EmployeeCreate", model, ct);
        }

        try
        {
            var id = await employees.CreateAsync(model, ct);
            logger.LogInformation("Employee {EmployeeID} created employee {NewEmployeeID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Employee_Created";
            TempData["MessageOk"] = true;

            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Creating an employee failed.");
            AddBusinessError(ex.MessageKey);
            return await FormViewAsync("EmployeeCreate", model, ct);
        }
    }

    [HttpGet("/admin/employees/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await employees.GetAsync(id, ct);

        if (form is null)
        {
            return NotFound();
        }

        return AdminView("EmployeeEdit", form);
    }

    [HttpPost("/admin/employees/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, EmployeeFormViewModel model, CancellationToken ct)
    {
        model = CopyWithId(model, id);

        if (!ModelState.IsValid)
        {
            return await FormViewAsync("EmployeeEdit", model, ct);
        }

        try
        {
            await employees.UpdateAsync(id, model, ct);
            logger.LogInformation("Employee {EmployeeID} updated employee {TargetEmployeeID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Employee_Saved";
            TempData["MessageOk"] = true;

            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Updating employee {EmployeeID} failed.", id);
            AddBusinessError(ex.MessageKey);
            return await FormViewAsync("EmployeeEdit", model, ct);
        }
    }

    [HttpPost("/admin/employees/departments")]
    public async Task<IActionResult> CreateDepartment(DepartmentFormModel model, CancellationToken ct)
    {
        try
        {
            var id = await employees.CreateDepartmentAsync(model, ct);
            logger.LogInformation("Employee {EmployeeID} created department {DepartmentID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Department_Created";
            TempData["MessageOk"] = true;
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Creating a department failed.");
            TempData["MessageKey"] = ex.MessageKey;
            TempData["MessageOk"] = false;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/admin/employees/departments/{id:int}/rename")]
    public async Task<IActionResult> RenameDepartment(int id, DepartmentFormModel model, CancellationToken ct)
    {
        try
        {
            await employees.RenameDepartmentAsync(id, model, ct);
            logger.LogInformation("Employee {EmployeeID} renamed department {DepartmentID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Department_Renamed";
            TempData["MessageOk"] = true;
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Renaming department {DepartmentID} failed.", id);
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

    /// <summary>Rebuilds the form with the department dropdown, keeping what the Manager typed.</summary>
    private async Task<IActionResult> FormViewAsync(string viewName, EmployeeFormViewModel model, CancellationToken ct)
    {
        model.Departments = await employees.GetDepartmentsAsync(ct);
        return AdminView(viewName, model);
    }

    /// <summary>The id comes from the route, so a posted hidden field cannot move the edit elsewhere.</summary>
    private static EmployeeFormViewModel CopyWithId(EmployeeFormViewModel model, int id) => new()
    {
        EmployeeId = id,
        FullName = model.FullName,
        Email = model.Email,
        Phone = model.Phone,
        Gender = model.Gender,
        Position = model.Position,
        DepartmentId = model.DepartmentId
    };

    private void AddBusinessError(string messageKey) =>
        ModelState.AddModelError(
            messageKey switch
            {
                "Error_EmployeeEmailTaken" or "Error_EmployeeEmailInvalid"
                    => nameof(EmployeeFormViewModel.Email),
                "Error_EmployeePhoneTaken" or "Error_EmployeePhoneInvalid"
                    => nameof(EmployeeFormViewModel.Phone),
                "Error_EmployeeGenderInvalid" => nameof(EmployeeFormViewModel.Gender),
                "Error_EmployeePositionInvalid" or "Error_DepartmentHasManager"
                    => nameof(EmployeeFormViewModel.Position),
                "Error_DepartmentNotFound" => nameof(EmployeeFormViewModel.DepartmentId),
                "Error_TooLong" => nameof(EmployeeFormViewModel.FullName),
                _ => string.Empty
            },
            messageKey);
}
