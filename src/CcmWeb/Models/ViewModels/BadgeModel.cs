using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>
/// One badge: a coded database value shown as the label for the reader's language, with the colour
/// design.md 2.2 gives that group. Views pass it to <c>Views/Shared/_Badge.cshtml</c>.
/// </summary>
/// <param name="Group">Label group in the resource files: CustomerTier, ContractStatus, Priority,
/// FeedbackStatus, ScheduleStatus or CustomerActive.</param>
/// <param name="Code">The value exactly as the database stores it (English code).</param>
/// <param name="Style">CSS class that carries the colour (b-success, g-gold, ...).</param>
public sealed record BadgeModel(string Group, string Code, string Style)
{
    /// <summary>Tier of a customer: Standard, Silver, Gold, Diamond.</summary>
    public static BadgeModel Tier(string? tier) =>
        new("Tier", tier ?? WorkConstants.CustomerTiers.Standard, Badges.ForTier(tier));

    /// <summary>Status of a contract.</summary>
    public static BadgeModel ContractStatus(string? status) =>
        new("Contract_Status", status ?? string.Empty, Badges.ForContractStatus(status));

    /// <summary>Priority of a feedback item: the code is the stored number 1 / 2 / 3.</summary>
    public static BadgeModel Priority(byte priority) =>
        new("Priority", priority is 1 or 2 or 3 ? priority.ToString() : "3", Badges.ForPriority(priority));

    /// <summary>Status of a feedback item.</summary>
    public static BadgeModel FeedbackStatus(string? status) =>
        new("FeedbackStatus", status ?? string.Empty, Badges.ForFeedbackStatus(status));

    /// <summary>Overdue / Today / Upcoming, as <c>vw_CareSchedule</c> decides it.</summary>
    public static BadgeModel ScheduleStatus(string? status) =>
        new("ScheduleStatus", status ?? string.Empty, Badges.ForScheduleStatus(status));

    /// <summary>Whether a customer is still cared for (soft delete).</summary>
    public static BadgeModel Active(bool isActive) => new(
        "Customer",
        isActive ? "Active" : "Inactive",
        Badges.ForActive(isActive));
}
