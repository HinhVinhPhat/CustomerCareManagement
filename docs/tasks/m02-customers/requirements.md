# M02 - Customers

Status: **approved on 18/10/2026** (decisions D1a, D2a, D3a confirmed by the user; D4 accepted as
their consequence - any SQL change means re-running the four files, and file 01 drops and recreates
`CustomerCareDB`). Implementation follows `plan.md`; the test round is documented in
`test-results.md`.
References: `docs/design.md` sections 3.4, 6.3, 6.4, 6.5, 5.1, 2.2 and phase 3 of section 8;
`.agents/skills/ccm-workflow`; `.agents/skills/ccm-database` (`CUSTOMER`, `sp_SearchCustomer`,
`sp_SetCustomerStatus`, `vw_ContractInfo`, `vw_InteractionHistory`, `fn_CustomerAge`,
`fn_CustomerContractTotal`).

## 1. Goal

Turn the first real business area into screens: find a customer, add one, correct one, put one out of
care and put one back - and open a customer to see everything the database already knows about them
(their contracts, their interaction history, their feedback).

This module also builds the **shared list building blocks** that every later list page reuses
(design.md 3.4): filter bar, data table with sorting and paging, badge, empty state, confirmation
dialog. They are built here, once, with the first screen that needs them - M01 deliberately did not
create them (M01 section 2 "Explicitly NOT in M01").

At the end of M02 a Manager or a Sales person can do all of the above, a Support person can look but
not touch, and no screen can hard-delete a customer.

## 2. Scope

| # | Feature | Source |
|---|---|---|
| 1 | Shared building blocks: filter bar, data table (sort + paging 10/20/50), badge, empty state, confirmation dialog, form field with its error slot | design.md 3.4 |
| 2 | `/customers` list: filters `q` (name/email/phone), tier, owner, "include inactive", "only my customers" - all kept in the URL | design.md 6.3 |
| 3 | List columns: customer (name + individual/business icon), contact (email over phone), tier badge, owner, total value, created date, actions | design.md 6.3 |
| 4 | Sorting by name (default), created date, total value, ascending or descending, kept in the URL | design.md 3.4 |
| 5 | Paging with "showing X-Y of Z", previous/next and page numbers | design.md 3.4 |
| 6 | `/customers/create` and `/customers/{id}/edit`: type first (Individual / Business), business hides Gender and Date of birth, no tier field | design.md 6.4 |
| 7 | Web validation: name required; at least an email or a phone; phone digits only, 9-11; e-mail well formed and not already used; date of birth before today | design.md 6.4 |
| 8 | `/customers/{id}`: summary header (name, tier, type, active state, contact, address, age for an individual, source, owner, total contract value) + Deactivate / Reactivate with a confirmation | design.md 6.5 |
| 9 | Detail tabs, **read-only in M02**: Overview, Contracts (`vw_ContractInfo`), Interaction history (`vw_InteractionHistory`), Feedback | design.md 6.5 |
| 10 | Permissions: Manager and Sales may view and edit; Support may only view (no Add/Edit/Deactivate button **and** blocked on the server) | design.md 5.1 |
| 11 | Every visible string from a resource key in both `.resx` files; coded values (type, gender, tier) through `L.Label(...)` | `ccm-language` |
| 12 | Unit tests for the paging and filter logic; real-database tests for deactivate and the constraints | `ccm-workflow` |
| 13 | The topbar quick search box becomes real: it becomes a GET form to `/customers?q=...` (until now it shows the "coming soon" note) | design.md 3.2, M01 scope note |
| 14 | The sidebar marks "Customers" as a built page (no longer "coming soon"), and the Customers item stays highlighted while a customer detail or edit page is open | M01 carry-over |

### Explicitly NOT in M02

- Creating or editing contracts, and the "+ Add contract" button on the detail page (M03).
- Recording contacts, care records and appointments, and the "Add contact" / "Add care" buttons (M04).
- Reading and handling a feedback item, including the Feedback tab's action buttons (M04).
- The real dashboard and the report pages (M05).
- Employees, accounts, audit log and the profile page (M06).
- Any hard delete of a customer: the only way out is `sp_SetCustomerStatus` (AGENTS.md 4,
  design.md 9).
- A `Tier` input anywhere: the trigger owns that column (AGENTS.md 4).
- A 404 page and the final responsive polish (M07) - only the pieces the customer screens need.

## 3. Data used by this module

| Use | DB object | Note |
|---|---|---|
| Customer list and its filters | `sp_SearchCustomer(@Keyword, @Tier, @OwnerEmployeeID, @ActiveOnly)` | Returns `TotalValue` through `fn_CustomerContractTotal`, and `IsActive` |
| Total value column | `dbo.fn_CustomerContractTotal` | Called by the procedure; **not** `vw_CustomerRevenue`, which has no `IsActive` filter |
| Deactivate / reactivate | `sp_SetCustomerStatus(@CustomerID, @IsActive)` | Throws 50006 when the customer does not exist |
| Age in the detail header | `dbo.fn_CustomerAge(@CustomerID)` | NULL when there is no date of birth (business customers) |
| Detail tab "Contracts" | `vw_ContractInfo` filtered by `CustomerID` | |
| Detail tab "Interaction history" | `vw_InteractionHistory` filtered by `CustomerID` | Contacts + care + feedback, newest first |
| Detail tab "Feedback" | `FEEDBACK` filtered by `CustomerID` | Read-only until M04 |
| Create / edit a customer | `INSERT` / `UPDATE` on `CUSTOMER` through EF Core | `UseSqlOutputClause(false)` is already configured for `Customer` (M01); `Tier`, `CreatedDate` and `IsActive` are never written by the form |
| Owner dropdown and owner filter | `EMPLOYEE` | 8 sample employees |
| Page size default (only if D2 is approved) | `SETTINGS` key `PageSize` | New key, needs the CHECK constraint extended |

**No SQL file changes are needed unless D1 or D2 is approved.** Both are small, both are listed in
section 4, and both mean re-running the four files (file 01 drops and recreates `CustomerCareDB`).
Note that customer changes are **not** written to `AUDIT_LOG`: the database only audits `CONTRACT`
and `FEEDBACK` events (schema reference, triggers table). That is a database rule, not a gap in the
screen.

## 4. Approved decisions (18/10/2026)

D1a, D2a and D3a were chosen; D4 follows from them. Each row keeps the rejected option, so the
reason stays on record.

| # | Question | Decision (18/10/2026) | Why, and what it costs |
|---|---|---|---|
| D1 | `sp_SearchCustomer` returns **all** matching rows, ordered by name, with no paging and no sort parameter. Where do paging and column sorting happen? | **(a) Extend the procedure** | New parameters `@PageNumber INT = 1`, `@PageSize INT = 20`, `@SortBy NVARCHAR(20) = N'Name'` (`Name` / `CreatedDate` / `TotalValue`), `@SortDesc BIT = 0`, plus a `TotalRows` column (`COUNT(*) OVER ()`). Paging without sorting in the same place would sort only the page currently shown, so the two go together; the sort column is read through a `CASE`, never concatenated into SQL. Rejected: (b) page and sort in the application over the full result - every list request would load every customer. Cost: one edit in `database/02_Function_View_Proc_Trigger.sql` and a re-run of the four files |
| D2 | Where does the page size come from? (M01b parked `PageSize` for M02.) | **(a) A `SETTINGS` key** | `PageSize` (Int, default 20), set by a Manager on `/settings`, plus a 10/20/50 choice on the list page kept in the URL. Rejected: (b) a constant in `WorkConstants` with the 10/20/50 choice in the URL only. Cost: `CHK_Settings_Value` extended in file 01, a seed row in file 03, and a numeric field in `_SystemSettings.cshtml` |
| D3 | How much of the detail page does M02 build? | **(a) Header plus all four tabs, read-only** | Contracts, history and feedback already have views and sample rows, and M03/M04 only add the buttons that change them. Rejected: (b) header plus Overview only, leaving the tabs to the later modules |
| D4 | Any SQL change means re-running the four files: `CustomerCareDB` is dropped and recreated, the sample data is reloaded and the demo `PasswordHash` values go back to SHA2 (each account re-hashes on its next sign-in). | **Accepted** | The data is sample data only, and the application holds no state outside the database except the language and theme cookies |

## 5. Assumptions

1. The login from M01 runs the module: the `employeeID` and the role come from the authenticated
   claim, never from the form. "Only my customers" and a new customer's owner default to that
   `employeeID`.
2. `CustomerType` accepts `Individual` and `Business`; `Gender` accepts `Male`, `Female`, `Other`;
   `Tier` accepts `Standard`, `Silver`, `Gold`, `Diamond` (`CHK_Customer_*` in file 01). The screen
   shows the Vietnamese label of each code and posts the code.
3. `Source` is free text. The form offers the five values that exist in the sample data (`Website`,
   `Giới thiệu`, `Sự kiện`, `Facebook`, `Điện thoại`) as suggestions and stores whatever is typed.
   It is business data, so it stays Vietnamese (`ccm-language`).
4. An empty optional field is sent as `NULL`, never as an empty string (AGENTS.md 3): email, phone,
   address, source, owner, gender, date of birth.
5. Support may open the list and the detail page and sees no Add, Edit, Deactivate or Reactivate
   button; the actions themselves are `[Authorize(Roles = "Manager,Sales")]` on the server, which is
   what the test checks (hiding a button is not a permission).
6. The page size from the URL is validated to 1-100 and falls back to the default when it is not a
   number, so a hand-written URL cannot page the whole table.
7. The customer list uses `sp_SearchCustomer` and never re-writes its JOIN or its `LIKE` conditions
   in C# (AGENTS.md 4: do not rewrite an existing JOIN).
8. `docs/mockups/static-mockups/customers.html` is only a visual reference (static, fake data, and
   it has no owner filter default, no "include inactive" and no paging) - `design.md` 6.3 wins.

## 6. Definition of done

1. `dotnet build` has 0 errors and `dotnet test` runs the whole suite green (the M01/M01b tests stay
   green; the M02 tests are added).
2. Logging in as `john` (Manager) and `mary` (Sales), the sidebar item "Customers" opens
   `/customers`; as `david` (Support) the same page opens but no Add/Edit/Deactivate control is
   offered, and typing `/customers/create` returns the 403 page, not the login page.
3. The list shows exactly what `sp_SearchCustomer` returns with the same parameters: the same rows,
   in the same order, with the same total value; a keyword, a tier, an owner, "only my customers" and
   "include inactive" each change the result in the way the database does.
4. Paging works with a size other than the default (for example 5): the row counts of the pages add
   up to the row count of the same query without paging, "showing X-Y of Z" matches, and a page
   number past the end shows the empty state instead of an error.
5. Every filter, the sort and the page are in the URL: copying the address into a new tab gives the
   same list back.
6. Adding an individual customer with an e-mail and a phone creates a row with `Tier = Standard`,
   `IsActive = 1`, `CreatedDate = today`, `Gender`/`DateOfBirth` NULL for a business customer; adding
   one with neither e-mail nor phone is refused with the message next to the field and nothing is
   written.
7. A duplicate e-mail (the filtered unique index `UX_Customer_Email`) is reported next to the e-mail
   field as a business message - never as SQL text, a constraint name or error 2601.
8. Editing changes only what was typed: `Tier` is unchanged (the trigger owns it), `CreatedDate` and
   `IsActive` are unchanged.
9. Deactivating a customer through the confirmation dialog calls `sp_SetCustomerStatus` (proved by
   the row: `IsActive = 0`), the customer disappears from the default list, appears with "include
   inactive" on, and the detail page offers Reactivate. No `DELETE` statement exists anywhere in the
   module.
10. The detail page shows the values the database holds - age from `fn_CustomerAge` (blank for a
    business), total value from `fn_CustomerContractTotal` - and the three data tabs list exactly the
    rows of `vw_ContractInfo`, `vw_InteractionHistory` and `FEEDBACK` for that customer, each with its
    empty state.
11. A customer named with HTML or a search term with `'` renders as text, never as markup, and every
    POST carries an anti-forgery token (a POST without one is rejected).
12. Every new visible string exists in **both** `SharedResource.resx` and `SharedResource.en.resx`
    (Vietnamese neutral file, English second), and each page is split into partials - the page file
    holds the header and the `<partial>` calls only.
13. The topbar search box sends the user to `/customers?q=<keyword>` and the result is the same list
    the keyword filter produces on the page itself; the sidebar "Customers" item is a real link and
    stays highlighted on `/customers`, `/customers/5` and `/customers/5/edit`.
14. `docs/tasks/m02-customers/test-results.md` records the round with real evidence and honest
    PASS / FAIL / BLOCKED / NOT RUN marks.
