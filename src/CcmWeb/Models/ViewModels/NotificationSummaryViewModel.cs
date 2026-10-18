namespace CcmWeb.Models.ViewModels;

/// <summary>Counts shown by the notification bell (design.md 3.2).</summary>
public sealed class NotificationSummaryViewModel
{
    public int ExpiringContracts { get; init; }

    public int OverdueFeedback { get; init; }

    public int CareDueTodayOrOverdue { get; init; }

    public int Total => ExpiringContracts + OverdueFeedback + CareDueTodayOrOverdue;
}
