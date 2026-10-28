# M05 - Dashboard, reports, notifications

Status: **approved on 24/10/2026 and implemented**; the test round ran on 27/10/2026 (see
`test-results.md`, `bug-report.md` and `report.md`), waiting for the user to review the hand-over. The six
questions in section 7 were accepted with their recommendations.

References: `docs/design.md` sections 2.2, 3.2, 3.4, 4, 5.1, 5.2, 6.2, 6.12, 7 and phase 6 of section 8;
`.agents/skills/ccm-workflow`; `.agents/skills/ccm-database` (`sp_DashboardSummary`, `sp_MonthlyRevenue`,
`sp_MonthlyCareStats`, `sp_SearchCustomer`, `vw_CustomerRevenue`, `vw_ExpiringContract`,
`vw_OpenFeedback`, `vw_CareSchedule`); `.agents/skills/ccm-web-ui`; `.agents/skills/ccm-language`.

## 1. Goal

Give each role a home page that shows what has to be done today, and give Manager, Sales and Support the
reports they are allowed to read. Everything is **read-only**: M05 adds no write path and the database
keeps all the business logic. The dashboard, the reports and the notification bell read only from the
existing views and procedures; the application does not recompute totals, revenue or tiers itself.

Today `/` is a placeholder page (`Home/Index.cshtml`), the bell shows three counts only, and the
sidebar entry "Reports" is marked as not built.

## 2. Scope

| # | Feature | Source |
|---|---|---|
| 1 | Dashboard `/`: KPI card row from `sp_DashboardSummary`; each card links to the related page | design.md 6.2 |
| 2 | Dashboard revenue bar chart for the 12 months of a selected year (`?year=`, default the current year) from `sp_MonthlyRevenue` | design.md 6.2 |
| 3 | Dashboard "Top customers": the 5 active customers with the highest total value, tier badge, from `vw_CustomerRevenue` | design.md 6.2 |
| 4 | Dashboard "Contracts expiring soon": customer, end date, days remaining, link to the contract (the renew dialog lives on the contract page, built in M03) from `vw_ExpiringContract` | design.md 6.2 |
| 5 | Dashboard "Feedback to handle": priority, content (truncated), due date, assignee, overdue marker, link to the item, from `vw_OpenFeedback` ordered by priority then due date | design.md 6.2 |
| 6 | Dashboard "Care schedule": Overdue / Today / Upcoming groups from `vw_CareSchedule`, link to `/care` | design.md 6.2 |
| 7 | Blocks shown by role (section 3); a block the role may not open is not rendered, so no card links to a 403 | design.md 5.1, 6.2 |
| 8 | `/reports` with three tabs, each its own URL: `/reports/revenue`, `/reports/customers`, `/reports/care`; `/reports` redirects to the first tab the role may open | design.md 4, 6.12 |
| 9 | Revenue tab: year select, bar chart by month, table month / contract count / revenue with a yearly total row, from `sp_MonthlyRevenue` | design.md 6.12 |
| 10 | Customers tab: ranking by total value from `vw_CustomerRevenue`, filter by tier, sortable, paged; donut chart of the tier proportions of active customers | design.md 6.12 |
| 11 | Care tab: year select; visits by month and care type, the average satisfaction score of surveys, a bar chart of visits per month, from `sp_MonthlyCareStats` | design.md 6.12 |
| 12 | "Export" button on each tab (CSV, see question 2); exports the data of the current filter, not just the page on screen | design.md 6.12 |
| 13 | Notification bell opens a grouped list: expiring contracts, overdue feedback, care overdue/due today; each group shows its count, up to 3 items and a "view all" link to the filtered page; a group the role cannot open is hidden and not counted | design.md 3.2 |
| 14 | Quick search in the topbar shows up to 8 customer suggestions after the user stops typing (`sp_SearchCustomer`), with the existing keyword form as the fallback when scripts are off | design.md 3.2, M01 carry-over |
| 15 | Sidebar marks "Reports" as a built page; `/reports/care` keeps it highlighted | M01 carry-over |
| 16 | Empty, error and "no data for this year" states for every block and every tab; one failing block never blanks the page and never shows a fake 0 | AGENTS.md 5 |
| 17 | Every visible string in both `.resx` files; labels through `L.Label(...)`; money, dates and month names in the user's language | `ccm-language` |
| 18 | Unit tests for the pure rules; real-database tests that compare each figure on screen with a direct SQL query | `ccm-workflow` |

### Explicitly NOT in M05

- Any write: no new SQL table, view or procedure, no change to a trigger, no re-scaffold.
- Employees, accounts, products CRUD, audit log, profile (M06).
- Real `.xlsx` files, PDF export, printing layouts, e-mail or scheduled reports (see question 2).
- Customer-level reports beyond the ranking (per-owner, per-product, churn, forecasts).
- Comparing two years, drill-down charts, date ranges other than a calendar year.
- A charting library (see question 1) and any other new package.
- Live push of notifications; the bell is read on each page load like today.
- The renew action itself (stays in M03) and the "Mark as cared for" action (stays in M04).
- Lazy loading of blocks with skeletons: the page is rendered on the server in one request.

## 3. Permissions to implement

The application connects as `app_ccm` (role `role_Manager`), so the database grants do not separate the
roles; the **application** does, with `[Authorize(Roles = ...)]` and by not rendering the block.

| Screen / block | Manager | Sales | Support | Reason |
|---|---|---|---|---|
| Dashboard `/` | yes | yes | yes | matrix 5.1 |
| KPI: total customers, new customers (30 days) | yes | yes | yes | |
| KPI: active contracts, expiring contracts | yes | yes | yes | Support may view contracts |
| KPI: unresolved feedback (+ overdue) | yes | **no** | yes | Sales has no Feedback page |
| KPI: revenue this year | yes | yes | **no** | revenue is not a Support report |
| Revenue chart, Top customers | yes | yes | **no** | revenue data |
| Expiring contracts list | yes (+ link) | yes (+ link) | list only, no renew link | renew is Manager/Sales |
| Feedback to handle list | yes | **no** | yes | |
| Care schedule list | yes | yes (view) | yes | |
| `/reports/revenue` | yes | yes | **403** | matrix: revenue report |
| `/reports/customers` | yes | yes | **403** | assumption, question 3 |
| `/reports/care` | yes | **403** | yes | matrix: care report |
| Export (CSV) | same as its tab | same as its tab | same as its tab | checked on the server |
| Bell: expiring contracts | yes | yes | yes | |
| Bell: overdue feedback | yes | hidden | yes | |
| Bell: care overdue/today | yes | yes | yes | |
| Quick search suggestions | yes | yes | yes | |

The 6 KPI cards of design.md 6.2 therefore become 6 for Manager, 5 for Sales and 5 for Support. The
grid adapts so that a hidden block leaves no gap.

## 4. Data used by this module

| Use | DB object | Note |
|---|---|---|
| KPI cards | `sp_DashboardSummary` | One row. `AvgSatisfaction180Days` is returned but has no card in design.md 6.2, so it is not shown |
| Revenue chart and table | `sp_MonthlyRevenue @Year` | 12 rows; counts contracts `Active` and `Expired` by **sign date**; a month with nothing shows 0 and is a real zero |
| Top customers, ranking, tier donut | `vw_CustomerRevenue` | Counts `Active` and `Expired` contracts of every period; the view also returns inactive customers, so the application filters `IsActive = 1` |
| Expiring contracts | `vw_ExpiringContract` | Active contracts ending within 30 days; `DaysRemaining` comes from the view |
| Feedback to handle | `vw_OpenFeedback` | `New` + `InProgress`; `IsOverdue` from the view |
| Care schedule | `vw_CareSchedule` | Latest care record of each active customer, appointment within 7 days; `ScheduleStatus` from the view |
| Care report | `sp_MonthlyCareStats @Year` | One row per month and care type: `VisitCount`, `AvgSatisfaction` |
| Year choices | `CONTRACT` (min/max `SignDate`) | Read-only query; the current year is always offered |
| Quick search | `sp_SearchCustomer` | `@PageSize = 8`, active customers only |

The three procedures are read with `Database.SqlQueryRaw<T>` and typed `SqlParameter`s (never a
formatted string, never `DBNull` without a type; see M04 B02). The result classes are plain types in
`Models/ViewModels`, not scaffolded entities.

## 5. Calculation notes (so nobody has to guess)

- **Revenue is the database's number.** The page shows `sp_MonthlyRevenue` as returned. The yearly total
  is the sum of the 12 rows, and the "Revenue this year" KPI comes from `sp_DashboardSummary`; both use
  the same rule in SQL, so for the current year they must be equal. A test compares them.
- **Average satisfaction is shown only where the database gives it.** `sp_MonthlyCareStats` returns an
  average per month **and type**, plus a visit count that includes surveys with no score. A combined
  monthly average cannot be rebuilt correctly from those two columns, so the Care tab shows the average
  in the Survey column and does not invent a total average.
- **Percentages in the donut** are customer counts per tier divided by the number of active customers;
  the labels show both the count and the percentage, so the chart never relies on colour alone.
- **Bar heights** are proportional to the largest value of the year (minimum visible height for a
  non-zero value). Each chart is paired with its table or with value labels, for accessibility.
- **Tier labels** use the same label helper and badge as the customer list; there is no tier column that
  the application writes.

## 6. Definition of done

1. Every feature in section 2 works for the roles in section 3, in Vietnamese and in English.
2. Each KPI, chart value, table row and export on screen matches a direct SQL query on
   `CustomerCareDB_Test` (the cases in `plan.md`).
3. A role without permission gets `/access-denied` from the server for a direct URL and for an export
   URL, not only a missing button.
4. `dotnet build` has no errors or warnings; `dotnet test` passes, including the new cases.
5. No SQL file is changed, nothing is re-scaffolded, the live `CustomerCareDB` is not written to.
6. `test-results.md`, `bug-report.md` and `report.md` exist and say honestly what was and was not run.

## 7. Questions to settle before coding

| # | Question | Recommendation |
|---|---|---|
| 1 | **Charts.** The project has no chart library, and AGENTS.md says to ask before adding one. Options: (a) draw the bar and donut charts on the server with CSS/SVG, no package; (b) add Chart.js from the CDN already used for Tailwind and Font Awesome | **(a).** Three simple charts (12 bars, 12 bars, one donut) do not need a library; (a) keeps working with scripts off, follows the theme variables and the dark theme, and every chart is next to its table. Choose (b) only if you want hover tooltips and animation |
| 2 | **Export.** design.md says "Export Excel/CSV". A real `.xlsx` needs a new package (for example ClosedXML) | **CSV only**, UTF-8 with a BOM so Excel shows Vietnamese correctly; cells that start with `=`, `+`, `-` or `@` are neutralised against formula injection. A CSV opens in Excel; add `.xlsx` later only if the course requires it |
| 3 | **Who reads the Customers tab.** The matrix only has "Revenue report" and "Care report"; the Customers tab (ranking by value, tier donut) is revenue data | Manager and Sales, not Support. The Support menu stays "Reports (care)" as in design.md 5.2 |
| 4 | **Bell content.** Counts only (today), or a grouped list as in design.md 3.2? | Grouped list: count, up to 3 items and a "view all" link per group, hidden for a group the role cannot open. This costs up to three small extra queries per page load; if that is too heavy, keep counts and add only the links |
| 5 | **Satisfaction average.** Add a SQL change to return a correct monthly average, or show it only in the Survey column? | Survey column only, **no SQL change** (section 5). If you want a combined average, it becomes a small separate task that edits `02_Function_View_Proc_Trigger.sql` |
| 6 | **Quick-search suggestions.** In M05, or leave the topbar box as a plain keyword form? | In M05: it is listed in design.md 3.2, the topbar comment already defers it to this module, and it needs one small JSON endpoint and about 40 lines of script |

If any answer differs from the recommendation, only the matching step of `plan.md` changes.
