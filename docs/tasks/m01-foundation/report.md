# M01 - Foundation - hand-over report

## 1. What was done

The ASP.NET Core MVC application now exists and runs. M01 delivered the frame that every later
module builds on:

- `CcmWeb.sln` (classic `.sln`, so it also opens in older Visual Studio), `src/CcmWeb` (MVC,
  `net10.0`) and `tests/CcmWeb.Tests` (xUnit, 22 tests).
- The four `database/*.sql` files were run on the real SQL Server: `CustomerCareDB` was created and
  the application login `app_ccm` works (never `sa`).
- EF Core **database-first**: `ApplicationDbContext` plus entities and keyless view entities were
  scaffolded from the real database. Nothing generated was hand-edited.
- `ApplicationDbContext.Custom.cs` turns `UseSqlOutputClause(false)` on for the four tables with
  triggers, so writes through EF no longer fail with error 334.
- `SessionContextInterceptor` runs `sp_set_session_context N'EmployeeID', ...` on every connection
  EF opens, which is what the audit triggers read.
- Cookie sign-in at `/login`, sign-out as `POST /logout`, with the demo SHA2 hash accepted once and
  replaced by a `PasswordHasher` hash; role authorization with a fallback policy, an access-denied
  page and an error page; the login POST is rate limited per IP.
- Shared shell split into partials: `_Layout`, `_HeadAssets`, `_Sidebar` (role-based menu from
  `Helpers/Navigation.cs`), `_Topbar`, `_NotificationBell` view component, `site.css`, `site.js`.
- `ContractExpiryService` calls `sp_ExpireContracts` at start-up and once a day.
- A temporary landing page at `/`, replaced by the real dashboard in M05.

## 2. Main paths

```
CcmWeb.sln
src/CcmWeb/Program.cs                              pipeline, DI, authentication, rate limit
src/CcmWeb/Helpers/Utils.cs                        claims, SQL error translation, hash, formatting
src/CcmWeb/Helpers/WorkConstants.cs                roles, schedule status, rate-limit policy name
src/CcmWeb/Helpers/Navigation.cs                   the sidebar menu and the role matrix
src/CcmWeb/Models/EF/ApplicationDbContext.cs       scaffolded (generated)
src/CcmWeb/Models/EF/ApplicationDbContext.Custom.cs trigger-safe configuration
src/CcmWeb/Models/EF/SessionContextInterceptor.cs  audit-log actor
src/CcmWeb/Models/Entities/*                       scaffolded (generated)
src/CcmWeb/Services/AccountService.cs              sign-in, legacy hash upgrade
src/CcmWeb/Services/NotificationService.cs         the three bell counts
src/CcmWeb/Services/ContractExpiryService.cs       sp_ExpireContracts at start-up and daily
src/CcmWeb/Controllers/AccountController.cs        /login, /logout
src/CcmWeb/Controllers/HomeController.cs           /, /access-denied, /error
src/CcmWeb/Views/Shared/                           _Layout, _HeadAssets, _Sidebar, _Topbar, Error
src/CcmWeb/Views/Account/Login.cshtml              sign-in page
src/CcmWeb/Views/Home/Index.cshtml                 temporary landing page (M05 replaces it)
src/CcmWeb/wwwroot/css/site.css                    the only place colours are defined
src/CcmWeb/wwwroot/js/site.js                      drawer, toast, bell panel, login helpers
tests/CcmWeb.Tests/TestDatabase.cs                 rebuilds CustomerCareDB_Test from database/*.sql
docs/tasks/m01-foundation/                         requirements, plan, test results, bugs, this report
```

## 3. Verification actually run

- `dotnet build` -> 0 errors (8 NuGet audit warnings, see section 5).
- `dotnet test` -> **22 passed, 0 failed** against the real SQL Server test database.
- The application was started and driven through the browser and through HTTP requests; the results
  case by case are in `test-results.md`: 17 cases PASS, 2 cases NOT RUN (T08, T18), 0 BLOCKED.
- Three bugs were found and fixed during the round (`bug-report.md`).

Not run, and why:

| Case | Why |
|---|---|
| T08 (403 for a wrong role) | M01 has no action restricted to a role yet; the access-denied page itself was fetched and renders. The first role-restricted action arrives in M02. |
| T18 (mobile drawer) | No viewport emulation is available here, and screenshots need a visible desktop window. The markup and the toggle handler exist (`lg:` breakpoint, `menuBtn` in `site.js`) but the behaviour was not observed. |

## 4. Decisions taken while implementing (deviations from the approved plan)

1. **`Views/Account` instead of `Views/Accounts`.** The view engine looks in
   `Views/<ControllerName>/`; the skill's mapping table was wrong and has been corrected.
2. **`UseStaticFiles()` instead of `MapStaticAssets()`.** With the fallback authorization policy,
   `MapStaticAssets` endpoints were redirected to the sign-in page (bug B02).
3. **The `Microsoft.Extensions.Identity.Core` package reference was removed.** The build warns
   `NU1510` because `PasswordHasher` already comes from the ASP.NET Core shared framework.
4. **`CcmWeb.sln` is a classic solution file.** The .NET 10 SDK now creates `.slnx` by default, which
   older tooling cannot open.
5. **The topbar search box, the feed-behind menu entries and the notification links say "arrives in
   a later module"** instead of pointing at pages that do not exist yet. M02 wires the search box to
   `sp_SearchCustomer`.
6. **The final smoke check ran against the live `CustomerCareDB`**, which upgraded `john`'s password
   hash to `PasswordHasher` - the designed behaviour of the first sign-in.
7. **The project database connection is also written in the code.** At the user's request,
   `Models/EF/ApplicationDbContext.Custom.cs` now holds
   `FallbackConnectionString` (`Server=localhost\SQLEXPRESS;Database=CustomerCareDB;Trusted_Connection=True;TrustServerCertificate=True`,
   Windows Authentication, exactly as in SSMS) plus a parameterless constructor and an `OnConfiguring`
   override, and `Program.cs` uses that constant when `ConnectionStrings:DefaultConnection` is not
   configured. No password is stored in the repository. Verified two ways: a scratch console that
   built `new ApplicationDbContext()` (no options) read 10 customers and 16 contracts from
   `CustomerCareDB` as the Windows user, and the application started with the User Secret removed,
   logged the warning and ran `sp_ExpireContracts` successfully.

## 5. Waiting on the user

1. **NuGet audit warnings.** `System.Security.Cryptography.Pkcs` 9.0.11 (transitive, reached through
   `Microsoft.Data.SqlClient` -> `Azure.Identity`, a code path this project does not use) has a
   moderate and a high severity advisory. Clearing the warnings means adding a direct reference to a
   patched version, which `AGENTS.md` says needs approval before a package is added.
2. **`docs/design.md` section 10 (REST API, JWT) and section 11 (Docker with `api` + `web`)** are
   still marked as outdated. Section 10/11 do not affect M01, but they should be rewritten or removed
   before M07.
3. **Approval to start M02 (Customers).** Requirements and a plan will be written first, as before.

## 6. How to run it

```powershell
# 1. Create the database (drops and recreates CustomerCareDB)
Get-ChildItem database\*.sql | Sort-Object Name | ForEach-Object {
    sqlcmd -S ".\SQLEXPRESS" -E -C -I -f 65001 -b -i $_.FullName
}
# file 04 needs the application password as a variable:
sqlcmd -S ".\SQLEXPRESS" -E -C -I -f 65001 -b -v APP_PASSWORD="App_Str0ng!Pass2026" -i database\04_CreateAppUser.sql

# 2. Run the web application (the connection string lives in User Secrets)
dotnet run --project src/CcmWeb

# 3. Run the tests (they rebuild CustomerCareDB_Test from the scripts)
dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj
```

Demo accounts, password `123456`: `john`, `susan` (Manager), `mary`, `kevin` (Sales), `david`,
`helen` (Support).
