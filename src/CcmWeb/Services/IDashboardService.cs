using CcmWeb.Models.ViewModels;

namespace CcmWeb.Services;

/// <summary>
/// Trang tổng quan. Chỉ chạy những truy vấn mà vai trò được xem, và mỗi khối hỏng riêng: một khối
/// lỗi không được biến cả trang thành trang lỗi, cũng không được hiện số 0 giả.
/// </summary>
public interface IDashboardService
{
    Task<DashboardViewModel> GetAsync(
        int? year, string? role, CancellationToken ct = default);
}
