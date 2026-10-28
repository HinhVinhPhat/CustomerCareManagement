using CcmWeb.Models.ViewModels;

namespace CcmWeb.Tests;

/// <summary>
/// The contact list is newest first by default, so the default address is empty and only a change
/// from that default is written into the pager links.
/// </summary>
public class ContactFilterTests
{
    [Fact]
    public void The_contact_filter_defaults_to_newest_first_and_only_writes_the_exception()
    {
        var ascending = new ContactFilter { Desc = false };

        Assert.Equal("?desc=false", ascending.PageQuery(1));
        Assert.Equal(string.Empty, new ContactFilter().PageQuery(1));
    }
}
