using System.Security.Claims;
using CcmWeb.Helpers;
using CcmWeb.Models.EF;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CcmWeb.Tests;

/// <summary>
/// The audit triggers read SESSION_CONTEXT('EmployeeID'), so the interceptor must set it on every
/// connection EF opens. Proved here on the test database.
/// </summary>
[Collection("database")]
public class SessionContextTests
{
    private const string ReadActorSql = "SELECT CAST(SESSION_CONTEXT(N'EmployeeID') AS INT) AS Value";

    [Fact]
    public async Task The_interceptor_sets_the_employee_id_of_the_signed_in_user()
    {
        await using var db = CreateContext(employeeID: 3);

        var actor = await db.Database.SqlQueryRaw<int?>(ReadActorSql).SingleAsync();

        Assert.Equal(3, actor);
    }

    [Fact]
    public async Task Without_a_signed_in_user_the_employee_id_stays_null()
    {
        await using var db = CreateContext(employeeID: null);

        var actor = await db.Database.SqlQueryRaw<int?>(ReadActorSql).SingleAsync();

        Assert.Null(actor);
    }

    private static ApplicationDbContext CreateContext(int? employeeID)
    {
        var httpContext = new DefaultHttpContext();

        if (employeeID is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(Utils.EmployeeIdClaim, employeeID.Value.ToString())], "test"));
        }

        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(TestDatabase.ConnectionString)
            .AddInterceptors(new SessionContextInterceptor(accessor))
            .Options;

        return new ApplicationDbContext(options);
    }
}
