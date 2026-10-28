using CcmWeb.Models.ViewModels;

namespace CcmWeb.Helpers;

/// <summary>
/// Các phép tính thuần của trang tổng quan và trang báo cáo (M05). Không chỗ nào ở đây đọc cơ sở dữ
/// liệu: mọi con số vẫn do cơ sở dữ liệu trả về, lớp này chỉ sắp xếp, gộp nhóm và đổi ra tỷ lệ để
/// hiển thị - nhờ vậy kiểm thử được mà không cần SQL Server.
/// </summary>
public static class ReportRules
{
    /// <summary>
    /// Năm đang xem. Năm thiếu, không phải số dương hoặc nằm ngoài khoảng có dữ liệu thì lấy năm hiện
    /// tại, để một địa chỉ sửa tay không làm trang báo cáo rỗng.
    /// </summary>
    public static int ClampYear(int? year, int currentYear, int? minYear = null, int? maxYear = null)
    {
        if (year is null or < 1 or > 9999)
        {
            return currentYear;
        }

        if (minYear is { } min && year < min)
        {
            return currentYear;
        }

        if (maxYear is { } max && year > max)
        {
            return currentYear;
        }

        return year.Value;
    }

    /// <summary>
    /// Danh sách năm cho ô chọn: liên tục, mới nhất trước, luôn có năm hiện tại kể cả khi chưa có
    /// hợp đồng nào.
    /// </summary>
    public static IReadOnlyList<int> AvailableYears(
        int? minSignYear, int? maxSignYear, int currentYear)
    {
        var min = Math.Min(minSignYear ?? currentYear, currentYear);
        var max = Math.Max(maxSignYear ?? currentYear, currentYear);

        // Dữ liệu hỏng (một năm 1900) không được tạo ra hàng trăm lựa chọn vô nghĩa.
        if (max - min > 100)
        {
            min = Math.Max(min, currentYear - 10);
            max = Math.Max(max, currentYear);
        }

        var years = new List<int>(max - min + 1);

        for (var year = max; year >= min; year--)
        {
            years.Add(year);
        }

        return years;
    }

    /// <summary>
    /// Xoay kết quả (tháng, loại chăm sóc) của <c>sp_MonthlyCareStats</c> thành 12 dòng tháng. Tháng
    /// không có lần chăm sóc nào là dòng 0 thật, không phải tháng bị thiếu. Điểm hài lòng chỉ lấy từ
    /// dòng Khảo sát: không cộng trung bình của các loại với nhau (xem requirements.md mục 5).
    /// </summary>
    public static IReadOnlyList<CareMonthRow> PivotCareStats(IEnumerable<MonthlyCareStatRow> rows)
    {
        var byMonth = rows
            .GroupBy(row => row.Month)
            .ToDictionary(group => group.Key, group => group.ToList());

        var months = new List<CareMonthRow>(12);

        for (var month = 1; month <= 12; month++)
        {
            var counts = WorkConstants.CareTypes.All
                .ToDictionary(type => type, _ => 0, StringComparer.Ordinal);

            var total = 0;
            decimal? surveyAverage = null;

            if (byMonth.TryGetValue(month, out var monthRows))
            {
                foreach (var row in monthRows)
                {
                    if (counts.ContainsKey(row.CareType))
                    {
                        counts[row.CareType] = row.VisitCount;
                    }

                    total += row.VisitCount;

                    if (row.CareType == WorkConstants.CareTypes.Survey)
                    {
                        surveyAverage = row.AvgSatisfaction;
                    }
                }
            }

            months.Add(new CareMonthRow(month, counts, total, surveyAverage));
        }

        return months;
    }

    /// <summary>
    /// Chiều cao của từng cột theo giá trị lớn nhất. Giá trị dương luôn có chiều cao tối thiểu để còn
    /// nhìn thấy; năm không có số liệu thì mọi cột bằng 0 và không có phép chia cho 0.
    /// </summary>
    public static IReadOnlyList<double> BarHeights(
        IEnumerable<decimal> values, double maxPercent = 100, double minVisible = 4)
    {
        var list = values.ToList();
        var max = list.Count == 0 ? 0m : list.Max();

        if (max <= 0)
        {
            return list.Select(_ => 0d).ToList();
        }

        return list
            .Select(value => value <= 0
                ? 0d
                : Math.Max(minVisible, (double)(value / max) * maxPercent))
            .ToList();
    }

    /// <summary>
    /// Tỷ lệ phần trăm số khách hàng theo hạng. Dùng cách làm tròn "phần dư lớn nhất" để tổng luôn
    /// đúng 100% khi có khách hàng; không có khách hàng nào thì mọi tỷ lệ bằng 0.
    /// </summary>
    public static IReadOnlyList<TierShare> TierShares(IEnumerable<(string Tier, int Count)> counts)
    {
        var byTier = counts
            .GroupBy(item => item.Tier)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));

        var ordered = WorkConstants.CustomerTiers.All
            .Select(tier => new { Tier = tier, Count = byTier.GetValueOrDefault(tier) })
            .ToList();

        var total = ordered.Sum(item => item.Count);

        if (total <= 0)
        {
            return ordered.Select(item => new TierShare(item.Tier, 0, 0)).ToList();
        }

        var exact = ordered
            .Select(item => new
            {
                item.Tier,
                item.Count,
                Exact = item.Count * 100m / total,
                Floor = item.Count * 100 / total
            })
            .ToList();

        var remaining = 100 - exact.Sum(item => item.Floor);

        var bonus = exact
            .OrderByDescending(item => item.Exact - item.Floor)
            .ThenBy(item => Array.IndexOf(WorkConstants.CustomerTiers.All, item.Tier))
            .Take(remaining)
            .Select(item => item.Tier)
            .ToHashSet(StringComparer.Ordinal);

        return exact
            .Select(item => new TierShare(
                item.Tier,
                item.Count,
                item.Floor + (bonus.Contains(item.Tier) ? 1 : 0)))
            .ToList();
    }
}

/// <summary>
/// Khối nào của trang tổng quan thuộc vai trò nào (requirements.md mục 3). View và controller cùng
/// hỏi lớp này, nên một khối không thể vừa hiện ra vừa bị máy chủ từ chối.
/// </summary>
public static class DashboardRules
{
    /// <summary>Phản hồi cần xử lý: Quản lý và Hỗ trợ; Kinh doanh không có trang phản hồi.</summary>
    public static bool SeesFeedback(string? role) =>
        role is WorkConstants.Roles.Manager or WorkConstants.Roles.Support;

    /// <summary>Doanh thu: Quản lý và Kinh doanh; Hỗ trợ không đọc báo cáo doanh thu.</summary>
    public static bool SeesRevenue(string? role) =>
        role is WorkConstants.Roles.Manager or WorkConstants.Roles.Sales;

    /// <summary>Chỉ Quản lý và Kinh doanh được mở form gia hạn của hợp đồng.</summary>
    public static bool CanRenewContracts(string? role) => SeesRevenue(role);
}
