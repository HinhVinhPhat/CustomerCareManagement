using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;

namespace CcmWeb.Tests;

/// <summary>
/// The contract list keeps its filters, sort and page in the address (design.md 6.6). These tests
/// pin the shape of that query string so a pager or sort link cannot silently drop a filter.
/// </summary>
public class ContractFilterTests
{
    [Fact]
    public void The_expiring_filter_is_written_as_a_boolean_the_controller_can_bind()
    {
        var filter = new ContractFilter { Expiring = true };

        var query = filter.PageQuery(2);

        Assert.Contains("expiring=true", query);
        Assert.DoesNotContain("expiring=1", query);
    }

    [Fact]
    public void A_sort_link_keeps_every_active_filter()
    {
        var filter = new ContractFilter
        {
            Q = "ABC",
            Status = WorkConstants.ContractStatus.Active,
            Staff = 2,
            From = new DateOnly(2026, 1, 1),
            To = new DateOnly(2026, 12, 31),
            Expiring = true
        };

        var query = filter.SortQuery(ContractFilter.SortByEndDate);

        Assert.Contains("q=ABC", query);
        Assert.Contains("status=Active", query);
        Assert.Contains("staff=2", query);
        Assert.Contains("from=2026-01-01", query);
        Assert.Contains("to=2026-12-31", query);
        Assert.Contains("expiring=true", query);
        Assert.Contains("sort=EndDate", query);
        Assert.DoesNotContain("desc=true", query);
    }

    [Fact]
    public void Clicking_the_sort_column_that_is_already_ascending_asks_for_descending()
    {
        var filter = new ContractFilter { Sort = ContractFilter.SortByEndDate };

        Assert.Contains("desc=true", filter.SortQuery(ContractFilter.SortByEndDate));
    }

    [Fact]
    public void A_page_size_link_goes_back_to_the_first_page()
    {
        var filter = new ContractFilter { Page = 3, PageSize = 10 };

        var query = filter.PageSizeQuery(50);

        Assert.Contains("pageSize=50", query);
        Assert.DoesNotContain("page=", query);
    }

    [Fact]
    public void The_default_state_has_no_query_string()
    {
        var filter = new ContractFilter();

        Assert.Equal(string.Empty, filter.PageQuery(1));
        Assert.False(filter.HasFilters);
    }

    [Fact]
    public void A_filter_with_a_value_reports_that_it_has_filters()
    {
        Assert.True(new ContractFilter { Status = WorkConstants.ContractStatus.Pending }.HasFilters);
        Assert.True(new ContractFilter { Expiring = true }.HasFilters);
        Assert.True(new ContractFilter { Staff = 1 }.HasFilters);
    }
}
