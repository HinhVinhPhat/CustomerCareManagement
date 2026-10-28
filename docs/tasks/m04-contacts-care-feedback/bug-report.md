# M04 - Contacts, care, feedback - bug report

Bugs found by running the module during the M04 test round. Each was fixed so the round could
continue, and the affected cases were re-run afterwards. No other module was changed. The fixes and
the re-runs are also reflected in `test-results.md`.

---

## B01 - Every M04 form was rejected with 400 Bad Request

**Type:** blocking for every write in the module. **Found by:** T07, recording the first contact from
the `/contacts` dialog.

**Steps to reproduce**

1. Sign in as Manager or Sales and open `/contacts`.
2. Press "Record contact", choose a customer and a channel, type a content, then save.
3. The browser lands on the same address with an empty page and no message.

**Expected:** the contact is saved, a toast appears and the new row is on top.
**Actual:** HTTP 400 with an empty body; nothing is written.

**Cause:** the M04 forms were written with a literal `action="/..."`, for example:

```html
<form method="post" action="/contacts">
```

The form tag helper only emits the hidden anti-forgery field when it generates the form itself. With
a literal action the POST had no `__RequestVerificationToken`, so the anti-forgery filter refused it
before the controller ran. The same defect existed in the care form, the feedback form and the
assign/status/note forms - six write paths in total.

**Fix:** an explicit token in every M04 write form:

```cshtml
<form method="post" action="/contacts">
    @Html.AntiForgeryToken()
```

Applied to `Views/Contacts/_ContactForm.cshtml`, `Views/Care/_CareForm.cshtml`,
`Views/Feedbacks/_FeedbackForm.cshtml`, `_AssignDialog.cshtml`, `_AssignPanel.cshtml`,
`_StatusPanel.cshtml` and `_NotePanel.cshtml`.

**Re-verified:** T07, T08, T10, T14, T26, T29, T30, T31, T32, T44. Each form now contains the token
and the POST reaches the controller; the pages rendered the expected toasts and rows.

---

## B02 - Changing the feedback status threw an unhandled exception

**Type:** blocking for the status action. **Found by:** T28, moving an unassigned item out of `New`.

**Steps to reproduce**

1. Open a feedback item with no assignee.
2. Choose `InProgress` and press Update.
3. The browser shows an empty page with the POST address in the address bar.

**Expected:** the business rule "assign first" is shown as an alert and the status stays `New`.
**Actual:** HTTP 500, no message, no state change.

**Cause:** the service passed `DBNull.Value` as an untyped raw-SQL parameter:

```csharp
await db.Database.ExecuteSqlRawAsync(
    "EXEC dbo.sp_UpdateFeedbackStatus @FeedbackID = {0}, @Status = {1}, @Note = {2}",
    [feedbackId, status, (object?)note ?? DBNull.Value], ct);
```

EF Core 10 cannot infer a store type for `DBNull`:

```
System.InvalidOperationException: The current provider doesn't have a store type mapping for
properties of type 'DBNull'.
```

The failure happened before the procedure ran, so error 50004 could not be translated and the user saw
the generic exception page instead of the alert.

**Fix:** a typed parameter, so a NULL is sent as an `NVARCHAR(500)` NULL:

```csharp
var noteParameter = new SqlParameter("@Note", SqlDbType.NVarChar, FeedbackRules.MaxNoteLength)
{
    Value = (object?)note ?? DBNull.Value
};

await db.Database.ExecuteSqlRawAsync(
    "EXEC dbo.sp_UpdateFeedbackStatus @FeedbackID = @FeedbackID, @Status = @Status, @Note = @Note",
    [
        new SqlParameter("@FeedbackID", feedbackId),
        new SqlParameter("@Status", status),
        noteParameter
    ], ct);
```

in `src/CcmWeb/Services/FeedbackService.cs`.

**Re-verified:** T28 (50004 now becomes the expected alert), T31 (resolve with a note, resolved date
from the trigger), T32 (note only), T33 (500-character cap), T34 (empty note), T35 (invalid status),
T36 (50003). The note itself was read back from SQL Server, not only from the screen.

---

## B03 - The new unit tests failed on their first run

**Type:** test data / a helper whose name promised more than it checked. **Found by:** T02, the first
`dotnet test` of the round (`Failed: 9, Passed: 110`).

**Steps to reproduce**

1. Run `dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj`.
2. Open the failures for `CareRulesTests` and `FeedbackRulesTests`.

**Expected:** the new theory data binds to the `byte?` parameters and the helper reports an invalid
status as "no assignee required".
**Actual:** nine failures:

- Every `[InlineData]` row that passed an `int` (for example `4`) to a `byte?` parameter failed
  argument conversion. Rows whose score was `null` passed, which made the pattern clear.
- `FeedbackRules.NeedsAssignee("Unknown")` returned `true` although the status is not valid.

**Cause:** xUnit passes attribute values as `int`; it does not narrow them to `byte?` for a nullable
parameter. `NeedsAssignee` only checked `status != New`, so any non-null string - including an invalid
one - was treated as needing an assignee.

**Fix:** explicit `(byte)` casts in the theory data of `tests/CcmWeb.Tests/CareRulesTests.cs`, and
`NeedsAssignee` now requires a valid status:

```csharp
public static bool NeedsAssignee(string? status) =>
    IsValidStatus(status) && status != WorkConstants.FeedbackStatuses.New;
```

The application never reaches `NeedsAssignee` with an invalid status (the service validates first), so
no user-facing behaviour changed; the helper now matches its own name.

**Re-verified:** T02 and T03 - `119 passed, 0 failed, 0 skipped`. The care and feedback form tests that
were already passing stayed green.
