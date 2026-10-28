using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;

namespace CcmWeb.Tests;

/// <summary>
/// Các phép tính thuần của trang tổng quan và báo cáo (M05): năm, biểu đồ, xoay dữ liệu chăm sóc,
/// tỷ lệ hạng khách hàng. Không cần cơ sở dữ liệu.
/// </summary>
public class ReportRulesTests
{
    [Theory]
    [InlineData(null, 2026, 2020, 2026, 2026)]
    [InlineData(0, 2026, 2020, 2026, 2026)]
    [InlineData(10000, 2026, 2020, 2026, 2026)]
    [InlineData(2024, 2026, 2020, 2026, 2024)]
    [InlineData(2019, 2026, 2020, 2026, 2026)]   // below min -> current year
    [InlineData(2027, 2026, 2020, 2026, 2026)]   // above max -> current year
    public void ClampYear_returns_a_safe_year(int? year, int current, int? min, int? max, int expected)
    {
        Assert.Equal(expected, ReportRules.ClampYear(year, current, min, max));
    }

    [Fact]
    public void AvailableYears_always_contains_the_current_year()
    {
        var years = ReportRules.AvailableYears(minSignYear: 2024, maxSignYear: 2025, currentYear: 2026);

        Assert.Contains(2026, years);
        Assert.Equal(years.OrderByDescending(y => y).ToList(), years.ToList());
    }

    [Fact]
    public void AvailableYears_is_continuous_and_newest_first()
    {
        var years = ReportRules.AvailableYears(2024, 2026, 2026);

        Assert.Equal(new[] { 2026, 2025, 2024 }, years);
    }

    [Fact]
    public void AvailableYears_limits_an_absurd_range_to_ten_years()
    {
        var years = ReportRules.AvailableYears(1900, 2026, 2026);

        Assert.True(years.Count <= 11);
        Assert.Contains(2026, years);
    }

    [Fact]
    public void BarHeights_are_zero_when_there_is_no_data()
    {
        Assert.Equal(new[] { 0d, 0d }, ReportRules.BarHeights([0m, 0m]));
    }

    [Fact]
    public void BarHeights_give_the_largest_value_the_full_height()
    {
        var heights = ReportRules.BarHeights([10m, 5m]);

        Assert.Equal(100d, heights[0]);
        Assert.Equal(50d, heights[1]);
    }

    [Fact]
    public void BarHeights_keep_a_positive_value_visible()
    {
        var heights = ReportRules.BarHeights([100m, 1m]);

        Assert.True(heights[1] >= 4);
    }

    [Fact]
    public void BarHeights_handle_an_empty_list()
    {
        Assert.Empty(ReportRules.BarHeights([]));
    }

    [Fact]
    public void PivotCareStats_produces_twelve_months()
    {
        var rows = new[]
        {
            new MonthlyCareStatRow { Month = 3, CareType = WorkConstants.CareTypes.Survey, VisitCount = 2, AvgSatisfaction = 4.5m }
        };

        var result = ReportRules.PivotCareStats(rows);

        Assert.Equal(12, result.Count);
        Assert.Equal(2, result[2].TotalVisits);
        Assert.Equal(4.5m, result[2].SurveyAverage);
        Assert.Null(result[0].SurveyAverage);
    }

    [Fact]
    public void PivotCareStats_keeps_the_survey_score_out_of_other_types()
    {
        var rows = new[]
        {
            new MonthlyCareStatRow { Month = 1, CareType = WorkConstants.CareTypes.Renewal, VisitCount = 3, AvgSatisfaction = 99m }
        };

        var result = ReportRules.PivotCareStats(rows);

        Assert.Null(result[0].SurveyAverage);
        Assert.Equal(3, result[0].TotalVisits);
    }

    [Fact]
    public void TierShares_sum_to_one_hundred_when_there_are_customers()
    {
        var shares = ReportRules.TierShares(
        [
            (WorkConstants.CustomerTiers.Standard, 1),
            (WorkConstants.CustomerTiers.Gold, 2)
        ]);

        Assert.Equal(100, shares.Sum(share => share.Percent));
        Assert.Equal(3, shares.Sum(share => share.Count));
    }

    [Fact]
    public void TierShares_are_zero_when_there_are_no_customers()
    {
        var shares = ReportRules.TierShares([]);

        Assert.All(shares, share => Assert.Equal(0, share.Percent));
        Assert.All(shares, share => Assert.Equal(0, share.Count));
    }

    [Fact]
    public void TierShares_cover_every_tier_in_order()
    {
        var shares = ReportRules.TierShares([]);

        Assert.Equal(WorkConstants.CustomerTiers.All, shares.Select(share => share.Tier).ToArray());
    }
}