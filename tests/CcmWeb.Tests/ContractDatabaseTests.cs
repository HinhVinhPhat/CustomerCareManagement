using System.Security.Claims;
using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CcmWeb.Tests;

/// <summary>
/// The contract procedures and triggers on a real SQL Server (the test database). The application
/// never writes CONTRACT, CONTRACT_DETAIL, TotalValue or the customer tier itself: every case here
/// goes through <see cref="ContractService"/>, so it also proves the service calls the procedure with
/// the shape the database expects.
/// </summary>
[Collection("database")]
public class ContractDatabaseTests
{
    // Employee 2 is the "mary" account (Sales); the audit trigger must record it as the actor.
    private const int SalesEmployee = 2;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public async Task A_new_contract_gets_its_lines_the_trigger_total_and_the_customer_tier()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);

        var contractId = await Service(db).CreateAsync(Form(customer.CustomerId,
        [
            Line(productId: 1, quantity: 2),
            Line(productId: 5, quantity: 1)
        ]));

        var info = await db.VwContractInfos.AsNoTracking().SingleAsync(v => v.ContractId == contractId);
        var lines = await db.VwContractDetails.AsNoTracking()
            .Where(v => v.ContractId == contractId).ToListAsync();
        var tier = await db.Customers.AsNoTracking()
            .Where(c => c.CustomerId == customer.CustomerId)
            .Select(c => c.Tier).SingleAsync();

        Assert.Equal(2, lines.Count);
        Assert.Equal(60_000_000m, info.TotalValue);   // 2 x 5.000.000 + 50.000.000, from the trigger
        Assert.Equal(WorkConstants.CustomerTiers.Gold, tier);
    }

    [Fact]
    public async Task The_insert_lands_in_the_audit_log_with_the_signed_in_employee()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);

        var contractId = await Service(db).CreateAsync(Form(customer.CustomerId, [Line(1, 1)]));

        var entry = await db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.TableName == "CONTRACT" && a.RecordId == contractId && a.Action == "Insert");

        Assert.Equal(SalesEmployee, entry.EmployeeId);
    }

    [Fact]
    public async Task A_contract_without_lines_is_refused_by_the_procedure()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).CreateAsync(Form(customer.CustomerId, [])));

        Assert.Equal("Error_ContractHasNoLines", exception.MessageKey);
    }

    [Fact]
    public async Task The_same_product_twice_is_refused_by_the_procedure()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).CreateAsync(Form(customer.CustomerId, [Line(1, 1), Line(1, 2)])));

        Assert.Equal("Error_ProductDuplicated", exception.MessageKey);
    }

    [Fact]
    public async Task A_quantity_below_one_is_refused_by_the_procedure()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).CreateAsync(Form(customer.CustomerId, [Line(1, 0)])));

        Assert.Equal("Error_QuantityInvalid", exception.MessageKey);
    }

    [Fact]
    public async Task An_unknown_product_is_refused_by_the_procedure()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).CreateAsync(Form(customer.CustomerId, [Line(999_999, 1)])));

        Assert.Equal("Error_ProductNotFound", exception.MessageKey);
    }

    [Fact]
    public async Task An_end_date_before_the_sign_date_is_refused_by_the_procedure()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);

        var form = Form(customer.CustomerId, [Line(1, 1)]);
        form.EndDate = Today.AddDays(-1);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).CreateAsync(form));

        Assert.Equal("Error_DateInvalid", exception.MessageKey);
    }

    [Fact]
    public async Task An_inactive_customer_cannot_get_a_contract()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db, isActive: false);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).CreateAsync(Form(customer.CustomerId, [Line(1, 1)])));

        Assert.Equal("Error_CustomerNotActive", exception.MessageKey);
    }

    [Fact]
    public async Task Signing_a_pending_contract_activates_it_and_writes_a_status_change_entry()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);

        var contractId = await Service(db).CreateAsync(Form(customer.CustomerId, [Line(1, 1)],
            status: WorkConstants.ContractStatus.Pending));

        await Service(db).SignAsync(contractId, Today);

        var info = await db.VwContractInfos.AsNoTracking().SingleAsync(v => v.ContractId == contractId);
        var entry = await db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.TableName == "CONTRACT" && a.RecordId == contractId && a.Action == "StatusChange");

        Assert.Equal(WorkConstants.ContractStatus.Active, info.Status);
        Assert.Equal("Pending -> Active", entry.Detail);
        Assert.Equal(SalesEmployee, entry.EmployeeId);
    }

    [Fact]
    public async Task Signing_an_active_contract_is_refused_by_the_procedure()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);
        var contractId = await Service(db).CreateAsync(Form(customer.CustomerId, [Line(1, 1)]));

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).SignAsync(contractId, Today));

        Assert.Equal("Error_ContractStatusForbids", exception.MessageKey);
    }

    [Fact]
    public async Task Renewing_moves_the_end_date_forward()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);
        var contractId = await Service(db).CreateAsync(Form(customer.CustomerId, [Line(1, 1)]));

        var newEndDate = Today.AddYears(2);
        await Service(db).RenewAsync(contractId, newEndDate);

        var info = await db.VwContractInfos.AsNoTracking().SingleAsync(v => v.ContractId == contractId);

        Assert.Equal(newEndDate, info.EndDate);
        Assert.Equal(WorkConstants.ContractStatus.Active, info.Status);
    }

    [Fact]
    public async Task Renewing_to_the_same_or_an_earlier_date_is_refused_by_the_procedure()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);
        var contractId = await Service(db).CreateAsync(Form(customer.CustomerId, [Line(1, 1)]));

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).RenewAsync(contractId, Today.AddYears(1)));

        Assert.Equal("Error_DateInvalid", exception.MessageKey);
    }

    [Fact]
    public async Task Cancelling_keeps_the_contract_and_appends_the_reason()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);
        var contractId = await Service(db).CreateAsync(Form(customer.CustomerId, [Line(1, 1)],
            note: "Hợp đồng năm đầu"));

        await Service(db).CancelAsync(contractId, "Khách hàng đổi kế hoạch");

        var info = await db.VwContractInfos.AsNoTracking().SingleAsync(v => v.ContractId == contractId);

        Assert.Equal(WorkConstants.ContractStatus.Cancelled, info.Status);
        Assert.Contains("Hợp đồng năm đầu", info.Note);
        Assert.Contains("Khách hàng đổi kế hoạch", info.Note);
    }

    [Fact]
    public async Task A_cancelled_contract_cannot_be_cancelled_again()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);
        var contractId = await Service(db).CreateAsync(Form(customer.CustomerId, [Line(1, 1)]));
        await Service(db).CancelAsync(contractId, "once");

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).CancelAsync(contractId, "twice"));

        Assert.Equal("Error_ContractStatusForbids", exception.MessageKey);
    }

    [Fact]
    public async Task An_unknown_contract_id_is_reported_as_not_found()
    {
        await using var db = CreateContext();

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).CancelAsync(999_999, null));

        Assert.Equal("Error_ContractNotFound", exception.MessageKey);
    }

    [Fact]
    public async Task The_detail_page_reads_the_header_the_lines_and_the_audit_log()
    {
        await using var db = CreateContext();
        var customer = await AddCustomerAsync(db);
        var contractId = await Service(db).CreateAsync(Form(customer.CustomerId,
        [
            Line(1, 2),
            Line(3, 1)
        ]));

        var detail = await Service(db).GetDetailAsync(contractId);

        Assert.NotNull(detail);
        Assert.Equal(customer.CustomerName, detail.Header.CustomerName);
        Assert.Equal(2, detail.Lines.Count);
        Assert.Equal(18_000_000m, detail.Header.TotalValue);
        Assert.Contains(detail.Audit, entry => entry.Action == "Insert" && entry.EmployeeId == SalesEmployee);
    }

    [Fact]
    public async Task A_missing_contract_has_no_detail_page()
    {
        await using var db = CreateContext();

        Assert.Null(await Service(db).GetDetailAsync(999_999));
    }

    // ------------------------------------------------------------------ helpers

    private static ContractFormViewModel Form(
        int customerId,
        ContractLineInput[] lines,
        string status = WorkConstants.ContractStatus.Active,
        string? note = null) => new()
    {
        CustomerId = customerId,
        EmployeeId = SalesEmployee,
        SignDate = Today,
        EndDate = Today.AddYears(1),
        Status = status,
        Note = note,
        Lines = lines,
        LinesJson = ContractRules.BuildDetailJson(lines)
    };

    private static ContractLineInput Line(int productId, int quantity) =>
        new() { ProductId = productId, Quantity = quantity, UnitPrice = null };

    private static async Task<Customer> AddCustomerAsync(ApplicationDbContext db, bool isActive = true)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var customer = new Customer
        {
            CustomerName = $"Khách hợp đồng {suffix}",
            CustomerType = "Individual",
            Email = $"contract.{suffix}@test.local",
            Phone = "0900000000",
            Gender = "Male",
            CreatedDate = Today,
            Tier = WorkConstants.CustomerTiers.Standard,
            IsActive = isActive
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    private static ContractService Service(ApplicationDbContext db) =>
        new(db, NullLogger<ContractService>.Instance);

    private static ApplicationDbContext CreateContext()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(Utils.EmployeeIdClaim, SalesEmployee.ToString())], "test"))
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(TestDatabase.ConnectionString)
            .AddInterceptors(new SessionContextInterceptor(new HttpContextAccessor { HttpContext = httpContext }))
            .Options;

        return new ApplicationDbContext(options);
    }
}
