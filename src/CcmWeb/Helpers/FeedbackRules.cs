namespace CcmWeb.Helpers;

/// <summary>
/// The rules of <c>FEEDBACK</c> shared by the service, the controller and the tests (design.md 6.11).
/// The status change itself goes through <c>sp_UpdateFeedbackStatus</c>; the assignment goes through
/// <c>sp_AssignFeedback</c>. The service never writes <c>ResolvedDate</c> or the audit log.
/// </summary>
public static class FeedbackRules
{
    /// <summary>Length of <c>FEEDBACK.Note</c>.</summary>
    public const int MaxNoteLength = 500;

    /// <summary>Length of <c>FEEDBACK.Content</c>.</summary>
    public const int MaxContentLength = 2000;

    /// <summary>Separator used when a new note is appended to the existing one.</summary>
    public const string NoteSeparator = " | ";

    public static bool IsValidStatus(string? status) =>
        status is not null && WorkConstants.FeedbackStatuses.All.Contains(status);

    /// <summary>
    /// Only <c>New</c> may be left without an assignee; every other status needs one
    /// (<c>CHK_Feedback_Assignee</c> and error 50004 carry the same rule).
    /// </summary>
    public static bool NeedsAssignee(string? status) =>
        IsValidStatus(status) && status != WorkConstants.FeedbackStatuses.New;

    /// <summary>
    /// Appends the new text to the old note, so the history is never silently replaced. An empty
    /// addition changes nothing; the result never exceeds <see cref="MaxNoteLength"/>.
    /// </summary>
    public static string? AppendNote(string? current, string? addition)
    {
        var extra = addition?.Trim();

        if (string.IsNullOrEmpty(extra))
        {
            return current;
        }

        var text = string.IsNullOrWhiteSpace(current)
            ? extra
            : current.TrimEnd() + NoteSeparator + extra;

        return text.Length <= MaxNoteLength ? text : text[..MaxNoteLength].TrimEnd();
    }
}
