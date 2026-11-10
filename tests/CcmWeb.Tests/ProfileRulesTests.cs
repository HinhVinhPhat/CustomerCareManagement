using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CcmWeb.Tests;

/// <summary>
/// The /profile page on the real test database. The employee comes from the signed-in claim, so these
/// cases use their own employee and account instead of touching the demo rows.
/// </summary>
[Collection("database")]
public class ProfileRulesTests
{
    [Fact]
    public async Task The_profile_page_reads_the_person_and_their_sign_in_role()
    {
        await using var db = AdminTestSupport.CreateContext();
        var (employeeId, username) = await NewPersonAsync(db, WorkConstants.Roles.Support);

        var info = await Profiles(db).GetAsync(employeeId);

        Assert.NotNull(info);
        Assert.Equal(username, info.Username);
        Assert.Equal(WorkConstants.Roles.Support, info.Role);
        Assert.False(string.IsNullOrWhiteSpace(info.DepartmentName));
    }

    [Fact]
    public async Task A_wrong_current_password_does_not_change_anything()
    {
        await using var db = AdminTestSupport.CreateContext();
        var (employeeId, _) = await NewPersonAsync(db, WorkConstants.Roles.Sales);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Profiles(db)
            .ChangePasswordAsync(employeeId, Form("khong-dung", "MatKhau456")));

        Assert.Equal("Error_CurrentPasswordWrong", exception.MessageKey);
    }

    [Fact]
    public async Task A_new_password_shorter_than_eight_characters_is_refused()
    {
        await using var db = AdminTestSupport.CreateContext();
        var (employeeId, _) = await NewPersonAsync(db, WorkConstants.Roles.Sales);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Profiles(db)
            .ChangePasswordAsync(employeeId, Form("MatKhau123", "1234567")));

        Assert.Equal("Error_PasswordTooShort", exception.MessageKey);
    }

    [Fact]
    public async Task The_new_password_must_differ_from_the_current_one()
    {
        await using var db = AdminTestSupport.CreateContext();
        var (employeeId, _) = await NewPersonAsync(db, WorkConstants.Roles.Sales);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Profiles(db)
            .ChangePasswordAsync(employeeId, Form("MatKhau123", "MatKhau123")));

        Assert.Equal("Error_PasswordSameAsOld", exception.MessageKey);
    }

    [Fact]
    public async Task A_correct_current_password_is_replaced_by_the_new_one()
    {
        await using var db = AdminTestSupport.CreateContext();
        var (employeeId, _) = await NewPersonAsync(db, WorkConstants.Roles.Sales);

        await Profiles(db).ChangePasswordAsync(employeeId, Form("MatKhau123", "MatKhau456"));

        var account = await db.Accounts.AsNoTracking()
            .SingleAsync(a => a.EmployeeId == employeeId);
        var hasher = new PasswordHasher<Account>();

        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(account, account.PasswordHash, "MatKhau456"));
        Assert.Equal(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(account, account.PasswordHash, "MatKhau123"));
    }

    [Fact]
    public async Task Changing_the_password_of_a_demo_sha2_account_upgrades_the_hash()
    {
        await using var db = AdminTestSupport.CreateContext();
        var (employeeId, _) = await NewPersonAsync(db, WorkConstants.Roles.Sales);

        // Put the row back into the demo form, the way the sample data ships it.
        await db.Accounts
            .Where(a => a.EmployeeId == employeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                a => a.PasswordHash, Utils.ComputeLegacySha256("123456")));

        // ExecuteUpdate writes around the tracker, so the context must read the row again.
        db.ChangeTracker.Clear();

        await Profiles(db).ChangePasswordAsync(employeeId, Form("123456", "MatKhau456"));

        var account = await db.Accounts.AsNoTracking()
            .SingleAsync(a => a.EmployeeId == employeeId);

        Assert.False(Utils.IsLegacySha256Hash(account.PasswordHash));
    }

    [Fact]
    public async Task An_employee_without_an_account_has_no_profile_to_change()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();

        var employeeId = await new EmployeeService(db, NullLogger<EmployeeService>.Instance)
            .CreateAsync(new EmployeeFormViewModel
            {
                FullName = $"Chưa có tài khoản {suffix}",
                Email = $"noaccount.{suffix}@test.local",
                Phone = AdminTestSupport.UniquePhone(),
                Gender = WorkConstants.EmployeeGenders.Male,
                Position = WorkConstants.Positions.Staff,
                DepartmentId = 1
            });

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Profiles(db)
            .ChangePasswordAsync(employeeId, Form("MatKhau123", "MatKhau456")));

        Assert.Equal("Error_AccountNotFound", exception.MessageKey);
    }

    // ------------------------------------------------------------------ helpers

    private static ProfileService Profiles(ApplicationDbContext db) =>
        new(db, new PasswordHasher<Account>(), NullLogger<ProfileService>.Instance);

    private static ChangePasswordViewModel Form(string current, string next) => new()
    {
        CurrentPassword = current,
        NewPassword = next,
        ConfirmPassword = next
    };

    /// <summary>An employee with an account of their own, so the demo rows stay untouched.</summary>
    private static async Task<(int EmployeeId, string Username)> NewPersonAsync(
        ApplicationDbContext db, string role)
    {
        var suffix = AdminTestSupport.Suffix();

        var employeeId = await new EmployeeService(db, NullLogger<EmployeeService>.Instance)
            .CreateAsync(new EmployeeFormViewModel
            {
                FullName = $"Người dùng thử {suffix}",
                Email = $"profile.{suffix}@test.local",
                Phone = AdminTestSupport.UniquePhone(),
                Gender = WorkConstants.EmployeeGenders.Female,
                Position = WorkConstants.Positions.Staff,
                DepartmentId = 1
            });

        var username = $"profile{suffix}";

        await new AdminAccountService(
                db, new PasswordHasher<Account>(), NullLogger<AdminAccountService>.Instance)
            .CreateAsync(new AccountCreateViewModel
            {
                EmployeeId = employeeId,
                Username = username,
                Role = role,
                Password = "MatKhau123",
                ConfirmPassword = "MatKhau123"
            });

        return (employeeId, username);
    }
}
