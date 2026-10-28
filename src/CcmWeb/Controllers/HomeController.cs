using System.Diagnostics;
using CcmWeb.Helpers;
using CcmWeb.Models;
using CcmWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

public class HomeController(
    IDashboardService dashboard,
    ILogger<HomeController> logger) : Controller
{
    /// <summary>
    /// Trang tổng quan (design.md 6.2). Chỉ đọc; mọi con số đến từ thủ tục và view của cơ sở dữ liệu.
    /// <paramref name="year"/> chọn năm của biểu đồ doanh thu, không ảnh hưởng các khối khác.
    /// </summary>
    [HttpGet("/")]
    public async Task<IActionResult> Index(int? year, CancellationToken ct)
    {
        var model = await dashboard.GetAsync(year, User.GetRole(), ct);

        logger.LogDebug("Dashboard rendered for employee {EmployeeID}, year {Year}.",
            User.GetEmployeeID(), model.Year);

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet("/access-denied")]
    public IActionResult AccessDenied() => View();

    /// <summary>Trang lỗi chung: không bao giờ hiện chi tiết kỹ thuật (design.md 6.15).</summary>
    [AllowAnonymous]
    [HttpGet("/error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
