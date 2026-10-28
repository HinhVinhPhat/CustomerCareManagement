# M04 - Contacts, care, feedback - hand-over report

## 1. What was done

The daily customer-care work is usable end to end. Every role can record a contact; Manager and
Support can plan and log care visits; only Manager and Support can receive, assign, handle and close
feedback. All writes that carry a business rule go through the existing database objects, and the
application never writes the columns a trigger owns.

- `/contacts`: filter by customer name, staff, channel and contact-date range; sortable columns;
  10/20/50 paging; channel label with an icon; empty and out-of-range states. "Record contact" opens
  the shared dialog, which also accepts `?create=true&customerId=...&returnUrl=...` from the customer
  page. The server sets the staff member and the time; a contact is never edited or deleted.
- `/care`: two tabs. The schedule reads `vw_CareSchedule` and groups Overdue / Today / Next 7 days,
  with a badge and a role-aware "Mark as cared for" action. The log reads `CARE` with customer, staff,
  type and care-date filters, sortable columns and paging. `/care/create` takes the date from the
  server (today), offers only active customers, shows the satisfaction score only for a Survey and
  normalises it on the server.
- `/feedbacks`: counter cards (New / In progress / Overdue) from `vw_OpenFeedback`; the default list
  is exactly that view, while Resolved and Closed are read from `FEEDBACK`. Filters: customer, status,
  priority, type, overdue. `/feedbacks/create` writes a `New` item with no assignee and a server
  `CreatedTime`. The detail page shows the item, assigns through `sp_AssignFeedback`, changes status
  and adds a note through `sp_UpdateFeedbackStatus`; `ResolvedDate` comes from its trigger. The note
  is appended with `" | "` and capped at 500 characters.
- Roles: the menu, the buttons and the server agree. Sales has no Feedback entry and is sent to
  `/access-denied` by `/feedbacks` and `/care/create`; Support has all three areas; Manager has
  everything. The rules are `[Authorize(Roles = ...)]`, not merely hidden markup.
- Shared rules live in `Helpers/CareRules.cs` and `Helpers/FeedbackRules.cs`; coded values live in
  `WorkConstants` and match the CHECK constraints.
- No SQL file was changed and nothing was re-scaffolded: `CONTACT`, `CARE`, `FEEDBACK`,
  `vw_CareSchedule`, `vw_OpenFeedback`, `sp_AssignFeedback`, `sp_UpdateFeedbackStatus` and the two
  feedback triggers already existed.

## 2. Main paths

```
src/CcmWeb/Controllers/ContactsController.cs       /contacts list and dialog POST
src/CcmWeb/Controllers/CareController.cs           /care tabs, /care/create
src/CcmWeb/Controllers/FeedbacksController.cs      /feedbacks, create, details, assign, status, note
src/CcmWeb/Services/IContactService.cs             contact service contract
src/CcmWeb/Services/ContactService.cs              contact list + immutable create
src/CcmWeb/Services/ICareService.cs                care service contract
src/CcmWeb/Services/CareService.cs                 schedule, log, create, score normalisation
src/CcmWeb/Services/IFeedbackService.cs            feedback service contract
src/CcmWeb/Services/FeedbackService.cs             open/closed split, counters, procedures, note
src/CcmWeb/Services/LookupQueries.cs               active customers, staff, active-customer guard
src/CcmWeb/Helpers/CareRules.cs                    score/type/appointment rules
src/CcmWeb/Helpers/FeedbackRules.cs                status, assignee, note append/cap
src/CcmWeb/Helpers/WorkConstants.cs                contact channels, care types, feedback values
src/CcmWeb/Helpers/Utils.cs                        50002/50003/50004/50012 and 547 messages
src/CcmWeb/Helpers/Navigation.cs                   Contacts, Care, Feedback are built entries
src/CcmWeb/Models/ViewModels/ContactViewModels.cs  filter, row, dialog form, URL builder
src/CcmWeb/Models/ViewModels/CareViewModels.cs     schedule, log, filters, create form
src/CcmWeb/Models/ViewModels/FeedbackViewModels.cs filter, counters, detail, create form
src/CcmWeb/Views/Contacts/                          list, filters, table, row, dialog, pager, empty
src/CcmWeb/Views/Care/                              tabs, schedule, log, filters, create, pager, empty
src/CcmWeb/Views/Feedbacks/                         counters, list, filters, detail, assign/status/note
src/CcmWeb/wwwroot/js/site.js                       contact dialog, care score toggle, assign dialog
src/CcmWeb/wwwroot/css/site.css                     dialog-wide and score-choice styles
src/CcmWeb/Resources/SharedResource*.resx           M04 keys in both languages (399 keys each)
tests/CcmWeb.Tests/CareRulesTests.cs                15 cases, no database
tests/CcmWeb.Tests/FeedbackRulesTests.cs            17 cases, no database
tests/CcmWeb.Tests/FeedbackFilterTests.cs           1 case, no database
tests/CcmWeb.Tests/CareFilterTests.cs               1 case, no database
tests/CcmWeb.Tests/ContactFilterTests.cs            1 case, no database
docs/tasks/m04-contacts-care-feedback/              requirements, plan, results, bugs, this report
```

## 3. Verification actually run

- `dotnet build src\CcmWeb\CcmWeb.csproj` -> **0 errors, 0 warnings**.
- `dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj` -> **119 passed, 0 failed, 0 skipped**, of
  which 35 are this module's new cases.
- The browser round used `CustomerCareDB_Test` with `john` (Manager), `mary` (Sales) and `david`
  (Support): list/filter/sort/page, create contact, care and feedback, assign from both the list and
  the detail page, status plus note, the 500-character cap, validation errors, empty states, the
  English/Vietnamese switch, a XSS payload in a contact, and the 403 paths for Sales.
- SQL Server was read directly for the actor and the trigger-owned values: feedback 9 had
  `ResolvedDate = 20/10/2026` and a 500-character note; `AUDIT_LOG` recorded `Assign` /
  `StatusChange` with employees 1 and 3, never NULL. Three direct INSERT probes confirmed 547 on
  `CHK_Care_SatisfactionScore`, `CHK_Care_NextAppointment` and `CHK_Feedback_Assignee`; each was
  rolled back.
- The live `CustomerCareDB` was not written to; EF InMemory and SQLite were not used; no M04 SQL file
  was changed.

The full case table is in `test-results.md` (44 PASS, 2 NOT RUN, 0 FAIL after the fixes). The defects
found and fixed during the round are in `bug-report.md`: B01 (every M04 form lacked an anti-forgery
token), B02 (`DBNull` as an untyped parameter broke every status change), B03 (the new tests' theory
data and the `NeedsAssignee` helper).

## 4. Not run / known gaps

- **T45 mobile layout:** not run; this environment offers no viewport control. The markup uses the
  same responsive table and card rules as M02/M03, which were also not verified at phone width.
- **T46 a second page of feedback:** not run; the seeded feedback set is smaller than one page. The
  pager markup is shared with the contact and care lists, which were tested with two pages.
- **Missing record page:** `/feedbacks/9999` returns an empty 404 body. `/customers/9999` and
  `/contracts/9999` behave the same, so this is pre-existing application behaviour, not a M04
  regression; a friendly 404 page is not part of the approved M04 scope.
- No load, concurrency or long session test was attempted.

## 5. Waiting for the user

1. **Accept M04** (this report, `test-results.md` and `bug-report.md`). No code will be committed
   until you ask.
2. **Decide on the known gaps:** whether the mobile layout and the friendly 404 page should be a new
   small task, and whether the feedback tab on a customer page should stay readable by Sales (it is
   the M02 tab, and the M04 scope only allowed adding the action column and the detail link).
3. **M03 follow-ups still open from the previous hand-over:** the renew suggestion rule (end date +
   1 year can land in the past) and the empty error box when no product remains. B01-B04 of M03 are
   fixed and were re-verified; their acceptance is still pending.
