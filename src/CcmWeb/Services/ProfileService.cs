using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// The profile page. Changing a password follows the same path as signing in: the demo SHA2 hash is
/// accepted once and replaced by a PasswordHasher hash, so an old account is upgraded instead of
/// rejected.
/// </summary>
public sealed class ProfileService(
    ApplicationDbContext db,
    IPasswordHasher<Account> passwordHasher,
    ILogger<ProfileService> logger) : IProfileService
{
    public async Task<ProfileInfo?> GetAsync(int employeeId, CancellationToken ct = default) =>
        await db.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeId == employeeId)
            .Select(e => new ProfileInfo(
                e.FullName,
                e.Email,
                e.Phone,
                e.Gender,
                e.Position,
                e.Department.DepartmentName,
                e.Account == null ? string.Empty : e.Account.Username,
                e.Account == null ? string.Empty : e.Account.Role))
            .FirstOrDefaultAsync(ct);

    public async Task ChangePasswordAsync(
        int employeeId, ChangePasswordViewModel form, CancellationToken ct = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.EmployeeId == employeeId, ct)
            ?? throw new BusinessException("Error_AccountNotFound");

        var (verified, needsRehash) = PasswordRules.Verify(passwordHasher, account, form.CurrentPassword);

        if (!verified)
        {
            throw new BusinessException("Error_CurrentPasswordWrong");
        }

        if (!PasswordRules.IsAcceptable(form.NewPassword))
        {
            throw new BusinessException("Error_PasswordTooShort");
        }

        if (form.NewPassword == form.CurrentPassword && !needsRehash)
        {
            throw new BusinessException("Error_PasswordSameAsOld");
        }

        account.PasswordHash = passwordHasher.HashPassword(account, form.NewPassword);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Changing the password of employee {EmployeeID} failed.", employeeId);
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }
}
