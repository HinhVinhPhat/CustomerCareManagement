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
/// The administration screens against the real test database: the product catalogue, employees and
/// departments, accounts and the audit log. Every write goes through the Service, so each case also
/// proves the service sends what the database expects - and the audit triggers are the database's own
/// work, never the service's.
/// </summary>
[Collection("database")]
public class AdminDatabaseTests
{
    // ------------------------------------------------------------------ products

    [Fact]
    public async Task Creating_a_product_writes_the_row_and_an_audit_entry_with_the_signed_in_employee()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();

        var id = await Products(db).CreateAsync(new ProductFormViewModel
        {
            ProductName = $"Sản phẩm thử {suffix}",
            ProductType = WorkConstants.ProductTypes.Product,
            UnitPrice = 1_250_000m,
            Description = "Mô tả thử"
        });

        var row = await db.ProductServices.AsNoTracking().SingleAsync(p => p.ProductId == id);
        var entry = await db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.TableName == "PRODUCT_SERVICE" && a.RecordId == id && a.Action == "Insert");

        Assert.Equal($"Sản phẩm thử {suffix}", row.ProductName);
        Assert.Equal(1_250_000m, row.UnitPrice);
        Assert.Equal(AdminTestSupport.ManagerEmployee, entry.EmployeeId);
        Assert.Contains("Product", entry.Detail);
    }

    [Fact]
    public async Task Editing_a_price_writes_an_audit_entry_that_names_the_old_and_the_new_price()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();

        var form = new ProductFormViewModel
        {
            ProductName = $"Dịch vụ thử {suffix}",
            ProductType = WorkConstants.ProductTypes.Service,
            UnitPrice = 2_000_000m
        };

        var id = await Products(db).CreateAsync(form);

        await Products(db).UpdateAsync(id, new ProductFormViewModel
        {
            ProductId = id,
            ProductName = form.ProductName,
            ProductType = form.ProductType,
            UnitPrice = 3_500_000m
        });

        var entry = await db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.TableName == "PRODUCT_SERVICE" && a.RecordId == id && a.Action == "Update");
        var price = await db.ProductServices.AsNoTracking()
            .Where(p => p.ProductId == id).Select(p => p.UnitPrice).SingleAsync();

        Assert.Equal(3_500_000m, price);
        Assert.Contains("UnitPrice", entry.Detail);
        Assert.Contains("2000000", entry.Detail);
        Assert.Contains("3500000", entry.Detail);
    }

    [Fact]
    public async Task A_product_name_that_is_already_used_is_refused()
    {
        await using var db = AdminTestSupport.CreateContext();
        var name = $"Trùng tên {AdminTestSupport.Suffix()}";

        await Products(db).CreateAsync(new ProductFormViewModel
        {
            ProductName = name,
            ProductType = WorkConstants.ProductTypes.Product,
            UnitPrice = 100_000m
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Products(db).CreateAsync(
            new ProductFormViewModel
            {
                ProductName = name,
                ProductType = WorkConstants.ProductTypes.Product,
                UnitPrice = 200_000m
            }));

        Assert.Equal("Error_ProductNameTaken", exception.MessageKey);
    }

    [Fact]
    public async Task A_product_that_no_contract_uses_is_deleted_and_the_delete_is_logged()
    {
        await using var db = AdminTestSupport.CreateContext();

        var id = await Products(db).CreateAsync(new ProductFormViewModel
        {
            ProductName = $"Sản phẩm xoá {AdminTestSupport.Suffix()}",
            ProductType = WorkConstants.ProductTypes.Product,
            UnitPrice = 500_000m
        });

        await Products(db).DeleteAsync(id);

        Assert.False(await db.ProductServices.AsNoTracking().AnyAsync(p => p.ProductId == id));
        Assert.True(await db.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.TableName == "PRODUCT_SERVICE" && a.RecordId == id && a.Action == "Delete"));
    }

    [Fact]
    public async Task A_product_that_a_contract_already_uses_is_not_deleted()
    {
        await using var db = AdminTestSupport.CreateContext();

        // Product 1 is part of the sample contracts, so it has contract lines.
        var exception = await Assert.ThrowsAsync<BusinessException>(() => Products(db).DeleteAsync(1));

        Assert.Equal("Error_ProductInUse", exception.MessageKey);
        Assert.True(await db.ProductServices.AsNoTracking().AnyAsync(p => p.ProductId == 1));
    }

    [Fact]
    public async Task The_product_list_filters_sorts_and_pages_in_the_database()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();
        var name = $"Lọc {suffix}";

        for (var i = 1; i <= 3; i++)
        {
            await Products(db).CreateAsync(new ProductFormViewModel
            {
                ProductName = $"{name} {i}",
                ProductType = WorkConstants.ProductTypes.Product,
                UnitPrice = i * 1_000_000m
            });
        }

        var result = await Products(db).SearchAsync(new ProductFilter
        {
            Q = name,
            Sort = ProductFilter.SortByPrice,
            Desc = true,
            PageSize = 2
        });

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(3_000_000m, result.Rows[0].UnitPrice);
        Assert.Equal(0, result.Rows[0].UsageCount);
    }

    // ------------------------------------------------------------------ employees and departments

    [Fact]
    public async Task Creating_an_employee_writes_the_row_and_an_audit_entry()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();

        var id = await Employees(db).CreateAsync(new EmployeeFormViewModel
        {
            FullName = $"Nhân viên thử {suffix}",
            Email = $"employee.{suffix}@test.local",
            Phone = AdminTestSupport.UniquePhone(),
            Gender = WorkConstants.EmployeeGenders.Female,
            Position = WorkConstants.Positions.Staff,
            DepartmentId = 1
        });

        var entry = await db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.TableName == "EMPLOYEE" && a.RecordId == id && a.Action == "Insert");

        Assert.Equal(AdminTestSupport.ManagerEmployee, entry.EmployeeId);
        Assert.True(await db.Employees.AsNoTracking().AnyAsync(e => e.EmployeeId == id));
    }

    [Fact]
    public async Task An_email_that_another_employee_already_uses_is_refused()
    {
        await using var db = AdminTestSupport.CreateContext();
        var email = $"same.{AdminTestSupport.Suffix()}@test.local";

        await Employees(db).CreateAsync(NewEmployee(email, AdminTestSupport.UniquePhone()));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Employees(db)
            .CreateAsync(NewEmployee(email, AdminTestSupport.UniquePhone())));

        Assert.Equal("Error_EmployeeEmailTaken", exception.MessageKey);
    }

    [Fact]
    public async Task A_phone_number_that_is_not_9_to_11_digits_is_refused()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Employees(db)
            .CreateAsync(NewEmployee($"phone.{suffix}@test.local", "0123")));

        Assert.Equal("Error_EmployeePhoneInvalid", exception.MessageKey);
    }

    [Fact]
    public async Task A_second_manager_in_one_department_is_refused()
    {
        await using var db = AdminTestSupport.CreateContext();

        // Department 3 has no Manager in the sample data, so the first one is accepted.
        var first = NewEmployee($"head.{AdminTestSupport.Suffix()}@test.local", AdminTestSupport.UniquePhone());
        first.Position = WorkConstants.Positions.Manager;
        first.DepartmentId = 3;

        await Employees(db).CreateAsync(first);

        var second = NewEmployee($"head2.{AdminTestSupport.Suffix()}@test.local", AdminTestSupport.UniquePhone());
        second.Position = WorkConstants.Positions.Manager;
        second.DepartmentId = 3;

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Employees(db).CreateAsync(second));

        Assert.Equal("Error_DepartmentHasManager", exception.MessageKey);
    }

    [Fact]
    public async Task A_department_is_added_renamed_and_shows_its_headcount_and_its_head()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();

        var departmentId = await Employees(db).CreateDepartmentAsync(new DepartmentFormModel
        {
            DepartmentName = $"Phòng thử {suffix}"
        });

        var employee = NewEmployee($"dept.{suffix}@test.local", AdminTestSupport.UniquePhone());
        employee.Position = WorkConstants.Positions.Manager;
        employee.DepartmentId = departmentId;
        await Employees(db).CreateAsync(employee);

        await Employees(db).RenameDepartmentAsync(departmentId, new DepartmentFormModel
        {
            DepartmentName = $"Phòng đã đổi {suffix}"
        });

        var row = (await Employees(db).GetDepartmentRowsAsync())
            .Single(d => d.DepartmentId == departmentId);

        Assert.Equal($"Phòng đã đổi {suffix}", row.DepartmentName);
        Assert.Equal(1, row.EmployeeCount);
        Assert.Equal(employee.FullName, row.HeadName);
    }

    [Fact]
    public async Task A_department_name_that_is_already_used_is_refused()
    {
        await using var db = AdminTestSupport.CreateContext();
        var name = $"Phòng trùng {AdminTestSupport.Suffix()}";

        await Employees(db).CreateDepartmentAsync(new DepartmentFormModel { DepartmentName = name });

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Employees(db)
            .CreateDepartmentAsync(new DepartmentFormModel { DepartmentName = name }));

        Assert.Equal("Error_DepartmentNameTaken", exception.MessageKey);
    }

    [Fact]
    public async Task The_employee_list_can_be_filtered_by_department_and_position()
    {
        await using var db = AdminTestSupport.CreateContext();

        var result = await Employees(db).SearchAsync(new EmployeeFilter
        {
            Department = 1,
            Position = WorkConstants.Positions.Manager,
            PageSize = 50
        });

        Assert.True(result.TotalRows >= 1);
        Assert.All(result.Rows, row =>
        {
            Assert.Equal(1, row.DepartmentId);
            Assert.Equal(WorkConstants.Positions.Manager, row.Position);
        });
    }

    // ------------------------------------------------------------------ accounts

    [Fact]
    public async Task A_new_account_is_stored_as_a_hash_that_verifies_the_typed_password()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();
        var employeeId = await NewEmployeeIdAsync(db, suffix);
        var username = $"user{suffix}";

        var id = await Accounts(db).CreateAsync(new AccountCreateViewModel
        {
            EmployeeId = employeeId,
            Username = username,
            Role = WorkConstants.Roles.Support,
            Password = "MatKhau123",
            ConfirmPassword = "MatKhau123"
        });

        var account = await db.Accounts.AsNoTracking().SingleAsync(a => a.AccountId == id);
        var hasher = new PasswordHasher<Account>();

        Assert.NotEqual("MatKhau123", account.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(account, account.PasswordHash, "MatKhau123"));
        Assert.True(account.IsActive);

        Assert.True(await db.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.TableName == "ACCOUNT" && a.RecordId == id && a.Action == "Insert"));
    }

    [Fact]
    public async Task A_second_account_for_the_same_employee_is_refused()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();
        var employeeId = await NewEmployeeIdAsync(db, suffix);

        await Accounts(db).CreateAsync(new AccountCreateViewModel
        {
            EmployeeId = employeeId,
            Username = $"first{suffix}",
            Role = WorkConstants.Roles.Sales,
            Password = "MatKhau123"
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Accounts(db)
            .CreateAsync(new AccountCreateViewModel
            {
                EmployeeId = employeeId,
                Username = $"second{suffix}",
                Role = WorkConstants.Roles.Sales,
                Password = "MatKhau123"
            }));

        Assert.Equal("Error_EmployeeHasAccount", exception.MessageKey);
    }

    [Fact]
    public async Task A_username_that_is_already_taken_is_refused()
    {
        await using var db = AdminTestSupport.CreateContext();
        var employeeId = await NewEmployeeIdAsync(db, AdminTestSupport.Suffix());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Accounts(db)
            .CreateAsync(new AccountCreateViewModel
            {
                EmployeeId = employeeId,
                Username = "john",
                Role = WorkConstants.Roles.Sales,
                Password = "MatKhau123"
            }));

        Assert.Equal("Error_UsernameTaken", exception.MessageKey);
    }

    [Fact]
    public async Task An_unknown_role_is_refused_before_it_reaches_the_database()
    {
        await using var db = AdminTestSupport.CreateContext();
        var employeeId = await NewEmployeeIdAsync(db, AdminTestSupport.Suffix());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Accounts(db)
            .CreateAsync(new AccountCreateViewModel
            {
                EmployeeId = employeeId,
                Username = $"boss{AdminTestSupport.Suffix()}",
                Role = "Boss",
                Password = "MatKhau123"
            }));

        Assert.Equal("Error_AccountRoleInvalid", exception.MessageKey);
    }

    [Fact]
    public async Task A_manager_cannot_lock_or_demote_the_account_they_are_signed_in_with()
    {
        await using var db = AdminTestSupport.CreateContext();

        var form = await Accounts(db).GetAsync(1, AdminTestSupport.ManagerEmployee);

        Assert.NotNull(form);
        Assert.True(form.IsSelf);

        form.Role = WorkConstants.Roles.Manager;
        form.IsActive = false;

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Accounts(db)
            .UpdateAsync(1, form, AdminTestSupport.ManagerEmployee));

        Assert.Equal("Error_SelfLockout", exception.MessageKey);
    }

    [Fact]
    public async Task The_last_active_manager_cannot_be_locked()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();
        var employeeId = await NewEmployeeIdAsync(db, suffix);

        var accountId = await Accounts(db).CreateAsync(new AccountCreateViewModel
        {
            EmployeeId = employeeId,
            Username = $"manager{suffix}",
            Role = WorkConstants.Roles.Manager,
            Password = "MatKhau123"
        });

        // Everything below runs inside a transaction that is rolled back, so the suite never leaves
        // the sample managers locked for the tests that run after this one.
        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.Accounts
            .Where(a => a.AccountId == 1 || a.AccountId == 5)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsActive, false));

        var form = new AccountEditViewModel
        {
            AccountId = accountId,
            Username = $"manager{suffix}",
            EmployeeName = "Người thử",
            Role = WorkConstants.Roles.Manager,
            IsActive = false
        };

        var exception = await Assert.ThrowsAsync<BusinessException>(() => Accounts(db)
            .UpdateAsync(accountId, form, actorAccountId: 1));

        Assert.Equal("Error_LastManager", exception.MessageKey);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Resetting_a_password_stores_a_new_hash_and_never_the_typed_text()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();
        var employeeId = await NewEmployeeIdAsync(db, suffix);

        var accountId = await Accounts(db).CreateAsync(new AccountCreateViewModel
        {
            EmployeeId = employeeId,
            Username = $"reset{suffix}",
            Role = WorkConstants.Roles.Sales,
            Password = "MatKhau123"
        });

        var form = await Accounts(db).GetForResetAsync(accountId);
        Assert.NotNull(form);

        form.Password = "MatKhau456";
        form.ConfirmPassword = "MatKhau456";
        await Accounts(db).ResetPasswordAsync(accountId, form);

        var account = await db.Accounts.AsNoTracking().SingleAsync(a => a.AccountId == accountId);
        var hasher = new PasswordHasher<Account>();

        Assert.NotEqual("MatKhau456", account.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(account, account.PasswordHash, "MatKhau456"));
        Assert.Equal(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(account, account.PasswordHash, "MatKhau123"));
    }

    [Fact]
    public async Task Locking_an_account_keeps_the_row_and_is_written_to_the_audit_log()
    {
        await using var db = AdminTestSupport.CreateContext();
        var suffix = AdminTestSupport.Suffix();
        var employeeId = await NewEmployeeIdAsync(db, suffix);

        var accountId = await Accounts(db).CreateAsync(new AccountCreateViewModel
        {
            EmployeeId = employeeId,
            Username = $"lock{suffix}",
            Role = WorkConstants.Roles.Support,
            Password = "MatKhau123"
        });

        var form = await Accounts(db).GetAsync(accountId, AdminTestSupport.ManagerEmployee);
        Assert.NotNull(form);

        form.IsActive = false;
        await Accounts(db).UpdateAsync(accountId, form, AdminTestSupport.ManagerEmployee);

        var account = await db.Accounts.AsNoTracking().SingleAsync(a => a.AccountId == accountId);
        var entry = await db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.TableName == "ACCOUNT" && a.RecordId == accountId && a.Action == "Update");

        Assert.False(account.IsActive);
        Assert.Contains("Active -> Locked", entry.Detail);
    }

    // ------------------------------------------------------------------ audit log

    [Fact]
    public async Task The_audit_screen_reads_newest_first_and_filters_in_the_database()
    {
        await using var db = AdminTestSupport.CreateContext();
        var name = $"Nhật ký {AdminTestSupport.Suffix()}";

        var first = await Products(db).CreateAsync(new ProductFormViewModel
        {
            ProductName = name,
            ProductType = WorkConstants.ProductTypes.Product,
            UnitPrice = 100_000m
        });

        var second = await Products(db).CreateAsync(new ProductFormViewModel
        {
            ProductName = $"{name} 2",
            ProductType = WorkConstants.ProductTypes.Product,
            UnitPrice = 200_000m
        });

        var result = await AuditLogs(db).SearchAsync(new AuditLogFilter
        {
            Table = "PRODUCT_SERVICE",
            Action = "Insert",
            Employee = AdminTestSupport.ManagerEmployee,
            From = DateOnly.FromDateTime(DateTime.Today),
            To = DateOnly.FromDateTime(DateTime.Today),
            PageSize = 50
        });

        var recordIds = result.Rows.Select(r => r.RecordId).ToList();

        Assert.Contains(first, recordIds);
        Assert.Contains(second, recordIds);
        Assert.All(result.Rows, row =>
        {
            Assert.Equal("PRODUCT_SERVICE", row.TableName);
            Assert.Equal("Insert", row.Action);
            Assert.Equal(AdminTestSupport.ManagerEmployee, row.EmployeeId);
        });

        // Newest first: the second product was written after the first one.
        Assert.True(result.Rows.First().LogId > result.Rows.Last().LogId);
    }

    [Fact]
    public async Task A_day_after_the_last_entry_matches_nothing()
    {
        await using var db = AdminTestSupport.CreateContext();
        var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(1);

        var result = await AuditLogs(db).SearchAsync(new AuditLogFilter { From = tomorrow, To = tomorrow });

        Assert.Equal(0, result.TotalRows);
        Assert.Empty(result.Rows);
    }

    // ------------------------------------------------------------------ helpers

    private static ProductCatalogService Products(ApplicationDbContext db) =>
        new(db, NullLogger<ProductCatalogService>.Instance);

    private static EmployeeService Employees(ApplicationDbContext db) =>
        new(db, NullLogger<EmployeeService>.Instance);

    private static AdminAccountService Accounts(ApplicationDbContext db) =>
        new(db, new PasswordHasher<Account>(), NullLogger<AdminAccountService>.Instance);

    private static AuditLogService AuditLogs(ApplicationDbContext db) => new(db);

    private static EmployeeFormViewModel NewEmployee(string email, string phone) => new()
    {
        FullName = $"Nhân viên {email.Split('@')[0]}",
        Email = email,
        Phone = phone,
        Gender = WorkConstants.EmployeeGenders.Male,
        Position = WorkConstants.Positions.Staff,
        DepartmentId = 1
    };

    private static async Task<int> NewEmployeeIdAsync(ApplicationDbContext db, string suffix) =>
        await Employees(db).CreateAsync(NewEmployee(
            $"account.{suffix}@test.local", AdminTestSupport.UniquePhone()));
}
