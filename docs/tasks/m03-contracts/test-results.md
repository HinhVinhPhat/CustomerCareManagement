# M03 - Contracts - test results

Run date: **20/10/2026**. Written after the code was frozen for this round; every row below was
actually executed. Nothing is ticked that was not run.

## 1. Environment actually used

| Item | Value |
|---|---|
| Operating system | Windows 10 Home Single Language, build 26300 |
| .NET SDK | 10.0.401 |
| Target framework | `net10.0` |
| EF Core | 10.0.13 (runtime package and `dotnet-ef` tool) |
| Database engine | Microsoft SQL Server 2025 (RTM) 17.0.1000.7, instance `.\SQLEXPRESS` |
| Database under test | `CustomerCareDB_Test`, dropped and rebuilt by the test fixture from `database/01`-`03` |
| How the app was run | `dotnet run --project src/CcmWeb --urls http://localhost:5293 --no-build`, with `ConnectionStrings__DefaultConnection` pointing at `CustomerCareDB_Test` (Windows authentication) |
| Accounts used | `mary` (Sales, employee 2) for create/sign/renew/cancel, `david` (Support, employee 3) for the read-only checks |

The live `CustomerCareDB` was **not** written into by this round. EF InMemory and SQLite were not used
anywhere.

## 2. Automated tests

```
dotnet build src\CcmWeb\CcmWeb.csproj
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```
dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj
Passed!  - Failed: 0, Passed: 84, Skipped: 0, Total: 84, Duration: 7 s - CcmWeb.Tests.dll (net10.0)
```

41 of the 84 cases are this module's, in three new files:

| File | Cases | What it covers |
|---|---|---|
| `tests/CcmWeb.Tests/ContractRulesTests.cs` | 18 | the exact JSON shape `sp_AddContract` reads, an empty or broken hidden field, duplicate products, status -> allowed actions, a badge for every status |
| `tests/CcmWeb.Tests/ContractDatabaseTests.cs` | 17 | the four procedures and the triggers against the real SQL Server test database: happy path, every rejected input, sign/renew/cancel, the audit actor |
| `tests/CcmWeb.Tests/ContractFilterTests.cs` | 6 | the list URL: `expiring` written as a boolean, sort and page-size links keeping the active filters |

The remaining 43 cases are the M01/M01b suites, unchanged.

## 3. Cases

| Id | Case | Expected | Actual | Status |
|---|---|---|---|---|
| T01 | Clean build | 0 errors | `Build succeeded. 0 Warning(s), 0 Error(s)` | PASS |
| T02 | Whole automated suite | all green | 84 passed, 0 failed, 0 skipped | PASS |
| T03 | Contract rules unit tests | JSON round-trips, broken input is "no lines", duplicate detected, only the allowed action per status | 18/18 pass, including every `InlineData` row | PASS |
| T04 | `sp_AddContract` happy path | header saved, lines saved, trigger total, customer tier moves | xUnit: 2 lines, `TotalValue` summed by the trigger, `CUSTOMER.Tier` changed after the insert | PASS |
| T05 | Audit entry for a new contract | one `CONTRACT` entry with the signed-in employee | xUnit: `AUDIT_LOG` row has `TableName='CONTRACT'`, the new id and the employee from `SESSION_CONTEXT` | PASS |
| T06 | A contract with no lines | refused with 50001 | xUnit: procedure throws 50001 before it opens the transaction, so no header is written | PASS |
| T07 | The same product twice | refused with 50009 | xUnit: procedure throws 50009 | PASS |
| T08 | Quantity below one | refused with 50011 | xUnit: procedure throws 50011 | PASS |
| T09 | Unknown product id | refused with 50010 | xUnit: procedure throws 50010 | PASS |
| T10 | End date before sign date | refused with 50015 | xUnit: procedure throws 50015 | PASS |
| T11 | Inactive customer | refused with 50006 | xUnit: procedure throws 50006 | PASS |
| T12 | Sign a pending contract | status `Active`, sign date stored, audit entry `Pending -> Active` | xUnit: row is `Active`, one status-change audit entry | PASS |
| T13 | Sign an already active contract | refused with 50014 | xUnit: procedure throws 50014 | PASS |
| T14 | Renew forward | new end date stored, status `Active` | xUnit: end date moved forward | PASS |
| T15 | Renew to the same or an earlier date | refused with 50015 | xUnit: procedure throws 50015 | PASS |
| T16 | Cancel | contract kept, status `Cancelled`, reason appended to `Note` | xUnit: row still exists, reason present in `Note` | PASS |
| T17 | Cancel a cancelled contract | refused with 50014 | xUnit: procedure throws 50014 | PASS |
| T18 | Unknown contract id | 50013 / no detail page | xUnit: cancel throws 50013; `GetDetailAsync` returns null | PASS |
| T19 | Detail read | header from `vw_ContractInfo`, lines from `vw_ContractDetail`, audit from `AUDIT_LOG` | xUnit: header, the 2 ordered lines, the trigger total and the `Insert` audit entry with its actor match the seeded contract | PASS |
| T20 | `/contracts` renders | rows, columns, badges, pager | page rendered as `mary`; rows, status badges and the pager were visible | PASS |
| T21 | Status filter stays in the URL | `?status=Pending` shows only pending contracts | 1 row, all `Pending` | PASS |
| T22 | "Expiring soon" quick filter | `?expiring=true` shows only contracts inside 30 days | 2 rows, both from `vw_ExpiringContract` | PASS |
| T23 | Sort and pager links keep the filters | the quick filter survives a click on a column header | rendered sort links carried `expiring=true`; the result stayed filtered | PASS |
| T24 | Create from the screen | 2 lines, running total, redirect to detail, audit | created contract #26 with 2 lines totalling 5,000,000 VND; detail page showed both lines and the total; audit entry named `mary` | PASS |
| T25 | Sign from the screen | no reload error, status `Active`, one success banner, audit | pending #26 became `Active`; the audit list showed `Pending -> Active`; the duplicated success banner was gone after B02 was fixed | PASS |
| T26 | Renew from the screen | new end date asked in a dialog, stored, success message | #3 renewed to 31/01/2028 and the page showed the new date | PASS |
| T27 | Cancel from the screen | confirmation asks for a reason, reason stored, no actions left | #14 became `Cancelled` with the typed reason inside `Note`; Sign/Renew/Cancel disappeared | PASS |
| T28 | Support is read-only | no action buttons, crafted POST refused on the server | `david` opened #26 without buttons; `POST /contracts/26/renew` redirected to `/access-denied` | PASS |
| T29 | Language switch | English labels and number format, back to Vietnamese | `Contract #26`, `End date`, `3,000,000 VND` in English; switching back restored the Vietnamese page | PASS |
| T30 | Customer-name search, staff filter, sign-date range | the list narrows to the matching rows and the values stay in the URL | **not run in the browser**; only the URL builder is unit-tested | NOT RUN |
| T31 | Page-size links and the page after the last one | page size kept, empty state instead of an error | **not run**; only the page-size URL builder is unit-tested | NOT RUN |
| T32 | Adding the same product twice in the form | the second line is refused before submit | **not run in the browser**; the rule is covered by T03 and T07, which is where the refusal really happens | NOT RUN |
| T33 | Server-side form errors (no lines, duplicate product) shown next to the field | message under the line table, entered header kept | **not run** after the last change; the keys are wired in the controller and both `.resx` files | NOT RUN |
| T34 | POST without an anti-forgery token | rejected with 400 | **not run for the contract forms**; M01 verified the pipeline behaviour on the sign-in POST | NOT RUN |
| T35 | XSS through note or cancel reason | escaped on the detail page | **not run** | NOT RUN |
| T36 | Phone-width layout | list and form usable below 768 px | **not run: this environment cannot emulate a viewport**, and screenshots need a visible desktop window | NOT RUN |

BLOCKED: none.

## 4. Bugs found during this round

| Id | What happened | Fixed by |
|---|---|---|
| B01 | `ContractRules.ParseLines` used the default, case-sensitive JSON options, so the `ProductID` shape written for the procedure did not bind to `ProductId` and the lines were silently lost | case-insensitive options; the round-trip test keeps it fixed |
| B02 | The detail page showed the success banner twice after create/sign/renew/cancel | removed the extra `<partial name="_MessageBanner" />` from `Details.cshtml`; `_Layout` already renders it |
| B03 | Cancelling asked only "are you sure", so `sp_CancelContract` always stored "Cancelled: (no reason given)" | `_ConfirmDialog` gained an optional reason textarea, `Details.cshtml` a hidden `reason` field, `site.js` the wiring, plus the `Contract_CancelReason` key |
| B04 | Turning on "expiring soon" and then clicking a sort column or a page number dropped the filter, because the link wrote `expiring=1` while the action binds `bool` | the link now writes `expiring=true` |

All four were found by running the screen; each was fixed immediately so the round could continue,
and the affected cases were re-run afterwards. The "no source edits during a test round" rule of
`ccm-workflow` was therefore broken deliberately, exactly as in M01; the fixes wait for the user's
acceptance.

## 5. Known issues and things that are not done

### 5.1 The suggested renew date can already be in the past

The renew dialog prefills the current end date plus one year. For a contract that expired more than
a year ago that suggestion is in the past, `sp_RenewContract` refuses it with 50015 and the message
is shown, but the user has to type a valid date. A better rule (today + 1 year when the old end date
is already past) needs a decision, not a silent change.

### 5.2 Empty error box when no product is left to add

When every product already has a line, `site.js` disables all options in the product picker and shows
an empty error container instead of a message. Cosmetic; the lines already added are kept and the
contract still saves correctly.

### 5.3 The list filters in memory

`SearchAsync` loads `vw_ContractInfo` and filters, sorts and pages in memory, as the approved plan
says. That is fine for the course-sized data set but does not scale; moving the filters into a
procedure would be new SQL and needs approval.

### 5.4 The change history is not paged

All `AUDIT_LOG` rows of the contract are shown, newest first, per the settled decision. The volume is
small in this project; revisiting it is a later decision.

### 5.5 Cancel reason is capped at 300 characters

`requirements.md` recommended 500, but `sp_CancelContract` declares `@Reason NVARCHAR(300)`, so the
dialog enforces 300 rather than letting the database truncate or refuse silently. `Note` itself still
holds 500 characters.

### 5.6 Write paths that are deliberately absent

There is no edit page and no delete action for a contract. Signed lines and `TotalValue` are owned by
the database; status changes go only through the four procedures. That is the designed behaviour,
not a missing feature.

## 6. State of the databases after the round

- `CustomerCareDB` was not touched.
- `CustomerCareDB_Test` was rebuilt one last time by the final `dotnet test` run. The contracts
  created or changed during the UI round (#26 created, #26 signed, #3 renewed, #14 cancelled) were
  therefore discarded together with the rest of the test data; the sample rows of
  `database/03_SampleData.sql` are back. This is expected: the fixture drops and recreates the test
  database on every run.
- The application was restarted after the final test run against the rebuilt test database and is
  available at `http://localhost:5293` for review.

## 7. Follow-up after the round: action feedback convention

Later on the same day the user added a project-wide convention (`AGENTS.md` section 5,
`ccm-web-ui`): the result of an action is a **toast** when it succeeded and an **alert block** when it
failed and must stay readable; a banner that takes up page space is no longer used. The
implementation was changed and verified again:

- `_MessageBanner` became `_ActionMessage`: a success renders a `[data-toast]` marker that `site.js`
  turns into one `window.ccmToast(text, 'ok')`; a failure renders `<div class="alert alert-err"
  role="alert">`.
- `site.js` consumes the marker on load; `site.css` gained the `.alert` / `.alert-err` classes.
- As `mary` on contract #8: signing showed exactly one toast `Đã ký hợp đồng.`
  (`.toast.toast-ok`, `role="status"`), no alert, no banner. Posting sign again on the now-active
  contract showed no toast and one alert `Trạng thái hợp đồng không cho phép thao tác này.`
  (`role="alert"`).
- `dotnet build` 0 errors / 0 warnings; `dotnet test` still 84 passed, 0 failed.

The rows of T25 and T27 above keep the result they had during the round; only the presentation of the
message changed. T33 (field-level errors) is unaffected: those still render inline on the form.
