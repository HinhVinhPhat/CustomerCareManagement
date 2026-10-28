using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.Entities;
using CcmWeb.Models.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// The contract screens (design.md 6.6-6.8). Every write goes through <c>sp_AddContract</c>,
/// <c>sp_SignContract</c>, <c>sp_RenewContract</c> and <c>sp_CancelContract</c>; the trigger owns
/// <c>TotalValue</c> and the customer tier, so the application never writes either.
/// </summary>
public sealed class ContractService(ApplicationDbContext db, ILogger<ContractService> logger) : IContractService
{
    public async Task<ContractSearchResult> SearchAsync(ContractFilter filter, CancellationToken ct = default)
    {
        var rows = await db.VwContractInfos.AsNoTracking().ToListAsync(ct);

        IEnumerable<VwContractInfo> query = rows;

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            query = query.Where(v => v.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(v => v.Status == filter.Status);
        }

        if (filter.Staff is { } staff)
        {
            query = query.Where(v => v.EmployeeId == staff);
        }

        if (filter.From is { } from)
        {
            query = query.Where(v => v.SignDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(v => v.SignDate <= to);
        }

        if (filter.Expiring)
        {
            var expiringIds = await db.VwExpiringContracts.AsNoTracking()
                .Select(v => v.ContractId).ToListAsync(ct);
            var set = expiringIds.ToHashSet();
            query = query.Where(v => set.Contains(v.ContractId));
        }

        // Sort
        query = (filter.Sort, filter.Desc) switch
        {
            (ContractFilter.SortByCustomerName, false) => query.OrderBy(v => v.CustomerName).ThenBy(v => v.ContractId),
            (ContractFilter.SortByCustomerName, true) => query.OrderByDescending(v => v.CustomerName).ThenByDescending(v => v.ContractId),
            (ContractFilter.SortByEndDate, false) => query.OrderBy(v => v.EndDate).ThenBy(v => v.ContractId),
            (ContractFilter.SortByEndDate, true) => query.OrderByDescending(v => v.EndDate).ThenByDescending(v => v.ContractId),
            (ContractFilter.SortByTotalValue, false) => query.OrderBy(v => v.TotalValue).ThenBy(v => v.ContractId),
            (ContractFilter.SortByTotalValue, true) => query.OrderByDescending(v => v.TotalValue).ThenByDescending(v => v.ContractId),
            (_, true) => query.OrderByDescending(v => v.SignDate).ThenByDescending(v => v.ContractId),
            _ => query.OrderBy(v => v.SignDate).ThenBy(v => v.ContractId),
        };

        var totalRows = query.Count();

        var page = query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(v => new ContractListRow(
                v.ContractId, v.CustomerId, v.CustomerName, v.EmployeeId, v.FullName,
                v.SignDate, v.EndDate, v.DaysRemaining, v.TotalValue, v.Status, v.Note))
            .ToList();

        return new ContractSearchResult(page, totalRows);
    }

    public async Task<ContractDetailViewModel?> GetDetailAsync(int contractId, CancellationToken ct = default)
    {
        var info = await db.VwContractInfos.AsNoTracking()
            .FirstOrDefaultAsync(v => v.ContractId == contractId, ct);

        if (info is null) return null;

        var lines = await db.VwContractDetails.AsNoTracking()
            .Where(v => v.ContractId == contractId)
            .OrderBy(v => v.ProductId)
            .Select(v => new ContractLineRow(
                v.ProductId, v.ProductName, v.ProductType, v.Quantity, v.UnitPrice, v.LineTotal))
            .ToListAsync(ct);

        var audit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.TableName == "CONTRACT" && a.RecordId == contractId)
            .OrderByDescending(a => a.LogTime)
            .Select(a => new ContractAuditRow(a.LogTime, a.EmployeeId, a.Employee == null ? null : a.Employee.FullName, a.Action, a.Detail))
            .ToListAsync(ct);

        // Suggested renew date: one year after the current end date, or 30 days from today if expired.
        var suggested = info.EndDate.AddYears(1);

        return new ContractDetailViewModel
        {
            Header = new ContractHeader(
                info.ContractId, info.CustomerId, info.CustomerName, info.EmployeeId, info.FullName,
                info.DepartmentName, info.SignDate, info.EndDate, info.DaysRemaining,
                info.TotalValue, info.Status, info.Note),
            Lines = lines,
            Audit = audit,
            SuggestedRenewDate = suggested,
        };
    }

    public async Task<int> CreateAsync(ContractFormViewModel form, CancellationToken ct = default)
    {
        // Build the JSON array the procedure expects.
        var lines = form.Lines.Count > 0 ? form.Lines : ContractRules.ParseLines(form.LinesJson);
        var json = ContractRules.BuildDetailJson(lines);

        var customerId = form.CustomerId!.Value;
        var employeeId = form.EmployeeId!.Value;
        var endDate = form.EndDate;
        var signDate = form.SignDate;
        var status = form.Status;
        var note = string.IsNullOrWhiteSpace(form.Note) ? null : form.Note.Trim();

        try
        {
            var outputParam = new SqlParameter("@ContractID", System.Data.SqlDbType.Int)
            {
                Direction = System.Data.ParameterDirection.Output,
            };

            await db.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_AddContract @CustomerID = {0}, @EmployeeID = {1}, @EndDate = {2}, " +
                "@DetailJson = {3}, @SignDate = {4}, @Note = {5}, @Status = {6}, @ContractID = {7} OUTPUT",
                new object[] { customerId, employeeId, endDate, json, signDate!, note!, status, outputParam },
                ct);

            return (int)outputParam.Value;
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "sp_AddContract failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task SignAsync(int contractId, DateOnly? signDate, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_SignContract @ContractID = {0}, @SignDate = {1}",
                new object[] { contractId, signDate! },
                ct);
        }
        catch (SqlException ex)
        {
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task RenewAsync(int contractId, DateOnly newEndDate, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_RenewContract @ContractID = {0}, @NewEndDate = {1}",
                new object[] { contractId, newEndDate },
                ct);
        }
        catch (SqlException ex)
        {
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task CancelAsync(int contractId, string? reason, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_CancelContract @ContractID = {0}, @Reason = {1}",
                new object[] { contractId, (string.IsNullOrWhiteSpace(reason) ? null : reason.Trim())! },
                ct);
        }
        catch (SqlException ex)
        {
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task<IReadOnlyList<CustomerOption>> GetCustomersAsync(CancellationToken ct = default) =>
        await db.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.CustomerName)
            .Select(c => new CustomerOption(c.CustomerId, c.CustomerName))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Models.ViewModels.EmployeeOption>> GetStaffAsync(CancellationToken ct = default) =>
        await db.Employees.AsNoTracking()
            .OrderBy(e => e.FullName)
            .Select(e => new Models.ViewModels.EmployeeOption(e.EmployeeId, e.FullName))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProductOption>> GetProductsAsync(CancellationToken ct = default) =>
        await db.ProductServices.AsNoTracking()
            .OrderBy(p => p.ProductName)
            .Select(p => new ProductOption(p.ProductId, p.ProductName, p.ProductType, p.UnitPrice))
            .ToListAsync(ct);
}

public sealed record ContractSearchResult(IReadOnlyList<ContractListRow> Rows, int TotalRows);
