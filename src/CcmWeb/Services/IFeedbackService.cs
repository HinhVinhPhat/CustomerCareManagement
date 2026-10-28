using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>One page of the feedback list plus the number of matches before paging.</summary>
public sealed record FeedbackSearchResult(IReadOnlyList<FeedbackRow> Rows, int TotalRows);

/// <summary>
/// The feedback screens (design.md 6.11). Open items (New, InProgress) come from
/// <c>vw_OpenFeedback</c>; Resolved and Closed come from <c>FEEDBACK</c> because the view
/// deliberately carries open items only. Assigning and changing the status go through
/// <c>sp_AssignFeedback</c> and <c>sp_UpdateFeedbackStatus</c>; <c>ResolvedDate</c> and the audit log
/// stay owned by their triggers.
/// </summary>
public interface IFeedbackService
{
    Task<FeedbackSearchResult> SearchAsync(FeedbackFilter filter, CancellationToken ct = default);

    Task<FeedbackCounters> GetCountersAsync(CancellationToken ct = default);

    Task<FeedbackDetailViewModel?> GetDetailAsync(int feedbackId, CancellationToken ct = default);

    Task<int> CreateAsync(FeedbackCreateViewModel form, CancellationToken ct = default);

    Task AssignAsync(int feedbackId, int employeeId, CancellationToken ct = default);

    Task UpdateStatusAsync(
        int feedbackId, string status, string? note, CancellationToken ct = default);

    Task AppendNoteAsync(int feedbackId, string note, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerOption>> GetCustomersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<EmployeeOption>> GetStaffAsync(CancellationToken ct = default);
}
