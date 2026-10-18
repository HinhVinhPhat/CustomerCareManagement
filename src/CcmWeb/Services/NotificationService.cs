using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Services;

public sealed class NotificationService(ApplicationDbContext db) : INotificationService
{
    public async Task<NotificationSummaryViewModel> GetSummaryAsync()
    {
        var expiringContracts = await db.VwExpiringContracts
            .AsNoTracking()
            .CountAsync();

        var overdueFeedback = await db.VwOpenFeedbacks
            .AsNoTracking()
            .CountAsync(f => f.IsOverdue == 1);

        var careDue = await db.VwCareSchedules
            .AsNoTracking()
            .CountAsync(c => c.ScheduleStatus == WorkConstants.ScheduleStatus.Overdue
                          || c.ScheduleStatus == WorkConstants.ScheduleStatus.Today);

        return new NotificationSummaryViewModel
        {
            ExpiringContracts = expiringContracts,
            OverdueFeedback = overdueFeedback,
            CareDueTodayOrOverdue = careDue
        };
    }
}
