# M01 - Foundation - bug report

Bugs found by running the application during the M01 test round. B01 and B02 blocked the whole
round, so each was fixed as soon as it was found and the affected cases were re-run; B03 is
cosmetic and was fixed with them. No other module was touched.

---

## B01 - The sign-in page returned HTTP 500 ("The view 'Login' was not found")

**Type:** blocking. **Found by:** T02, on the first request to `/`.

**Steps to reproduce**

1. Start the application.
2. Open `http://localhost:5199/`.

**Expected:** the sign-in page.
**Actual:** the generic error page; the server log shows

```
fail: Microsoft.AspNetCore.Mvc.ViewFeatures.ViewResultExecutor[3]
      The view 'Login' was not found. Searched locations: /Views/Account/Login.cshtml, /Views/Shared/Login.cshtml
System.InvalidOperationException: The view 'Login' was not found.
```

**Cause:** the view lived in `Views/Accounts/` while the controller is `AccountController`, and the
default view engine looks in `Views/<ControllerName>/` - that is `Views/Account/`. The
`ccm-web-ui` skill also named the folder `Views/Accounts/`, so the mistake came from the written
reference, not from the code.

**Fix:** moved the folder to `src/CcmWeb/Views/Account/` and corrected the mapping table in
`.agents/skills/ccm-web-ui/SKILL.md` and the file list in `docs/tasks/m01-foundation/plan.md`.

**Re-verified:** T02, T03, T04, T05, T06, T07, T13 - all PASS.

---

## B02 - Every static file was redirected to the sign-in page

**Type:** blocking. **Found by:** T02, after the first fix: the page rendered without styles or
JavaScript.

**Steps to reproduce**

1. Sign in and open the dashboard.
2. Watch the browser console or request an asset directly.

**Expected:** `200` with `text/javascript` / `text/css`.
**Actual:** the console showed `SyntaxError: Unexpected token '<'`, and

```
GET /js/site.js  -> 302 Location: /login?ReturnUrl=%2Fjs%2Fsite.js
GET /css/site.css -> 302 Location: /login?ReturnUrl=%2Fcss%2Fsite.css
GET /favicon.ico -> 302 Location: /login?ReturnUrl=%2Ffavicon.ico
```

so `site.js` was never executed (the drawer, the notification panel and the demo-account buttons did
nothing) and the stylesheet was never applied.

**Cause:** `app.MapStaticAssets()` registers the assets as endpoints, and the fallback authorization
policy (`RequireAuthenticatedUser`) applies to every endpoint that does not say `[AllowAnonymous]`,
so the asset requests were challenged and redirected to the sign-in page before they could be
served.

**Fix:** serve the assets before routing and authorization, which is what this project needs:

```csharp
app.UseHttpsRedirection();
app.UseStaticFiles();   // before UseRouting()/UseAuthorization()
app.UseRouting();
```

**Re-verified:** `GET /js/site.js` -> 200 `text/javascript` 3061 bytes, `GET /css/site.css` -> 200
`text/css` 4162 bytes, `window.ccmToast` is a function in the browser, and the "john · Manager"
demo button fills the form. T02-T18 re-run.

---

## B03 - Built menu entries still carried `data-soon=""`

**Type:** cosmetic. **Found by:** T03, by reading the rendered sidebar HTML.

**Expected:** only entries whose page does not exist yet carry `data-soon="true"`.
**Actual:** the Dashboard entry, which is a real page, was rendered as
`<a href="/" class="nav-link active" data-soon="">`.

**Cause:** `data-soon="@(item.Built ? null : "true")"` - Razor emitted the attribute with an empty
value instead of dropping it. The behaviour was harmless (`site.js` only reacts to
`[data-soon="true"]` and the link kept `href="/"`), but the markup was wrong and any future
`[data-soon]` selector would have matched it.

**Fix:** emit an explicit value in `Views/Shared/_Sidebar.cshtml`:

```razor
data-soon="@(item.Built ? "false" : "true")"
```

**Re-verified:** the Support menu renders 7 `data-soon="true"` attributes (all entries except
Dashboard) and the Dashboard link no longer carries one.
