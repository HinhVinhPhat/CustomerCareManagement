# M01 - Foundation

Status: **approved on 18/10/2026** - implemented and tested in module M01; see `test-results.md` and
`report.md` in this folder. Reference: `docs/design.md` section 8 phases 1-2,
`.agents/skills/ccm-workflow`.

## 1. Goal

Stand up the ASP.NET Core MVC application so that every later module can reuse it: the project
skeleton, the EF Core database-first model over `CustomerCareDB`, the mandatory
`SESSION_CONTEXT('EmployeeID')` hook for the audit log, cookie login with role-based
authorization, and the shared page frame (sidebar + topbar) that all other screens render into.

At the end of M01 a person can log in as one of the three demo roles, see a menu built for that
role, be blocked from pages the role may not open, and log out. No business screen (customers,
contracts, contacts, care, feedback, reports, admin) is built in this module.

## 2. Scope

| # | Feature | Source |
|---|---|---|
| 1 | Solution + `src/CcmWeb` (MVC) + `tests/CcmWeb.Tests` (xUnit) | `ccm-mvc-architecture` |
| 2 | EF Core database-first `ApplicationDbContext` + entities, scaffolded from `CustomerCareDB` | `ccm-mvc-architecture` |
| 3 | Trigger-safe EF configuration: `UseSqlOutputClause(false)` on `CONTRACT`, `CONTRACT_DETAIL`, `CUSTOMER`, `FEEDBACK` | `ccm-mvc-architecture` |
| 4 | `SESSION_CONTEXT('EmployeeID')` interceptor + `GetEmployeeID()` helper | design.md 6.1 |
| 5 | Login `/login`, logout `POST /logout`; the SHA2 demo hash is accepted once, then re-hashed with `PasswordHasher` | design.md 6.1 |
| 6 | Cookie authentication, role authorization, fallback policy = login required | design.md 5, 6.1 |
| 7 | Shared layout: `_Layout`, `_Sidebar` (role menu), `_Topbar` (search box, notification bell, user menu) | design.md 3.1, 3.2, 5.2 |
| 8 | Notification bell with real counts | design.md 3.2 |
| 9 | 403 access-denied page and 500 error page | design.md 6.15 |
| 10 | Temporary landing page `/` (placeholder, replaced by the real dashboard in M05) | design.md 6.2 (partial) |
| 11 | `sp_ExpireContracts` called at start-up and once a day by a `BackgroundService` | AGENTS.md 4 |
| 12 | xUnit tests for `TranslateSqlError`, the legacy SHA2 check and the formatting helpers | `ccm-workflow` |

### Explicitly NOT in M01

- Customers, contracts, contacts, care, feedback, reports, admin screens (M02-M06).
- The real dashboard and the report pages (M05).
- Shared list/form building blocks (filter bar, pager, `_Badge`, data table) beyond what the
  layout itself needs - they arrive with the first screen that uses them (M02).
- Profile page and change password (design.md 6.14) - they belong with account handling (M06).
- 404 page and responsive polish (M07).
- The Docker `web` service and Dockerfile (M07).

## 3. Data used by this module

| Use | DB object |
|---|---|
| Login | `ACCOUNT` joined to `EMPLOYEE` (for the display name) |
| Password re-hash after the first login | `ACCOUNT.PasswordHash` through `ExecuteUpdateAsync` |
| Audit-log actor | `sp_set_session_context N'EmployeeID', @employeeID` |
| Notification bell counts | `vw_ExpiringContract`, `vw_OpenFeedback` (`IsOverdue = 1`), `vw_CareSchedule` |
| Contract expiry at start-up and daily | `sp_ExpireContracts` |
| Topbar quick search | `sp_SearchCustomer` - results link to `/customers?q=...`; the customer screen itself is M02 |

**No SQL object is created or changed in this module.** `database/*.sql` stays as it is; the files
are only *run* to create the database.

## 4. Assumptions

1. **Framework version - decided.** `AGENTS.md` originally required .NET 8 (LTS), but this machine
   has only the .NET 10 SDK (10.0.400, 10.0.401) and the runtimes 9.0.7 and 10.0.11/10.0.12 - there
   is no .NET 8 runtime, so a `net8.0` application could not be run locally. The user approved
   **`net10.0` with EF Core 10**, which satisfies the skill requirement ("EF Core 8 or later" for
   `UseSqlOutputClause` and `SqlQueryRaw`). `AGENTS.md` section 3 was updated to match.
2. The four `database/*.sql` files are run on the local **SQL Server 2025 Express**
   (`.\SQLEXPRESS`, Windows authentication) to create `CustomerCareDB`. The database does not
   exist yet (the instance also holds unrelated databases; none of them is touched).
3. The connection string is named `DefaultConnection`, uses the login `app_ccm` created by file
   `04`, and is stored in **User Secrets** for development - never in `appsettings.json`. It needs
   `TrustServerCertificate=True` against the local self-signed certificate.
4. The demo password `123456` is accepted on the first login through the SHA2-256 branch
   (UTF-16LE input) and is immediately re-hashed with `PasswordHasher`, after which the
   `PasswordHasher` branch is used.
5. Tailwind, Font Awesome and the Be Vietnam Pro font are loaded from CDN, exactly as the
   reference build in `docs/mockups/static-mockups` does. Internet is available.
6. The demo accounts (password `123456`): `john`, `susan` (Manager), `mary`, `kevin` (Sales),
   `david`, `helen` (Support).
7. The notification bell and the topbar search box are the only pieces that read data before M02.
   If they are considered out of scope, the bell can be moved to M05 and M01 ships the topbar
   without it.

## 5. Definition of done

1. `dotnet build` succeeds with no errors; `dotnet test` runs the M01 unit tests and they PASS.
2. The app runs locally against the database and the login page is reachable at `/login`.
3. Logging in as `john`, `mary` and `david` with `123456` succeeds; the sidebar shows exactly the
   items of that role (design.md 5.2) and never the Admin item for Sales or Support.
4. After the first successful login of an account, `ACCOUNT.PasswordHash` is no longer 64 hex
   characters (it is a `PasswordHasher` hash) and logging in again still works.
5. A wrong password and an unknown username produce the same message; a locked account
   (`IsActive = 0`) with the correct password reports the lock.
6. An anonymous request to any page redirects to `/login`; after logging in, a local `returnUrl`
   is honoured and an external one is ignored. A logged-in user without the role gets the 403
   page, not the login page.
7. Logout only works as a POST, clears the cookie and returns to `/login`.
8. `SESSION_CONTEXT('EmployeeID')` is set on connections opened by EF while a user is logged in,
   proven by a test on the separate test database (the audit log itself stays empty in M01
   because only the contract and feedback triggers write it).
9. A `CUSTOMER` row can be inserted and updated through EF without SQL error 334, proving the
   `UseSqlOutputClause(false)` configuration.
10. The verification results are recorded in `docs/tasks/m01-foundation/test-results.md` with real
    evidence, honestly marked PASS / FAIL / BLOCKED / NOT RUN.
