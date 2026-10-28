using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace CcmWeb.Services;

/// <summary>
/// Chuông thông báo: đọc đúng ba view mà design.md 3.2 chỉ định. Câu chữ của từng việc được dịch
/// ngay tại đây bằng resource key, nên phần view chỉ việc in ra.
///
/// Mỗi nhóm nằm trong một try/catch riêng: chuông nằm trong layout nên một view hỏng không được phép
/// làm hỏng cả trang (bug-report B03). Nhóm hỏng thì ẩn đi và không bị tính vào tổng, lỗi ghi vào log
/// máy chủ.
/// </summary>
public sealed class NotificationService(
    ApplicationDbContext db,
    IStringLocalizer<SharedResource> localizer,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<NotificationSummaryViewModel> GetSummaryAsync(
        string? role, CancellationToken ct = default)
    {
        return new NotificationSummaryViewModel
        {
            ExpiringContracts = await LoadGroupAsync(
                "expiring contracts", GetExpiringAsync, ct),
            OverdueFeedback = DashboardRules.SeesFeedback(role)
                ? await LoadGroupAsync("overdue feedback", GetOverdueFeedbackAsync, ct)
                : null,
            CareDue = await LoadGroupAsync("care schedule", GetCareDueAsync, ct)
        };
    }

    /// <summary>Chạy một nhóm; lỗi thì ghi log và trả <c>null</c> để nhóm đó chỉ đơn giản là không hiện.</summary>
    private async Task<NotificationGroup?> LoadGroupAsync(
        string blockName, Func<CancellationToken, Task<NotificationGroup>> load, CancellationToken ct)
    {
        try
        {
            return await load(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The {Block} notification group could not be read.", blockName);
            return null;
        }
    }

    private async Task<NotificationGroup> GetExpiringAsync(CancellationToken ct)
    {
        var count = await db.VwExpiringContracts.AsNoTracking().CountAsync(ct);

        var rows = await db.VwExpiringContracts
            .AsNoTracking()
            .OrderBy(view => view.EndDate)
            .ThenBy(view => view.ContractId)
            .Take(WorkConstants.Bell.ItemsPerGroup)
            .Select(view => new
            {
                view.ContractId,
                view.CustomerName,
                view.EndDate,
                view.DaysRemaining
            })
            .ToListAsync(ct);

        var items = rows
            .Select(row => new NotificationItem(
                row.CustomerName,
                localizer["Bell_Item_Expiring", Utils.FormatDate(row.EndDate), row.DaysRemaining ?? 0],
                $"/contracts/{row.ContractId}"))
            .ToList();

        return new NotificationGroup(
            "Bell_ExpiringContracts", "fa-file-contract", count, items,
            "/contracts?expiring=true");
    }

    private async Task<NotificationGroup> GetOverdueFeedbackAsync(CancellationToken ct)
    {
        var query = db.VwOpenFeedbacks
            .AsNoTracking()
            .Where(view => view.IsOverdue == 1);

        var count = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(view => view.Priority)
            .ThenBy(view => view.DueDate)
            .Take(WorkConstants.Bell.ItemsPerGroup)
            .Select(view => new { view.FeedbackId, view.CustomerName, view.DueDate })
            .ToListAsync(ct);

        var items = rows
            .Select(row => new NotificationItem(
                row.CustomerName,
                localizer["Bell_Item_FeedbackOverdue", Utils.FormatDate(row.DueDate)],
                $"/feedbacks/{row.FeedbackId}"))
            .ToList();

        return new NotificationGroup(
            "Bell_OverdueFeedback", "fa-comment-dots", count, items,
            "/feedbacks?overdue=true");
    }

    private async Task<NotificationGroup> GetCareDueAsync(CancellationToken ct)
    {
        var query = db.VwCareSchedules
            .AsNoTracking()
            .Where(view => view.ScheduleStatus == WorkConstants.ScheduleStatus.Overdue
                        || view.ScheduleStatus == WorkConstants.ScheduleStatus.Today);

        var count = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(view => view.NextAppointmentDate)
            .ThenBy(view => view.CustomerName)
            .Take(WorkConstants.Bell.ItemsPerGroup)
            .Select(view => new
            {
                view.CustomerId,
                view.CustomerName,
                view.CareType,
                view.NextAppointmentDate
            })
            .ToListAsync(ct);

        var items = rows
            .Select(row => new NotificationItem(
                row.CustomerName,
                localizer[
                    "Bell_Item_Care",
                    localizer.Label("CareType", row.CareType),
                    Utils.FormatDate(row.NextAppointmentDate)],
                $"/customers/{row.CustomerId}"))
            .ToList();

        return new NotificationGroup(
            "Bell_CareDue", "fa-heart", count, items, "/care");
    }
}
