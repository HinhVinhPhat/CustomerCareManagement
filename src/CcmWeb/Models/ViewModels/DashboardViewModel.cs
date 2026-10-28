using CcmWeb.Helpers;

namespace CcmWeb.Models.ViewModels;

/// <summary>Một khách hàng trong bảng "Khách hàng hàng đầu" (từ <c>vw_CustomerRevenue</c>).</summary>
public sealed record DashboardCustomerRow(
    int CustomerId,
    string CustomerName,
    string Tier,
    int? ContractCount,
    decimal TotalValue);

/// <summary>Một hợp đồng trong danh sách "Hợp đồng sắp hết hạn" (từ <c>vw_ExpiringContract</c>).</summary>
public sealed record DashboardExpiringRow(
    int ContractId,
    int CustomerId,
    string CustomerName,
    DateOnly EndDate,
    int DaysRemaining,
    decimal TotalValue);

/// <summary>Một phản hồi trong danh sách "Phản hồi cần xử lý" (từ <c>vw_OpenFeedback</c>).</summary>
public sealed record DashboardFeedbackRow(
    int FeedbackId,
    string CustomerName,
    byte Priority,
    string? Content,
    DateOnly? DueDate,
    string? Assignee,
    bool IsOverdue);

/// <summary>Một dòng của lịch chăm sóc (từ <c>vw_CareSchedule</c>).</summary>
public sealed record DashboardCareRow(
    int CareId,
    int CustomerId,
    string CustomerName,
    string CareType,
    string? Content,
    DateOnly? NextAppointmentDate,
    string ScheduleStatus);

/// <summary>Lịch chăm sóc chia đúng ba nhóm mà view quyết định: quá hạn, hôm nay, sắp tới.</summary>
public sealed class DashboardCareGroups
{
    public IReadOnlyList<DashboardCareRow> Rows { get; init; } = [];

    public IReadOnlyList<DashboardCareRow> Overdue =>
        Rows.Where(row => row.ScheduleStatus == WorkConstants.ScheduleStatus.Overdue).ToList();

    public IReadOnlyList<DashboardCareRow> Today =>
        Rows.Where(row => row.ScheduleStatus == WorkConstants.ScheduleStatus.Today).ToList();

    public IReadOnlyList<DashboardCareRow> Upcoming =>
        Rows.Where(row => row.ScheduleStatus == WorkConstants.ScheduleStatus.Upcoming).ToList();

    public int Total => Rows.Count;
}

/// <summary>
/// Trang tổng quan (design.md 6.2). Mỗi khối có giá trị riêng và một khóa lỗi riêng: một truy vấn
/// hỏng chỉ làm hỏng đúng khối đó, không kéo cả trang thành trang lỗi và không hiện số 0 giả.
///
/// Một khối mà vai trò không được xem là <c>null</c> (không phải danh sách rỗng), để view phân biệt
/// "ẩn" với "không có dữ liệu".
/// </summary>
public sealed class DashboardViewModel
{
    public int Year { get; init; }

    public IReadOnlyList<int> Years { get; init; } = [];

    public DashboardSummaryRow? Summary { get; init; }

    public string? SummaryErrorKey { get; init; }

    public IReadOnlyList<MonthlyRevenueRow>? Revenue { get; init; }

    public string? RevenueErrorKey { get; init; }

    public IReadOnlyList<DashboardCustomerRow>? TopCustomers { get; init; }

    public string? TopCustomersErrorKey { get; init; }

    public IReadOnlyList<DashboardExpiringRow> Expiring { get; init; } = [];

    public string? ExpiringErrorKey { get; init; }

    public IReadOnlyList<DashboardFeedbackRow>? Feedback { get; init; }

    public string? FeedbackErrorKey { get; init; }

    public DashboardCareGroups Care { get; init; } = new();

    public string? CareErrorKey { get; init; }

    /// <summary>Nút gia hạn chỉ có với Quản lý và Kinh doanh; Hỗ trợ chỉ xem danh sách.</summary>
    public bool CanRenewContracts { get; init; }

    /// <summary>
    /// Vai trò được xem khối doanh thu và khối khách hàng hàng đầu. Khối được vẽ theo cờ này chứ
    /// không theo danh sách: khi truy vấn hỏng, danh sách là <c>null</c> nhưng khối vẫn phải hiện
    /// khóa lỗi của nó (bug-report B02).
    /// </summary>
    public bool ShowRevenue { get; init; }

    /// <summary>Vai trò được xem khối phản hồi cần xử lý; cùng lý do với <see cref="ShowRevenue"/>.</summary>
    public bool ShowFeedback { get; init; }

    /// <summary>Năm không có hợp đồng nào: biểu đồ vẫn đủ 12 cột 0 kèm lời nhắn.</summary>
    public bool HasRevenue => Revenue?.Any(row => row.Revenue > 0) == true;

    public decimal RevenueTotal => Revenue?.Sum(row => row.Revenue) ?? 0m;
}
