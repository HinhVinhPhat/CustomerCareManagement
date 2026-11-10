using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>The entrance of the administration area: the product catalogue, the first page of the group.</summary>
[Authorize(Roles = WorkConstants.Roles.Manager)]
public class AdminController : Controller
{
    [HttpGet("/admin")]
    public IActionResult Index() => RedirectToAction(nameof(AdminProductsController.Index), "AdminProducts");
}

/// <summary>
/// The system audit log (design.md 6.13). Read-only: the rows are written by the database triggers,
/// and the screen may only look at them.
/// </summary>
[Authorize(Roles = WorkConstants.Roles.Manager)]
public class AdminAuditLogsController(
    IAuditLogService auditLogs,
    IEmployeeService employees,
    ISettingService settings,
    ILogger<AdminAuditLogsController> logger) : Controller
{
    [HttpGet("/admin/audit-logs")]
    public async Task<IActionResult> Index(
        int? employee,
        string? table,
        string? action,
        DateOnly? from,
        DateOnly? to,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default)
    {
        var filter = new AuditLogFilter
        {
            Employee = employee is > 0 ? employee : null,
            // An unknown table or action is dropped, not passed on: a filter nothing can match would
            // look like an empty log.
            Table = WorkConstants.Audit.Tables.Contains(table ?? string.Empty) ? table : null,
            Action = WorkConstants.Audit.Actions.Contains(action ?? string.Empty) ? action : null,
            From = from,
            To = to,
            Page = Utils.SanitisePage(page),
            PageSize = pageSize is null
                ? await settings.GetPageSizeAsync(ct)
                : Utils.SanitisePageSize(pageSize)
        };

        try
        {
            var result = await auditLogs.SearchAsync(filter, ct);

            return AdminView("AuditLogs", new AuditLogListViewModel
            {
                Filter = filter,
                Rows = result.Rows,
                TotalRows = result.TotalRows,
                Employees = await employees.GetStaffAsync(ct)
            });
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The audit log could not be loaded.");
            return AdminView("AuditLogs", new AuditLogListViewModel { Filter = filter, ErrorKey = ex.MessageKey });
        }
    }

    /// <summary>
    /// The four administration screens live together under <c>Views/Admin</c>, next to the partials
    /// they share, so the view is named by its path instead of by the controller folder.
    /// </summary>
    private ViewResult AdminView(string name, object model) =>
        View($"~/Views/Admin/{name}.cshtml", model);
}
