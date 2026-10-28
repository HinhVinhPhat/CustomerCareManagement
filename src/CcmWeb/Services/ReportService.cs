using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// Ba tab báo cáo. Mỗi con số đến từ <c>sp_MonthlyRevenue</c>, <c>sp_MonthlyCareStats</c> hoặc
/// <c>vw_CustomerRevenue</c>; lớp này chỉ đọc, lọc theo hạng, sắp xếp và phân trang.
/// </summary>
public sealed class ReportService(
    ApplicationDbContext db,
    ILogger<ReportService> logger) : IReportService
{
    public async Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken ct = default)
    {
        try
        {
            var range = await db.Database
                .SqlQueryRaw<YearRangeRow>(
                    "SELECT MIN(YEAR(SignDate)) AS MinYear, MAX(YEAR(SignDate)) AS MaxYear FROM dbo.CONTRACT")
                .ToListAsync(ct);

            var row = range.FirstOrDefault();

            return ReportRules.AvailableYears(row?.MinYear, row?.MaxYear, DateTime.Today.Year);
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "Reading the contract year range failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task<IReadOnlyList<MonthlyRevenueRow>> GetRevenueAsync(
        int year, CancellationToken ct = default)
    {
        try
        {
            // Thủ tục luôn trả đủ 12 tháng; tháng không có hợp đồng là 0 thật, không phải tháng thiếu.
            var rows = await db.Database
                .SqlQueryRaw<MonthlyRevenueRow>(
                    "EXEC dbo.sp_MonthlyRevenue @Year = {0}", year)
                .ToListAsync(ct);

            return rows.OrderBy(row => row.Month).ToList();
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "sp_MonthlyRevenue failed for year {Year}.", year);
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task<CustomerRankResult> GetCustomerRankingAsync(
        CustomerRankFilter filter, bool paged, CancellationToken ct = default)
    {
        try
        {
            // View trả cả khách hàng đã ngừng hoạt động; báo cáo chỉ tính khách hàng đang hoạt động.
            var query = db.VwCustomerRevenues
                .AsNoTracking()
                .Where(view => view.IsActive);

            if (filter.Tier is not null)
            {
                query = query.Where(view => view.Tier == filter.Tier);
            }

            var totalRows = await query.CountAsync(ct);

            query = filter.Sort switch
            {
                CustomerRankFilter.SortByName => filter.Desc
                    ? query.OrderByDescending(view => view.CustomerName)
                    : query.OrderBy(view => view.CustomerName),
                CustomerRankFilter.SortByContracts => filter.Desc
                    ? query.OrderByDescending(view => view.ContractCount)
                        .ThenBy(view => view.CustomerName)
                    : query.OrderBy(view => view.ContractCount)
                        .ThenBy(view => view.CustomerName),
                _ => filter.Desc
                    ? query.OrderByDescending(view => view.TotalValue)
                        .ThenBy(view => view.CustomerName)
                    : query.OrderBy(view => view.TotalValue)
                        .ThenBy(view => view.CustomerName)
            };

            if (paged)
            {
                query = query
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize);
            }

            var rows = await query
                .Select(view => new CustomerRankRow(
                    view.CustomerId,
                    view.CustomerName,
                    view.CustomerType,
                    view.Tier,
                    view.ContractCount,
                    view.TotalValue))
                .ToListAsync(ct);

            return new CustomerRankResult(rows, totalRows);
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "Reading the customer ranking failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task<IReadOnlyList<TierShare>> GetTierSharesAsync(CancellationToken ct = default)
    {
        try
        {
            var counts = await db.VwCustomerRevenues
                .AsNoTracking()
                .Where(view => view.IsActive)
                .GroupBy(view => view.Tier)
                .Select(group => new { Tier = group.Key, Count = group.Count() })
                .ToListAsync(ct);

            return ReportRules.TierShares(
                counts.Select(count => (count.Tier, count.Count)));
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "Reading the customer tier counts failed.");
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }

    public async Task<IReadOnlyList<CareMonthRow>> GetCareAsync(
        int year, CancellationToken ct = default)
    {
        try
        {
            var rows = await db.Database
                .SqlQueryRaw<MonthlyCareStatRow>(
                    "EXEC dbo.sp_MonthlyCareStats @Year = {0}", year)
                .ToListAsync(ct);

            return ReportRules.PivotCareStats(rows);
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "sp_MonthlyCareStats failed for year {Year}.", year);
            throw new BusinessException(Utils.TranslateSqlError(ex) ?? "Error_Unexpected", inner: ex);
        }
    }
}
