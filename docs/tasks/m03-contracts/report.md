# M03 - Contracts - hand-over report

## 1. What was done

The contract area is usable end to end. A Manager or a Sales person can list contracts, filter them,
open one, add a contract with several product lines, then sign, renew or cancel it - always through
the database procedures. Support can read the same pages but cannot change anything.

- `/contracts`: filters (customer name, status, staff, sign-date range, "expiring soon"), sortable
  columns, "N days left" with the red rule under 30 days, value and status badges, pager, empty state.
- `/contracts/create`: header (customer, staff, sign date, end date, status, note) plus a master-detail
  line table; the unit price is prefilled from the catalogue and stays editable, the line total and the
  contract total update while typing, a product already used is no longer offered, and the lines are
  posted as the JSON array `sp_AddContract` reads.
- `/contracts/{id}`: info card, line table with a total row, actions that depend on the status
  (Sign only for `Pending`, Renew for `Active`/`Expired`, Cancel for `Active`/`Pending`), the change
  history from `AUDIT_LOG`, and dialogs for renew (new end date) and cancel (reason).
- Roles: `[Authorize(Roles = "Manager,Sales")]` on every write action, so Support is refused on the
  server even with a crafted POST; the buttons are also hidden.
- `Helpers/ContractRules.cs` holds the rules that do not need a database call: which status allows
  which action, the JSON shape, the parser and the duplicate-product check. The controller, the views
  and the tests all read the same code instead of repeating a status check.
- No SQL file was changed and nothing was re-scaffolded: the tables, views, procedures and triggers
  already existed.

## 2. Main paths

```
src/CcmWeb/Controllers/ContractsController.cs        /contracts, /contracts/create, /details, sign/renew/cancel
src/CcmWeb/Services/IContractService.cs              contract service contract
src/CcmWeb/Services/ContractService.cs               views, procedures, audit read, dropdowns
src/CcmWeb/Helpers/ContractRules.cs                  status -> action, JSON build/parse, duplicate product
src/CcmWeb/Helpers/WorkConstants.cs                  ContractStatus values
src/CcmWeb/Helpers/Utils.cs                          SQL error codes 50001-50015 -> resource keys
src/CcmWeb/Helpers/Navigation.cs                     "Contracts" is now a built menu entry
src/CcmWeb/Models/ViewModels/ContractListViewModel.cs  filter, row, sort/page query builder
src/CcmWeb/Models/ViewModels/ContractFormViewModel.cs  header + line inputs + dropdown options
src/CcmWeb/Views/Contracts/Index.cshtml              list page shell
src/CcmWeb/Views/Contracts/_FilterBar.cshtml         filters
src/CcmWeb/Views/Contracts/_ContractTable.cshtml     table shell
src/CcmWeb/Views/Contracts/_ContractRow.cshtml       one row, days-left colour, badge
src/CcmWeb/Views/Contracts/_Pager.cshtml             pager
src/CcmWeb/Views/Contracts/_EmptyState.cshtml        empty result
src/CcmWeb/Views/Contracts/Create.cshtml             master-detail form
src/CcmWeb/Views/Contracts/Details.cshtml            info, lines, actions, change history, dialogs
src/CcmWeb/Views/Shared/_ConfirmDialog.cshtml        shared confirm dialog, optional reason (also used by M02)
src/CcmWeb/wwwroot/js/site.js                        line add/remove, totals, confirm dialog, renew dialog
src/CcmWeb/wwwroot/css/site.css                      master-detail and days-left styles
src/CcmWeb/Resources/SharedResource*.resx            contract keys in both languages
tests/CcmWeb.Tests/ContractRulesTests.cs             18 cases, no database
tests/CcmWeb.Tests/ContractDatabaseTests.cs          17 cases, real SQL Server
tests/CcmWeb.Tests/ContractFilterTests.cs            6 cases, list URL
docs/tasks/m03-contracts/                            requirements, plan, test results, bugs, this report
```

## 3. Verification actually run

- `dotnet build src\CcmWeb\CcmWeb.csproj` -> **0 errors, 0 warnings**.
- `dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj` -> **84 passed, 0 failed, 0 skipped**, of
  which 41 are this module's new cases, against the real SQL Server `CustomerCareDB_Test`.
- The application was started against `CustomerCareDB_Test` and driven through the browser and HTTP
  requests: 29 cases PASS, 7 NOT RUN, 0 BLOCKED (`test-results.md`).
- Four bugs were found and fixed during the round (`bug-report.md`): a case-sensitive JSON parser,
  a duplicated success banner, a cancel dialog without a reason field, and the "expiring soon" filter
  disappearing from sort/pager links.
- After the round the feedback convention changed to toast/alert; the new `_ActionMessage` was
  verified on both paths (success toast, failure alert) and the build and suite were run again -
  still 84/84 (`test-results.md` section 7).

Not run, and why:

| Case | Why |
|---|---|
| T30 (customer-name search, staff, sign-date range) | only the URL builder is unit-tested; the filters were not exercised in the browser |
| T31 (page size, page beyond the last one) | same: URL builder covered, screen not exercised |
| T32 (duplicate product in the form) | unit test and the procedure test cover the rule; the picker behaviour was not tried in a browser |
| T33 (server-side form errors next to the field) | keys are wired but the re-rendered form was not checked after the last change |
| T34 (POST without anti-forgery token on a contract form) | M01 verified the pipeline on the sign-in POST, not repeated here |
| T35 (XSS through note or reason) | not attempted |
| T36 (phone-width layout) | no viewport emulation in this environment; screenshots need a visible desktop window |

## 4. Decisions taken while implementing (deviations from the approved plan)

1. **`Helpers/ContractRules.cs` was added.** The plan put the JSON and status rules inside the
   service/controller; they are pure functions, so they live in one helper that the tests can call
   without a database. No behaviour change against the plan.
2. **The cancel reason is capped at 300 characters, not 500.** `requirements.md` recommended 500, but
   `sp_CancelContract.@Reason` is `NVARCHAR(300)`. The dialog and the hidden field enforce the real
   limit; `Note` still holds 500.
3. **`_ConfirmDialog` is shared with M02.** It gained an optional textarea, so cancelling a contract
   asks for a reason while deactivating a customer keeps the simple confirmation.
4. **The detail page follows the status, not the role alone.** Even a Manager sees no button when the
   status forbids the action; the server still re-checks both with the procedure.
5. **The renew suggestion is the current end date + 1 year.** It can be in the past for a contract
   that expired more than a year ago; the procedure then shows its message. Changing the suggestion
   is a small behaviour change and is listed in section 5.
6. **`contracts` menu entry is now built.** The remaining entries keep their "arrives in a later
   module" marker.
7. **The four bugs were fixed during the round, without a separate approval batch**, exactly as in
   M01: each blocked or degraded the case being run. The fixes are listed with reproduction steps and
   are waiting for the user's acceptance.
8. **Action feedback follows the new toast/alert convention** set by the user after the round
   (`AGENTS.md` 5). `_MessageBanner` was replaced by `_ActionMessage`: a success is a floating toast,
   a failure is an alert block. Contract #8 was used to check both paths; the build and the 84 tests
   were run again. T25 and T27 keep their original result, only the presentation changed.

## 5. Waiting on the user

1. **Review and accept this round**, including the four fixes made while testing (section 4.7).
2. **Renew suggestion (known issue 5.1 in `test-results.md`).** For a contract that expired more than
   a year ago, the prefilled date is in the past. Change it to "today + 1 year when the old end date
   is already past"? It is a small change, but it changes behaviour, so it needs approval.
3. **Empty error box when no product is left to add (5.2).** Cosmetic; fix it with a proper message in
   the same small batch if you agree.
4. **Start M04 (contacts, care, feedback).** Requirements and a plan will be written first, as before.

## 6. How to run it

```powershell
# 1. Rebuild the test database from the scripts (drops CustomerCareDB_Test)
dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj

# 2. Run the application against the test database
$env:ConnectionStrings__DefaultConnection="Server=.\SQLEXPRESS;Database=CustomerCareDB_Test;Integrated Security=True;TrustServerCertificate=True"
dotnet run --project src\CcmWeb --urls http://localhost:5293

# 3. Run the tests again (they rebuild CustomerCareDB_Test, so any row created in the UI is discarded)
dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj
```

Demo accounts, password `123456`: `mary` (Sales) can create/sign/renew/cancel, `david` (Support) can
only read.
