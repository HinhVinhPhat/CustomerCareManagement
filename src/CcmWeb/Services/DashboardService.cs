using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

/// <summary>
/// Dựng trang tổng quan từ <c>sp_DashboardSummary</c>, <c>sp_MonthlyRevenue</c> và bốn view của file
/// 02. Doanh thu, số khách hàng, số phản hồi đều là con số của cơ sở dữ liệu, không tính lại ở đây.
///
/// Mỗi khối nằm trong một try/catch riêng: hỏng một khối thì chỉ khối đó có khóa lỗi, các khối còn
/// lại vẫn hiện. Toàn bộ phương thức chỉ đọc, không mở giao dịch ghi.
/// </summary>
public sealed class DashboardService(
    ApplicationDbContext db,
    IReportService reports,
    ILogger<DashboardService> logger) : IDashboardService
{
    public async Task<DashboardViewModel> GetAsync(
        int? year, string? role, CancellationToken ct = default)
    {
        var currentYear = DateTime.Today.Year;

        IReadOnlyList<int> years;

        try
        {
            years = await reports.GetYearsAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ô chọn năm không đọc được thì vẫn phải hiện trang với năm hiện tại, không phải trang lỗi.
            logger.LogError(ex, "The year picker could not be filled; falling back to {Year}.", currentYear);
            years = [currentYear];
        }

        var selectedYear = ReportRules.ClampYear(year, currentYear, years.Min(), years.Max());

        DashboardSummaryRow? summary = null;
        string? summaryError = null;

        try
        {
            var rows = await db.Database
                .SqlQueryRaw<DashboardSummaryRow>("EXEC dbo.sp_DashboardSummary")
                .ToListAsync(ct);

            summary = rows.FirstOrDefault();

            if (summary is null)
            {
                summaryError = "Error_Unexpected";
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "sp_DashboardSummary failed.");
            summaryError = BlockError(ex);
        }

        IReadOnlyList<MonthlyRevenueRow>? revenue = null;
        string? revenueError = null;

        if (DashboardRules.SeesRevenue(role))
        {
            try
            {
                revenue = await reports.GetRevenueAsync(selectedYear, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "The revenue block failed for year {Year}.", selectedYear);
                revenueError = BlockError(ex);
            }
        }

        IReadOnlyList<DashboardCustomerRow>? topCustomers = null;
        string? topCustomersError = null;

        if (DashboardRules.SeesRevenue(role))
        {
            try
            {
                topCustomers = await db.VwCustomerRevenues
                    .AsNoTracking()
                    .Where(view => view.IsActive)
                    .OrderByDescending(view => view.TotalValue)
                    .ThenBy(view => view.CustomerName)
                    .Take(WorkConstants.Dashboard.TopCustomerCount)
                    .Select(view => new DashboardCustomerRow(
                        view.CustomerId,
                        view.CustomerName,
                        view.Tier,
                        view.ContractCount,
                        view.TotalValue))
                    .ToListAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "The top-customer block failed.");
                topCustomersError = BlockError(ex);
            }
        }

        IReadOnlyList<DashboardExpiringRow> expiring = [];
        string? expiringError = null;

        try
        {
            expiring = await db.VwExpiringContracts
                .AsNoTracking()
                .OrderBy(view => view.EndDate)
                .ThenBy(view => view.ContractId)
                .Select(view => new DashboardExpiringRow(
                    view.ContractId,
                    view.CustomerId,
                    view.CustomerName,
                    view.EndDate,
                    view.DaysRemaining ?? 0,
                    view.TotalValue))
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The expiring-contract block failed.");
            expiringError = BlockError(ex);
        }

        IReadOnlyList<DashboardFeedbackRow>? feedback = null;
        string? feedbackError = null;

        if (DashboardRules.SeesFeedback(role))
        {
            try
            {
                feedback = await db.VwOpenFeedbacks
                    .AsNoTracking()
                    .OrderBy(view => view.Priority)
                    .ThenBy(view => view.DueDate)
                    .ThenBy(view => view.CreatedTime)
                    .Select(view => new DashboardFeedbackRow(
                        view.FeedbackId,
                        view.CustomerName,
                        view.Priority,
                        view.Content,
                        view.DueDate,
                        view.FullName,
                        view.IsOverdue == 1))
                    .ToListAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "The feedback block failed.");
                feedbackError = BlockError(ex);
            }
        }

        DashboardCareGroups care = new();
        string? careError = null;

        try
        {
            var rows = await db.VwCareSchedules
                .AsNoTracking()
                .OrderBy(view => view.NextAppointmentDate)
                .ThenBy(view => view.CustomerName)
                .Select(view => new DashboardCareRow(
                    view.CareId,
                    view.CustomerId,
                    view.CustomerName,
                    view.CareType,
                    view.Content,
                    view.NextAppointmentDate,
                    view.ScheduleStatus))
                .ToListAsync(ct);

            care = new DashboardCareGroups { Rows = rows };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The care-schedule block failed.");
            careError = BlockError(ex);
        }

        return new DashboardViewModel
        {
            Year = selectedYear,
            Years = years,
            Summary = summary,
            SummaryErrorKey = summaryError,
            Revenue = revenue,
            RevenueErrorKey = revenueError,
            TopCustomers = topCustomers,
            TopCustomersErrorKey = topCustomersError,
            Expiring = expiring,
            ExpiringErrorKey = expiringError,
            Feedback = feedback,
            FeedbackErrorKey = feedbackError,
            Care = care,
            CareErrorKey = careError,
            CanRenewContracts = DashboardRules.CanRenewContracts(role),
            ShowRevenue = DashboardRules.SeesRevenue(role),
            ShowFeedback = DashboardRules.SeesFeedback(role)
        };
    }

    /// <summary>Một lần đọc hỏng: dịch mã lỗi SQL nếu biết, còn không thì dùng thông báo chung.</summary>
    private static string BlockError(Exception exception) =>
        Utils.TranslateSqlError(exception) ?? "Error_Unexpected";
}
