---
name: ccm-web-ui
description: Building the Razor UI (Views, _Layout, partials, ViewComponents, ViewModels) for the ASP.NET Core MVC application of the customer care management system, porting from the static HTML + Tailwind build in docs/mockups/static-mockups - layout, role-based menu, dashboard, filtered and paged lists, forms, the master-detail contract form, badges, notifications, empty and error states, Vietnamese wording with the English/Vietnamese switch. Use this skill when creating or editing any .cshtml file, site.css or wwwroot/js file, when porting a page from the reference build to Razor, and when the user mentions UI, page, screen, dashboard, form, list table, sidebar or View.
---

# Razor UI

The spec for each page (layout, filters, columns, buttons, permissions) is in `docs/design.md`
sections 2 to 9. **Read the matching section before building a page.** Sections 10 (REST API) and
11 (Docker api + web) of design.md are outdated versus the MVC architecture: controller actions
replace endpoints. Code structure is in `ccm-mvc-architecture`, permissions in `ccm-auth-security`.

## The reference build to port

`docs/mockups/static-mockups/` is a static build running on fake data, used as a **visual
reference** and not run with the real application:

| Reference | Port to |
|---|---|
| `login.html` | `Views/Account/Login.cshtml` (no more fake account list) |
| `dashboard.html` | `Views/Home/Index.cshtml` (figures from `sp_DashboardSummary` and the views) |
| `customers.html` | `Views/Customers/Index.cshtml` (data from `sp_SearchCustomer`) |
| `js/layout.js` (sidebar, topbar, `NAV`) | `Views/Shared/_Layout.cshtml`; the menu is built server-side by role |
| `js/mock-data.js` | **Drop it**, do not carry it across |
| `js/tailwind-config.js`, `css/style.css` | `wwwroot/css/site.css` and the Tailwind config block in `_Layout` |

Keep the established style: Tailwind (CDN), Font Awesome, the Be Vietnam Pro font. Colours live in
CSS variables in `site.css` (`--primary`, `--bg-page`, `--bg-sidebar`, `--border`, `--text`,
`--success`, `--warning`, `--danger`...), and the Tailwind config maps them to the names
`primary`, `page`, `side`, `line`, `ink`, `muted`. Reuse the existing classes (`card`, `btn`,
`btn-primary`, `input`, `badge`, `b-success`, `b-warning`, `b-neutral`, `g-standard`, `g-gold`,
`num`, `tbl`, `nav-link`) and add new classes the same way; never hard-code a colour in a page.

The Tailwind CDN and Font Awesome CDN need Internet. That is fine for a demo; if the app must run
offline, download them into `wwwroot/lib` and point there.

## Layout and navigation

- `_Layout.cshtml` builds the sidebar and topbar, loads CSS/JS, has the `@RenderBody()` area and the
  single host, `Views/Shared/_ActionMessage.cshtml`, that turns the `TempData` message key of the last
  action into a toast (success) or an alert block (failure).
- The sidebar shows only items for the current role (`User.IsInRole`); the open item is highlighted
  from `ViewContext.RouteData`. The role matrix is in design.md section 5.
- The notification bell is a **ViewComponent** `Notifications` that calls a Service (counts
  expiring contracts, open and overdue feedback, overdue and today's care appointments), grouped per
  design.md 3.2. Never hard-code the numbers.
- The post-login landing page per role follows design.md. The generic error page and the
  `AccessDenied` page are built like any other page: their wording lives in the resource files.
- `_Topbar.cshtml` carries the language switch and the theme switch as two partials,
  `Views/Shared/_LanguageSwitch.cshtml` and `_ThemeSwitch.cshtml`, so both the shell and the sign-in
  page (which has no sidebar) can show them with one `<partial>` call.
- `_Layout.cshtml` writes `<html lang="@CultureInfo.CurrentUICulture.TwoLetterISOLanguageName"
  data-theme="@theme">`; the theme comes from `ISettingService.ResolveThemeAsync()`, decided on the
  server, so there is no flash.

## Language and theme

- The **default is Vietnamese**, English is the second language, and every visible string is a key in
  `Resources/SharedResource*.resx` (details in the `ccm-language` skill). A page never types a
  sentence, a label, a tooltip, `placeholder`, `title` or `aria-label` itself: even an attribute text
  is `@L["Key"]`. `Views/_ViewImports.cshtml` injects the localizer once as `L`, so a view uses
  `@L["Key"]` and `@L.Label("Role", role)`.
- Coded values (roles, statuses, contract/product types, theme and language codes) stay English in
  the database and in the posted form value; only what the user reads is translated. A hidden input
  therefore holds `value="Language"`, while the `<option>` next to it shows `Tiếng Việt`.
- **Never read a colour from a page.** Every colour comes from a CSS variable declared in
  `wwwroot/css/site.css`; `bg-white` is written `bg-card`. The dark theme is the same set of variable
  names re-declared under `[data-theme="dark"]`, so a new page is dark-ready for free. A page that
  hard-codes `#fff` or `bg-white` breaks in dark mode and fails review.
- The frame is three named surfaces and their tones live only in `site.css`: the sidebar (`.side`,
  `--bg-sidebar` / `--sidebar-fg` / `--sidebar-hover` / `--sidebar-line`, with `.side-brand`,
  `.side-foot`, `.side-name` for its rows), the topbar (`.topbar`, `--bg-topbar` / `--topbar-fg` /
  `--topbar-line`) and the sign-in panel (`.brand-panel`, `--bg-brand`). `_Sidebar.cshtml` and
  `_Topbar.cshtml` therefore carry no colour and no `text-white`; re-tinting the frame is an edit in
  `site.css` alone.
- A status or a choice always has text (or an icon) **together with** the colour - never colour
  alone - because the dark palette changes what a colour looks like.
- The two switches are plain submit buttons inside a small form with an anti-forgery token; they work
  with JavaScript disabled, so nothing in the switch path may depend on `site.js`.
- The hidden `returnUrl` comes from `Utils.ReturnUrlWithoutCulture(Context.Request)`, never straight
  from `Path + QueryString`: a `?culture=` left in the address would beat the cookie the switch has just
  written (the query-string provider is first on purpose), so the user would click and see nothing
  change. Every other query parameter stays, and the controller still checks `Url.IsLocalUrl`.
- Anything a preference touches must survive a failed database read: the layout falls back to the
  hard-coded defaults rather than showing an error page.

## Razor rules

1. Each page has its own **ViewModel** in `Models/ViewModels`; never pass an entity straight to a View.
2. Use tag helpers (`asp-for`, `asp-action`, `asp-route-*`, `asp-validation-for`). A POST form
   always has an anti-forgery token (the `<form>` tag helper adds it).
3. **Never use `Html.Raw` with user-entered data.** `@Model.CustomerName` is already encoded. Data
   passed to JavaScript uses `@Json.Serialize(...)`.
4. A View contains no DB queries and no business rules. Display calculations (labels, formatting,
   badges) go in `Helpers/Utils.cs` or a `_Badge` partial view.
5. Every visible string comes from a resource key (`ccm-language`). Format dates and money only
   through the `Utils` helpers: dates `dd/MM/yyyy` in both languages, money with the separators of
   the chosen language. Never format with the ambient server culture. `<input type="date">` always
   posts `yyyy-MM-dd`, so `DateOnly` model binding stays correct.
6. **Split every page into sections.** A page `.cshtml` holds only the page header plus the calls to
   its partials (`<partial name="_Xxx" model="..." />`); each block - filters, data table, form,
   summary card, tab panel, timeline, modal - is its own partial under `Views/<Controller>/`, and a
   block shared by several pages lives in `Views/Shared/`. Never build a whole page as one long
   `.cshtml`, and never copy a block between pages: extract a partial view or ViewComponent as soon
   as a list, form or action repeats. The `_Layout` is only the shell (sidebar, topbar, notification
   bell, `@RenderBody()`), never page content.

## Display conventions

- Wording comes from the resource files, never typed straight into a page; dates `dd/MM/yyyy`,
  times `HH:mm`, money right-aligned (class `num`).
- Badges for customer tier, contract status, priority and schedule status per design.md 2.2. Red is
  reserved for errors, cancellation and overdue. Every status has text (or an icon) together with
  the colour; never use colour as the only signal.
- Tables: paging 10/20/50, column sorting, an empty state with a description and an action button,
  and a dimmed grey skeleton while loading. Keep filters in the query string so going back and
  sharing a URL work.
- On success: redirect to the list (Post-Redirect-Get, so F5 does not resubmit) and show the result
  of the action as a **toast** through `window.ccmToast(message, 'ok' | 'err' | 'info')` (the classes
  `.toast-ok` / `.toast-err` / `.toast-info`), or as an **alert block** at the top of the content when
  the message must stay readable or points at a next action. The text is always a resource key, and
  one action shows exactly one message. Do **not** render a success banner inside the page body and do
  **not** stack a banner and a toast. The browser's native `alert()` / `confirm()` are never used; a
  confirmation goes through `_ConfirmDialog`. A field-level business error shows red on the form
  itself with the message from `BusinessException`, in the user's language, **keeping the entered
  data**.
- Dangerous actions (cancel a contract, deactivate a customer) need a confirmation dialog.
- Responsive: tables become card lists below 768px, the sidebar becomes a drawer.

## Form rules matching the DB

- A **Business** customer: hide Gender and Date of Birth and send NULL. At least an Email or a Phone
  is required. Phone is digits only, 9-11. **There is no customer-tier field** (the system ranks
  automatically).
- The 1-5 satisfaction score appears only when the care type is Survey. The next appointment date
  cannot be before the care date.
- Unassigned feedback: do not allow choosing "InProgress / Resolved / Closed". Status changes through
  a dedicated button that calls the procedure, not free editing.
- Buttons per contract status (design.md 6.8): Pending has Sign and Cancel; Active has Renew and
  Cancel; Expired has Renew; Cancelled has none.
- Choice lists (type, status, channel...) come from `WorkConstants`, matching the DB CHECK constraints.
  The stored value stays the English code; the `<option>` text comes from the resource key of
  `ccm-language` section 4, never from the code itself.
- Validate with DataAnnotations on the ViewModel plus client-side checks; the DB is still the final
  authority.

## Master-detail contract form

Top part: customer, end date, note. Bottom part: a product-line table added/removed with JavaScript
(choosing a product fills the unit price, the unit price stays editable for a discount, quantity is
entered, line total and grand total are shown). The total on the form is a preview only; the official
value is the `TotalValue` the DB computes after `sp_AddContract`.

List model binding needs continuously indexed field names: `Items[0].ProductID`, `Items[0].Quantity`,
`Items[0].UnitPrice`, `Items[1]...`. When removing a row in the middle, **re-index before
submitting**, otherwise rows after a gap are dropped. Do not allow submitting with no rows or a
duplicate product (the DB also blocks this with errors 50001, 50009).

## Checking a finished page

- Log in as `john`, `mary`, `david` in turn and check the menu and buttons match the role.
- Read the page in **both languages** and in **both themes**: no leftover English in the Vietnamese
  page, no white patch in dark mode (check a card, a table header, a badge, an input and the active
  menu entry), and a reload keeps the choice.
- All states are covered: with data, empty, business error (keeping the entered data).
- Enter `<img src=x onerror=alert(1)>` into a text field; only the text shows, no script runs.
- Narrow the window to phone size and the page still works; unicode and special characters round-trip
  correctly.
- No reference to `mock-data`, `localStorage('ccm_user')` or any fake data remains in the real code.
