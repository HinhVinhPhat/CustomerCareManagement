using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// The account screens. Every write hashes the password first; a hash is never read back into a
/// ViewModel, so no screen can show one even by accident.
/// </summary>
public sealed class AdminAccountService(
    ApplicationDbContext db,
    IPasswordHasher<Account> passwordHasher,
    ILogger<AdminAccountService> logger) : IAdminAccountService
{
    /// <summary>Only used as the user argument of PasswordHasher, which ignores it.</summary>
    private static readonly Account HashingProbe = new();

    public async Task<AccountSearchResult> SearchAsync(
        AccountFilter filter, CancellationToken ct = default)
    {
        var query = db.Accounts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            query = query.Where(a => a.Username.Contains(q) || a.Employee.FullName.Contains(q));
        }

        if (filter.Role is not null)
        {
            query = query.Where(a => a.Role == filter.Role);
        }

        if (filter.State == AccountFilter.StateActive)
        {
            query = query.Where(a => a.IsActive);
        }
        else if (filter.State == AccountFilter.StateLocked)
        {
            query = query.Where(a => !a.IsActive);
        }

        query = (filter.Sort, filter.Desc) switch
        {
            (AccountFilter.SortByEmployee, false) => query
                .OrderBy(a => a.Employee.FullName).ThenBy(a => a.Username),
            (AccountFilter.SortByEmployee, true) => query
                .OrderByDescending(a => a.Employee.FullName).ThenBy(a => a.Username),
            (_, true) => query.OrderByDescending(a => a.Username),
            _ => query.OrderBy(a => a.Username)
        };

        var totalRows = await query.CountAsync(ct);

        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(a => new AccountRow(
                a.AccountId,
                a.Username,
                a.EmployeeId,
                a.Employee.FullName,
                a.Role,
                a.IsActive))
            .ToListAsync(ct);

        return new AccountSearchResult(rows, totalRows);
    }

    public async Task<IReadOnlyList<AccountlessEmployee>> GetAccountlessEmployeesAsync(
        CancellationToken ct = default) =>
        await db.Employees
            .AsNoTracking()
            .Where(e => e.Account == null)
            .OrderBy(e => e.FullName)
            .Select(e => new AccountlessEmployee(e.EmployeeId, e.FullName, e.Position))
            .ToListAsync(ct);

    public async Task<int> CreateAsync(AccountCreateViewModel form, CancellationToken ct = default)
    {
        var employeeId = form.EmployeeId ?? 0;
        var employee = await db.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeId == employeeId)
            .Select(e => new { e.EmployeeId, HasAccount = e.Account != null })
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessException("Error_EmployeeNotFound");

        if (employee.HasAccount)
        {
            // ACCOUNT.EmployeeID is UNIQUE; saying it here points at the dropdown, not at SQL.
            throw new BusinessException("Error_EmployeeHasAccount");
        }

        var username = (form.Username ?? string.Empty).Trim();
        EnsureUsernameShape(username);
        EnsureRoleValid(form.Role);
        EnsurePasswordAcceptable(form.Password);

        var usernameTaken = await db.Accounts
            .AsNoTracking()
            .AnyAsync(a => a.Username == username, ct);

        if (usernameTaken)
        {
            throw new BusinessException("Error_UsernameTaken");
        }

        var account = new Account
        {
            Username = username,
            PasswordHash = passwordHasher.HashPassword(HashingProbe, form.Password),
            Role = form.Role!,
            EmployeeId = employeeId,
            IsActive = true
        };

        db.Accounts.Add(account);
        await SaveAsync(ct);

        return account.AccountId;
    }

    public async Task<AccountEditViewModel?> GetAsync(
        int accountId, int actorAccountId, CancellationToken ct = default)
    {
        var form = await db.Accounts
            .AsNoTracking()
            .Where(a => a.AccountId == accountId)
            .Select(a => new AccountEditViewModel
            {
                AccountId = a.AccountId,
                Username = a.Username,
                EmployeeName = a.Employee.FullName,
                Role = a.Role,
                IsActive = a.IsActive
            })
            .FirstOrDefaultAsync(ct);

        if (form is null)
        {
            return null;
        }

        return new AccountEditViewModel
        {
            AccountId = form.AccountId,
            Username = form.Username,
            EmployeeName = form.EmployeeName,
            Role = form.Role,
            IsActive = form.IsActive,
            IsSelf = form.AccountId == actorAccountId
        };
    }

    public async Task UpdateAsync(
        int accountId, AccountEditViewModel form, int actorAccountId, CancellationToken ct = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct)
            ?? throw new BusinessException("Error_AccountNotFound");

        EnsureRoleValid(form.Role);

        if (account.AccountId == actorAccountId)
        {
            // A Manager must not be able to shut themselves out of the system.
            if (!form.IsActive || form.Role != account.Role)
            {
                throw new BusinessException("Error_SelfLockout");
            }
        }

        var losesManager = account.Role == WorkConstants.Roles.Manager
            && (!form.IsActive || form.Role != WorkConstants.Roles.Manager);

        if (losesManager && !await AnotherActiveManagerExistsAsync(account.AccountId, ct))
        {
            throw new BusinessException("Error_LastManager");
        }

        account.Role = form.Role!;
        account.IsActive = form.IsActive;

        await SaveAsync(ct);
    }

    public async Task<ResetPasswordViewModel?> GetForResetAsync(
        int accountId, CancellationToken ct = default) =>
        await db.Accounts
            .AsNoTracking()
            .Where(a => a.AccountId == accountId)
            .Select(a => new ResetPasswordViewModel
            {
                AccountId = a.AccountId,
                Username = a.Username,
                EmployeeName = a.Employee.FullName
            })
            .FirstOrDefaultAsync(ct);

    public async Task ResetPasswordAsync(
        int accountId, ResetPasswordViewModel form, CancellationToken ct = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct)
            ?? throw new BusinessException("Error_AccountNotFound");

        EnsurePasswordAcceptable(form.Password);

        account.PasswordHash = passwordHasher.HashPassword(HashingProbe, form.Password);
        await SaveAsync(ct);
    }

    /// <summary>
    /// A locked or demoted Manager may leave the system without one. Counting the other active
    /// Managers is the only rule that keeps the administration reachable.
    /// </summary>
    private async Task<bool> AnotherActiveManagerExistsAsync(int exceptAccountId, CancellationToken ct) =>
        await db.Accounts
            .AsNoTracking()
            .AnyAsync(a => a.IsActive
                && a.Role == WorkConstants.Roles.Manager
                && a.AccountId != exceptAccountId, ct);

    private static void EnsureRoleValid(string? role)
    {
        if (!WorkConstants.Roles.All.Contains(role ?? string.Empty))
        {
            throw new BusinessException("Error_AccountRoleInvalid");
        }
    }

    private static void EnsureUsernameShape(string username)
    {
        if (username.Length is 0 or > WorkConstants.Accounts.UsernameMaxLength
            || !username.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
        {
            throw new BusinessException("Error_UsernameInvalid");
        }
    }

    private static void EnsurePasswordAcceptable(string? password)
    {
        if (!PasswordRules.IsAcceptable(password))
        {
            throw new BusinessException("Error_PasswordTooShort");
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Saving an account failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }
}
