# M03 - Contracts

Status: **approved** (questions 1-5 of section 5 are settled as recorded in `plan.md` Step 0).
Implementation follows `plan.md`; the test round is documented in `test-results.md`, the defects in
`bug-report.md`.
References: `docs/design.md` sections 3.4, 5.1, 6.6, 6.7, 6.8, 2.2 and phase 4 of section 8;
`.agents/skills/ccm-workflow`; `.agents/skills/ccm-database` (`CONTRACT`, `CONTRACT_DETAIL`,
`sp_AddContract`, `sp_SignContract`, `sp_RenewContract`, `sp_CancelContract`, `vw_ContractInfo`,
`vw_ContractDetail`, `vw_ExpiringContract`, `trg_Contract_AuditLog`).

## 1. Goal

Make the most logic-heavy business area usable on the web: list the contracts, open one to read its
lines and history, and let a Manager or a Sales person add a new contract with several product lines,
then sign it, renew it or cancel it - always through the procedures of the database, never by editing
the tables directly. Support may read but never change. A contract is never deleted; cancelling is
the only way out.

The master-detail form of this module (a header plus an editable table of lines with a running total)
is the one shared piece M02 did not need; it is built here, with this screen, and reused by no other
screen in the plan.

## 2. Scope

| # | Feature | Source |
|---|---|---|
| 1 | `/contracts` list: filters customer name, status, staff, sign-date range, quick filter "expiring soon (30 days)" | design.md 6.6 |
| 2 | List columns: contract no., customer, assigned staff, sign date, end date (with "N days left", red when under 30), value, status badge | design.md 6.6 |
| 3 | `/contracts/create`: header (customer*, staff*, sign date, end date*, status Active/Pending, note) + detail-line table (product, quantity, unit price prefilled and editable, line total), running total, no duplicate product | design.md 6.7 |
| 4 | Save sends the lines as JSON and calls `sp_AddContract`, then redirects to the new contract's detail | design.md 6.7 |
| 5 | Display of the contract errors: end date before sign date, no lines, duplicate product, unknown product, bad quantity, bad date (50001, 50008, 50009, 50010, 50011, 50015) | design.md 6.7 |
| 6 | `/contracts/{id}`: info card (customer link, staff, sign/end date, days remaining, status, note), detail table with a total row, action buttons by status | design.md 6.8 |
| 7 | Sign / Renew / Cancel through `sp_SignContract`, `sp_RenewContract`, `sp_CancelContract`, with confirmation dialogs (renew asks for the new end date, cancel asks for a reason) | design.md 6.8 |
| 8 | "Change history" block from `AUDIT_LOG` (`TableName = 'CONTRACT'`) | design.md 6.8 |
| 9 | Permissions: Manager and Sales may view, create, sign, renew and cancel; Support may only view (no buttons **and** blocked on the server) | design.md 5.1 |
| 10 | Badges: contract status (Active / Pending signature / Expired / Cancelled) and "days left" colour rule | design.md 2.2 |
| 11 | Every visible string from a resource key in both `.resx` files; coded values through `L.Label(...)` | `ccm-language` |
| 12 | The sidebar marks "Contracts" as a built page and keeps the item highlighted on a detail page | M01/M02 carry-over |
| 13 | Unit tests for the JSON line building and the status rules; real-database tests for the four procedures and the triggers | `ccm-workflow` |

### Explicitly NOT in M03

- Recording contacts, care records and appointments (M04).
- Reading and handling a feedback item (M04).
- The real dashboard and the report pages (M05).
- Employees, accounts, audit log administration and the profile page (M06).
- Any hard delete of a contract: cancelling is the only way out, and a cancelled contract stays in the list.
- Editing an already signed contract's lines or value: the trigger owns `TotalValue` and the procedures own the status (AGENTS.md 4).
- Refunds, online payments, multi-branch (AGENTS.md 4).

## 3. Data used by this module

| Use | DB object | Note |
|---|---|---|
| List | `vw_ContractInfo` | Filtered in C# by customer name, status, staff, sign-date range; "expiring soon" reads `vw_ExpiringContract` |
| Detail header | `vw_ContractInfo` (one row) | |
| Detail lines | `vw_ContractDetail` | Ordered by product; total row summed in the view |
| Add | `sp_AddContract` | `@CustomerID, @EmployeeID, @EndDate, @DetailJson, @SignDate, @Note, @Status, @ContractID OUTPUT` |
| Sign | `sp_SignContract` | `@ContractID, @SignDate` |
| Renew | `sp_RenewContract` | `@ContractID, @NewEndDate` |
| Cancel | `sp_CancelContract` | `@ContractID, @Reason` |
| Customer link on the detail page | `CUSTOMER` (via `vw_ContractInfo.CustomerID`) | Reuses the customer name; no new query |
| Products for the line picker | `PRODUCT_SERVICE` | Ordered by name; a deleted/hidden product is simply not offered |
| Change history | `AUDIT_LOG` | `TableName = 'CONTRACT'`, ordered by `LogTime DESC` |
| Customer tier refresh | `trg_Contract_UpdateTier` | Fires on insert; the application never writes `Tier` |
| `TotalValue` | `trg_ContractDetail_UpdateTotal` | Owned by the trigger; never written by the application |

## 4. Error codes this module can hit

| Code | Meaning | Shown to the user |
|---|---|---|
| 50001 | Contract has no detail lines | "A contract needs at least one product line." |
| 50006 | Invalid customer (missing or inactive) | "This customer does not exist or is no longer active." |
| 50007 | Invalid employee | "The signing employee does not exist." |
| 50008 | Invalid contract detail JSON | generic database rule |
| 50009 | Duplicate product in the details | "The same product was added twice." |
| 50010 | Product does not exist | "One of the chosen products no longer exists." |
| 50011 | Invalid quantity | "The quantity must be a positive whole number." |
| 50013 | Contract not found | "This contract does not exist." |
| 50014 | Contract status forbids the action | "The contract's status does not allow this action." |
| 50015 | Invalid date | "The end date must not be before the sign date." |
| 547 / 2601 / 2627 | CHECK / FK / unique violations | the matching message or the generic database rule |

## 5. Open questions for the user

1. **Renewal end date**: the dialog asks for the new end date. Should the new date be allowed to be earlier than today (to correct a mistake), or only later than the current end date? Recommendation: later than the current end date, because `sp_RenewContract` already rejects an earlier one.
2. **Cancel reason**: free text, optional, appended to `Note`. Maximum length? Recommendation: 500 characters, matching `Note`.
3. **"Expiring soon" threshold**: 30 days, as `vw_ExpiringContract` defines it. No change needed unless you want a different number.
4. **Contract number display**: `ContractID` is an identity; show it as `#123`. No separate number column exists.
5. **Audit log on the detail page**: read-only list, newest first, paginated? Recommendation: show the last 20 entries with a "show all" link is out of scope; show all (the volume is small) in M03, and revisit if it gets noisy.

## 6. Acceptance

- A Manager or Sales person can create a contract with several lines, see the total update as lines are added, and be redirected to its detail page.
- The detail page shows the lines and the total, and offers Sign / Renew / Cancel only when the status allows it.
- A cancelled or expired contract offers no action buttons.
- Support can open a contract but sees no action buttons, and a crafted POST is refused on the server.
- The list filters and the "expiring soon" quick filter work and stay in the URL.
- The customer tier on the customer page moves after a contract is added (the trigger does it; the app does not).
- No screen writes `TotalValue`, `Tier` or `AUDIT_LOG`.
