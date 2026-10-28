namespace CcmWeb.Models.ViewModels;

/// <summary>
/// The summary card of the customer detail page (design.md 6.5). <c>Age</c> comes from
/// <c>fn_CustomerAge</c> and <c>TotalValue</c> from <c>fn_CustomerContractTotal</c>, so neither is
/// calculated in the application.
/// </summary>
public sealed record CustomerHeader(
    int CustomerId,
    string CustomerName,
    string CustomerType,
    string? Email,
    string? Phone,
    string? Gender,
    DateOnly? DateOfBirth,
    string? Address,
    string? Source,
    DateOnly CreatedDate,
    string Tier,
    int? OwnerEmployeeId,
    string? OwnerName,
    bool IsActive,
    int? Age,
    decimal TotalValue);

/// <summary>The numbers on the Overview tab.</summary>
public sealed class CustomerOverview
{
    public int ContractCount { get; init; }

    public decimal TotalValue { get; init; }

    /// <summary>Average of the satisfaction scores that were given; null when none was.</summary>
    public double? AverageSatisfaction { get; init; }

    /// <summary>Open feedback, as <c>vw_OpenFeedback</c> defines "open" (New or InProgress).</summary>
    public int OpenFeedbackCount { get; init; }

    /// <summary>The next appointment of the latest care record, from <c>vw_CareSchedule</c>.</summary>
    public DateOnly? NextAppointmentDate { get; init; }

    /// <summary>Overdue / Today / Upcoming, again as the database defines it.</summary>
    public string? NextAppointmentStatus { get; init; }
}

/// <summary>One row of the Contracts tab, from <c>vw_ContractInfo</c>.</summary>
public sealed record CustomerContractRow(
    int ContractId,
    DateOnly SignDate,
    DateOnly EndDate,
    int? DaysRemaining,
    decimal TotalValue,
    string Status,
    string? Note,
    string OwnerName);

/// <summary>One entry of the Interaction history tab, from <c>vw_InteractionHistory</c>.</summary>
public sealed record CustomerHistoryRow(
    DateTime? LogTime,
    string Category,
    string SubType,
    string? Content,
    string? Result,
    string? EmployeeName);

/// <summary>One row of the Feedback tab.</summary>
public sealed record CustomerFeedbackRow(
    int FeedbackId,
    DateTime CreatedTime,
    string FeedbackType,
    string? Content,
    byte Priority,
    string FeedbackStatus,
    DateOnly? DueDate,
    string? OwnerName);

/// <summary>
/// The whole customer detail page. The three data tabs are read-only in M02: the buttons that
/// create a contract (M03) and record a contact, care or feedback answer (M04) arrive with those
/// modules.
/// </summary>
public sealed class CustomerDetailViewModel
{
    public required CustomerHeader Customer { get; init; }

    public CustomerOverview Overview { get; init; } = new();

    public IReadOnlyList<CustomerContractRow> Contracts { get; init; } = [];

    public IReadOnlyList<CustomerHistoryRow> History { get; init; } = [];

    public IReadOnlyList<CustomerFeedbackRow> Feedback { get; init; } = [];
}
