namespace CcmWeb.Helpers;

/// <summary>
/// The colour a badge carries for a coded database value (design.md 2.2). The text always comes from
/// the resource files through <c>L.Label(group, code)</c>; this class only decides the colour, and a
/// status is never shown by colour alone.
/// </summary>
public static class Badges
{
    /// <summary>Customer tier: Standard, Silver, Gold, Diamond.</summary>
    public static string ForTier(string? tier) => tier switch
    {
        WorkConstants.CustomerTiers.Silver => "g-silver",
        WorkConstants.CustomerTiers.Gold => "g-gold",
        WorkConstants.CustomerTiers.Diamond => "g-diamond",
        _ => "g-standard"
    };

    /// <summary>Contract status, from <c>CHK_Contract_Status</c>.</summary>
    public static string ForContractStatus(string? status) => status switch
    {
        "Active" => "b-success",
        "Pending" => "b-warning",
        "Expired" => "b-neutral",
        "Cancelled" => "b-danger",
        _ => "b-neutral"
    };

    /// <summary>Feedback priority: 1 High, 2 Medium, 3 Low.</summary>
    public static string ForPriority(byte priority) => priority switch
    {
        1 => "b-danger",
        2 => "b-warning",
        _ => "b-neutral"
    };

    /// <summary>Feedback status, from <c>CHK_Feedback_Status</c>.</summary>
    public static string ForFeedbackStatus(string? status) => status switch
    {
        "New" => "b-info",
        "InProgress" => "b-warning",
        "Resolved" => "b-success",
        "Closed" => "b-neutral",
        _ => "b-neutral"
    };

    /// <summary>Care schedule: Overdue, Today, Upcoming (<c>vw_CareSchedule</c>).</summary>
    public static string ForScheduleStatus(string? status) => status switch
    {
        WorkConstants.ScheduleStatus.Overdue => "b-danger",
        WorkConstants.ScheduleStatus.Today => "b-warning",
        WorkConstants.ScheduleStatus.Upcoming => "b-info",
        _ => "b-neutral"
    };

    /// <summary>The soft-delete state of a customer: still cared for, or put aside.</summary>
    public static string ForActive(bool isActive) => isActive ? "b-success" : "b-neutral";

    /// <summary>An account that can sign in, or one a Manager has locked.</summary>
    public static string ForAccountState(bool isActive) => isActive ? "b-success" : "b-danger";

    /// <summary>The action of an <c>AUDIT_LOG</c> row. A delete and a status change stand out.</summary>
    public static string ForAuditAction(string? action) => action switch
    {
        "Insert" => "b-info",
        "Update" => "b-neutral",
        "Assign" => "b-warning",
        "StatusChange" => "b-warning",
        "Delete" => "b-danger",
        _ => "b-neutral"
    };
}
