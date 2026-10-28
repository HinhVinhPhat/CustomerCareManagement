using CcmWeb.Helpers;

namespace CcmWeb.Tests;

/// <summary>
/// Khối nào của trang tổng quan thuộc vai trò nào (M05): Kinh doanh không có phản hồi,
/// Hỗ trợ không có doanh thu.
/// </summary>
public class DashboardRolesTests
{
    [Theory]
    [InlineData("Manager", true, true)]
    [InlineData("Sales", false, true)]
    [InlineData("Support", true, false)]
    [InlineData(null, false, false)]
    [InlineData("Nope", false, false)]
    public void Roles_see_the_right_blocks(string? role, bool seesFeedback, bool seesRevenue)
    {
        Assert.Equal(seesFeedback, DashboardRules.SeesFeedback(role));
        Assert.Equal(seesRevenue, DashboardRules.SeesRevenue(role));
        Assert.Equal(seesRevenue, DashboardRules.CanRenewContracts(role));
    }
}