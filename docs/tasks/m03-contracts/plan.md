# M03 - Contracts - plan

Companion to `requirements.md`. One approval covers both documents. Steps run in order; anything out
of scope or any business change stops the work and asks the user first.

## 1. Steps

### Step 0 - decisions (settled with the user before coding)

Questions 1-5 of `requirements.md` section 5 are settled: renewal only later than the current end
date, cancel reason free text up to 500 characters, "expiring soon" stays at 30 days, contract
number is `#ContractID`, the audit log shows all entries for the contract (newest first). None of
these needs a SQL change, so Step 1 is empty.

### Step 1 - SQL

No SQL change. `CONTRACT`, `CONTRACT_DETAIL`, `vw_ContractInfo`, `vw_ContractDetail`,
`vw_ExpiringContract`, `sp_AddContract`, `sp_SignContract`, `sp_RenewContract`,
`sp_CancelContract` and the three triggers already exist and are correct. Re-scaffold is not needed:
the contract tables and views are not scaffolded (they are read through `SqlQueryRaw` / the existing
`ApplicationDbContext` entities). If a later step needs a column that is not there, stop and ask.

### Step 2 - shared values and helpers

1. `Helpers/WorkConstants.cs`: `ContractStatus` (`Pending`, `Active`, `Expired`, `Cancelled`),
   `ContractSortFields` (`SignDate`, `EndDate`, `TotalValue`, `CustomerName` - matching what the
   list sorts by), plus `ContractStatuses` labels already in the resource files.
2. `Helpers/Utils.cs`: SQL error mapping for what this screen can hit: 50001, 50006, 50007, 50008,
   50009, 50010, 50011, 50013, 50014, 50015, and 547/2601/2627 with their constraint names.
3. `Helpers/Badges.cs`: `ForContractStatus` already exists (Pending -> info, Active -> success,
   Expired -> neutral, Cancelled -> danger); no change needed.
4. `Helpers/Navigation.cs`: the "contracts" entry flips `Built` to `true`.

### Step 3 - services

1. `Services/IContractService.cs` + `ContractService.cs`:
   - `SearchAsync(ContractFilter filter)` -> list rows from `vw_ContractInfo` (in-memory filter by
     customer name, status, staff, sign-date range) plus the "expiring soon" quick filter reading
     `vw_ExpiringContract`.
   - `GetDetailAsync(int contractId)` -> header from `vw_ContractInfo`, lines from
     `vw_ContractDetail`, audit entries from `AUDIT_LOG` where `TableName = 'CONTRACT'`.
   - `CreateAsync(ContractFormViewModel form)` -> builds the JSON array of lines, calls
     `sp_AddContract` with `SqlQueryRaw` (OUTPUT `ContractID`), returns the new id.
   - `SignAsync(int contractId, DateOnly? signDate)`, `RenewAsync(int contractId, DateOnly newEndDate)`,
     `CancelAsync(int contractId, string? reason)` -> call the matching procedure.
   - `GetCustomersAsync()` / `GetProductsAsync()` / `GetStaffAsync()` for the dropdowns.
2. `Models/ViewModels/ContractListViewModel.cs`, `ContractFilter.cs`, `ContractFormViewModel.cs`,
   `ContractDetailViewModel.cs`, `ContractListRow.cs`, `ContractLineRow.cs`, `ContractAuditRow.cs`.
3. `Models/EF` is not changed: no scaffold, no migration.

### Step 4 - controllers

1. `Controllers/ContractsController.cs`: `/contracts` (GET), `/contracts/create` (GET/POST),
   `/contracts/{id}` (GET), `/contracts/{id}/sign` (POST), `/contracts/{id}/renew` (POST),
   `/contracts/{id}/cancel` (POST), with `[Authorize]` and role checks (Manager, Sales edit;
   Support view only).

### Step 5 - views

1. `Views/Contracts/Index.cshtml` + partials: filter bar, table, pager (reuse the M02 building
   blocks), empty state.
2. `Views/Contracts/Create.cshtml` + partials: header form, detail-line table with "+ add line",
   running total, client-side preview of the customer tier. The lines are serialised to JSON in a
   hidden field on submit.
3. `Views/Contracts/Details.cshtml` + partials: info card, detail table with total row, action
   buttons by status, renew/cancel dialogs, audit log list.
4. `Views/Shared/_ConfirmDialog.cshtml` is reused for cancel; a small date-picker dialog is added for
   renew.

### Step 6 - styling and script

1. `wwwroot/css/site.css`: master-detail table styles, "days left" colour rule (red under 30 days),
   status badge reuse.
2. `wwwroot/js/site.js`: add/remove a detail line, recompute the line total and the contract total,
   prevent duplicate products, serialise the lines to JSON before submit.

### Step 7 - language

Add the contract keys to both `.resx` files: list, form, detail, status, action dialogs, errors.

### Step 8 - tests

1. Unit tests: JSON line building, duplicate-product rejection, status -> allowed actions.
2. Real-database tests: `sp_AddContract` (happy path, no lines, duplicate product, bad quantity,
   bad date, inactive customer), `sp_SignContract`, `sp_RenewContract`, `sp_CancelContract`,
   `trg_Contract_UpdateTier` (tier moves after a contract), `trg_Contract_AuditLog` (one entry per
   change).

### Step 9 - verification

Build, run the app, exercise create/sign/renew/cancel as Manager and Sales, view as Support, check
the audit log, restore the test database state.

## 2. Risks

- **JSON shape**: `sp_AddContract` expects `[{"ProductID":..,"Quantity":..,"UnitPrice":..}]`. The
  web app must produce exactly that shape; a wrong property name fails with 50008.
- **Nested triggers**: `trg_Contract_UpdateTier` needs nested triggers ON (the SQL Server default).
  If a DBA has turned them off, the tier will not move and the test must catch it.
- **DateOnly vs DateTime**: `SignDate`/`EndDate` are DATE columns -> `DateOnly` in C#. Pass them
  as parameters, never as strings.
- **TotalValue is trigger-owned**: the application sends nothing for it and must not read a stale
  value; re-read the contract after save.
- **AUDIT_LOG actor**: the `DbConnectionInterceptor` must set `SESSION_CONTEXT` 'EmployeeID',
  otherwise the audit entry has a NULL actor. Verify in the test.
- **EF trigger trap**: `CONTRACT` and `CONTRACT_DETAIL` have triggers, so `SaveChanges` against
  them would fail with error 334. This module does not use EF to write contracts (it uses the
  procedures), so the trap does not apply here - but any future EF write to those tables must
  configure `UseSqlOutputClause(false)`.

## 3. Out-of-scope guard

Anything not in section 1 (contacts, care, feedback, dashboard, reports, admin, a new library, a
generic repository) stops the work and asks the user first. Small, obvious tasks (typo, colour
change, one-line fix) do not need the whole process.
