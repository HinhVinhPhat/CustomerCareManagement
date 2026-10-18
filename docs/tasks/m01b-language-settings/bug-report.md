# M01b - Vietnamese UI, English/Vietnamese switch and settings - bug report

One bug was found in the M01b test round. It has since been **fixed and re-verified** (see the end of
this file); no other module was touched, and no FAIL row appeared in `test-results.md`.

---

## BUG-01 - The language switch appears to do nothing on a URL that carries `?culture=`

**Type:** low severity (display only, no data written wrongly).
**Found by:** T06 (query string) and T05 (the switch) run against each other.
**Status:** fixed on 18/10/2026 with the user's approval.

**Steps to reproduce**

1. Open `http://localhost:5293/login?culture=en` (a bookmark, a link, or a manual check - the
   application itself never generates such a link).
2. The page is in English.
3. Click the `VI` button in the topbar.

**Expected:** the page comes back in Vietnamese.
**Actual:** the page stayed English. The recorded exchange:

```
GET  /login?culture=en                    -> <html lang="en" data-theme="light">
POST /preferences/language value=vi,
     returnUrl=/login?culture=en          -> 302 (culture cookie written as vi)
GET  /login?culture=en                    -> <html lang="en" data-theme="light">   <-- still English
```

**Cause:** the two switch partials built their hidden `returnUrl` from
`Context.Request.Path + Context.Request.QueryString`, which kept `culture=en`. After the redirect the
query-string culture provider - the first provider in the chain, on purpose - read that parameter and
overrode the cookie that had just been written. The switch was not broken: the cookie **was** written
correctly (a later request without the parameter was Vietnamese), but the user saw no change.

**Fix (as built)**

```csharp
/// <summary>
/// The address to come back to after a language or theme switch: the current path and query with
/// the culture parameters removed. A <c>?culture=</c> left in the address would win over the cookie
/// the switch has just written (it is the first culture provider on purpose), so the user would
/// click and see nothing change.
/// </summary>
public static string ReturnUrlWithoutCulture(HttpRequest request)
{
    var kept = request.Query
        .Where(pair => pair.Key is not ("culture" or "ui-culture"))
        .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value.ToString())}")
        .ToList();

    // Only a local address: the controller checks this again before redirecting.
    return kept.Count == 0 ? request.Path.Value ?? "/" : $"{request.Path}?{string.Join('&', kept)}";
}
```

`Views/Shared/_LanguageSwitch.cshtml` and `_ThemeSwitch.cshtml` now both call
`Utils.ReturnUrlWithoutCulture(Context.Request)`. The `returnUrl` is still checked with `Url.IsLocalUrl`
in `PreferencesController`, so the open-redirect protection is unchanged, and the helper escapes the
kept parameters again so a Vietnamese search term survives as a valid address.

**Re-verification (18/10/2026, app on `http://localhost:5293`)**

| Case | Expected | Actual | Status |
|---|---|---|---|
| `GET /login?culture=en` | English, return URL without the parameter | `lang=en`, `returnUrl=/login` | PASS |
| `GET /login?culture=en&q=abc` | the other parameter survives | `returnUrl=/login?q=abc` | PASS |
| `GET /login?culture=en&page=2&ui-culture=en` | both culture forms dropped | `returnUrl=/login?page=2` | PASS |
| Switch to `VI` on `/login?culture=en` | the page really turns Vietnamese | `lang=vi`, title `Đăng nhập \| Hệ thống chăm sóc khách hàng` | PASS |
| `GET /settings?culture=en&x=1` while signed in | `returnUrl=/settings?x=1` | as expected | PASS |
| Unit tests (`UtilsTests`) | the helper behaves | 4 new cases, all green; suite `43/43 PASS` | PASS |

**Why it waited:** fixing it changes source during the test round, which the workflow forbids without
approval. Impact was small (the application itself never creates a `?culture=` link); the query-string
provider exists for manual checks and tests, as agreed in the plan.
