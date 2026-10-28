namespace CcmWeb.Models.ViewModels;

/// <summary>Một việc trong nhóm thông báo: tiêu đề, dòng phụ và đường dẫn mở thẳng tới việc đó.</summary>
public sealed record NotificationItem(string Title, string Detail, string? Link);

/// <summary>
/// Một nhóm của chuông: số việc, vài việc đầu tiên và đường dẫn tới danh sách đầy đủ. Nhóm mà vai
/// trò không được mở là <c>null</c> - vừa không hiện, vừa không bị tính vào tổng trên chuông.
/// </summary>
public sealed record NotificationGroup(
    string TitleKey,
    string Icon,
    int Count,
    IReadOnlyList<NotificationItem> Items,
    string Url);

/// <summary>Nội dung chuông thông báo (design.md 3.2), luôn đọc từ cơ sở dữ liệu.</summary>
public sealed class NotificationSummaryViewModel
{
    public NotificationGroup? ExpiringContracts { get; init; }

    public NotificationGroup? OverdueFeedback { get; init; }

    public NotificationGroup? CareDue { get; init; }

    /// <summary>Ba nhóm theo thứ tự hiển thị; nhóm bị ẩn không có mặt.</summary>
    public IReadOnlyList<NotificationGroup> Groups =>
        new[] { ExpiringContracts, OverdueFeedback, CareDue }
            .OfType<NotificationGroup>()
            .ToList();

    public int Total => Groups.Sum(group => group.Count);
}
