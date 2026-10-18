# M01 - Foundation - plan

Companion to `requirements.md`. One approval covers both documents. Steps are done in order; if
something out of scope shows up, work stops and the user is asked.

## 1. Steps

### Step 0 - framework decision (blocking)

Confirm `net10.0` (proposed) or install .NET 8 and use `net8.0`. Everything below assumes the
chosen TFM is written into `src/CcmWeb/CcmWeb.csproj` and `tests/CcmWeb.Tests/CcmWeb.Tests.csproj`.

### Step 1 - create the database on the real server

Run the four files in order against `.\SQLEXPRESS` (this creates `CustomerCareDB`; file `01` drops
and recreates it):

```powershell
Get-ChildItem database\*.sql | Sort-Object Name | ForEach-Object {
  sqlcmd -S ".\SQLEXPRESS" -E -C -I -f 65001 -b -i $_.FullName
}
```

Expected: each file exits 0 and `03_SampleData.sql` prints
`OK: sample data loaded, TotalValue and Tier match the triggers.`
Record the SQL Server version for `test-results.md`.

### Step 2 - solution and projects

```powershell
dotnet new sln -n CcmWeb
dotnet new mvc   -o src/CcmWeb       -f <TFM>
dotnet new xunit -o tests/CcmWeb.Tests -f <TFM>
dotnet sln add src/CcmWeb tests/CcmWeb.Tests
dotnet add tests/CcmWeb.Tests reference src/CcmWeb
```

Packages for `src/CcmWeb`: `Microsoft.EntityFrameworkCore.SqlServer`,
`Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Identity.Core` (for
`PasswordHasher<Account>`). For `tests/CcmWeb.Tests`:
`Microsoft.EntityFrameworkCore.SqlServer` (the tests open a real connection).

The default MVC template files that are not used (`Views/Home/Privacy.cshtml`,
`wwwroot/lib` bootstrap/jQuery defaults, `favicon` handling) are removed or replaced only where
the module needs it - no unrelated cleanup.

### Step 3 - connection string and secrets

```powershell
dotnet user-secrets init --project src/CcmWeb
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=CustomerCareDB;User Id=app_ccm;Password=<from .env.example>;TrustServerCertificate=True" --project src/CcmWeb
```

`appsettings.json` keeps only non-secret values (logging, allowed hosts).

### Step 4 - scaffold the model (database-first)

```powershell
dotnet ef dbcontext scaffold "Name=ConnectionStrings:DefaultConnection" Microsoft.EntityFrameworkCore.SqlServer `
  --project src/CcmWeb --context ApplicationDbContext --context-dir Models/EF `
  --output-dir Models/Entities --no-onconfiguring --force
```

Notes: table and view names become pluralised `DbSet` names (for example `VwExpiringContracts`,
`Accounts`); the generated files under `Models/EF` and `Models/Entities` are **never hand-edited**
(they are overwritten on the next scaffold). The actual entity/`DbSet` names are read from the
generated files before writing any Service.

### Step 5 - code

1. `Models/EF/ApplicationDbContext.Custom.cs` - `partial void OnModelCreatingPartial` with
   `UseSqlOutputClause(false)` for `Contract`, `ContractDetail`, `Customer`, `Feedback`.
2. `Models/EF/SessionContextInterceptor.cs` - `DbConnectionInterceptor` running
   `EXEC sp_set_session_context N'EmployeeID', @employeeID` on every connection open.
3. `Helpers/Utils.cs` - `BusinessException`, `TranslateSqlError` (+ constraint/duplicate
   lookups), `GetEmployeeID(this ClaimsPrincipal)`, money/date formatting helpers.
4. `Helpers/WorkConstants.cs` - role constants (`Manager`, `Sales`, `Support`) and the other
   lookup lists as the modules need them (start with roles).
5. `Services/IAccountService.cs` + `AccountService.cs` - authenticate (SHA2 branch then
   `PasswordHasher`, constant-time compare, dummy hash for unknown users), re-hash on success,
   report a locked account only after a correct password.
6. `Services/INotificationService.cs` + `NotificationService.cs` - counts from
   `vw_ExpiringContract`, `vw_OpenFeedback` (`IsOverdue = 1`), `vw_CareSchedule`.
7. `ViewComponents/NotificationBellViewComponent.cs` + its view.
8. `Services/ContractExpiryService.cs` - `BackgroundService` calling `sp_ExpireContracts` once at
   start-up and every 24 h, errors logged and never crashing the host.
9. `Controllers/AccountController.cs` - `GET /login`, `POST /login`, `POST /logout`
   (anti-forgery, rate limited, local `returnUrl` only). `[AllowAnonymous]` on the GET/POST login
   actions only.
10. `Controllers/HomeController.cs` - `/` (placeholder), `/error`, `/access-denied`.
11. `Program.cs` - `AddHttpContextAccessor`, interceptor registration,
    `AddDbContext<ApplicationDbContext>`, `AddScoped` services, `AddControllersWithViews` +
    `AutoValidateAntiforgeryTokenAttribute`, cookie authentication (`/login`, `/access-denied`,
    8 h sliding), authorization fallback = authenticated user, rate limiter for the login POST,
    `UseExceptionHandler("/error")`, `UseStatusCodePagesWithReExecute`, static files, routing,
    authentication, authorization.
12. Views - `Views/Shared/_Layout.cshtml`, `_Sidebar.cshtml`, `_Topbar.cshtml`,
    `Views/Accounts/Login.cshtml`, `Views/Home/Index.cshtml` (placeholder),
    `Views/Home/AccessDenied.cshtml`, `Views/Shared/Error.cshtml`,
    `Views/Shared/Components/NotificationBell/Default.cshtml`.
13. `wwwroot/css/site.css` + the Tailwind config block: CSS variables and the reused classes
    (`card`, `btn`, `btn-primary`, `input`, `badge`, `nav-link`, `tbl`, `num`) ported from
    `docs/mockups/static-mockups/css/style.css` and `js/tailwind-config.js`.
    `wwwroot/js/site.js` - sidebar drawer toggle and the toast helper.

### Step 6 - unit tests (`tests/CcmWeb.Tests`)

Only rules that matter: `TranslateSqlError` (50004 passthrough, 547 constraint message,
2601/2627 duplicate message, unknown -> null), the legacy SHA2 vector for `123456`
(`EC278A38...`), and the money/date formatting helpers. Plus the two real-database tests
(T09, T10 below) against `CustomerCareDB_Test`.

### Step 7 - verify

Build, run the app, walk through T01-T18 below, then write
`docs/tasks/m01-foundation/test-results.md` (case, environment, input, action, expected, actual,
status, evidence).

## 2. Files to create

```
CcmWeb.sln
src/CcmWeb/CcmWeb.csproj
src/CcmWeb/Program.cs
src/CcmWeb/appsettings.json
src/CcmWeb/Helpers/Utils.cs
src/CcmWeb/Helpers/WorkConstants.cs
src/CcmWeb/Models/EF/ApplicationDbContext.cs          (scaffolded, generated)
src/CcmWeb/Models/EF/ApplicationDbContext.Custom.cs   (hand-written partial)
src/CcmWeb/Models/EF/SessionContextInterceptor.cs
src/CcmWeb/Models/Entities/*                          (scaffolded, generated)
src/CcmWeb/Models/ViewModels/LoginViewModel.cs
src/CcmWeb/Services/IAccountService.cs
src/CcmWeb/Services/AccountService.cs
src/CcmWeb/Services/INotificationService.cs
src/CcmWeb/Services/NotificationService.cs
src/CcmWeb/Services/ContractExpiryService.cs
src/CcmWeb/ViewComponents/NotificationBellViewComponent.cs
src/CcmWeb/Controllers/AccountController.cs
src/CcmWeb/Controllers/HomeController.cs
src/CcmWeb/Views/_ViewImports.cshtml
src/CcmWeb/Views/_ViewStart.cshtml
src/CcmWeb/Views/Shared/_Layout.cshtml
src/CcmWeb/Views/Shared/_Sidebar.cshtml
src/CcmWeb/Views/Shared/_Topbar.cshtml
src/CcmWeb/Views/Shared/Error.cshtml
src/CcmWeb/Views/Shared/Components/NotificationBell/Default.cshtml
src/CcmWeb/Views/Account/Login.cshtml
src/CcmWeb/Views/Home/Index.cshtml
src/CcmWeb/Views/Home/AccessDenied.cshtml
src/CcmWeb/wwwroot/css/site.css
src/CcmWeb/wwwroot/js/site.js
tests/CcmWeb.Tests/CcmWeb.Tests.csproj
tests/CcmWeb.Tests/UtilsTests.cs
tests/CcmWeb.Tests/LegacyHashTests.cs
tests/CcmWeb.Tests/SessionContextTests.cs   (real test DB)
tests/CcmWeb.Tests/TriggerWriteTests.cs     (real test DB)
docs/tasks/m01-foundation/test-results.md   (after the test round)
docs/tasks/m01-foundation/report.md         (after the test round)
```

Files expected to change outside `src/`/`tests/`: `AGENTS.md` (the TFM decision, section 3),
`.gitignore` (already covers `bin/`, `obj/`), and `README.md` (how to run the app) - nothing else.

## 3. Planned test cases

Database-level cases use a freshly loaded `CustomerCareDB_Test` (copies of the four SQL files
with the database name replaced, run with `sqlcmd -I -f 65001`). The UI round points the app at
the test database through User Secrets so the live database is not written into by the tests.

| Id | Case | How | Expected |
|---|---|---|---|
| T01 | Clean build | `dotnet build` | 0 errors |
| T02 | Login as Manager | `john` / `123456` at `/login` | lands on `/`, sidebar = Dashboard, Customers, Contracts, Contacts, Care, Feedback, Reports, Admin |
| T03 | Role menus differ | log in as `mary`, then `david` | sidebar matches design.md 5.2; no Admin item |
| T04 | First login re-hashes | `SELECT LEN(PasswordHash) FROM ACCOUNT WHERE Username='john'` before and after | 64 hex before, `PasswordHasher` string after; login still works |
| T05 | Bad credentials | unknown user vs wrong password | identical message "Incorrect username or password" |
| T06 | Locked account | set `IsActive = 0` for `helen` in the test DB | correct password reports the lock; wrong password reports bad credentials; restored afterwards |
| T07 | Anonymous access | open `/contracts` while logged out | redirect to `/login?returnUrl=%2Fcontracts`; external `returnUrl` ignored |
| T08 | Wrong role | `mary` opens `/admin/accounts` (Sales has no permission) | 403 access-denied page, not the login page |
| T09 | `SESSION_CONTEXT` on EF connections | xUnit: open a `DbContext` with the interceptor and a stub identity, run `SELECT CAST(SESSION_CONTEXT(N'EmployeeID') AS INT)`, then a null-identity connection | returns the employee id for the logged-in case, NULL otherwise |
| T10 | Trigger-safe writes | xUnit against the test DB: `Add` + update a `Customer` through EF | succeeds, no error 334 |
| T11 | `TranslateSqlError` | xUnit | 50004 message passed through; 547/2601/2627 mapped to the English messages; unknown -> null |
| T12 | Legacy SHA2 vector | xUnit | SHA-256 of `123456` as UTF-16LE = `EC278A38...` and `8D969EEF...` (UTF-8) is rejected |
| T13 | Logout | POST `/logout`; then a GET to `/logout` | POST clears the cookie and redirects to `/login`; GET is not accepted (405/404) |
| T14 | Notification bell | open the topbar with the test data | real counts (2 expiring contracts, overdue feedback, overdue/today care), nothing hard-coded |
| T15 | Contract expiry job | test DB: make an Active contract with `EndDate` in the past, start the app | `sp_ExpireContracts` runs at start-up and the contract becomes Expired |
| T16 | Anti-forgery | submit the login POST without the token | request rejected |
| T17 | No fake data left | grep `src/` for `mock-data`, `ccm_user`, `localStorage` | no matches |
| T18 | Mobile layout | narrow the window under 768 px | sidebar becomes a drawer opened by ☰, content stays usable |

A case that cannot be run (missing SDK, SQL Server not reachable, ...) is recorded as BLOCKED,
never ticked.

## 4. Risks and open points

- **TFM decision** (Step 0) blocks everything else.
- The SQL Server instance is SQL Server 2025 (17.0). EF Core must support it - if the scaffold
  fails against 17.0, the fallback is `Microsoft.EntityFrameworkCore.SqlServer` at the newest
  version and, if needed, `--no-transactions` on the scaffold call.
- `sqlcmd` needs the `-I` switch (QUOTED_IDENTIFIER) because file `01` creates a filtered index.
- .NET has no SQL Server LocalDB dependency here: the tests and the app both use
  `.\SQLEXPRESS`, so the connection string is needed in the test project too (a test-only
  User Secret or an environment variable in the test run; no secret is committed).
