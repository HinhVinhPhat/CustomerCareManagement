# M01 - Foundation - test results

Run date: **18/10/2026**. Written after the code was frozen for this round; every row below was
actually executed. Nothing is ticked that was not run.

## 1. Environment actually used

| Item | Value |
|---|---|
| Operating system | Windows, Visual Studio / .NET CLI |
| .NET SDK | 10.0.400 and 10.0.401 |
| .NET runtime | Microsoft.AspNetCore.App 10.0.12, Microsoft.NETCore.App 10.0.12 |
| EF Core | 10.0.13 (runtime package and `dotnet-ef` tool) |
| Database engine | Microsoft SQL Server 2025 (RTM) 17.0.1000.7, instance `.\SQLEXPRESS` |
| Database under test | `CustomerCareDB_Test`, rebuilt by the test fixture from `database/01`-`03` with the database name replaced |
| Database for the final smoke check | `CustomerCareDB` (the application's development database) |
| Application login | `app_ccm`, created by `database/04_CreateAppUser.sql` |
| How the app was run | `dotnet run --project src/CcmWeb --no-launch-profile`, `ASPNETCORE_URLS=http://localhost:5199` |

The live `CustomerCareDB` was **not** written into by the automated tests. During the UI round the
application's connection string pointed at `CustomerCareDB_Test` through User Secrets; it was put
back to `CustomerCareDB` afterwards.

## 2. Automated tests

`dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj`

```
Passed!  - Failed: 0, Passed: 22, Skipped: 0, Total: 22, Duration: 4 s - CcmWeb.Tests.dll (net10.0)
```

Includes four tests that talk to the real SQL Server test database (`SESSION_CONTEXT`, trigger-safe
writes, and two real `SqlException` translations). EF InMemory / SQLite were not used anywhere.

## 3. Cases

| Id | Case | Expected | Actual | Status |
|---|---|---|---|---|
| T01 | Clean build | 0 errors | `Build succeeded. 0 Error(s)`, 8 NuGet audit warnings (see 5.1) | PASS |
| T02 | Sign in as Manager (`john` / `123456`) | lands on `/`, menu = all 8 entries | landed on `/` as "John Smith · Manager"; menu: Dashboard, Customers, Contracts, Contacts, Care, Feedback, Reports, Administration | PASS |
| T03 | Role menus (`mary` Sales, `david` Support) | Sales has no Feedback/Administration, Support has no Administration | Sales: Dashboard, Customers, Contracts, Contacts, Care, Reports. Support: the same plus Feedback. Neither shows Administration | PASS |
| T04 | First sign-in replaces the demo SHA2 hash | 64 hex chars before, `PasswordHasher` value after, sign-in still works | `john len=64 legacy=yes` before; `len=84 legacy=no` (`AQAAAAIAAYag...`) after; log: `Upgraded AccountID 1 from the demo SHA2 hash to PasswordHasher` | PASS |
| T04b | Second sign-in uses the `PasswordHasher` branch | sign-in succeeds | `Employee 1 signed in with role Manager` after the hash was already upgraded | PASS |
| T05 | Wrong password vs unknown username | identical answer, no hint that the account exists | both return HTTP 200 with "Incorrect username or password."; the locked-account message is not shown for a wrong password | PASS |
| T06 | Locked account (`helen`, `IsActive = 0`) | the lock is reported only with the correct password | correct password -> "This account has been locked. Please contact an administrator."; wrong password -> "Incorrect username or password."; after `IsActive = 1` the sign-in succeeds again | PASS |
| T07 | Anonymous access and `returnUrl` | protected page bounces to `/login`; only a local `returnUrl` is honoured | `/` -> 302 `/login?ReturnUrl=%2F`; `/contracts` -> 302 `/login?ReturnUrl=%2Fcontracts`; local `returnUrl=/contracts` -> 302 `Location: /contracts`; `returnUrl=https://evil.example/x` -> 302 `Location: /` | PASS |
| T08 | A page the role may not open returns 403 | the access-denied page, not the sign-in page | **could not be triggered: M01 has no action restricted to a role** (the first one arrives in M02). The page itself renders: `GET /access-denied` -> 200 with the "Access denied" text | NOT RUN |
| T09 | `SESSION_CONTEXT('EmployeeID')` on EF connections | the signed-in employee id, NULL without a user | xUnit: `CAST(SESSION_CONTEXT(N'EmployeeID') AS INT)` = 3 with a claim, NULL without | PASS |
| T10 | Trigger-safe writes | a `CUSTOMER` insert/update through EF does not fail with error 334 | xUnit: the insert returns a generated id; the customer table has a trigger and no 334 was raised | PASS |
| T11 | SQL errors become English business messages | 50004 passed through, 547 and 2601/2627 mapped, unknown -> null | xUnit: unit cases plus two real `SqlException`s raised by the test database (duplicate `UX_Customer_Email`, `CHK_Customer_Business`) returning the expected English text | PASS |
| T12 | Legacy SHA2 vector | `EC278A38...CCE24` for `123456` as UTF-16LE, PasswordHasher hash not mistaken for it | xUnit: 5 assertions pass | PASS |
| T13 | Sign out | POST signs out and clears the cookie; GET is not accepted | `POST /logout` -> 302 and the next `GET /` -> 302 `/login?ReturnUrl=%2F`; `GET /logout` -> 405 and the user stays signed in | PASS |
| T14 | Notification bell shows real counts | 2 expiring contracts, 2 overdue feedback, 2 care items due (values in the test database) | rendered 2 / 2 / 2 and a badge total of 6, matching `vw_ExpiringContract`, `vw_OpenFeedback` and `vw_CareSchedule` | PASS |
| T15 | Contract expiry job at start-up | an `Active` contract with a past `EndDate` becomes `Expired` | contract 3 set to `Active`, `EndDate=2026-10-01`; after a restart the log says `1 contract(s) moved to Expired` and the row is `Expired`. The row was restored afterwards | PASS |
| T16 | POST without an anti-forgery token | rejected | HTTP 400 | PASS |
| T17 | No fake data left in the application | no `mock-data`, `ccm_user` or `localStorage` in `src/` | no matches | PASS |
| T18 | Mobile layout below 768 px | sidebar becomes a drawer opened by ☰ | **not run: this environment cannot emulate a viewport**, and screenshots need a visible desktop window | NOT RUN |

BLOCKED: none.

## 4. Bugs found during this round

| Id | What happened | Fixed by |
|---|---|---|
| B01 | Sign-in page raised "The view 'Login' was not found" (HTTP 500) because the view folder was `Views/Accounts` while the controller is `AccountController` | renamed the folder to `Views/Account`; the reference in `ccm-web-ui` was corrected too |
| B02 | Every static file (`/js/site.js`, `/css/site.css`, `/favicon.ico`) was redirected to the sign-in page, so the page had no script and no styling | `MapStaticAssets()` registers endpoints that the fallback authorization policy denied; replaced with `app.UseStaticFiles()` before `UseRouting()` |
| B03 | `data-soon=""` was still rendered on menu entries whose page exists | the sidebar now writes an explicit `"true"`/`"false"` |

All three were blocking or cosmetic defects found by running the application; each was fixed
immediately so the round could continue, and the affected cases were re-run afterwards.

## 5. Known issues and things that are not done

### 5.1 NuGet audit warnings (decision needed)

The build reports 8 `NU1902` / `NU1903` warnings: `System.Security.Cryptography.Pkcs` 9.0.11 has a
known moderate and a known high severity advisory. The package is pulled in transitively
(`Microsoft.EntityFrameworkCore.SqlServer` -> `Microsoft.Data.SqlClient` -> `Azure.Identity`), and the
application never uses the Azure/Entra code path because it signs in with SQL credentials.
Removing the warnings means adding a direct reference to a patched version of that package, which is
a new package reference and therefore needs the user's approval.

### 5.2 Deliberate limitations of M01

- The topbar search box and the menu entries for pages that do not exist yet say "arrives in a later
  module" instead of failing; the customer screen and the live `sp_SearchCustomer` suggestions come
  with M02.
- The notification panel lists the three counts without links, because the contract, feedback and
  care pages do not exist yet.
- `/` is a placeholder page; `sp_DashboardSummary` and the real dashboard come with M05.
- A request for an address that matches no endpoint while signed out is redirected to the sign-in
  page (the fallback authorization policy applies to endpoint-less requests). The 404 page is part
  of M07.
- `Views/Home/Index.cshtml` and the notification panel are the only screens in this module.

### 5.3 Effect on the live development database

The final smoke check signed in as `john` against `CustomerCareDB`, which upgraded that account's
`PasswordHash` from the demo SHA2 value to a `PasswordHasher` value, exactly as the first sign-in is
designed to do. Running `database/01`-`03` again restores the demo hashes. The other accounts in the
live database were not touched.

`CustomerCareDB_Test` is left on the server; `dotnet test` drops and rebuilds it, so it can also be
deleted by hand.
