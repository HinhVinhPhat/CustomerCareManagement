# M05 - Dashboard, reports, notifications - plan

Companion to `requirements.md`. One approval covers both documents. Steps run in order; anything out of
scope or any business change stops the work and asks the user first.

## 1. Steps

### Step 0 - decisions

Questions 1-6 of `requirements.md` section 7 are settled before coding. The steps below assume the
recommendations (CSS/SVG charts, CSV export, Customers tab for Manager and Sales, grouped bell list,
Survey-only satisfaction average, quick-search suggestions in). A different answer changes only the step
named in brackets: 1 -> Step 6 and `site.css`; 2 -> Step 4 and Step 6; 3 -> Step 5; 4 -> Step 4 and
Step 6; 5 -> a new SQL step (stop and re-plan); 6 -> Steps 5, 6 and 7.

### Step 1 - SQL

No SQL change and no re-scaffold. `sp_DashboardSummary`, `sp_MonthlyRevenue`, `sp_MonthlyCareStats`,
`sp_SearchCustomer` and the four views already exist. The entities `VwCustomerRevenues`,
`VwExpiringContracts`, `VwOpenFeedbacks`, `VwCareSchedules` are already scaffolded and used by M02-M04.

### Step 2 - shared values and helpers

1. `Helpers/WorkConstants.cs`: a `Reports` group with the tab keys `revenue`, `customers`, `care` and the
   role list of each tab (the same lists the controller attributes use, so they cannot drift), and
   `Dashboard.TopCustomerCount = 5`, `Bell.ItemsPerGroup = 3`, `Search.SuggestionCount = 8`.
2. `Helpers/ReportRules.cs` (pure, testable, no database):
   - `ClampYear(year, min, max)`: an out-of-range or missing year falls back to the current year.
   - `AvailableYears(minSignYear, maxSignYear, currentYear)`: the continuous list, newest first, always
     containing the current year.
   - `PivotCareStats(rows)`: turns the (month, type) rows into 12 month rows with a visit count per care
     type, a month total and the Survey average; a missing month is a zero row.
   - `BarHeights(values, maxPercent)`: proportional heights, a non-zero value never rounds to 0.
   - `TierShares(counts)`: counts and percentages that add up to 100 (largest-remainder rounding).
3. `Helpers/CsvWriter.cs` (pure, testable): quotes fields containing `,`, `"` or a line break, doubles
   quotes, prefixes `'` to a cell that starts with `=`, `+`, `-` or `@` (formula injection), writes
   numbers without thousands separators, dates as `dd/MM/yyyy`, UTF-8 with a BOM.
4. `Helpers/Navigation.cs`: `reports` flips `Built` to `true`; the Reports entry stays visible to all
   roles, as design.md 5.2 says. `administration` stays `false`.
5. `Helpers/Utils.cs`: no new error code is expected (M05 writes nothing); a failing read is logged on the
   server and shown as the generic message of the block.

### Step 3 - ViewModels and result types

`Models/ViewModels/`:

1. `DashboardViewModel.cs`: the KPI values, the selected year, the 12 revenue rows, the top customers,
   the expiring, feedback and care lists, and for **each block** a nullable error key so one block can
   fail alone. A block the role may not see is `null`, not an empty list (the view tells "hidden" from
   "empty").
2. `ReportViewModels.cs`: `RevenueReportViewModel` (year, years, rows, total), `CustomerReportViewModel`
   (filter: tier, sort, desc, page, page size; rows; tier shares; total rows) and `CareReportViewModel`
   (year, years, the pivot, the Survey average, the chart values).
3. `ReportRows.cs`: plain result classes for `SqlQueryRaw<T>`: `DashboardSummaryRow`, `MonthlyRevenueRow`
   (`Month`, `ContractCount`, `Revenue`), `MonthlyCareStatRow` (`Month`, `CareType`, `VisitCount`,
   `AvgSatisfaction` nullable). Property names equal the column names; `Revenue`/`AvgSatisfaction` are
   `decimal`.
4. `NotificationSummaryViewModel.cs`: extended with up to 3 items per group (a small `NotificationItem`
   with label, detail, link); the existing three counts stay, and a group the role cannot open is `null`.
5. `CustomerSuggestionViewModel.cs`: id, name, type, a link; nothing else leaves the server.

### Step 4 - Services

1. `IDashboardService` / `DashboardService`:
   - `GetAsync(year, role)` runs **only** the queries the role may see (section 3 of the requirements),
     each in its own try/catch that records the error key of that block; it never returns tracked
     entities or `IQueryable`.
   - Revenue and summary through `SqlQueryRaw<T>` with typed `SqlParameter`s; lists through the views
     with `AsNoTracking`, `Take(5)` or the view's own ordering (`Priority, DueDate` for feedback).
   - Top customers: `IsActive == true`, ordered by `TotalValue` descending, tie-break by `CustomerName`.
2. `IReportService` / `ReportService`:
   - `GetRevenueAsync(year)`, `GetCustomersAsync(filter)`, `GetCareAsync(year)`, `GetYearsAsync()` (the
     `MIN/MAX(SignDate)` query), and the three `Export...Async` methods that return rows **without paging**
     for the same filter.
   - `MonthlyCareStats` is pivoted by `ReportRules.PivotCareStats`; no average is computed in C#.
3. `INotificationService` / `NotificationService`: `GetSummaryAsync(role)` returns counts and up to 3
   items per group, reading the same three views as today; the overdue-feedback group is `null` for Sales.
4. `ICustomerService` gets `SuggestAsync(keyword, count)` calling `sp_SearchCustomer` with `@PageSize = 8`,
   active only; a keyword shorter than 2 characters returns an empty list without a query.
5. DI registration in `Program.cs`, one line per service, no interface that has a single caller and no
   repository layer.

### Step 5 - Controllers

1. `HomeController.Index`: reads `year`, calls `DashboardService.GetAsync(year, role)`; the role comes from
   the claim. The class keeps `/access-denied` and `/error` untouched.
2. `ReportsController` (new, attribute routing):
   - `GET /reports` redirects to the first tab of the role (Manager and Sales: revenue; Support: care).
   - `GET /reports/revenue` `[Authorize(Roles = Manager,Sales)]`; `GET /reports/customers` the same;
     `GET /reports/care` `[Authorize(Roles = Manager,Support)]`.
   - `GET /reports/revenue/export`, `/reports/customers/export`, `/reports/care/export` with the same role
     attribute as their tab; they return `text/csv; charset=utf-8` with `Content-Disposition`
     `ccm-<tab>-<year or filter>.csv`. The file name is built from validated values only.
3. `CustomersController`: `GET /customers/suggest?q=` returns a small JSON array (all roles, signed in).
   It is read-only and needs no anti-forgery token; it never returns phone, email or address.
4. Controllers stay thin: validate the query values, call a service, return a View/File/Json.

### Step 6 - Views (every page split into partials; the page file holds only the header and `<partial>` calls)

1. `Views/Home/Index.cshtml` and `Views/Home/Dashboard/`: `_KpiCards`, `_YearPicker`, `_RevenueChart`,
   `_TopCustomers`, `_ExpiringContracts`, `_FeedbackToHandle`, `_CareSchedule`, `_BlockError`,
   `_BlockEmpty`. The placeholder text and its resource keys (`Dashboard_FrameTitle`, `Dashboard_FrameBody`)
   are removed because nothing uses them any more.
2. `Views/Reports/`: `Revenue.cshtml`, `Customers.cshtml`, `Care.cshtml`, and the partials `_Tabs`,
   `_YearFilter`, `_TierFilter`, `_RevenueTable`, `_CustomersTable`, `_CareTable`, `_ExportButton`,
   `_Pager` (each module already has its own `_Pager.cshtml`; the report copy uses the same markup and the lists are not refactored).
3. `Views/Shared/Charts/`: `_BarChart` (CSS/SVG, one value per bar, a label under each bar, the value in
   the `title` and as visible text), `_Donut` (SVG circle segments, legend with count and percentage).
   Colours only through the CSS variables of `site.css`; the dark theme is covered by the same variables.
4. `Views/Shared/Components/NotificationBell/Default.cshtml`: grouped list with count, up to 3 items and
   the "view all" link (`/contracts?expiring=true`, `/feedbacks?overdue=true`, `/care`); a `null` group is
   not rendered.
5. `Views/Shared/_Topbar.cshtml`: the suggestion list container next to the existing search form.
6. `wwwroot/css/site.css`: classes for `.kpi`, `.chart-bar`, `.chart-donut`, `.legend`; no hard-coded colour.
7. `wwwroot/js/site.js`: debounce (250 ms), minimum 2 characters, `fetch` of `/customers/suggest`, keyboard
   up/down/Enter/Escape, results built with `textContent` (never `innerHTML` with server data), the plain
   form submit when scripts are off.
8. Responsive: below 768 px the KPI row is two columns, the chart scrolls horizontally inside its own box,
   tables follow the same card-list rule as M02-M04.

### Step 7 - resources

`Resources/SharedResource.resx` (Vietnamese) and `SharedResource.en.resx` in step: dashboard blocks, KPI
titles, report tabs, column headers, empty and error messages, export button, bell groups, suggestions,
month names are **not** resource keys (they come from the culture). Each key exists in both files; the
existing `LocalizationTests` must still pass.

### Step 8 - tests (written with the code, run in Step 9)

Unit tests, no database, in `tests/CcmWeb.Tests/`: `ReportRulesTests.cs`, `CsvWriterTests.cs`,
`DashboardRolesTests.cs` (which blocks a role gets). Real-SQL-Server tests, against
`CustomerCareDB_Test` only: `ReportDatabaseTests.cs` comparing the service output with direct queries.

### Step 9 - verification and documents

1. Recreate `CustomerCareDB_Test` from copies of the four SQL files (never edit the originals), load the
   sample data, `dotnet build`, `dotnet test`.
2. Run the application against the test DB and execute the cases of section 2 as `john`, `mary` and
   `david`, in both languages and in light and dark theme.
3. Write `test-results.md` (PASS/FAIL/BLOCKED/NOT RUN with evidence), `bug-report.md` (if anything fails;
   the batch needs your approval before any fix), and `report.md`. Update `requirements.md` status.
   `docs/design.md` is changed only if a contradiction is found, and then only after asking.

## 2. Planned test cases

Evidence for every data case is a direct SQL query on `CustomerCareDB_Test`, pasted into
`test-results.md` next to the screen value.

| Id | Case | Expected |
|---|---|---|
| T01 | `dotnet build` | 0 errors, 0 warnings |
| T02 | `dotnet test` (existing + new) | all pass |
| T03 | `ReportRules.ClampYear` / `AvailableYears` | bad year -> current year; list continuous, newest first, includes the current year |
| T04 | `ReportRules.PivotCareStats` | 12 rows, a missing month is zero, Survey average only in the Survey column, totals match |
| T05 | `ReportRules.TierShares` | percentages add up to 100 for several count sets, including all-zero (no divide by zero) |
| T06 | `ReportRules.BarHeights` | non-zero value never 0 height; all-zero year gives flat bars, no NaN |
| T07 | `CsvWriter` quoting and formula guard | `a,b`, `"x"`, line break quoted; `=1+1`, `+1`, `-1`, `@x` neutralised; BOM present |
| T08 | Dashboard role matrix (unit) | Manager 6 KPI cards, Sales 5 (no feedback), Support 5 (no revenue); block lists as in requirements 3 |
| T09 | Login `john`, open `/` | six KPI cards; each equals `EXEC sp_DashboardSummary` |
| T10 | KPI links | each card opens its page; no card of any role leads to `/access-denied` |
| T11 | Revenue chart, year = current | 12 bars equal `EXEC sp_MonthlyRevenue @Year`; labels and values visible |
| T12 | Revenue KPI vs table total | "Revenue this year" equals the yearly total of the Revenue tab |
| T13 | Change the year on the dashboard | URL keeps `?year=`; chart updates; a year with no contracts shows 12 zero bars and an empty-state note, not an error |
| T14 | Top customers | 5 rows, only active customers, descending by value, tier badge matches `CUSTOMER.Tier`; deactivate a top customer in the test DB and re-check |
| T15 | Expiring contracts | rows equal `vw_ExpiringContract`; days remaining as in the view; Manager/Sales link to the contract, Support sees no renew link |
| T16 | Feedback to handle | equals `vw_OpenFeedback` ordered by priority then due date; overdue marked with text and colour; Sales does not see the block |
| T17 | Care schedule | Overdue / Today / Upcoming groups equal `vw_CareSchedule`; Sales sees it read-only |
| T18 | `mary` (Sales) dashboard | no feedback card or list, revenue blocks present |
| T19 | `david` (Support) dashboard | no revenue card, chart or top customers; feedback and care blocks present |
| T20 | Empty database state (test DB with no feedback, no care) | each block shows its empty state, KPIs show real 0 |
| T21 | One block fails (a test double of one query throws; the application login cannot be restricted per view because `app_ccm` is in `role_Manager`) | only that block shows the error message; the other blocks render; no SQL text on screen |
| T22 | `/reports` as `john`, `mary`, `david` | redirects to revenue, revenue, care |
| T23 | Revenue tab | rows equal `sp_MonthlyRevenue`; total row equals `SUM(Revenue)`; chart equals the table |
| T24 | Customers tab | ranking equals `vw_CustomerRevenue` for active customers; tier filter, sort, paging 10/20/50 work; donut counts equal `COUNT(*) GROUP BY Tier` |
| T25 | Care tab | table equals `sp_MonthlyCareStats` pivoted; Survey average equals SQL `AVG`; no combined average shown |
| T26 | Permissions on pages | `david` -> `/reports/revenue` and `/reports/customers` give 403 page; `mary` -> `/reports/care` gives 403 page; direct URL, not just a missing menu item |
| T27 | Permissions on exports | the same three refusals for the three `/export` URLs; an anonymous request goes to login |
| T28 | Export revenue | file opens, UTF-8 BOM, header in the user's language, 12 rows + total, numbers equal the table |
| T29 | Export customers with a tier filter | file has all filtered rows (not only page 1), same order as the screen |
| T30 | Export with a hostile customer name (`=HYPERLINK(...)`, a comma, a quote, a line break) inserted in the test DB | cell is neutralised / quoted; file still parses into the right number of columns |
| T31 | Bell for `john` | three groups; counts equal the three views; up to 3 items each; "view all" links open the filtered pages |
| T32 | Bell for `mary` | no feedback group; the badge total does not include feedback |
| T33 | Bell with zero items | no red badge number, groups show "nothing to do" |
| T34 | Quick search "nguyen", "NGUYEN", "nguyễn" | up to 8 suggestions, active customers only; accent handling the same as `/customers?q=` |
| T35 | Quick search with 1 character / empty result / a `<script>` string | no request or empty list; no result; the string is shown as text, nothing runs |
| T36 | Quick search with scripts disabled | Enter submits `/customers?q=...` as before |
| T37 | Language switch on `/`, `/reports/*`, the bell | every string and month name changes; money `80.000.000 VNĐ` / `80,000,000 VND`; dates `dd/MM/yyyy` |
| T38 | Dark theme | charts, donut, badges and KPI cards stay readable; no hard-coded colour found in the new markup |
| T39 | Accessibility basics | each chart has its table or visible values; status is never colour alone; tabs and suggestions work with the keyboard |
| T40 | Audit actor and data safety | opening dashboard/reports/exports writes nothing to `AUDIT_LOG`, `CONTRACT`, `CUSTOMER`, `FEEDBACK` (row counts before and after) |
| T41 | Mobile layout (below 768 px) | usable; **NOT RUN** unless a viewport control is available (same limit as M02-M04) |
| T42 | A second page of the customer ranking | pager page 2 works; **NOT RUN** if the sample set is smaller than one page |
| T43 | Live database untouched | `CustomerCareDB` has no new rows and no schema change |

## 3. Risks

- **`SqlQueryRaw` on a procedure** cannot be composed with `Where`/`OrderBy`; the result is read with
  `ToListAsync` and shaped in C#. A column the result class does not map (a name or type mismatch) fails
  at run time, so each result class is exercised by a real-database test.
- **Untyped NULL parameters** (the M04 B02 defect): every `SqlParameter` is typed; the year is an `int`.
- **Revenue is counted by sign date, ranking by all-time value.** The two numbers on one page answer
  different questions; the headers say so ("revenue in <year>" and "total contract value").
- **`vw_CustomerRevenue` includes inactive customers.** The filter lives in the service and has a test
  (T14, T24); a missed filter would put a deactivated customer in the top five.
- **Care average.** A combined average cannot be derived from the procedure's output (requirements 5);
  the temptation to average the averages is guarded by T25.
- **Time zone and "today".** The views use `GETDATE()` of SQL Server and the application shows
  `DateOnly` as `dd/MM/yyyy`; the application does not compute "overdue" or "days remaining" itself.
- **Cost of the bell on every page.** It adds up to three small queries per request for the lists (the
  counts already exist). If a measurable slowdown appears in the test round it is reported as a finding,
  not tuned silently.
- **CSV injection and encoding.** Covered by T07, T28-T30. Opening in Excel with a Vietnamese locale is
  checked by hand where Excel is available; otherwise recorded as NOT RUN.
- **Quick-search XSS.** Suggestions are inserted with `textContent` and links are built from the numeric
  id (T35).
- **Open redirect / parameter tampering.** `year`, `tier`, `sort` and `page` are validated against
  allowed lists and ranges before reaching a service or a file name.

## 4. Out-of-scope guard

Anything not in section 2 of `requirements.md` (new SQL, a charting or Excel package, product or employee
administration, scheduled reports, editing data from the dashboard, a combined satisfaction average,
year-over-year comparison) stops the work and asks you first. Small, obvious tasks (a label, a colour, a
one-line fix) do not need the whole process.

## 5. Note on module naming

`ccm-workflow` lists M05 as "Dashboard, revenue report, care report, feedback report, notifications".
design.md 6.12 defines three report tabs (Revenue, Customers, Care) and no separate feedback report; the
feedback figures live on the dashboard and on `/feedbacks` (M04). This plan follows design.md. If you want
a feedback report tab (for example by status, type and average time to resolve), it needs a decision and a
new SQL object, so it is not included.
