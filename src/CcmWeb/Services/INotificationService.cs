using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

public interface INotificationService
{
    /// <summary>
    /// Nội dung chuông thông báo (design.md 3.2): mỗi nhóm một số đếm, tối đa ba việc và đường dẫn
    /// tới danh sách đầy đủ. Nhóm mà vai trò không được mở là <c>null</c>.
    /// </summary>
    Task<NotificationSummaryViewModel> GetSummaryAsync(
        string? role, CancellationToken ct = default);
}
