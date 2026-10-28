using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>Một trang xếp hạng khách hàng kèm tổng số dòng khớp bộ lọc.</summary>
public sealed record CustomerRankResult(IReadOnlyList<CustomerRankRow> Rows, int TotalRows);

/// <summary>
/// Số liệu của ba tab báo cáo (design.md 6.12). Tất cả đều đọc từ thủ tục và view có sẵn: không có
/// phép tính doanh thu, hạng khách hàng hay điểm hài lòng nào được làm lại trong C#.
/// </summary>
public interface IReportService
{
    /// <summary>Năm cho ô chọn: liên tục, mới nhất trước, luôn có năm hiện tại.</summary>
    Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MonthlyRevenueRow>> GetRevenueAsync(int year, CancellationToken ct = default);

    /// <summary>
    /// Bảng xếp hạng khách hàng đang hoạt động. <paramref name="paged"/> = false dùng cho tệp CSV: lấy
    /// toàn bộ dòng khớp bộ lọc, không chỉ trang đang xem.
    /// </summary>
    Task<CustomerRankResult> GetCustomerRankingAsync(
        CustomerRankFilter filter, bool paged, CancellationToken ct = default);

    /// <summary>Tỷ lệ hạng của khách hàng đang hoạt động, cho biểu đồ tròn.</summary>
    Task<IReadOnlyList<TierShare>> GetTierSharesAsync(CancellationToken ct = default);

    /// <summary>Mười hai tháng của năm, đã xoay thành một dòng một tháng.</summary>
    Task<IReadOnlyList<CareMonthRow>> GetCareAsync(int year, CancellationToken ct = default);
}
