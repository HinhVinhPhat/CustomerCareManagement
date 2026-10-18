using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

public interface INotificationService
{
    /// <summary>Counts for the notification bell (design.md 3.2), always read from the database.</summary>
    Task<NotificationSummaryViewModel> GetSummaryAsync();
}
