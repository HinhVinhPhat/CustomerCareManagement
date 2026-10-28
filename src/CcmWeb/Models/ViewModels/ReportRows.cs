using System.ComponentModel.DataAnnotations.Schema;

namespace CcmWeb.Models.ViewModels;

// Các lớp dưới đây là khuôn hứng kết quả của thủ tục, không phải thực thể do scaffold sinh ra.
// Tên thuộc tính phải trùng tên cột mà thủ tục trả về, nếu không SqlQueryRaw không ánh xạ được.
// Kiểu decimal cần khai báo kiểu lưu trữ vì câu lệnh thô không đi qua mô hình của EF.

/// <summary>Một dòng của <c>sp_DashboardSummary</c>.</summary>
public sealed class DashboardSummaryRow
{
    public int TotalCustomers { get; set; }

    public int NewCustomers30Days { get; set; }

    public int ActiveContracts { get; set; }

    public int ExpiringContracts { get; set; }

    public int OpenFeedback { get; set; }

    public int OverdueFeedback { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RevenueThisYear { get; set; }

    /// <summary>Không có lần khảo sát nào trong 180 ngày thì cột này là NULL.</summary>
    [Column(TypeName = "decimal(4,2)")]
    public decimal? AvgSatisfaction180Days { get; set; }
}

/// <summary>Một dòng của <c>sp_MonthlyRevenue</c>: một tháng trong năm đang chọn.</summary>
public sealed class MonthlyRevenueRow
{
    public int Month { get; set; }

    public int ContractCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Revenue { get; set; }
}

/// <summary>Một dòng của <c>sp_MonthlyCareStats</c>: một tháng và một loại chăm sóc.</summary>
public sealed class MonthlyCareStatRow
{
    public int Month { get; set; }

    public string CareType { get; set; } = null!;

    public int VisitCount { get; set; }

    /// <summary>Chỉ có giá trị ở dòng Khảo sát; các loại khác là NULL.</summary>
    [Column(TypeName = "decimal(4,2)")]
    public decimal? AvgSatisfaction { get; set; }
}

/// <summary>Năm nhỏ nhất và lớn nhất có hợp đồng, dùng cho ô chọn năm.</summary>
public sealed class YearRangeRow
{
    public int? MinYear { get; set; }

    public int? MaxYear { get; set; }
}
