using CcmWeb.Models.ViewModels;

namespace CcmWeb.Tests;

/// <summary>
/// The care log lives in a tab, so its pager links must keep <c>tab=log</c>; otherwise the user
/// silently falls back to the schedule.
/// </summary>
public class CareFilterTests
{
    [Fact]
    public void The_care_filter_always_keeps_the_log_tab()
    {
        var filter = new CareFilter { Q = "An", PageSize = 20 };

        Assert.Equal("?tab=log&q=An", filter.PageQuery(1));
    }
}
