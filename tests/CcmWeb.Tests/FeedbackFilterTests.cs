using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;

namespace CcmWeb.Tests;

/// <summary>
/// The feedback filter must carry every active filter into the sort and pager links, so a copied
/// address opens the same list.
/// </summary>
public class FeedbackFilterTests
{
    [Fact]
    public void The_feedback_filter_writes_status_sort_and_page_into_the_address()
    {
        var filter = new FeedbackFilter
        {
            Status = WorkConstants.FeedbackStatuses.Resolved,
            Priority = WorkConstants.FeedbackPriorities.High,
            Overdue = false,
            Sort = FeedbackFilter.SortByDueDate,
            Desc = true,
            Page = 2,
            PageSize = 10
        };

        var query = filter.PageQuery(2);

        Assert.Contains("status=Resolved", query);
        Assert.Contains("priority=1", query);
        Assert.Contains("sort=DueDate", query);
        Assert.Contains("desc=true", query);
        Assert.Contains("page=2", query);
        Assert.Contains("pageSize=10", query);
    }
}
