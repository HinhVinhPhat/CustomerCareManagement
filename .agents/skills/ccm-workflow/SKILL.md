---
name: ccm-workflow
description: Working process for the customer care management project - split into modules M01-M07, write requirements and a plan for user approval, implement sequentially, test on real SQL Server (never InMemory or SQLite), store test-results.md, record and fix bugs in batches, and hand over a report. Use this skill when starting a module or a large feature, when planning, when writing or running tests, when the user reports a bug or a test FAILs, or when reporting progress. Small, obvious tasks (typo, colour change, one-line fix) do not need the whole process.
---

# Working process

The short version of "requirements -> plan -> implement -> test -> bug" so the user (a student
building a course project) stays in control of the AI without drowning in ceremony. Fewer
approval gates than a multi-team process; if you want it stricter, split the approval gates as
described at the end.

## Modules

Each module is a unit of work, done one at a time. If `docs/design.md` section 8 divides them
differently, follow design.md and point out the difference.

| Module | Contents |
|---|---|
| M01 Foundation | Create the project per `ccm-mvc-architecture`, scaffold the DbContext, audit-log interceptor, login and authorization, `_Layout` and menu |
| M02 Customers | List, search, add/edit, deactivate, detail page (contracts, interactions) |
| M03 Products and contracts | Products/services, master-detail contracts, sign, renew, cancel |
| M04 Contacts, care, feedback | Record contacts, care and appointments, receive, assign and resolve feedback |
| M05 Dashboard and reports | Dashboard, revenue report, care report, feedback report, notifications |
| M06 Administration | Employees, accounts, audit log |
| M07 Docker and finishing | Dockerfile, a compose with the full `web` service, README, final review |

## Steps per module

1. **Requirements and plan (one approval).** Write `docs/tasks/mXX-<slug>/requirements.md`
   (goal, pages/features, sources in `design.md`, which DB views/procedures will be called,
   assumptions, definition of done) and `plan.md` (steps, files to create/edit, planned test
   cases). Give both to the user to approve **once**. No approval, no code.
2. **Implement sequentially.** If SQL changes: SQL -> Services -> Controller -> Views. Stay
   within the approved scope. Anything out of scope or any business change: stop, report and
   ask for approval of the extra work.
3. **Test.** Run only after the module is implemented. Store results in
   `docs/tasks/mXX-<slug>/test-results.md` (format below).
4. **Bugs.** Record them in `bug-report.md` in the same folder (symptom, cause with evidence,
   proposed fix, blast radius). The user approves the whole bug batch once before you fix them,
   then re-run the affected cases (regression). A new bug outside an approved batch must be
   raised for approval; do not patch it on your own.
5. **Report.** At the end of the module write a short `report.md`: what was done, main paths,
   verification actually run, BLOCKED/NOT RUN parts, open bugs.

Only mark a module "Done" when the outputs exist and the mandatory cases **PASS**. If bugs
remain, keep it "In progress". Never mark it done just because code or docs were written.

## Bug investigation

- Use the source, logs and available data before asking. Separate a cause that **has evidence**
  from a hypothesis. Never invent logs, inputs or reproduction results.
- Optimisation or extra fixes beyond the root cause are separate suggestions; do not bundle them
  into the fix.

## Testing

**Use real SQL Server on a separate test DB.** Do not substitute EF Core InMemory or SQLite,
because they do not run triggers or stored procedures, which are exactly what needs checking.
Never write tests into the live DB.

Create the test DB: copy the 4 `database/*.sql` files to a temp folder, replace `CustomerCareDB`
with `CustomerCareDB_Test` in the **copies** (never edit the originals), then run them in order
with `sqlcmd -I -f 65001` (see `ccm-database`). Run each test round against a freshly reloaded DB
so results are reproducible.

Cases to include (verify directly with queries on the DB):

- A contract's `TotalValue` equals the sum of `Quantity * UnitPrice` after adding, editing and
  deleting detail lines.
- `Tier` changes exactly at the 20/50/100 million thresholds and when a contract is cancelled.
- Contract status transitions: sign, renew, cancel in valid and invalid states (errors 50013,
  50014, 50015).
- Feedback: unassigned feedback cannot move to a processing status (50004 through the procedure,
  547 on direct writes); `ResolvedDate` is filled and cleared automatically when reopened.
- A customer with contracts cannot be deleted (50005); after deactivation the customer disappears
  from the default list.
- `AUDIT_LOG` has the correct `EmployeeID` after an action performed through the app.
- Permissions: each role reaches the right pages, a wrong role is blocked; test with the
  accounts `john`, `mary`, `david`.
- UI (real application actions): login, forms, filtering, paging, empty and error states,
  narrowing the window, entering data with special characters and accented text.

Use xUnit unit tests in `tests/CcmWeb.Tests` for rules that matter (for example
`TranslateSqlError`, the legacy SHA2 hash check, formatting helpers), not for getters or a copy
of every CRUD line.

**Each test round leaves the source untouched.** Record FAIL with evidence; do not edit the
source or adjust the expected result to dodge a failure in that round. Never tick a case you did
not run; if the SDK, SQL Server or the UI environment is missing, record it clearly as BLOCKED.

### `test-results.md` format

One row or block per case: case id, environment (SQL Server version, commit), input, command or
action, expected result, actual result, status (**PASS / FAIL / BLOCKED / NOT RUN**), evidence
(query result, screenshot, log).

## Hand-over report (short)

What was done, main paths, verification results that were **actually run**, BLOCKED/NOT RUN
parts, and the step awaiting user approval. Commit only when the user asks.

## If you want a stricter process

It can be split into separate gates: approve requirements, then approve the plan, then approve
the test round, then approve the bug batch. The original CoffeeManager project did that with
large teams; here it is merged because the course project has fewer modules.
