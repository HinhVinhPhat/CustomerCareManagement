using System.Diagnostics;
using CcmWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.Controllers;

public class HomeController : Controller
{
    /// <summary>
    /// Temporary landing page. The real dashboard - the KPI cards from <c>sp_DashboardSummary</c>,
    /// the chart and the three lists - is built in M05.
    /// </summary>
    [HttpGet("/")]
    public IActionResult Index() => View();

    [AllowAnonymous]
    [HttpGet("/access-denied")]
    public IActionResult AccessDenied() => View();

    /// <summary>Generic error page: never shows technical details (design.md 6.15).</summary>
    [AllowAnonymous]
    [HttpGet("/error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
