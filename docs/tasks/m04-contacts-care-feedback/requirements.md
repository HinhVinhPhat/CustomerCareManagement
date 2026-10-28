# M04 - Contacts, care, feedback

Status: **approved** (questions 1-4 of section 7 are settled as recorded in `plan.md` Step 0).
Implementation and the test round are documented in `test-results.md`, the defects in
`bug-report.md` and the hand-over in `report.md`. References: `docs/design.md` sections 2.2, 3.4, 4, 5.1,
5.2, 6.5, 6.9, 6.10, 6.11, 7 and phase 5 of section 8; `.agents/skills/ccm-workflow`;
`.agents/skills/ccm-database` (`CONTACT`, `CARE`, `FEEDBACK`, `vw_OpenFeedback`, `vw_CareSchedule`,
`vw_InteractionHistory`, `sp_AssignFeedback`, `sp_UpdateFeedbackStatus`, `trg_Feedback_FillResolvedDate`,
`trg_Feedback_AuditLog`).

## 1. Goal

Make the daily customer-care work usable: record every contact with a customer, plan and log care
visits (including the satisfaction survey), and receive, assign, handle and close feedback. Support
is the desk that does most of this; Sales can record contacts and read the care log; the Manager can
do everything. The feedback status and the assignee change only through the procedures of the
database, and `ResolvedDate` stays owned by its trigger.

## 2. Scope

| # | Feature | Source |
|---|---|---|
| 1 | `/contacts`: filters customer name, staff, channel, contact-date range; columns time, customer, staff, channel (icon + label), content, result; sortable columns, paging; empty and error states | design.md 6.9 |
| 2 | "+ Record contact" dialog: customer*, channel*, content, result. The staff member and the time come from the signed-in claim and the server clock, never from the form | design.md 6.9 |
| 3 | `/care` tab "Schedule to do": `vw_CareSchedule` grouped Overdue (red), Today (orange), Upcoming 7 days (blue); customer, tap-to-call phone, type, content, appointment date, "Mark as cared for" | design.md 6.10 |
| 4 | `/care` tab "Care log": `CARE` with filters customer name, staff, type, care-date range; date, customer, staff, type, content, result, score, next appointment; sortable columns, paging | design.md 6.10 |
| 5 | `/care/create` form (also opened from the schedule and the customer detail): customer*, type*, content, result, satisfaction score 1-5 only when type = Survey, next appointment date. Care date is today; the appointment may not be before it | design.md 6.10 |
| 6 | `/feedbacks`: counter cards New / InProgress / Overdue (click to filter); filters customer name, status, priority, type; default unresolved, sorted by priority then due date; sortable columns; table priority, customer, type, truncated content, status, assignee or "Unassigned", due date (red when overdue); paging | design.md 6.11 |
| 7 | `/feedbacks/create`: customer*, type*, content, priority, due date. New items start as `New` with no assignee | design.md 6.11 |
| 8 | `/feedbacks/{id}`: full content, customer, assignee, due date, resolution date, note; "Assign" (any employee) through `sp_AssignFeedback`; "Change status" and "Add note" through `sp_UpdateFeedbackStatus`; the rule "assign first" is explained before the action and enforced by 50004 | design.md 6.11 |
| 9 | Assign from the list for an unassigned row, without opening the detail page | design.md 6.11 |
| 10 | Permissions: Contacts all roles read, all may add; Care all read, only Manager and Support may add; Feedback only Manager and Support, and Sales is refused on the server even with a direct URL or a crafted POST | design.md 5.1 |
| 11 | Customer detail `/customers/{id}`: "Add contact" (all roles) and "Add care" (Manager, Support) buttons; the Feedback tab links to the feedback detail for the roles that may open it | design.md 6.5 |
| 12 | Sidebar marks Contacts, Care and Feedback as built pages; `/feedbacks/5` keeps the Feedback item highlighted | M01 carry-over |
| 13 | Every visible string in both `.resx` files; channel, care type, feedback type, priority and status through `L.Label(...)`; safe handling of long content | `ccm-language` |
| 14 | Unit tests for the pure rules; real-database tests for the two procedures, the constraints and the triggers | `ccm-workflow` |

### Explicitly NOT in M04

- Dashboard, reports and charts (M05).
- Employees, accounts, product CRUD, audit-log administration, profile (M06).
- Editing or deleting a contact or a care record. They are history: a correction is a new record.
- Deleting feedback, editing `CreatedTime`, `ResolvedDate` or the audit log.
- Sending email/SMS/Zalo, file attachments, an automatic assignment rule, notifications on assignment.
- Changing the customer detail tabs built in M02 beyond the two buttons and the feedback link.
- A new SQL view/procedure for the lists: the existing views and tables are enough (see 4).

## 3. Permissions to implement

| Screen / action | Manager | Sales | Support |
|---|---|---|---|
| `/contacts` list and record | yes | yes | yes |
| `/care` list | yes | yes | yes |
| `/care/create` and "Mark as cared for" | yes | **no (view only)** | yes |
| `/feedbacks`, create, assign, status, note | yes | **no (no menu, 403)** | yes |

The menu keeps the entries of design.md 5.2: Sales sees Contacts and Care (view), no Feedback; Support
sees all three; Manager sees all three. Authorization is `[Authorize(Roles = ...)]`; hiding a button is
never the protection.

## 4. Data used by this module

| Use | DB object | Note |
|---|---|---|
| Contact list, filters, paging | `CONTACT` | Staff and time are written by the server |
| Record contact | `CONTACT` (insert through EF) | `CHK_Contact_Channel`; no trigger on this table |
| Care schedule | `vw_CareSchedule` | Latest care record of each active customer, appointment within 7 days; the view decides Overdue/Today/Upcoming |
| Care log, filters, paging | `CARE` | Read ordered by `CareDate DESC, CareID DESC` |
| Add care | `CARE` (insert through EF) | `CHK_Care_Type`, `CHK_Care_SatisfactionScore`, `CHK_Care_NextAppointment` |
| Feedback list, default (open) | `vw_OpenFeedback` | `New` + `InProgress`, priority, `IsOverdue`; the view is the definition of "open" |
| Feedback list, Resolved/Closed | `FEEDBACK` with `CUSTOMER`/`EMPLOYEE` | The view intentionally carries open items only |
| Counters New / InProgress / Overdue | `vw_OpenFeedback` | Counted from the same view, never re-derived |
| Feedback detail | `FEEDBACK` | Any status, including resolved and closed |
| New feedback | `FEEDBACK` (insert through EF) | Status `New`, no assignee, `CreatedTime` from the server |
| Assign | `sp_AssignFeedback` | Moves `New` to `InProgress`; 50002, 50003 |
| Change status / add note | `sp_UpdateFeedbackStatus` | 50002, 50004, 50012; `ResolvedDate` is filled/cleared by `trg_Feedback_FillResolvedDate` |
| Customer detail | `vw_InteractionHistory`, `vw_OpenFeedback` | Already read by M02; only the two buttons and the feedback link are added |

The application never writes `ResolvedDate`, `AUDIT_LOG`, `TotalValue` or `CUSTOMER.Tier`.
`FEEDBACK` already has `UseSqlOutputClause(false)` in `ApplicationDbContext.Custom.cs`.

## 5. Error codes this module can hit

| Code | Meaning | Shown to the user |
|---|---|---|
| 50002 | Feedback not found | "This feedback item does not exist." |
| 50003 | Employee not found | "The chosen employee does not exist." |
| 50004 | Feedback has no assignee | "Assign a staff member before moving this feedback to a processing status." |
| 50012 | Invalid feedback status | "The feedback status is not valid." |
| 547 `CHK_Feedback_Assignee` | Direct write would leave a processing item unassigned | same message as 50004 |
| 547 `CHK_Care_SatisfactionScore` | Score outside 1-5 or not a Survey | "A satisfaction score is only allowed for a Survey, from 1 to 5." |
| 547 `CHK_Care_NextAppointment` | Appointment before the care date | "The next appointment cannot be before the care date." |
| 547 `CHK_Feedback_DueDate` | Due date before the created date | new message: "The due date cannot be before the day the feedback was created." |
| 547 other / 2601 / 2627 | other CHECK/FK/unique | the existing generic messages, no SQL detail |

## 6. Decisions taken in this draft

1. **"Open" means exactly `vw_OpenFeedback`** (`New` or `InProgress`). The default list and the
   counter cards read that view; when the user asks for `Resolved` or `Closed`, the service reads
   `FEEDBACK` because the view deliberately does not carry those rows. The open definition is not
   copied into C#.
2. **A contact or a care record is not editable.** A mistake is corrected by a new record; history
   stays honest. The design has no edit page and no delete action.
3. **The care date is today**, as the add-care form in design.md 6.10 does not offer a date field.
   Historical rows stay in the log; "care date" filters still work.
4. **"Add note" appends, it does not replace.** `Note` is capped at 500 characters; the new text is
   appended with `" | "`, and the dialog warns when the result would exceed 500. The status form can
   append a note in the same save.
5. **Assignee list = all employees**, not only Support: the design says "assign a staff member".
6. **New contact/care/feedback only offers active customers**, matching the rest of the application.
   Deactivated customers remain readable in the lists.
7. **Paging is 20 by default with 10/20/50 links**, as in M02/M03; the lists query with LINQ
   (`Where`/`OrderBy`/`Skip`/`Take`) in the Service, so a page is a page of the whole result.
8. **Two child routes are added: `/care/create` and `/feedbacks/create`.** The sitemap in design.md
   section 4 lists only `/care` and `/feedbacks`, but both forms need an address, and the project
   already uses `/contracts/create` for the same pattern. Contacts keeps its dialog and posts to
   `/contacts`. If you prefer modals for care and feedback too, only the controller/views change.

## 7. Open questions for the user

1. **Append or replace the note** on feedback: recommendation in 6.4 is append with `" | "`.
2. **Care date editable?** Recommendation in 6.3 is "today only"; allow choosing a past date if you
   want to backfill history.
3. **Contact and care correction**: confirm that a wrong record is corrected by adding a new one
   (no edit/delete), as in 6.2.
4. **Feedback creation by Support**: the permission matrix gives Support create rights; confirm that
   Support may open `/feedbacks/create` (the other option is Manager-only creation, Support handles).

## 8. Acceptance

- All three roles see Contacts, Care and Feedback exactly as the matrix says; Sales has no Feedback
  entry and `GET /feedbacks` redirects to `/access-denied`.
- A contact recorded from the dialog stores the signed-in employee and the server time, and appears
  in the list and in the customer timeline.
- The care schedule groups Overdue/Today/Upcoming correctly, and "Mark as cared for" creates a new
  care record with today's date.
- A Survey asks for the 1-5 score; a non-Survey does not, and the database refuses a score on a
  non-Survey.
- The feedback counters match `vw_OpenFeedback`; the default list is open items sorted by priority
  then due date; Resolved/Closed are reachable through the filter.
- Assigning moves `New` to `InProgress`; moving an unassigned item to any status other than `New`
  shows 50004 and changes nothing.
- Resolving or closing fills `ResolvedDate`; reopening clears it (trigger).
- A note is appended, never silently replacing the old one.
- No screen writes `ResolvedDate`, `AUDIT_LOG`, `TotalValue` or `CUSTOMER.Tier`.
- Every change shows the toast/alert feedback of the new convention.
