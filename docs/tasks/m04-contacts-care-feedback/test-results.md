# M04 - Contacts, care, feedback - test results

Run date: **20/10/2026**. Written after the code was frozen for this round; every row below was
actually executed. Nothing is ticked that was not run. The first automated run and the two UI
defects found during this round are recorded in `bug-report.md`; the affected cases were re-run after
the fixes.

## 1. Environment actually used

| Item | Value |
|---|---|
| Operating system | Windows 10 Home Single Language, build 26300 |
| .NET SDK | 10.0.401 |
| Target framework | `net10.0` |
| EF Core | 10.0.13 |
| Database engine | Microsoft SQL Server 2025 (RTM) 17.0.1000.7, instance `.\SQLEXPRESS` |
| Database under test | `CustomerCareDB_Test`, dropped and rebuilt by the test fixture from `database/01`-`03` |
| How the app was run | `dotnet run --project src/CcmWeb --urls http://localhost:5293 --no-build`, with `ConnectionStrings__DefaultConnection` pointing at `CustomerCareDB_Test` (Windows authentication) |
| Accounts used | `john` (Manager, employee 1), `mary` (Sales, employee 2), `david` (Support, employee 3) |

The live `CustomerCareDB` was **not** written into by this round. EF InMemory and SQLite were not used
anywhere. The test fixture recreated `CustomerCareDB_Test` when `dotnet test` ran, so the rows created
through the browser before that run were discarded; a smoke test of `/contacts` and `/feedbacks`
after the reset confirmed the screens still load against the seeded data.

## 2. Automated tests

```
dotnet build src\CcmWeb\CcmWeb.csproj
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```
dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj
Passed!  - Failed: 0, Passed: 119, Skipped: 0, Total: 119, Duration: 3 s - CcmWeb.Tests.dll (net10.0)
```

35 of the 119 cases are this module's, in five new files:

| File | Cases | What it covers |
|---|---|---|
| `tests/CcmWeb.Tests/CareRulesTests.cs` | 15 | a score only for a Survey, the 1-5 range, an empty/valid/invalid next appointment, and the two form-level rules |
| `tests/CcmWeb.Tests/FeedbackRulesTests.cs` | 17 | the four statuses, when an assignee is required, and the note append/empty/cap rules |
| `tests/CcmWeb.Tests/FeedbackFilterTests.cs` | 1 | status, priority, sort, order, page and page size survive in the pager links |
| `tests/CcmWeb.Tests/CareFilterTests.cs` | 1 | a care-log pager link keeps `tab=log` |
| `tests/CcmWeb.Tests/ContactFilterTests.cs` | 1 | the default contact list writes no query and `desc=false` is kept |

The remaining 84 cases are the M01/M02/M03 suites, unchanged.

The **first** run of this round reported `Failed: 9, Passed: 110`; the cause was the new tests, not
the application (B03 in `bug-report.md`). After the test data was corrected the whole suite passed.

## 3. Cases

| Id | Case | Expected | Actual | Status |
|---|---|---|---|---|
| T01 | Clean build | 0 errors | `Build succeeded. 0 Warning(s), 0 Error(s)` | PASS |
| T02 | Whole automated suite | all green | 119 passed, 0 failed, 0 skipped | PASS |
| T03 | New M04 unit tests | care and feedback rules and the filter URLs | 35/35 pass, including every `InlineData` row | PASS |
| T04 | `/contacts` renders | seeded rows, columns, pager | 17 rows; headers Time, Customer, Staff, Channel, Content, Result; "Showing 1-17 of 17 results" | PASS |
| T05 | Contact filters | customer name, staff, channel and contact-date range narrow the list | `q=DEF&channel=Phone` -> 1 row; `staff=3&from=2026-01-01&to=2026-10-31` -> 3 rows, all `Lê Minh Đức` inside the range | PASS |
| T06 | Contact sort and paging | sortable columns, 10/20/50, page 2 | `?sort=Customer&desc=false` and `desc=true` returned different ends of the alphabet; `pageSize=10` showed 10 rows, page 2 showed 8 and "Showing 11-18 of 18 results" | PASS |
| T07 | Record a contact as Manager | dialog, server sets staff/time, row appears, toast | contact 18 written with employee 1, time from the server, first row, toast "Đã ghi nhận liên hệ." | PASS |
| T08 | Record a contact as Sales | all roles may record | contact 19 written with employee 2 (`Trần Thị Mai`); the add button was present | PASS |
| T09 | Contact length validation | content <= 1000, result <= 200, data kept | two inline errors, the 1001-character content and the chosen customer stayed in the reopened dialog | PASS |
| T10 | Contact dialog from a customer | opens with `?create=true&customerId=7`, keeps the return address | dialog open, customer 7 selected, `ReturnUrl=/customers/7?tab=history`; saving returned to that page with the toast | PASS |
| T11 | Contact content is HTML-encoded | no script execution | `<img src=x onerror="window.__xss=1">` was shown as text, `innerHTML` was escaped and `window.__xss` stayed 0 | PASS |
| T12 | Care schedule | the three groups and the count from `vw_CareSchedule` | groups Overdue, Today and Next 7 days; badge 4; the action link was present for Manager/Support only | PASS |
| T13 | Care log | newest first, score shown | 13 rows, `CareDate DESC`; the new Survey row showed `5/5` and its next appointment | PASS |
| T14 | Add care from the schedule | date is today and fixed, customer prefilled, row saved, toast | customer 5 preselected, date 20/10/2026, care 13 saved, toast, schedule moved that customer out of Overdue | PASS |
| T15 | Score field follows the type | hidden for a non-Survey, shown for Survey, cleared when switching back | hidden initially and for Renewal; shown for Survey; checking a score then switching to Renewal cleared and hid it | PASS |
| T16 | Next appointment before the care date | inline error, entered data kept | `Error_NextAppointmentBeforeCare` shown next to the field, content and customer kept | PASS |
| T17 | A score on a non-Survey | refused by the form and by `CHK_Care_SatisfactionScore` | crafted POST -> inline `Validation_SurveyScoreInvalid`; direct INSERT -> SQL error 547 on the CHECK | PASS |
| T18 | Inactive customer | refused by the service | customer 27 -> inline "The customer does not exist or is no longer active." | PASS |
| T19 | Care log filters | customer, staff, type and care-date range | `type=Survey` -> 5 rows, all Survey; `from=2026-10-01&to=2026-10-20` -> 3 rows inside the range; `q=DEF` -> 3 rows | PASS |
| T20 | Care log paging | page size and second page | `pageSize=10` showed 10 rows and "Showing 1-10 of 13 results", with page 2 offered | PASS |
| T21 | Feedback list | counters, open rows, default priority order | counters New 2 / In progress 3 / Overdue 2; 5 open rows from `vw_OpenFeedback`; first row High priority | PASS |
| T22 | Resolved and Closed lists | read from `FEEDBACK`, not the open view | `status=Resolved` -> 3 rows; `status=Closed` -> 1 row; the status filter stayed selected | PASS |
| T23 | Overdue quick filter | only items past their due date | `overdue=true` -> 2 rows, checkbox selected | PASS |
| T24 | Priority, type and customer filters | narrow the open list | `priority=1` -> 1 row; `type=Complaint` -> 1 row; `q=ABC` -> 1 row | PASS |
| T25 | New feedback validation | due date not before the created date | inline "Hạn xử lý không được trước ngày tạo.", content kept | PASS |
| T26 | New feedback saves | header + `New`/no assignee, redirect to detail, toast | feedback 10 created, detail opened, toast "Đã ghi nhận phản hồi." | PASS |
| T27 | Feedback detail | all fields, note, three forms with a token, rule hint | customer, created, type, assignee, due, resolved date, content and note rendered; assign/status/note forms each carried `__RequestVerificationToken`; the unassigned row showed the "assign first" hint | PASS |
| T28 | Move out of `New` without an assignee | 50004, state unchanged | alert "Hãy phân công nhân viên trước khi chuyển phản hồi sang trạng thái đang xử lý."; status stayed `New` | PASS |
| T29 | Assign from the detail page | `sp_AssignFeedback`, `New` -> `InProgress`, toast | assigned to employee 3, status `InProgress`, toast "Đã gán phản hồi." | PASS |
| T30 | Assign from the list dialog | dialog posts to the row, returns to the list | action `/feedbacks/10/assign`, `returnUrl=/feedbacks`, 9 staff options, token present; after saving the row showed `Lê Minh Đức` / `Đang xử lý` and the toast | PASS |
| T31 | Resolve with a note | `ResolvedDate` from the trigger, note appended | status `Resolved`, resolved date 20/10/2026, note `Chưa phân công \| Đã gửi hóa đơn`; toast | PASS |
| T32 | Add a note only | append, status and resolved date unchanged | note became `... \| Khách đã xác nhận`, `ResolvedDate` stayed 20/10/2026, toast "Đã thêm ghi chú." | PASS |
| T33 | Note cap | never more than 500 characters | crafted 600-character note; `LEN(Note)` = 500, old text kept at the front | PASS |
| T34 | Empty note | refused, no write | alert "Nhập nội dung ghi chú."; note length unchanged | PASS |
| T35 | Invalid status | refused before the procedure | alert "Trạng thái phản hồi không hợp lệ." | PASS |
| T36 | Unknown assignee | 50003, message | alert "Employee not found."; no assignment | PASS |
| T37 | Sales is refused | no menu, 403 on `/feedbacks` and `/care/create`, care log visible | `/feedbacks` and `/care/create` went to `/access-denied`; the menu had no Feedback; `/care?tab=log` was readable without the add link; `/contacts` could be recorded | PASS |
| T38 | Support may work | menu, care/create, feedback/create, assign | `david` saw Feedback in the menu and opened `/care/create` and `/feedbacks/create`; assigning from the list worked | PASS |
| T39 | Manager on customer detail | add contact, add care and feedback view links | both create links were present and the feedback table had a View link per row; Sales saw only "Record contact" | PASS |
| T40 | Language switch | English labels, Vietnamese data, back to Vietnamese | English headers/counters/buttons and "Showing 1-17 of 17 results"; customer names and content stayed Vietnamese; switching back restored Vietnamese | PASS |
| T41 | Audit actor | the signed-in employee, not NULL | `AUDIT_LOG` for feedback 9: `Assign` and `StatusChange` by employee 1; feedback 10: `Assign` by employee 3 | PASS |
| T42 | Database constraints | 547 for the three rules | direct INSERTs all failed with 547: `CHK_Care_SatisfactionScore`, `CHK_Care_NextAppointment`, `CHK_Feedback_Assignee`; each probe was rolled back | PASS |
| T43 | Empty and out-of-range states | a message, never a fake empty table | `q=zzzz` showed "No contacts match"; `page=99` showed "This page has no contacts" with a first-page link; feedback and care had their own empty states | PASS |
| T44 | Anti-forgery on every M04 write form | a token is posted and the POST succeeds | contact, care, feedback, assign, status and note forms all contained `__RequestVerificationToken`; after the fix every POST succeeded (B01) | PASS |
| T45 | Mobile layout | usable down to phone width | **not run**; no viewport control in this environment | NOT RUN |
| T46 | A second page of feedback | pager page 2 | **not run**; the seeded set is smaller than one page, and the pager markup is shared with the lists that were tested | NOT RUN |

## 4. Other checks

- `git diff` against the SQL files touched by M01/M02/M03 shows **no M04 SQL change**; the entities
  and views were already scaffolded, so nothing was re-scaffolded.
- The feedback note was read back with `sqlcmd` (`LEN(Note)` and `RIGHT(Note, 20)`), not only from the
  screen.
- The care score, resolved date and audit rows were read back from `CustomerCareDB_Test`; the app did
  not write `ResolvedDate` or `AUDIT_LOG` itself.
- The probes for the three CHECK constraints ran inside a transaction and rolled back; no probe row
  remains.

## 5. Not verified

- Phone-width layout (T45) and a feedback list with more than one page (T46).
- `/feedbacks/9999` returned an empty 404 body. `/customers/9999` and `/contracts/9999` behave the
  same; the missing-record page is app-wide and was already empty before M04, so it is recorded here
  only so it is not mistaken for a M04 regression.
- No load, concurrency or long-running session test was attempted.
