using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

/// <summary>
/// Login accounts (design.md 6.13). One account per employee, the role decides what the person may
/// open, and a lock replaces a delete. The password is only ever written as a hash; no ViewModel
/// carries one back.
/// </summary>
[Authorize(Roles = WorkConstants.Roles.Manager)]
public class AdminAccountsController(
    IAdminAccountService accounts,
    ISettingService settings,
    ILogger<AdminAccountsController> logger) : Controller
{
    [HttpGet("/admin/accounts")]
    public async Task<IActionResult> Index(
        string? q,
        string? role,
        string? state,
        string? sort = null,
        bool desc = false,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default)
    {
        var filter = new AccountFilter
        {
            Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            Role = WorkConstants.Roles.All.Contains(role ?? string.Empty) ? role : null,
            State = state is AccountFilter.StateActive or AccountFilter.StateLocked ? state : null,
            Sort = Utils.SanitiseSort(sort, AccountFilter.SortFields, AccountFilter.SortByUsername),
            Desc = desc,
            Page = Utils.SanitisePage(page),
            PageSize = pageSize is null
                ? await settings.GetPageSizeAsync(ct)
                : Utils.SanitisePageSize(pageSize)
        };

        try
        {
            var result = await accounts.SearchAsync(filter, ct);

            return AdminView("Accounts", new AccountListViewModel
            {
                Filter = filter,
                Rows = result.Rows,
                TotalRows = result.TotalRows
            });
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The account list could not be loaded.");
            return AdminView("Accounts", new AccountListViewModel { Filter = filter, ErrorKey = ex.MessageKey });
        }
    }

    [HttpGet("/admin/accounts/create")]
    /// <summary>
    /// The employee may arrive from the employee list ("create the account of this person"), so the
    /// dropdown starts on the right row instead of on nobody.
    /// </summary>
    public async Task<IActionResult> Create(int? employeeId, CancellationToken ct) =>
        AdminView("AccountCreate", new AccountCreateViewModel
        {
            EmployeeId = employeeId is > 0 ? employeeId : null,
            Employees = await accounts.GetAccountlessEmployeesAsync(ct)
        });

    [HttpPost("/admin/accounts/create")]
    public async Task<IActionResult> Create(AccountCreateViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await FormViewAsync(model, "AccountCreate", ct);
        }

        try
        {
            var id = await accounts.CreateAsync(model, ct);
            logger.LogInformation("Employee {EmployeeID} created account {AccountID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Account_Created";
            TempData["MessageOk"] = true;

            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Creating an account failed.");
            AddBusinessError(ex.MessageKey);
            return await FormViewAsync(model, "AccountCreate", ct);
        }
    }

    [HttpGet("/admin/accounts/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var form = await accounts.GetAsync(id, ActorAccountId, ct);

        if (form is null)
        {
            return NotFound();
        }

        return AdminView("AccountEdit", form);
    }

    [HttpPost("/admin/accounts/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, AccountEditViewModel model, CancellationToken ct)
    {
        // The identity of the account comes from the route; only the two editable values come from
        // the form, so a crafted POST cannot rename an account or point it at another employee.
        var current = await accounts.GetAsync(id, ActorAccountId, ct);

        if (current is null)
        {
            return NotFound();
        }

        model = new AccountEditViewModel
        {
            AccountId = id,
            Username = current.Username,
            EmployeeName = current.EmployeeName,
            IsSelf = current.IsSelf,
            Role = model.Role,
            IsActive = model.IsActive
        };

        if (!ModelState.IsValid)
        {
            return AdminView("AccountEdit", model);
        }

        try
        {
            await accounts.UpdateAsync(id, model, ActorAccountId, ct);
            logger.LogInformation("Employee {EmployeeID} updated account {AccountID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Account_Saved";
            TempData["MessageOk"] = true;

            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Updating account {AccountID} failed.", id);
            AddBusinessError(ex.MessageKey);
            return AdminView("AccountEdit", model);
        }
    }

    [HttpGet("/admin/accounts/{id:int}/password")]
    public async Task<IActionResult> Password(int id, CancellationToken ct)
    {
        var form = await accounts.GetForResetAsync(id, ct);

        if (form is null)
        {
            return NotFound();
        }

        return AdminView("AccountPassword", form);
    }

    [HttpPost("/admin/accounts/{id:int}/password")]
    public async Task<IActionResult> Password(int id, ResetPasswordViewModel model, CancellationToken ct)
    {
        var current = await accounts.GetForResetAsync(id, ct);

        if (current is null)
        {
            return NotFound();
        }

        model = new ResetPasswordViewModel
        {
            AccountId = id,
            Username = current.Username,
            EmployeeName = current.EmployeeName,
            Password = model.Password,
            ConfirmPassword = model.ConfirmPassword
        };

        if (!ModelState.IsValid)
        {
            return AdminView("AccountPassword", model);
        }

        try
        {
            await accounts.ResetPasswordAsync(id, model, ct);
            logger.LogInformation("Employee {EmployeeID} reset the password of account {AccountID}.",
                User.GetEmployeeID(), id);

            TempData["MessageKey"] = "Account_PasswordReset";
            TempData["MessageOk"] = true;

            return RedirectToAction(nameof(Index));
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "Resetting the password of account {AccountID} failed.", id);
            AddBusinessError(ex.MessageKey);
            return AdminView("AccountPassword", model);
        }
    }

    /// <summary>
    /// The four administration screens live together under <c>Views/Admin</c>, next to the partials
    /// they share, so the view is named by its path instead of by the controller folder.
    /// </summary>
    private ViewResult AdminView(string name, object model) =>
        View($"~/Views/Admin/{name}.cshtml", model);

    /// <summary>The signed-in account; it may not be locked or demoted by itself.</summary>
    private int ActorAccountId => User.GetAccountID() ?? 0;

    private async Task<IActionResult> FormViewAsync(
        AccountCreateViewModel model, string viewName, CancellationToken ct)
    {
        model.Employees = await accounts.GetAccountlessEmployeesAsync(ct);
        return AdminView(viewName, model);
    }

    private void AddBusinessError(string messageKey) =>
        ModelState.AddModelError(
            messageKey switch
            {
                "Error_EmployeeHasAccount" or "Error_EmployeeNotFound"
                    => nameof(AccountCreateViewModel.EmployeeId),
                "Error_UsernameTaken" or "Error_UsernameInvalid" or "Error_TooLong"
                    => nameof(AccountCreateViewModel.Username),
                "Error_AccountRoleInvalid" => nameof(AccountCreateViewModel.Role),
                "Error_PasswordTooShort" => nameof(AccountCreateViewModel.Password),
                _ => string.Empty
            },
            messageKey);
}
