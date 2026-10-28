# M02 - Customers - plan

Companion to `requirements.md`. One approval covers both documents. Steps run in order; anything out
of scope or any business change stops the work and asks the user first.

## 1. Steps

### Step 0 - the four decisions (settled 18/10/2026)

**D1a, D2a, D3a are approved and D4 is accepted**: paging and sorting move into
`sp_SearchCustomer`, the page size becomes a `SETTINGS` key, the detail page gets its header plus all
four read-only tabs, and the four SQL files are re-run (file 01 drops and recreates `CustomerCareDB`).
Steps 1 and the `PageSize` parts of steps 2, 6 and 7 are therefore in scope.

### Step 1 - SQL (only when D1a or D2a is approved)

1. `database/02_Function_View_Proc_Trigger.sql` (D1a): `sp_SearchCustomer` gains
   `@PageNumber INT = 1`, `@PageSize INT = 20`, `@SortBy NVARCHAR(20) = N'Name'`,
   `@SortDesc BIT = 0`, keeps its existing `WHERE` block untouched, and returns
   `COUNT(*) OVER () AS TotalRows`; the rows come from `OFFSET (@PageNumber - 1) * @PageSize ROWS
   FETCH NEXT @PageSize ROWS ONLY` and the `ORDER BY` uses a `CASE` over `@SortBy`/`@SortDesc`
   (parameterised - never string concatenation). The default call (no parameters) must return the
   same rows in the same order as today, so the extension stays backwards-compatible even though the
   only caller is the new service (nothing in the application calls this procedure yet).
2. `database/01_CreateTables.sql` (D2a): `CHK_Settings_Value` accepts `PageSize` (an integer string
   between 1 and 100); `DataType = 'Int'`, `Category = 'General'` for that key.
3. `database/03_SampleData.sql` (D2a): seed the system default `PageSize = 20`; keep the closing
   verification block printing its `OK` line.
4. Re-run the four files in order against `.\SQLEXPRESS` with `sqlcmd -I -f 65001`, record the
   exit codes and the `OK` line for `test-results.md`.
5. Update `.agents/skills/ccm-database/references/schema.md` (procedure signature, the settings key,
   the CHECK). Re-scaffold is **not** needed: procedures and views used through `SqlQueryRaw` do not
   appear in the scaffolded model. (If D2a also changes the `SETTINGS` columns - it does not - a
   re-scaffold would be required.)

### Step 2 - shared values and helpers

1. `Helpers/WorkConstants.cs`: `CustomerType` (`Individual`, `Business`), `CustomerGender`
   (`Male`, `Female`, `Other`), `CustomerTier` (`Standard`, `Silver`, `Gold`, `Diamond`, in rank
   order), `CustomerSource` (the five sample values, as suggestions only), `SortFields.Customers`
   (`Name`, `CreatedDate`, `TotalValue`), `Paging.DefaultPageSize = 20` and `AllowedPageSizes`
   `[10, 20, 50]`, plus the new `Settings.PageSize` key when D2a is approved.
2. `Helpers/Utils.cs`: `SanitisePage(int? page)` / `SanitisePageSize(int? size)` (1-100, fallback to
   the default), `SafeReturnUrl`-style helper if the list needs one, and the SQL error mapping for
   what this screen can hit: 50006 (invalid customer), 547 with `CHK_Customer_Business`,
   `CHK_Customer_Contact`, `CHK_Customer_Phone`, `CHK_Customer_Email`, `CHK_Customer_DateOfBirth`
   (one resource key each), and 2601/2627 with `UX_Customer_Email` (duplicate e-mail).
3. `Helpers/LabelKeys.cs`-style group names used by `L.Label("CustomerType", code)` so the coded
   values never reach the screen untranslated.
4. `Helpers/Navigation.cs`: the "customers" entry flips `Built` to `true` (the sidebar stops marking
   it "coming soon"), and `IsCurrent` also treats a child path as current, so `/customers/5` keeps
   the Customers item highlighted instead of highlighting nothing. Both are one-line changes with a
   unit test.

### Step 3 - services

1. `Services/ICustomerService.cs` + `CustomerService.cs`:
   - `SearchAsync(CustomerFilter filter)` -> `(IReadOnlyList<CustomerListRow> Rows, int TotalRows)`
     calling `sp_SearchCustomer` with `SqlQueryRaw` and the parameters above (or the plain call plus
     in-memory paging/sorting when D1b).
   - `GetDetailAsync(int customerId)` -> header values + `fn_CustomerAge` and
     `fn_CustomerContractTotal`, `null` when the customer does not exist.
   - `CreateAsync(CustomerFormModel model)` / `UpdateAsync(int id, CustomerFormModel model)` -
     normalized through one mapping method: trim, empty string -> NULL, business -> Gender and
     DateOfBirth forced NULL, `Tier`/`IsActive`/`CreatedDate` never set; catches `DbUpdateException`
     and rethrows a `BusinessException` with the resource key of the mapped error.
   - `SetStatusAsync(int customerId, bool isActive)` -> `sp_SetCustomerStatus`.
   - `GetOwnersAsync()` -> the employee list for the dropdown and the filter (value + name).
   - Detail tab data: `GetContractsAsync(customerId)` (`vw_ContractInfo`), `GetHistoryAsync`
     (`vw_InteractionHistory`), `GetFeedbackAsync` (`FEEDBACK`), `GetOverviewAsync` (contract count,
     average satisfaction, open feedback, next appointment).
2. No service returns a tracked entity or an `IQueryable` to the controller (AGENTS.md 3).

### Step 4 - view models

`Models/ViewModels/CustomerListViewModel.cs` (filter values + rows + paging + owners),
`CustomerFormViewModel.cs` (fields + the validation attributes with resource keys),
`CustomerDetailViewModel.cs` (+ one small model per tab). Validation attributes use resource keys,
and the business rules the database also enforces get a friendly message here first.

### Step 5 - controller

`Controllers/CustomersController.cs`, attribute routing exactly as in design.md 4:
`/customers`, `/customers/create`, `/customers/{id}`, `/customers/{id}/edit`, and
`POST /customers/{id}/status`. `[Authorize]` at the class level,
`[Authorize(Roles = "Manager,Sales")]` on create/edit/status. The controller checks `ModelState`,
calls the service, redirects after a successful POST (Post-Redirect-Get) with a `TempData` toast, and
keeps the entered data plus a field-level message on a business error. `employeeID` and role come
from the claim.

### Step 6 - views (each page split into partials)

```
Views/Customers/Index.cshtml              header + partials only
Views/Customers/_FilterBar.cshtml         q, tier, owner, include inactive, only mine, clear
Views/Customers/_CustomerTable.cshtml     header row with sort links + rows
Views/Customers/_CustomerRow.cshtml       one row (also used by the card list under 768px)
Views/Customers/_Pager.cshtml             "showing X-Y of Z", page numbers, 10/20/50
Views/Customers/_EmptyState.cshtml        icon + text + clear filters (+ add customer)
Views/Customers/Create.cshtml             header + form partial
Views/Customers/Edit.cshtml               header + form partial
Views/Customers/_Form.cshtml              the form itself
Views/Customers/_FormFields.cshtml        the field set, shared by create and edit
Views/Customers/Details.cshtml            header + tab partials
Views/Customers/_CustomerHeader.cshtml    summary card + Edit / Deactivate / Reactivate
Views/Customers/_TabOverview.cshtml
Views/Customers/_TabContracts.cshtml
Views/Customers/_TabHistory.cshtml        timeline using the shared timeline styles
Views/Customers/_TabFeedback.cshtml
Views/Shared/_Badge.cshtml                shared badge partial (design.md 2.2)
Views/Shared/_ConfirmDialog.cshtml        shared confirmation dialog (deactivate / reactivate)
```

Plus `wwwroot/css/site.css` (filter bar, pager, timeline, sortable header, card list under 768px) and
`wwwroot/js/site.js` (filter auto-submit, dialog wiring, sorting links). No colour outside
`site.css`; the tier badge colours already exist as `g-standard`, `g-silver`, `g-gold`, `g-diamond`.

### Step 7 - language

Add every new key to **both** `Resources/SharedResource.resx` (Vietnamese, neutral) and
`Resources/SharedResource.en.resx` (English) in the same commit-sized change: page titles, filter
labels, placeholders, column headers, buttons, validation messages, empty states, tab names,
confirmation texts, toasts, and the label groups `CustomerType_*`, `CustomerGender_*`,
`CustomerTier_*`. Dates `dd/MM/yyyy`, money right-aligned through `Utils.FormatMoney`.

### Step 8 - tests

Unit (`tests/CcmWeb.Tests`): the page/page-size sanitiser, the filter -> URL round trip, the sort
link builder, the customer mapping (empty -> NULL, business clears gender/date of birth), and the new
SQL error mappings. Real database (`CustomerCareDB_Test`): the cases marked "DB" in section 3 below.

### Step 9 - test round and documents

Run T01-T26 against a freshly reloaded `CustomerCareDB_Test` and the app pointed at it, then write
`docs/tasks/m02-customers/test-results.md`, `bug-report.md` and `report.md`. The source is not edited
during the round.

## 2. Files to create / edit

```
create  src/CcmWeb/Controllers/CustomersController.cs
create  src/CcmWeb/Services/ICustomerService.cs
create  src/CcmWeb/Services/CustomerService.cs
create  src/CcmWeb/Models/ViewModels/CustomerListViewModel.cs
create  src/CcmWeb/Models/ViewModels/CustomerFormViewModel.cs
create  src/CcmWeb/Models/ViewModels/CustomerDetailViewModel.cs
create  src/CcmWeb/Views/Customers/*                       (see step 6)
create  src/CcmWeb/Views/Shared/_Badge.cshtml
create  src/CcmWeb/Views/Shared/_ConfirmDialog.cshtml
create  tests/CcmWeb.Tests/CustomerPagingTests.cs
create  tests/CcmWeb.Tests/CustomerValidationTests.cs
create  tests/CcmWeb.Tests/CustomerDatabaseTests.cs        (real test DB)
create  docs/tasks/m02-customers/test-results.md           (after the test round)
create  docs/tasks/m02-customers/bug-report.md             (after the test round)
create  docs/tasks/m02-customers/report.md                 (after the test round)

edit    src/CcmWeb/Helpers/WorkConstants.cs                values, sort fields, paging, PageSize key
edit    src/CcmWeb/Helpers/Utils.cs                        paging helpers, SQL error mapping
edit    src/CcmWeb/Helpers/Navigation.cs                   Customers built = true, child path is current
edit    src/CcmWeb/Program.cs                              only if a new service must be registered
edit    src/CcmWeb/Views/Shared/_Sidebar.cshtml            no change needed (it reads Navigation)
edit    src/CcmWeb/Views/Shared/_Topbar.cshtml             the search box posts to /customers?q=...
edit    src/CcmWeb/Views/Shared/_HeadAssets.cshtml         no change expected
edit    src/CcmWeb/wwwroot/css/site.css                    filter bar, pager, timeline, mobile cards
edit    src/CcmWeb/wwwroot/js/site.js                      filters, dialog, sort links
edit    src/CcmWeb/Resources/SharedResource.resx           new keys (Vietnamese)
edit    src/CcmWeb/Resources/SharedResource.en.resx        new keys (English)
edit    src/CcmWeb/Views/Settings/_SystemSettings.cshtml   only when D2a: the PageSize field
edit    database/02_Function_View_Proc_Trigger.sql         only when D1a
edit    database/01_CreateTables.sql                       only when D2a
edit    database/03_SampleData.sql                         only when D2a (seed)
edit    .agents/skills/ccm-database/references/schema.md   procedure signature / settings key
edit    .agents/skills/ccm-web-ui/SKILL.md                 only if a new shared component is invented
edit    docs/design.md                                     only if a decision differs from it
```

`Views/Shared/_Sidebar.cshtml` needs no change: it already renders `data-soon` from `Built` and the
title from the resource key, so flipping the flag in `Helpers/Navigation.cs` is enough.

## 3. Planned test cases

Database cases use a freshly reloaded `CustomerCareDB_Test` (copies of the four SQL files with the
database name replaced) and the app pointed at it through User Secrets. The live database is never
written into.

| Id | Case | How | Expected |
|---|---|---|---|
| T01 | Clean build | `dotnet build` + `dotnet test` | 0 errors, whole suite green |
| T02 | List matches the database | open `/customers`; run `sp_SearchCustomer` with the same defaults | same rows, same order, same count (10 active) |
| T03 | Keyword filter | `?q=` with a name part, an e-mail part and a phone part | same rows as the procedure for that keyword; no row outside it |
| T04 | Tier filter | `?tier=Gold` | only Gold customers; `?tier=` (empty) shows all |
| T05 | Owner filter and "only mine" | `?owner=2`, then the "only my customers" toggle as `mary` | rows of that owner only; the toggle uses the claim's id, and editing the hidden field does not change it |
| T06 | Include inactive | deactivate one customer, then toggle | the deactivated row appears only with the toggle on; the default list hides it |
| T07 | Paging | `?pageSize=5&page=2`, then page 3 | pages add up to the unpaged count; "showing 6-10 of 10" style text is right |
| T08 | Page beyond the end | `?page=99` | empty state, HTTP 200, no error |
| T09 | Sorting | sort by total value ascending and descending, and by created date | order matches `ORDER BY` on the same data (DB check for D1a) |
| T10 | Filters live in the URL | change every filter, copy the address into a blank tab | the same list comes back; "Clear filters" resets all of them |
| T11 | Create an individual customer | form with name + e-mail + phone + gender + date of birth | row created: `Tier = Standard`, `IsActive = 1`, `CreatedDate = today`; redirect to the list with a success toast |
| T12 | Create a business customer | type Business | Gender and Date of birth are not shown; the stored row has both NULL (`CHK_Customer_Business` satisfied) |
| T13 | At least one contact | neither e-mail nor phone | refused with the message next to the field; `SELECT COUNT(*)` unchanged |
| T14 | Phone format | phone with a letter, and a 5-digit phone | refused with the message next to the field |
| T15 | Duplicate e-mail | e-mail of an existing customer | friendly message next to the e-mail field (index `UX_Customer_Email`); no row written, no SQL text on screen |
| T16 | Date of birth | a date in the future | refused; the DB rule `DateOfBirth < CreatedDate` is also exercised directly (`547`, `CHK_Customer_DateOfBirth`) |
| T17 | Edit keeps the trigger's columns | edit the name, owner and address | `Tier`, `CreatedDate`, `IsActive` unchanged in the database |
| T18 | Deactivate | confirmation dialog on the detail page | `IsActive = 0` in the row; gone from the default list; on the detail page an Inactive badge and a Reactivate button; toast |
| T19 | Reactivate | the Reactivate button | `IsActive = 1`; back in the default list |
| T20 | `sp_SetCustomerStatus` on a missing id | xUnit: call it with `@CustomerID = 999999` | `SqlException` 50006, and the service turns it into the "customer not found" resource key |
| T21 | No hard delete | grep the module for `DELETE`/`Remove(`/`ExecuteDelete` | nothing deletes a customer; the only status change is the procedure |
| T22 | Support is read-only | `david` opens the list and a detail page, then types `/customers/create` and posts the status URL | no Add/Edit/Deactivate control; both writes get the 403 page |
| T23 | Detail header values | compare the header with SQL (`fn_CustomerAge`, `fn_CustomerContractTotal`, owner, source) | equal; a business customer shows a blank age |
| T24 | Detail tabs | compare each tab with `vw_ContractInfo`, `vw_InteractionHistory` and `FEEDBACK` for that customer | same rows; a customer without contracts/history shows the empty state |
| T25 | Escaping | create a customer named `<b>x</b> <script>alert(1)</script>` and search with `'` | shown as text, never as markup; the search does not break |
| T26 | Anti-forgery and mobile | POST the create form without a token; narrow the window under 768px | 400 without the token; the table becomes a card list and the sidebar a drawer |

## 4. Risks and open points

- **The four decisions (D1-D4) block the SQL step.** If D1b/D2b are chosen, the module ships without
  touching `database/*.sql` at all - and with that, without a risk of losing the current data.
- **Re-running the files drops `CustomerCareDB`.** Only sample data is lost, but it also resets the
  `PasswordHash` values to the demo SHA2 string (each account re-hashes on its next sign-in) and the
  language/theme rows to their seeds. Worth doing right before the test round, not in the middle of
  it.
- **Paging and sorting must live together.** If D1a pages in SQL but the app sorts the returned page
  in C#, the sort would only order the page on screen. They are one change or neither.
- **Sorting by total value** sorts by `fn_CustomerContractTotal`, a scalar function call per row with
  no index behind it. With 10 customers this is nothing; with a real table it would need a different
  shape (a computed column or `vw_CustomerRevenue`). Recorded as a known limitation rather than
  solved now.
- **`TotalRows` arrives on every row** (`COUNT(*) OVER ()`). The service reads it from the first row
  and reports 0 when the result is empty, which is exactly when the pager must not show a page count.
- **The sample data has 10 customers**, so a page size of 10 or 20 shows a single page. The paging
  cases use `?pageSize=5`, which is why the URL validation accepts 1-100 while the UI offers 10/20/50.
- **`vw_InteractionHistory` may be empty** for a customer without contacts, care or feedback, and
  `fn_CustomerAge` is NULL for every business customer - both are normal states with their own
  display, not errors.
- **`sp_SearchCustomer` keeps its existing `WHERE` block** and its `LIKE` conditions: the screen must
  not rebuild that filtering in C# (AGENTS.md 4). If the procedure is extended, the new parameters are
  optional with today's defaults, and the default call returns exactly what it returns today - so the
  change cannot surprise a later module that calls it the old way.
- **`sqlcmd` needs `-I -f 65001`** (file 01 creates a filtered index) and file 04 needs the
  `APP_PASSWORD` variable - same as the M01 round.
- **Not decided here:** whether the customer list gets an export or a print view - neither is in
  `design.md`, so neither is built.
