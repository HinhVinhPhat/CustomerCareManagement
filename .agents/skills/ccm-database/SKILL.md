---
name: ccm-database
description: Single source of truth for the CustomerCareDB database (SQL Server, version 2) of the customer care management system - tables, columns, constraints, views, stored procedures, triggers, error codes 50001-50015, the AUDIT_LOG, and how to run the 4 SQL files. Read this skill before writing or changing any SQL, repository, Service, controller or screen that touches data - customers, contracts, contacts, care, feedback, employees, accounts, dashboard, reports. Read it even when you think you already know the schema, because the DB ships with business logic that outside code must not reimplement.
---

# Database CustomerCareDB (version 2)

The database already holds the business logic: a trigger computes contract value and ranks
customers, procedures validate status, views feed the dashboard. The web app (the Services
layer) only **calls into** it and never reimplements it. Writing it twice would drift, and
this project is graded on the database being genuinely used.

Per-table, per-view, per-procedure and per-trigger detail lives in `references/schema.md`.
Read it before writing a query. Below are the easiest mistakes.

## Important rules

1. **Never write `CONTRACT.TotalValue`.** The trigger `trg_ContractDetail_UpdateTotal` computes it from `CONTRACT_DETAIL`.
2. **Never write `CUSTOMER.Tier`.** The trigger `trg_Contract_UpdateTier` ranks it (>=100m Diamond, >=50m Gold, >=20m Silver, else Standard; only "Active" and "Expired" contracts count). Customer forms and Services must not accept this field.
3. **Change a contract only through a procedure:** add with `sp_AddContract`, sign with `sp_SignContract`, renew with `sp_RenewContract`, cancel with `sp_CancelContract`. Never `UPDATE CONTRACT SET Status` directly, because the procedures check whether the current status allows the transition.
4. **Never hard-delete a customer.** Use `sp_SetCustomerStatus` (soft delete, column `IsActive`). `DELETE` is blocked by a trigger while contracts remain (error 50005).
5. **Feedback:** assign with `sp_AssignFeedback`, change status with `sp_UpdateFeedbackStatus`. `ResolvedDate` is filled by a trigger; do not send it.
6. **Dashboard and lists read from views** (`vw_*`) or `sp_DashboardSummary`. To sort, add `ORDER BY` to the calling SELECT (a view is not sorted on its own).
7. **Audit log:** the triggers that write `AUDIT_LOG` read the actor from `SESSION_CONTEXT('EmployeeID')`. Every pooled connection must run `EXEC sp_set_session_context N'EmployeeID', <employeeID>` **before** any other statement, otherwise the log records `EmployeeID = NULL`. Concrete recipe in the `ccm-mvc-architecture` skill.
8. **`sp_ExpireContracts` does not run by itself.** SQL Server Express and the container have no SQL Agent. The web app must call it periodically from a `BackgroundService` (at start-up and once a day), otherwise an overdue contract still shows as "Active".

## Two error shapes for one rule

The "feedback must have an assignee" rule exists in two places and yields two different codes:

- Calling `sp_UpdateFeedbackStatus` before assigning: error **50004** (a clear English message).
- `INSERT` or `UPDATE` straight into `FEEDBACK` violating `CHK_Feedback_Assignee`: error **547** (a generic CHECK message).

So the Services layer should use the procedure to change status, and for error 547 it should
read the constraint name from the message (for example `CHK_Feedback_Assignee`,
`CHK_Customer_Business`, `CHK_Care_SatisfactionScore`) to return the right message instead of a
vague one.

## Valid values (CHECK constraints)

Use the exact spelling:

- `Gender`: Male | Female | Other
- `EMPLOYEE.Position`: Manager | Staff (each department has at most 1 manager)
- `CUSTOMER.CustomerType`: Individual | Business (a business must have `Gender` and `DateOfBirth` NULL)
- `Tier`: Standard | Silver | Gold | Diamond
- `PRODUCT_SERVICE.ProductType`: Product | Service
- `CONTRACT.Status`: Active | Expired | Cancelled | Pending
- `CONTACT.Channel`: Phone | Email | Zalo | InPerson | Other
- `CARE.CareType`: Renewal | Upsell | Birthday | Survey | RenewalReminder | Other. `SatisfactionScore` (1-5) only when CareType = Survey. `NextAppointmentDate >= CareDate`.
- `FEEDBACK.FeedbackType`: Complaint | Suggestion | GeneralFeedback | Compliment. `Priority`: 1 High, 2 Medium, 3 Low. `FeedbackStatus`: New | InProgress | Resolved | Closed.
- `ACCOUNT.Role`: Manager | Sales | Support
- `SETTINGS.SettingKey`: Language | Theme. `SettingValue` must match the key (`Language`: vi | en;
  `Theme`: light | dark). `EmployeeID IS NULL` is the system default and `UQ_Settings_Scope` allows
  one row per (key, scope). Read them through `vw_SystemSettings` / `vw_EffectiveSettings` - the
  "a personal row beats the system default" rule lives in the view, never in C#.
- A customer must have at least an email or a phone number; a phone number is digits only, 9-11 long; an email must look like `x@y.z`.

Keep these lists in **one** place in code (a constant or enum shared by validation and
dropdowns) instead of scattering string literals.

## Running the database

Four files in `database/`, run in order. File `01` **drops and recreates** the database, so old
data is lost.

| Order | File | Purpose |
|---|---|---|
| 1 | `01_CreateTables.sql` | Create the DB, tables, indexes |
| 2 | `02_Function_View_Proc_Trigger.sql` | Functions, views, procedures, triggers, roles (runs before the data so the triggers compute) |
| 3 | `03_SampleData.sql` | Sample data, dates relative to today |
| 4 | `04_CreateAppUser.sql` | The `app_ccm` login for the web app (`APP_PASSWORD` variable) |

With `sqlcmd` these flags are mandatory; without them it fails:

```bash
sqlcmd -S localhost,1433 -U sa -P "$SA_PASSWORD" -C -I -f 65001 -i database/01_CreateTables.sql
```

`-I` enables QUOTED_IDENTIFIER (needed by the filtered index), `-f 65001` reads UTF-8 correctly,
`-C` trusts the certificate in dev. File 04 adds `-v APP_PASSWORD="..."` (a strong password
without a single quote).

## Sample data

8 employees, 6 accounts (password `123456`): `john` and `susan` (Manager), `mary` and `kevin`
(Sales), `david` and `helen` (Support). The hashes in the DB are SHA2-256 for demo; see the
`ccm-auth-security` skill for how to verify them and move to `PasswordHasher`.

Business rows (customer names, addresses, product names, notes, feedback text) are **Vietnamese**
with the accents in place; coded values, usernames, e-mails, phone numbers and the stored values of
every status/type stay English (`ccm-language`). `SETTINGS` is seeded with two system rows,
`Language = vi` and `Theme = light`, and no personal row.

## When the schema must change

Edit the files in `database/`, run all four again, then update `references/schema.md` and
`docs/design.md` if they are affected. Never change the schema from application code or an EF
Core migration (this project is database-first).
