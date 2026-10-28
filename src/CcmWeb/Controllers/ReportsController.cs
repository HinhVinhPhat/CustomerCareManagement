using System.Globalization;
using CcmWeb.Helpers;
using CcmWeb.Models.ViewModels;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CcmWeb.Controllers;

/// <summary>
/// Ba tab báo cáo (design.md 6.12). Quyền được kiểm bằng <see cref="AuthorizeAttribute"/> trên từng
/// action, không chỉ bằng việc ẩn liên kết; tệp CSV dùng đúng quyền của tab chứa nó.
///
/// Bộ lọc đọc từ query string được làm sạch trước khi tới service hoặc trước khi trở thành một phần
/// của tên tệp.
/// </summary>
[Authorize]
public class ReportsController(
    IReportService reports,
    ISettingService settings,
    IStringLocalizer<SharedResource> localizer,
    ILogger<ReportsController> logger) : Controller
{
    private const string RevenueRoles =
        $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Sales}";

    private const string CareRoles =
        $"{WorkConstants.Roles.Manager},{WorkConstants.Roles.Support}";

    // ------------------------------------------------------------------ các tab

    /// <summary><c>/reports</c> mở tab đầu tiên mà vai trò được xem.</summary>
    [HttpGet("/reports")]
    public IActionResult Index()
    {
        var tab = WorkConstants.Reports.FirstTabFor(User.GetRole());

        return tab is null ? Forbid() : Redirect($"/reports/{tab}");
    }

    [HttpGet("/reports/revenue")]
    [Authorize(Roles = RevenueRoles)]
    public async Task<IActionResult> Revenue(int? year, CancellationToken ct)
    {
        var (selectedYear, years) = await ResolveYearAsync(year, ct);

        try
        {
            return View(new RevenueReportViewModel
            {
                Year = selectedYear,
                Years = years,
                Rows = await reports.GetRevenueAsync(selectedYear, ct)
            });
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The revenue report could not be loaded.");

            return View(new RevenueReportViewModel
            {
                Year = selectedYear,
                Years = years,
                ErrorKey = ex.MessageKey
            });
        }
    }

    [HttpGet("/reports/customers")]
    [Authorize(Roles = RevenueRoles)]
    public async Task<IActionResult> Customers(
        string? tier,
        string? sort,
        bool desc = false,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default)
    {
        var filter = new CustomerRankFilter
        {
            Tier = WorkConstants.CustomerTiers.All.Contains(tier ?? string.Empty) ? tier : null,
            Sort = Utils.SanitiseSort(
                sort, CustomerRankFilter.SortFields, CustomerRankFilter.SortByValue),
            // Không có ?sort= là lần mở đầu tiên: xếp theo tổng giá trị giảm dần.
            Desc = string.IsNullOrWhiteSpace(sort) || desc,
            Page = Utils.SanitisePage(page),
            PageSize = pageSize is null
                ? await settings.GetPageSizeAsync(ct)
                : Utils.SanitisePageSize(pageSize)
        };

        try
        {
            var ranking = await reports.GetCustomerRankingAsync(filter, paged: true, ct);

            return View(new CustomerReportViewModel
            {
                Filter = filter,
                Rows = ranking.Rows,
                TotalRows = ranking.TotalRows,
                TierShares = await reports.GetTierSharesAsync(ct)
            });
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The customer report could not be loaded.");

            return View(new CustomerReportViewModel
            {
                Filter = filter,
                ErrorKey = ex.MessageKey
            });
        }
    }

    [HttpGet("/reports/care")]
    [Authorize(Roles = CareRoles)]
    public async Task<IActionResult> Care(int? year, CancellationToken ct)
    {
        var (selectedYear, years) = await ResolveYearAsync(year, ct);

        try
        {
            return View(new CareReportViewModel
            {
                Year = selectedYear,
                Years = years,
                Rows = await reports.GetCareAsync(selectedYear, ct)
            });
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The care report could not be loaded.");

            return View(new CareReportViewModel
            {
                Year = selectedYear,
                Years = years,
                ErrorKey = ex.MessageKey
            });
        }
    }

    // ------------------------------------------------------------------ xuất CSV

    [HttpGet("/reports/revenue/export")]
    [Authorize(Roles = RevenueRoles)]
    public async Task<IActionResult> ExportRevenue(int? year, CancellationToken ct)
    {
        var (selectedYear, _) = await ResolveYearAsync(year, ct);

        try
        {
            var rows = await reports.GetRevenueAsync(selectedYear, ct);

            var lines = new List<IReadOnlyList<string?>>
            {
                new[]
                {
                    localizer["Reports_Revenue_Month"].Value,
                    localizer["Reports_Revenue_Contracts"].Value,
                    localizer["Reports_Revenue_Amount"].Value
                }
            };

            lines.AddRange(rows.Select(row => new string?[]
            {
                MonthName(row.Month),
                Integer(row.ContractCount),
                Number(row.Revenue)
            }));

            lines.Add(new string?[]
            {
                localizer["Reports_Revenue_Total"].Value,
                Integer(rows.Sum(row => row.ContractCount)),
                Number(rows.Sum(row => row.Revenue))
            });

            return Csv($"ccm-revenue-{selectedYear}.csv", lines);
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The revenue export failed.");
            return BackWithError(ex.MessageKey, nameof(Revenue), new { year = selectedYear });
        }
    }

    [HttpGet("/reports/customers/export")]
    [Authorize(Roles = RevenueRoles)]
    public async Task<IActionResult> ExportCustomers(
        string? tier,
        string? sort,
        bool desc = false,
        CancellationToken ct = default)
    {
        var filter = new CustomerRankFilter
        {
            Tier = WorkConstants.CustomerTiers.All.Contains(tier ?? string.Empty) ? tier : null,
            Sort = Utils.SanitiseSort(
                sort, CustomerRankFilter.SortFields, CustomerRankFilter.SortByValue),
            Desc = string.IsNullOrWhiteSpace(sort) || desc
        };

        try
        {
            // Không phân trang: tệp chứa toàn bộ dòng khớp bộ lọc, không chỉ trang đang xem.
            var ranking = await reports.GetCustomerRankingAsync(filter, paged: false, ct);

            var lines = new List<IReadOnlyList<string?>>
            {
                new[]
                {
                    localizer["Reports_Customers_Name"].Value,
                    localizer["Reports_Customers_Type"].Value,
                    localizer["Reports_Customers_Tier"].Value,
                    localizer["Reports_Customers_Contracts"].Value,
                    localizer["Reports_Customers_Value"].Value
                }
            };

            lines.AddRange(ranking.Rows.Select(row => new string?[]
            {
                row.CustomerName,
                localizer.Label("CustomerType", row.CustomerType).Value,
                localizer.Label("Tier", row.Tier).Value,
                Integer(row.ContractCount ?? 0),
                Number(row.TotalValue)
            }));

            return Csv($"ccm-customers-{filter.Tier ?? "all"}.csv", lines);
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The customer export failed.");
            return BackWithError(ex.MessageKey, nameof(Customers), new { tier = filter.Tier });
        }
    }

    [HttpGet("/reports/care/export")]
    [Authorize(Roles = CareRoles)]
    public async Task<IActionResult> ExportCare(int? year, CancellationToken ct)
    {
        var (selectedYear, _) = await ResolveYearAsync(year, ct);

        try
        {
            var rows = await reports.GetCareAsync(selectedYear, ct);

            var header = new List<string?>
            {
                localizer["Reports_Care_Month"].Value
            };
            header.AddRange(WorkConstants.CareTypes.All
                .Select(type => localizer.Label("CareType", type).Value));
            header.Add(localizer["Reports_Care_Total"].Value);
            header.Add(localizer["Reports_Care_Survey"].Value);

            var lines = new List<IReadOnlyList<string?>> { header };

            lines.AddRange(rows.Select(row =>
            {
                var cells = new List<string?>
                {
                    MonthName(row.Month)
                };

                cells.AddRange(WorkConstants.CareTypes.All
                    .Select(type => Integer(row.Counts.GetValueOrDefault(type))));
                cells.Add(Integer(row.TotalVisits));
                cells.Add(row.SurveyAverage is { } score ? Number(score) : string.Empty);

                return (IReadOnlyList<string?>)cells;
            }));

            return Csv($"ccm-care-{selectedYear}.csv", lines);
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The care export failed.");
            return BackWithError(ex.MessageKey, nameof(Care), new { year = selectedYear });
        }
    }

    // ------------------------------------------------------------------ phần dùng chung

    /// <summary>
    /// Năm của báo cáo và danh sách năm cho ô chọn. Nếu chỉ riêng danh sách năm đọc không được thì
    /// trang vẫn mở với năm hiện tại; truy vấn số liệu ngay sau đó sẽ báo lỗi nếu cơ sở dữ liệu hỏng.
    /// </summary>
    private async Task<(int Year, IReadOnlyList<int> Years)> ResolveYearAsync(
        int? year, CancellationToken ct)
    {
        var currentYear = DateTime.Today.Year;

        IReadOnlyList<int> years;

        try
        {
            years = await reports.GetYearsAsync(ct);
        }
        catch (BusinessException ex)
        {
            logger.LogWarning(ex, "The report year list could not be read.");
            years = [currentYear];
        }

        return (ReportRules.ClampYear(year, currentYear, years.Min(), years.Max()), years);
    }

    /// <summary>
    /// Tên tháng theo ngôn ngữ đang đọc ("tháng 1" / "January"). Không phải resource key: đây là
    /// dữ liệu của CultureInfo, đúng như plan.md mục Step 7 ghi.
    /// </summary>
    private static string MonthName(int month) =>
        CultureInfo.CurrentUICulture.DateTimeFormat.GetMonthName(month);

    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private FileContentResult Csv(string fileName, IEnumerable<IReadOnlyList<string?>> rows) =>
        File(CsvWriter.ToUtf8WithBom(CsvWriter.Build(rows)), "text/csv; charset=utf-8", fileName);

    /// <summary>Lỗi khi xuất tệp quay về tab và hiện thông báo, không trả về một tệp rỗng.</summary>
    private IActionResult BackWithError(string messageKey, string action, object routeValues)
    {
        TempData["MessageKey"] = messageKey;
        TempData["MessageOk"] = false;

        return RedirectToAction(action, routeValues);
    }
}
