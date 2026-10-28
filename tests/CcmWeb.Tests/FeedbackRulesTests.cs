using CcmWeb.Helpers;

namespace CcmWeb.Tests;

/// <summary>
/// The pure rules of the feedback screens: which statuses exist, when an assignee is required, how a
/// note is appended and how the list filters write themselves back into an address.
/// </summary>
public class FeedbackRulesTests
{
    [Theory]
    [InlineData(WorkConstants.FeedbackStatuses.New, true)]
    [InlineData(WorkConstants.FeedbackStatuses.InProgress, true)]
    [InlineData(WorkConstants.FeedbackStatuses.Resolved, true)]
    [InlineData(WorkConstants.FeedbackStatuses.Closed, true)]
    [InlineData("Unknown", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_the_four_coded_statuses_are_valid(string? status, bool valid) =>
        Assert.Equal(valid, FeedbackRules.IsValidStatus(status));

    [Theory]
    [InlineData(WorkConstants.FeedbackStatuses.New, false)]
    [InlineData(WorkConstants.FeedbackStatuses.InProgress, true)]
    [InlineData(WorkConstants.FeedbackStatuses.Resolved, true)]
    [InlineData(WorkConstants.FeedbackStatuses.Closed, true)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public void Every_status_but_new_needs_an_assignee(string? status, bool needed) =>
        Assert.Equal(needed, FeedbackRules.NeedsAssignee(status));

    [Fact]
    public void The_first_note_has_no_separator()
    {
        Assert.Equal("first", FeedbackRules.AppendNote(null, "first"));
        Assert.Equal("first", FeedbackRules.AppendNote("   ", " first "));
    }

    [Fact]
    public void A_new_note_is_appended_to_the_old_one()
    {
        Assert.Equal("old | new", FeedbackRules.AppendNote("old", "new"));
    }

    [Fact]
    public void An_empty_addition_keeps_the_existing_note()
    {
        Assert.Equal("old", FeedbackRules.AppendNote("old", null));
        Assert.Equal("old", FeedbackRules.AppendNote("old", "   "));
        Assert.Null(FeedbackRules.AppendNote(null, "   "));
    }

    [Fact]
    public void The_note_never_exceeds_the_column_length()
    {
        var current = new string('a', 495);

        var result = FeedbackRules.AppendNote(current, "1234567890");

        Assert.NotNull(result);
        Assert.Equal(FeedbackRules.MaxNoteLength, result!.Length);
        Assert.StartsWith(current, result);
    }

}
