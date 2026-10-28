using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations.Schema;

namespace CcmWeb.Services;

/// <summary>
/// The customer screens. The list is one call to <c>sp_SearchCustomer</c> - filtering, sorting and
/// paging all happen there, and the view's WHERE clause is never rebuilt in C# (AGENTS.md 4). Nothing
/// here deletes a customer: the soft delete goes through <c>sp_SetCustomerStatus</c>.
/// </summary>
public sealed class CustomerService(
    ApplicationDbContext db,
    IStringLocalizer<SharedResource> localizer,
    ILogger<CustomerService> logger) : ICustomerService
{
    public async Task<CustomerSearchResult> SearchAsync(
        CustomerFilter filter, int? ownerEmployeeId, CancellationToken ct = default)
    {
        var keyword = string.IsNullOrWhiteSpace(filter.Q) ? null : filter.Q.Trim();

        List<CustomerSearchRow> rows;

        try
        {
            rows = await db.Database
                .SqlQueryRaw<CustomerSearchRow>(
                    """
                    EXEC dbo.sp_SearchCustomer
                         @Keyword = {0}, @Tier = {1}, @OwnerEmployeeID = {2}, @ActiveOnly = {3},
                         @PageNumber = {4}, @PageSize = {5}, @SortBy = {6}, @SortDesc = {7}
                    """,
                    keyword!,
                    filter.Tier!,
                    ownerEmployeeId!,
                    !filter.IncludeInactive,
                    filter.Page,
                    filter.PageSize,
                    filter.Sort,
                    filter.Desc)
                .ToListAsync(ct);
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "sp_SearchCustomer failed (page {Page}, size {PageSize}).",
                filter.Page, filter.PageSize);
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }

        // The count comes with every row because the procedure counts before it pages; a page past
        // the end has no row at all, and then there is nothing to count.
        var totalRows = rows.Count > 0 ? rows[0].TotalRows : 0;

        var results = rows
            .Select(row => new CustomerListRow(
                row.CustomerId,
                row.CustomerName,
                row.CustomerType,
                row.Email,
                row.Phone,
                row.Address,
                row.Tier,
                row.CreatedDate,
                row.OwnerEmployeeId,
                row.OwnerName,
                row.IsActive,
                row.TotalValue))
            .ToList();

        return new CustomerSearchResult(results, totalRows);
    }

    public async Task<CustomerDetailViewModel?> GetDetailAsync(int customerId, CancellationToken ct = default)
    {
        var found = await db.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => new { Customer = c, OwnerName = c.OwnerEmployee!.FullName })
            .FirstOrDefaultAsync(ct);

        if (found is null)
        {
            return null;
        }

        // Age and the total contract value are the database's own answers (fn_CustomerAge,
        // fn_CustomerContractTotal), never a calculation in the application.
        var facts = await db.Database
            .SqlQueryRaw<CustomerFacts>(
                "SELECT dbo.fn_CustomerAge({0}) AS Age, dbo.fn_CustomerContractTotal({0}) AS TotalValue",
                customerId)
            .ToListAsync(ct);

        var fact = facts.FirstOrDefault();

        var contracts = await db.VwContractInfos
            .AsNoTracking()
            .Where(v => v.CustomerId == customerId)
            .OrderByDescending(v => v.SignDate)
            .ThenByDescending(v => v.ContractId)
            .ToListAsync(ct);

        var history = await db.VwInteractionHistories
            .AsNoTracking()
            .Where(v => v.CustomerId == customerId)
            .OrderByDescending(v => v.LogTime)
            .ToListAsync(ct);

        var feedback = await db.Feedbacks
            .AsNoTracking()
            .Where(f => f.CustomerId == customerId)
            .OrderByDescending(f => f.CreatedTime)
            .ThenByDescending(f => f.FeedbackId)
            .Select(f => new CustomerFeedbackRow(
                f.FeedbackId,
                f.CreatedTime,
                f.FeedbackType,
                f.Content,
                f.Priority,
                f.FeedbackStatus,
                f.DueDate,
                f.Employee!.FullName))
            .ToListAsync(ct);

        // "Open" is what vw_OpenFeedback says it is (New or InProgress), not a list of statuses here.
        var openFeedback = await db.VwOpenFeedbacks
            .AsNoTracking()
            .CountAsync(v => v.CustomerId == customerId, ct);

        var satisfaction = await db.Cares
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId && c.SatisfactionScore != null)
            .Select(c => (double?)c.SatisfactionScore)
            .AverageAsync(ct);

        // The next appointment of the latest care record, exactly as vw_CareSchedule defines it.
        var schedule = await db.VwCareSchedules
            .AsNoTracking()
            .Where(v => v.CustomerId == customerId)
            .OrderBy(v => v.NextAppointmentDate)
            .FirstOrDefaultAsync(ct);

        var customer = found.Customer;

        return new CustomerDetailViewModel
        {
            Customer = new CustomerHeader(
                customer.CustomerId,
                customer.CustomerName,
                customer.CustomerType,
                customer.Email,
                customer.Phone,
                customer.Gender,
                customer.DateOfBirth,
                customer.Address,
                customer.Source,
                customer.CreatedDate,
                customer.Tier,
                customer.OwnerEmployeeId,
                found.OwnerName,
                customer.IsActive,
                fact?.Age,
                fact?.TotalValue ?? 0m),
            Overview = new CustomerOverview
            {
                ContractCount = contracts.Count,
                TotalValue = fact?.TotalValue ?? 0m,
                AverageSatisfaction = satisfaction,
                OpenFeedbackCount = openFeedback,
                NextAppointmentDate = schedule?.NextAppointmentDate,
                NextAppointmentStatus = schedule?.ScheduleStatus
            },
            Contracts = contracts
                .Select(c => new CustomerContractRow(
                    c.ContractId, c.SignDate, c.EndDate, c.DaysRemaining, c.TotalValue, c.Status, c.Note, c.FullName))
                .ToList(),
            History = history
                .Select(h => new CustomerHistoryRow(
                    h.LogTime, h.Category, h.SubType, h.Content, h.Result, h.FullName))
                .ToList(),
            Feedback = feedback
        };
    }

    public async Task<CustomerFormViewModel?> GetForEditAsync(int customerId, CancellationToken ct = default)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

        if (customer is null)
        {
            return null;
        }

        return new CustomerFormViewModel
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.CustomerName,
            CustomerType = customer.CustomerType,
            Email = customer.Email,
            Phone = customer.Phone,
            Gender = customer.Gender,
            DateOfBirth = customer.DateOfBirth,
            Address = customer.Address,
            Source = customer.Source,
            OwnerEmployeeId = customer.OwnerEmployeeId,
            Owners = await GetOwnersAsync(ct)
        };
    }

    public async Task<int> CreateAsync(CustomerFormViewModel form, CancellationToken ct = default)
    {
        // Tier, CreatedDate and IsActive are left to the database defaults and to the trigger that
        // owns the tier, so the application never invents them.
        var customer = new Customer();
        Apply(form, customer);

        db.Customers.Add(customer);
        await SaveAsync(ct);

        return customer.CustomerId;
    }

    public async Task UpdateAsync(int customerId, CustomerFormViewModel form, CancellationToken ct = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct)
            ?? throw new BusinessException("Error_CustomerNotFound");

        Apply(form, customer);
        await SaveAsync(ct);
    }

    public async Task SetStatusAsync(int customerId, bool isActive, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_SetCustomerStatus @CustomerID = {0}, @IsActive = {1}",
                [customerId, isActive],
                ct);
        }
        catch (SqlException ex)
        {
            // 50006 when the customer is not there (the procedure checks before it updates).
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task<IReadOnlyList<EmployeeOption>> GetOwnersAsync(CancellationToken ct = default) =>
        await db.Employees
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .Select(e => new EmployeeOption(e.EmployeeId, e.FullName))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CustomerSuggestion>> SuggestAsync(
        string? keyword, int count, CancellationToken ct = default)
    {
        var term = keyword?.Trim();

        // Gõ một ký tự thì chưa gọi cơ sở dữ liệu: vừa nhanh, vừa tránh một loạt truy vấn vô ích.
        if (term is null || term.Length < WorkConstants.Search.MinKeywordLength)
        {
            return [];
        }

        var size = Math.Clamp(count, 1, WorkConstants.Search.SuggestionCount);

        try
        {
            var rows = await db.Database
                .SqlQueryRaw<CustomerSearchRow>(
                    """
                    EXEC dbo.sp_SearchCustomer
                         @Keyword = {0}, @ActiveOnly = {1}, @PageNumber = {2}, @PageSize = {3},
                         @SortBy = {4}
                    """,
                    term,
                    true,
                    1,
                    size,
                    CustomerFilter.SortByName)
                .ToListAsync(ct);

            return rows
                .Select(row => new CustomerSuggestion(
                    row.CustomerId,
                    row.CustomerName,
                    row.CustomerType,
                    localizer.Label("CustomerType", row.CustomerType).Value))
                .ToList();
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "sp_SearchCustomer failed for the quick search.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    /// <summary>
    /// Copies the form onto the entity. An empty optional field becomes NULL, never an empty string,
    /// and a business customer always loses its gender and date of birth, so CHK_Customer_Business
    /// can never be violated by a hand-crafted POST.
    /// </summary>
    private static void Apply(CustomerFormViewModel form, Customer customer)
    {
        var isBusiness = WorkConstants.CustomerTypes.IsBusiness(form.CustomerType);

        customer.CustomerName = form.CustomerName.Trim();
        customer.CustomerType = form.CustomerType;
        customer.Email = Trimmed(form.Email);
        customer.Phone = Trimmed(form.Phone);
        customer.Gender = isBusiness ? null : Trimmed(form.Gender);
        customer.DateOfBirth = isBusiness ? null : form.DateOfBirth;
        customer.Address = Trimmed(form.Address);
        customer.Source = Trimmed(form.Source);
        customer.OwnerEmployeeId = form.OwnerEmployeeId;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // CHK_Customer_*, FK_Customer_Owner and UX_Customer_Email (2601) all arrive here. A
            // resource key travels up; the English text SQL Server produced never reaches a screen.
            logger.LogWarning(ex, "Saving a customer failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }
}

/// <summary>
/// One row of <c>sp_SearchCustomer</c>, including the count of matches before paging. A plain class
/// with settable properties is what the raw query can materialise.
/// </summary>
public sealed class CustomerSearchRow
{
    public int CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string CustomerType { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public string Tier { get; set; } = null!;

    public DateOnly CreatedDate { get; set; }

    public int? OwnerEmployeeId { get; set; }

    public string? OwnerName { get; set; }

    public bool IsActive { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalValue { get; set; }

    /// <summary>Matching customers in total, counted by the procedure before it pages.</summary>
    public int TotalRows { get; set; }
}

/// <summary>The two values the detail header takes from scalar functions.</summary>
public sealed class CustomerFacts
{
    /// <summary>Null for a business customer, which has no date of birth.</summary>
    public int? Age { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalValue { get; set; }
}
