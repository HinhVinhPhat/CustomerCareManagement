# M03 - Contracts - bug report

Bugs found by running the module during the M03 test round. Each was fixed as soon as it was found so
the round could continue, and the affected cases were re-run afterwards. No other module was touched.

---

## B01 - The JSON line parser silently dropped every line

**Type:** blocking for anything that re-read the hidden field. **Found by:** T03, the round-trip
assertion between `BuildDetailJson` and `ParseLines`.

**Steps to reproduce**

1. Build the hidden field the way the service does:

   ```csharp
   var json = ContractRules.BuildDetailJson(
       [new ContractLineInput { ProductId = 2, Quantity = 4, UnitPrice = 15_000_000m }]);
   var lines = ContractRules.ParseLines(json);
   ```

**Expected:** one line with product 2, quantity 4 and unit price 15,000,000.
**Actual:** `lines.Count == 0`. Any code path that re-read JSON produced by `BuildDetailJson` behaved
as if the form had no lines, and the screen answered "A contract needs at least one product line".

**Cause:** `ParseLines` deserialised with the default options, which match property names
case-sensitively. `BuildDetailJson` must write `ProductID` because that is the name
`sp_AddContract` reads with `OPENJSON ... WITH`, while the view model calls the property `ProductId`,
and the page posts `ProductId`. The two spellings could never bind to each other.

**Fix:** one shared options object in `src/CcmWeb/Helpers/ContractRules.cs`:

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNameCaseInsensitive = true
};
```

**Re-verified:** T03 (the round-trip test now passes both spellings), T24 (a contract created from the
screen kept both of its lines and the correct total).

---

## B02 - The detail page showed the success banner twice

**Type:** cosmetic. **Found by:** T25, after signing a contract from the screen.

**Steps to reproduce**

1. Sign in as `mary` and sign a pending contract.
2. Land on `/contracts/{id}`.

**Expected:** one green "Contract signed" banner.
**Actual:** the same banner twice, one under the other, both closing independently.

**Cause:** `Views/Contracts/Details.cshtml` rendered the action-message partial
(`_MessageBanner` at the time, now `_ActionMessage`) itself, and `Views/Shared/_Layout.cshtml`
already renders it for every page. `TempData` is read once, but the partial was included twice, so
the message was emitted twice.

**Fix:** removed the extra partial from `Details.cshtml`; the layout remains the single place that
renders the result of the last action.

**Re-verified:** T25 and T27 - one message after signing and after cancelling. The presentation itself
changed later the same day (success is now a toast, a failure an alert block); see section 7 of
`test-results.md`.

---

## B03 - Cancelling a contract had nowhere to type the reason

**Type:** functional gap against the approved scope (`requirements.md` feature 7). **Found by:** T27,
while cancelling a contract.

**Steps to reproduce**

1. Open an active contract and press Cancel.
2. Confirm.

**Expected:** the confirmation asks for a reason and the reason ends up in `CONTRACT.Note`, as
`sp_CancelContract` is written to do.
**Actual:** the dialog only asked "are you sure"; the form posted no `reason`, so every cancellation
stored `Cancelled: (no reason given)` and the audit entry carried no explanation.

**Cause:** the shared `Views/Shared/_ConfirmDialog.cshtml` only had a title, a body and two buttons;
the cancel form in `Details.cshtml` had no `reason` field.

**Fix:**

- `_ConfirmDialog.cshtml`: an optional textarea (`maxlength="300"`, the width of
  `sp_CancelContract.@Reason`), hidden unless the triggering form carries `data-confirm-reason`.
- `Details.cshtml`: the cancel form carries `data-confirm-reason` and a hidden `name="reason"`
  input (`data-confirm-value`).
- `wwwroot/js/site.js`: copies the textarea into the hidden input just before submitting, and clears
  it after closing.
- `Resources/SharedResource.resx` / `SharedResource.en.resx`: `Contract_CancelReason`.

**Re-verified:** T27 - cancelling #14 with a typed reason stored it inside `Note` and the detail page
showed it. The dialog stays hidden for callers that do not carry `data-confirm-reason` (the customer
deactivation form is such a caller); that flow was not re-run in this round.

---

## B04 - Clicking a sort column or a page number dropped the "expiring soon" filter

**Type:** functional. **Found by:** T23, on the filtered list.

**Steps to reproduce**

1. Open `/contracts?expiring=true`.
2. Click the "Sign date" column header, or page 2.

**Expected:** the result stays limited to contracts inside the 30-day window, with the sort/page
applied on top.
**Actual:** the full list came back; the filter checkbox was off and the address no longer contained
`expiring`.

**Cause:** `ContractFilter.Build` wrote the quick filter as `expiring=1`, while the action takes
`bool expiring`. ASP.NET Core model binding accepts `true`/`false` for `bool`, so `1` bound to
`false` and the filter was silently lost.

**Fix:** write the value the binder understands, in
`src/CcmWeb/Models/ViewModels/ContractListViewModel.cs`:

```csharp
if (Expiring)
    parameters.Add("expiring=true");
```

**Re-verified:** T23 - the rendered sort and pager links now carry `expiring=true` and the filtered
result survives the click. `ContractFilterTests` asserts the query string so the value cannot regress.
