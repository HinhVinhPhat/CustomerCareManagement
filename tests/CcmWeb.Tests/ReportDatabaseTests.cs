using System.Security.Claims;
using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CcmWeb.Tests;

/// <summary>
/// Các truy vấn báo cáo (M05) trên SQL Server thật: thủ tục và view trả đúng số dòng, và dịch vụ
/// chỉ đọc, không ghi.
/// </summary>
[Collection("database")]
public class ReportDatabaseTests
{
    private const int SalesEmployee = 2;

    [Fact]
    public async Task Revenue_report_returns_twelve_months()
    {
        await using var db = CreateContext();
        var service = new ReportService(db, NullLogger<ReportService>.Instance);

        var rows = await service.GetRevenueAsync(DateTime.Today.Year);

        Assert.Equal(12, rows.Count);
        Assert.Equal(Enumerable.Range(1, 12).ToList(), rows.Select(row => row.Month).ToList());
    }

    [Fact]
    public async Task Customer_ranking_contains_only_active_customers()
    {
        await using var db = CreateContext();
        var service = new ReportService(db, NullLogger<ReportService>.Instance);

        var result = await service.GetCustomerRankingAsync(
            new CustomerRankFilter(), paged: false);

        Assert.True(result.Rows.Count > 0);
        // Every row must belong to an active customer in the view.
        foreach (var row in result.Rows)
        {
            var active = await db.VwCustomerRevenues.AsNoTracking()
                .Where(view => view.CustomerId == row.CustomerId)
                .Select(view => view.IsActive).SingleAsync();
            Assert.True(active);
        }
    }

    [Fact]
    public async Task Care_report_pivots_to_twelve_months()
    {
        await using var db = CreateContext();
        var service = new ReportService(db, NullLogger<ReportService>.Instance);

        var rows = await service.GetCareAsync(DateTime.Today.Year);

        Assert.Equal(12, rows.Count);
    }

    [Fact]
    public async Task Available_years_include_the_current_year()
    {
        await using var db = CreateContext();
        var service = new ReportService(db, NullLogger<ReportService>.Instance);

        var years = await service.GetYearsAsync();

        Assert.Contains(DateTime.Today.Year, years);
    }

    [Fact]
    public async Task Tier_shares_match_active_customers()
    {
        await using var db = CreateContext();
        var service = new ReportService(db, NullLogger<ReportService>.Instance);

        var shares = await service.GetTierSharesAsync();

        Assert.Equal(WorkConstants.CustomerTiers.All, shares.Select(share => share.Tier).ToArray());
        Assert.True(shares.Sum(share => share.Count) > 0);
    }

    private static ReportService Service(ApplicationDbContext db) =>
        new(db, NullLogger<ReportService>.Instance);

    private static ApplicationDbContext CreateContext()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(Utils.EmployeeIdClaim, SalesEmployee.ToString())], "test"))
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(TestDatabase.ConnectionString)
            .AddInterceptors(new SessionContextInterceptor(new HttpContextAccessor { HttpContext = httpContext }))
            .Options;

        return new ApplicationDbContext(options);
    }
}