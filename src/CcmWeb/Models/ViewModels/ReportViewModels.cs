using System.Globalization;
using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

// ---------------------------------------------------------------- báo cáo doanh thu

/// <summary>Trang báo cáo doanh thu: 12 tháng của một năm và dòng tổng cộng.</summary>
public sealed class RevenueReportViewModel
{
    public int Year { get; init; }

    public IReadOnlyList<int> Years { get; init; } = [];

    public IReadOnlyList<MonthlyRevenueRow> Rows { get; init; } = [];

    /// <summary>Tổng của 12 dòng, do ứng dụng cộng lại từ chính dữ liệu thủ tục trả về.</summary>
    public decimal Total => Rows.Sum(row => row.Revenue);

    public int ContractTotal => Rows.Sum(row => row.ContractCount);

    public string? ErrorKey { get; init; }
}

// ---------------------------------------------------------------- báo cáo khách hàng

/// <summary>Một dòng của bảng xếp hạng khách hàng.</summary>
public sealed record CustomerRankRow(
    int CustomerId,
    string CustomerName,
    string CustomerType,
    string Tier,
    int? ContractCount,
    decimal TotalValue);

/// <summary>Tỷ lệ một hạng khách hàng trong biểu đồ tròn.</summary>
public sealed record TierShare(string Tier, int Count, int Percent);

/// <summary>
/// Bộ lọc của bảng xếp hạng. Giá trị đọc từ query string đã được làm sạch trước khi tạo đối tượng;
/// mọi liên kết sắp xếp và phân trang đều giữ nguyên bộ lọc hiện tại.
/// </summary>
public sealed class CustomerRankFilter
{
    public const string SortByValue = "Value";
    public const string SortByName = "Name";
    public const string SortByContracts = "Contracts";

    public static readonly string[] SortFields = [SortByValue, SortByName, SortByContracts];

    public const string BaseUrl = "/reports/customers";

    /// <summary>Một giá trị của <c>CHK_Customer_Tier</c>; null = mọi hạng.</summary>
    public string? Tier { get; init; }

    public string Sort { get; init; } = SortByValue;

    public bool Desc { get; init; } = true;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = WorkConstants.Settings.DefaultPageSize;

    public bool HasFilters => Tier is not null;

    /// <summary>Bấm vào một tiêu đề cột: cùng cột thì đảo chiều, cột khác thì bắt đầu ở chiều hợp lý.</summary>
    public string SortQuery(string field)
    {
        var desc = Sort == field ? !Desc : field != SortByName;

        return Build(field, desc, 1, PageSize);
    }

    public string PageQuery(int page) => Build(Sort, Desc, page, PageSize);

    public string PageSizeQuery(int size) => Build(Sort, Desc, 1, size);

    /// <summary>Query string cho tệp CSV: giữ bộ lọc và cách sắp xếp, bỏ số trang và cỡ trang.</summary>
    public string ExportQuery => Build(Sort, Desc, 1, WorkConstants.Settings.DefaultPageSize);

    private string Build(string sort, bool desc, int page, int pageSize)
    {
        var parameters = new List<string>();

        // Bộ lọc hạng phải đi theo mọi liên kết sắp xếp và phân trang, nếu không người dùng bấm một
        // tiêu đề cột là mất bộ lọc vừa chọn.
        if (Tier is not null)
        {
            parameters.Add($"tier={Uri.EscapeDataString(Tier)}");
        }

        if (sort != SortByValue)
        {
            parameters.Add($"sort={Uri.EscapeDataString(sort)}");
        }

        // Mặc định: giá trị và số hợp đồng giảm dần, tên tăng dần.
        if (desc != (sort != SortByName))
        {
            parameters.Add($"desc={(desc ? "true" : "false")}");
        }

        if (page > 1)
        {
            parameters.Add($"page={page.ToString(CultureInfo.InvariantCulture)}");
        }

        if (pageSize != WorkConstants.Settings.DefaultPageSize)
        {
            parameters.Add($"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}");
        }

        return parameters.Count == 0
            ? string.Empty
            : $"?{string.Join('&', parameters)}";
    }
}

/// <summary>Trang báo cáo khách hàng: bảng xếp hạng, biểu đồ tỷ lệ hạng và thông tin phân trang.</summary>
public sealed class CustomerReportViewModel
{
    public required CustomerRankFilter Filter { get; init; }

    public IReadOnlyList<CustomerRankRow> Rows { get; init; } = [];

    public IReadOnlyList<TierShare> TierShares { get; init; } = [];

    public int TotalRows { get; init; }

    public string? ErrorKey { get; init; }

    public int TotalPages => TotalRows == 0 ? 0 : (int)Math.Ceiling(TotalRows / (double)Filter.PageSize);

    public int FirstRowNumber => TotalRows == 0 ? 0 : ((Filter.Page - 1) * Filter.PageSize) + 1;

    public int LastRowNumber => Math.Min(Filter.Page * Filter.PageSize, TotalRows);

    public bool HasPreviousPage => Filter.Page > 1 && TotalPages > 0;

    public bool HasNextPage => Filter.Page < TotalPages;

    public IReadOnlyList<int> PageNumbers
    {
        get
        {
            if (TotalPages <= 1)
            {
                return [];
            }

            var start = Math.Max(1, Filter.Page - 2);
            var end = Math.Min(TotalPages, start + 4);
            start = Math.Max(1, end - 4);

            return Enumerable.Range(start, end - start + 1).ToList();
        }
    }
}

// ---------------------------------------------------------------- báo cáo chăm sóc

/// <summary>Một tháng của báo cáo chăm sóc: số lần theo từng loại, tổng và điểm khảo sát.</summary>
public sealed record CareMonthRow(
    int Month,
    IReadOnlyDictionary<string, int> Counts,
    int TotalVisits,
    decimal? SurveyAverage);

/// <summary>Trang báo cáo chăm sóc: bảng xoay theo tháng và biểu đồ số lần chăm sóc.</summary>
public sealed class CareReportViewModel
{
    public int Year { get; init; }

    public IReadOnlyList<int> Years { get; init; } = [];

    public IReadOnlyList<CareMonthRow> Rows { get; init; } = [];

    public int TotalVisits => Rows.Sum(row => row.TotalVisits);

    /// <summary>Năm không có lần chăm sóc nào: vẫn hiện đủ 12 dòng 0 kèm lời nhắn.</summary>
    public bool HasData => TotalVisits > 0;

    public string? ErrorKey { get; init; }
}

// ---------------------------------------------------------------- biểu đồ

/// <summary>
/// Dữ liệu của biểu đồ cột. Nhãn và giá trị do trang truyền vào; chiều cao do
/// <see cref="ReportRules.BarHeights"/> tính, không phải một công thức trong Razor.
/// </summary>
/// <param name="Title">Câu mô tả cho trình đọc màn hình (đã dịch).</param>
/// <param name="Labels">Nhãn dưới mỗi cột.</param>
/// <param name="Values">Giá trị của mỗi cột, cùng thứ tự với nhãn.</param>
/// <param name="Money">True: in giá trị theo tiền; false: in số nguyên.</param>
public sealed record BarChartModel(
    string Title,
    IReadOnlyList<string> Labels,
    IReadOnlyList<decimal> Values,
    bool Money = true);

/// <summary>Dữ liệu của biểu đồ tròn hạng khách hàng; chú giải in kèm số lượng và tỷ lệ.</summary>
public sealed record DonutChartModel(string Title, IReadOnlyList<TierShare> Shares);

/// <summary>Ô chọn năm của một tab báo cáo (mỗi tab là một địa chỉ riêng).</summary>
public sealed record YearFilterModel(
    string Action,
    int Year,
    IReadOnlyList<int> Years);

/// <summary>Nút xuất CSV của một tab: địa chỉ giữ đúng bộ lọc đang xem.</summary>
public sealed record ExportLinkModel(string Url, string LabelKey);
