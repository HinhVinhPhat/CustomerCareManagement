# M04 - Contacts, care, feedback - plan

Companion to `requirements.md`. One approval covers both documents. Steps run in order; anything out
of scope or any business change stops the work and asks the user first.

## 1. Steps

### Step 0 - decisions

Questions 1-4 of `requirements.md` section 7 were settled before coding (the user approved the
recommendations):

1. Note = append with `" | "`, capped at 500 characters.
2. Care date = today, not editable.
3. No edit/delete for contacts and care records; correct with a new record.
4. Support may create feedback, as the permission matrix says.

If any answer differs, only that step changes; the rest of the plan stands.

### Step 1 - SQL

No SQL change. The tables, views, procedures and triggers already exist and are correct:
`CONTACT`, `CARE`, `FEEDBACK`, `vw_OpenFeedback`, `vw_CareSchedule`, `vw_InteractionHistory`,
`sp_AssignFeedback`, `sp_UpdateFeedbackStatus`, `trg_Feedback_FillResolvedDate`,
`trg_Feedback_AuditLog`. No re-scaffold: the entities and keyless views were scaffolded in M01 and are
already used by M02 (`VwOpenFeedbacks`, `VwCareSchedules`, `VwInteractionHistories`, `Feedbacks`,
`Cares`, `Contacts`).

### Step 2 - shared values and helpers

1. `Helpers/WorkConstants.cs`:
   - `ContactChannels`: `Phone`, `Email`, `Zalo`, `InPerson`, `Other` (`CHK_Contact_Channel`).
   - `CareTypes`: `Renewal`, `Upsell`, `Birthday`, `Survey`, `RenewalReminder`, `Other`.
   - `FeedbackTypes`: `Complaint`, `Suggestion`, `GeneralFeedback`, `Compliment`.
   - `FeedbackStatus`: `New`, `InProgress`, `Resolved`, `Closed` (+ `All` for validation only).
   - `FeedbackPriorities`: `1` High, `2` Medium, `3` Low, with a label group `Priority_1..3`.
2. `Helpers/CareRules.cs` (pure, testable):
   - `IsSurvey(type)`, `AllowsScore(type)`, `AppointmentIsValid(careDate, next)`.
   - normalises the score: `Survey` keeps 1-5, any other type sends NULL instead of letting 547 fire.
3. `Helpers/FeedbackRules.cs` (pure, testable):
   - `AllStatuses`, `IsValidStatus`, `NeedsAssignee(status)` (a valid status other than `New`),
   - `AppendNote(current, addition, max: 500)` with the `" | "` separator and a hard cap,
   - `IsOpen(status)` only for labels/filters; the list itself still reads `vw_OpenFeedback`.
4. `Helpers/Utils.cs`: map `CHK_Feedback_DueDate` to `Error_FeedbackDueDateInvalid`.
   `CHK_Care_SatisfactionScore`, `CHK_Care_NextAppointment`, `CHK_Feedback_Assignee`, 50002, 50003,
   50004 and 50012 are already mapped in M01/M02.
5. `Helpers/Navigation.cs`: `contacts`, `care` and `feedback` flip `Built` to `true`.
6. `Helpers/Badges.cs` already has `ForPriority`, `ForFeedbackStatus`, `ForScheduleStatus`; no change.

### Step 3 - ViewModels

`Models/ViewModels/`:

1. `ContactListViewModel.cs`: `ContactFilter` (Q, Staff, Channel, From, To, Sort, Desc, Page,
   PageSize, query builder), `ContactRow`, `ContactListViewModel` (rows, total, staff, channels, error
   key) and `ContactFormViewModel` (customer, channel, content, result; no staff/time - those come
   from the claim and the clock).
2. `CareViewModels.cs`: `CareFilter` (Q, Staff, CareType, From, To, Sort, Desc, page), `CareLogRow`,
   `CareScheduleRow`, `CareScheduleViewModel` (three groups + total), `CareLogViewModel`, and
   `CareFormViewModel` (customer, type, content, result, score, next appointment, return URL).
3. `FeedbackViewModels.cs`: `FeedbackFilter` (Q, Status = open default, Priority, Type, Overdue,
   Sort, Desc, page), `FeedbackRow`, `FeedbackCounters` (New, InProgress, Overdue),
   `FeedbackListViewModel`, `FeedbackCreateViewModel`, `FeedbackDetailViewModel` (+ audit-free detail
   fields, staff options, current note) and small form models for assign/status/note.

Every query builder writes the values the model binder understands (`expiring=true` lesson from
M03/B04) and keeps filters in sort/pager links.

### Step 4 - Services

1. `IContactService` / `ContactService`:
   - `SearchAsync(ContactFilter)` - LINQ over `CONTACT` joined to `CUSTOMER`/`EMPLOYEE`, filters by
     customer name, staff, channel, `ContactTime` range; sort by contact time (default, newest
     first), customer name, staff or channel; count then `Skip/Take`.
   - `CreateAsync(ContactFormViewModel, employeeId)` - sets `EmployeeId` from the claim and
     `ContactTime = DateTime.Now`, trims and nulls empty text, saves through EF, translates 547.
   - `GetCustomersAsync` (active only, ordered by name) and `GetStaffAsync`.
2. `ICareService` / `CareService`:
   - `GetScheduleAsync()` - `VwCareSchedules`, grouped Overdue/Today/Upcoming, ordered by appointment
     date then customer.
   - `SearchLogAsync(CareFilter)` - `CARE` joined to customer/employee, filters, sort by care date
     (default, newest first), customer name or type; paged.
   - `CreateAsync(CareFormViewModel, employeeId)` - `CareDate = DateOnly.FromDateTime(DateTime.Today)`,
     score normalised through `CareRules`, appointment validated, EF insert, 547 translated.
   - `GetCustomersAsync` / `GetStaffAsync`.
3. `IFeedbackService` / `FeedbackService`:
   - `SearchAsync(FeedbackFilter)` - default/open/status New/InProgress/overdue reads
     `VwOpenFeedbacks` (view = open definition, priority + due ordering, paging); status Resolved or
     Closed reads `FEEDBACK` joined to customer/employee with the same DTO and ordering. The column
     sort is priority (default), due date, created time or customer; the default stays priority then
     due date.
   - `GetCountersAsync()` - one query over `VwOpenFeedbacks`: New, InProgress, overdue.
   - `GetDetailAsync(id)` - `FEEDBACK` by id (any status) with customer and employee names, or null.
   - `CreateAsync(FeedbackCreateViewModel)` - insert through EF with `FeedbackStatus = New`,
     `EmployeeId = null`, `CreatedTime = DateTime.Now`, due date validated, 547 translated.
   - `AssignAsync(id, employeeId)` - `sp_AssignFeedback`; 50002/50003 translated.
   - `UpdateStatusAsync(id, status, note)` and `AppendNoteAsync(id, note)` - both read the current
     row, append through `FeedbackRules`, then call `sp_UpdateFeedbackStatus` with a valid status;
     50002/50004/50012 translated. `ResolvedDate` is never written by the service.
   - `GetStaffAsync` / `GetCustomersAsync`.
4. All writes catch `SqlException` and throw `BusinessException` with a resource key, exactly like
   M02/M03; no raw SQL text reaches a view. No `IQueryable` leaves a service.

### Step 5 - Controllers

1. `Controllers/ContactsController.cs`:
   - `[Authorize]`; `GET /contacts`, `POST /contacts` with
     `[Authorize(Roles = "Manager,Sales,Support")]`.
   - The create action takes the actor from `User.GetEmployeeID()`, checks `ModelState`, on success
     sets `TempData` and redirects to the list with a toast.
2. `Controllers/CareController.cs`:
   - `[Authorize]`; `GET /care` (tab = schedule|log, filters, page);
   - `GET/POST /care/create` with `[Authorize(Roles = "Manager,Support")]`;
   - `customerId` and a validated local `returnUrl` prefill/return; invalid or absolute URLs fall
     back to `/care`.
3. `Controllers/FeedbacksController.cs`:
   - `[Authorize(Roles = "Manager,Support")]` on the whole controller (Sales -> `/access-denied`);
   - `GET /feedbacks`, `GET/POST /feedbacks/create`, `GET /feedbacks/{id:int}`,
     `POST /feedbacks/{id:int}/assign`, `POST /feedbacks/{id:int}/status`,
     `POST /feedbacks/{id:int}/note`;
   - assign from the list accepts a validated local `returnUrl` (back to the filtered list) and falls
     back to the detail page;
   - every POST is anti-forgery protected by the global form tag helper.
4. Customer detail: add the two buttons and the feedback link in the existing views; no change to the
   M02 service.

### Step 6 - Views

`Views/Contacts/`:
- `Index.cshtml` shell; `_FilterBar`, `_ContactTable`, `_ContactRow`, `_Pager`, `_EmptyState`,
  `_RecordContactDialog`.
- The dialog is a plain `<dialog>` toggled by `site.js`, with the same fields and resource keys as the
  page. `?create=1&customerId=` (from the customer detail) opens it with the customer preselected.
- Channel is shown with an icon + label, never icon alone.

`Views/Care/`:
- `Index.cshtml` shell with two tabs; `_ScheduleTab` (three groups, count per group, “Mark as cared
  for” link to the pre-filled create page), `_LogTab` (filter bar, table, pager, empty state),
  `_CareRow`, `_Pager`.
- `Create.cshtml` + `_CareForm` + `_SatisfactionStars` (five radios, hidden unless Survey) and
  `_FormActions`.
- The phone in the schedule is a `tel:` link.

`Views/Feedbacks/`:
- `Index.cshtml` + `_Counters`, `_FilterBar`, `_FeedbackTable`, `_FeedbackRow`, `_Pager`,
  `_EmptyState`, `_AssignDialog` (shared by list and detail).
- `Create.cshtml` + `_FeedbackForm`.
- `Details.cshtml` + `_FeedbackSummary`, `_AssignPanel`, `_StatusPanel` (status select + note),
  `_NotePanel` (append), `_FeedbackMeta`.
- Overdue due dates get `text-danger-ink`; priority/status use `_Badge`.

`Views/Customers/`:
- `Details.cshtml`: “Add contact” (all roles) and “Add care” (Manager/Support) next to Edit, linking to
  `/contacts?create=1&customerId=…` and `/care/create?customerId=…`.
- `_TabFeedback.cshtml`: an action column with “Open” for Manager/Support only; keep the read-only
  table for Sales (Sales never gets a link they cannot open).

### Step 7 - styling and script

1. `wwwroot/css/site.css`: schedule group headers/colours via the existing variables, satisfaction
   stars (radio + label), long-content truncation, the existing `tbl-cards` responsive layout.
2. `wwwroot/js/site.js`: open/close for `[data-open-contact]`, `[data-open-assign]` (shared dialog
   helper if it stays small), satisfaction field toggle (`[data-care-type]`), note character counter
   against the 500-character cap, and the existing confirm dialog for nothing new.
3. No hard-coded colours in views; the five-star control uses CSS variables.

### Step 8 - language

Add keys to both `Resources/SharedResource.resx` and `SharedResource.en.resx`: page titles and
breadcrumbs, filter labels, table headers, buttons (“Record contact”, “Mark as cared for”, “Assign”,
“Change status”, “Add note”), form labels, counter labels, empty states, success messages and the new
error message `Error_FeedbackDueDateInvalid`. Channel, care type, feedback type, priority and status
reuse the existing `Channel_*`, `CareType_*`, `FeedbackType_*`, `Priority_*`, `FeedbackStatus_*`
groups. Dates `dd/MM/yyyy`, times `HH:mm`, server time rendered in the current culture.

### Step 9 - tests

1. Unit (`tests/CcmWeb.Tests`):
   - `CareRulesTests`: score allowed only for Survey; score normalisation; appointment equals today is
     valid, yesterday is not.
   - `FeedbackRulesTests`: every status valid/invalid; `NeedsAssignee`; append keeps the old text,
     honours `" | "`, caps at 500; an empty addition changes nothing.
   - `ContactFilterTests` / `FeedbackFilterTests`: query-string building (`overdue=true`,
     `status=...`, pager links keep filters).
2. Real SQL Server (`CustomerCareDB_Test`):
   - Contact insert stores the employee/time and a bad channel is refused by `CHK_Contact_Channel`.
   - Care insert: Survey with a score passes; a score on a non-Survey fails with 547
     (`Error_SurveyScoreOnly`); next appointment before care date fails (`Error_NextAppointmentBeforeCare`).
   - Feedback insert is `New`, unassigned, `ResolvedDate` NULL.
   - `sp_AssignFeedback`: assign moves New -> InProgress; unknown feedback 50002; unknown employee 50003;
     the audit row is `Assign` with the actor from `SESSION_CONTEXT`.
   - `sp_UpdateFeedbackStatus`: unassigned -> InProgress/Resolved/Closed fails 50004 (and the state is
     unchanged); unknown status 50012; Resolved fills `ResolvedDate`; reopening to New clears it;
     closing keeps it.
   - A direct EF update that leaves a processing item unassigned fails 547 and maps to 50004 text.
3. UI (real application, real test DB):
   - Roles: Manager, Sales (`mary`), Support (`david`); menus; `/feedbacks` as Sales -> `/access-denied`;
     crafted POSTs (care create as Sales, feedback assign as Sales) refused.
   - Contacts: record from the dialog; the list filters (customer, staff, channel, dates) and paging;
     the customer timeline shows the new contact.
   - Care: schedule groups and counts, “Mark as cared for” prefill, Survey score visible only for
     Survey, appointment validation, care log filters and paging.
   - Feedback: counters match the view; default order; status filter for Resolved/Closed; assign;
     resolve -> resolution date; reopen -> cleared; note appended; 50004 message on an unassigned
     crafted status change; audit entries.
   - Language switch and the toast/alert convention on every POST.
   - Empty states and narrow-window layout where the environment allows it.

### Step 10 - verification and documents

1. Build, run the app against `CustomerCareDB_Test`, execute the cases above, then run `dotnet test`.
2. Write `test-results.md` (PASS/FAIL/BLOCKED/NOT RUN with evidence), `bug-report.md`, `report.md`.
3. Update `requirements.md` status and `docs/design.md` only if a contradiction is found.

## 2. Risks

- **`vw_OpenFeedback` carries only open items.** Using it for the resolved/closed filter would always
  show nothing; the second query path is deliberate and must be covered by a test.
- **EF trigger trap on `FEEDBACK`.** The insert/update through EF must keep
  `UseSqlOutputClause(false)`; `sp_AssignFeedback`/`sp_UpdateFeedbackStatus` avoid EF writes entirely.
- **`ResolvedDate` is trigger-owned.** The service must never set it; tests check fill and clear.
- **Audit actor on feedback.** `SESSION_CONTEXT` must be set by the interceptor; verify the `Assign` row
  has the employee id, not NULL.
- **`CHK_Care_SatisfactionScore`** rejects a score unless the type is `Survey`; the form hides the
  field, but a crafted POST must be normalised/refused server-side.
- **`CHK_Feedback_Assignee`** rejects a direct write that leaves a processing item unassigned (547).
  The procedure gives 50004; both paths must map to the same readable message.
- **Date-only columns:** `CareDate`, `DueDate`, `NextAppointmentDate` bind as `DateOnly`; never send a
  formatted string.
- **Note append race:** two concurrent notes could lose one; accepted for this single-desk project,
  and the cap is enforced in one helper so it cannot drift.
- **Open redirect:** `returnUrl` must pass `Url.IsLocalUrl`; an absolute URL falls back to a safe page.

## 3. Out-of-scope guard

Anything not in section 1 (dashboard, reports, admin, product CRUD, profile, editing/deleting history,
notifications, a new library, a new SQL view) stops the work and asks the user first. Small, obvious
tasks (a label, a colour, a one-line fix) do not need the whole process.

## 4. Note on module naming

`ccm-workflow` lists M03 as "Products and contracts", but design.md puts product CRUD under
`/admin/products` in phase 7 and the approved M03 covered contracts only. M04 therefore follows
design.md phase 5: contacts, care, feedback. Product CRUD stays in the administration module (M06),
as agreed for M03.
