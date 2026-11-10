using System.Security.Claims;
using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Tests;

/// <summary>
/// The small pieces the M06 tests share: a context whose session carries the acting employee (so the
/// audit trigger records that person), and unique values for the columns the database keeps unique.
/// </summary>
internal static class AdminTestSupport
{
    /// <summary>Employee 1 is "john", the Manager account the administration screens belong to.</summary>
    public const int ManagerEmployee = 1;

    public static ApplicationDbContext CreateContext(int employeeId = ManagerEmployee)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(Utils.EmployeeIdClaim, employeeId.ToString())], "test"))
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(TestDatabase.ConnectionString)
            .AddInterceptors(new SessionContextInterceptor(new HttpContextAccessor { HttpContext = httpContext }))
            .Options;

        return new ApplicationDbContext(options);
    }

    /// <summary>Eight characters, used as the tail of every value that must be unique.</summary>
    public static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>A phone number that passes CHK_Employee_Phone and will not collide with another test.</summary>
    public static string UniquePhone()
    {
        var random = Random.Shared.Next(10_000_000, 99_999_999);
        return $"09{random}";
    }
}
