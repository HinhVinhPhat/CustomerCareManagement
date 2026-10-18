# M01b - Vietnamese UI, English/Vietnamese switch and settings - plan

Companion to `requirements.md`. **One approval covers both documents.** Steps run in order; if
something outside the approved scope shows up, work stops and the user is asked. Order inside a step
follows `AGENTS.md` section 3: SQL -> Service -> Controller -> Views.

> **Status (18/10/2026).** Implemented, tested and handed over - see `test-results.md`, `bug-report.md`
> and `report.md`. Four things changed after this plan was approved: the switches moved to
> `PreferencesController` with the URLs `/preferences/language` and `/preferences/theme` and became
> plain submit buttons (no `data-auto-submit`); the per-employee effective-value table on `/settings`
> was **removed** at the user's request (a colleague's own choices are private); BUG-01 (a `?culture=`
> parameter kept in the switch's return URL) was fixed in `Utils.ReturnUrlWithoutCulture`; and both
> projects pin the patched `System.Security.Cryptography.Pkcs` 10.0.13. All are listed in
> `test-results.md` sections 5.3, 6 and 7.

## 1. Steps

### Step 1 - database first

Edit the SQL first, then run all four files and re-scaffold the model.

1. `database/01_CreateTables.sql` - add `SETTINGS` (definition in `requirements.md` 3.1) at the end of
   the table list, after `AUDIT_LOG`, so the `EMPLOYEE` foreign key already exists.
2. `database/02_Function_View_Proc_Trigger.sql` - add `vw_SystemSettings` and `vw_EffectiveSettings`
   in the views section, each in its own batch (`CREATE VIEW` must be the first statement of a
   batch), and add a short comment above them saying what the fallback rule is. **No grant change**:
   line 576 grants `SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo` to `role_Manager`, and a
   schema-level grant also covers objects created later, so `app_ccm` (a `role_Manager` member) can
   read and write `SETTINGS`. Sales and Support have per-object grants and must **not** receive
   anything here.
3. `database/03_SampleData.sql` - add the 2 seeded system rows (Step 6 covers the rest of this file).

Then, against `.\SQLEXPRESS`:

```powershell
Get-ChildItem database\*.sql | Sort-Object Name | ForEach-Object {
  if ($_.Name -like '04*') {
    sqlcmd -S ".\SQLEXPRESS" -E -C -I -f 65001 -b -v APP_PASSWORD="App_Str0ng!Pass2026" -i $_.FullName
  } else {
    sqlcmd -S ".\SQLEXPRESS" -E -C -I -f 65001 -b -i $_.FullName
  }
}
```

File `01` drops and recreates `CustomerCareDB`, so afterwards the demo `PasswordHash` values are back
to the SHA2 form and `john` re-hashes on the next sign-in - known and expected.

4. Re-scaffold (the command is in `docs/tasks/m01-foundation/plan.md` Step 4) and confirm that
   `ApplicationDbContext.Custom.cs` (trigger configuration) and `SessionContextInterceptor.cs` were
   not touched: they live outside the generated files.
5. Update `.agents/skills/ccm-database/references/schema.md` with `SETTINGS` (columns, FK, UNIQUE,
   the three CHECKs), the two views, and the seeded rows.

### Step 2 - localization infrastructure

1. `src/CcmWeb/SharedResource.cs` - the empty marker class in namespace `CcmWeb`.
2. `src/CcmWeb/Resources/SharedResource.resx` (Vietnamese) and `Resources/SharedResource.en.resx`
   (English). Added as `EmbeddedResource` automatically by the SDK; no `.Designer.cs`, the localizer
   reads the files. Both files stay UTF-8 so Vietnamese diacritics round-trip.
3. `Views/_ViewImports.cshtml` - add `@using CcmWeb` and `@inject IStringLocalizer<SharedResource> L`
   so every view can use `@L["..."]` (see Risks: verified during implementation).
4. `Program.cs`:
   - `AddLocalization(options => options.ResourcesPath = "Resources")`;
   - `AddControllersWithViews().AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
     .AddDataAnnotationsLocalization(...)` pointing at `SharedResource`;
   - `Configure<RequestLocalizationOptions>`: `SetDefaultCulture("vi")`, supported cultures and UI
     cultures `vi`, `en`, providers in the order query string -> cookie -> `Accept-Language`
     (the framework default order, which keeps `?culture=en` usable for tests while the topbar writes
     the cookie);
   - `app.UseRequestLocalization()` after `UseStaticFiles()` and before `UseRouting()`.

### Step 3 - Settings service

1. `Helpers/WorkConstants.cs` - add `Settings` (keys `Language`, `Theme`, values `vi`/`en`,
   `light`/`dark`) and the password of nothing else. A list is added here only when a screen needs
   it, so no tiers/statuses until the module that shows them.
2. `Helpers/Utils.cs` - replace the hard-coded English messages:
   - `BusinessException` carries a resource **key** plus arguments, never a finished sentence;
   - `TranslateSqlError` returns the key (`Error_FeedbackNeedsAssignee`, `Error_Duplicate`,
     `Error_BusinessRule`, ...) for 50001-50015, 547, 2601/2627 and `null` when unknown;
   - one generic code-to-label helper (`Utils.Label(localizer, group, code)`) used for the setting
     values now and by the later modules for tier/status/role labels;
   - keep the money/date helpers and make them take the culture explicitly.
3. `Services/ISettingService.cs` + `SettingsService.cs`:
   - `GetSystemDefaultsAsync()` over `vw_SystemSettings`;
   - `GetEffectiveForAsync(employeeID)` over `vw_EffectiveSettings` (the fallback rule is in the
     view, not in C#);
   - `SaveForUserAsync(employeeID, key, value)` and `SaveSystemDefaultAsync(key, value)`;
   - validate the key and the value against `WorkConstants.Settings` **before** the database call,
     and translate a 547 that still gets through;
   - return small records/ViewModels, never tracked entities (`AGENTS.md` section 3).

### Step 4 - switches and the `/settings` page

1. `Controllers/SettingsController.cs`
   - `POST /settings/language` and `POST /settings/theme`: `[AllowAnonymous]` (the sign-in page needs
     them), anti-forgery token, value checked against the supported list, local `returnUrl` only,
     cookie written, and the row saved **only** when `User.Identity` is authenticated and the
     `employeeID` claim exists - never an id from the form;
   - cookie: the framework's `.AspNetCore.Culture` written with
     `CookieRequestCultureProvider.MakeCookieValue` for the language, a plain `.CcmWeb.Theme` cookie
     for the theme (1 year, `HttpOnly`, `SameSite=Lax`);
   - `GET /settings` + `POST /settings/system`: `[Authorize(Roles = "Manager")]`.
2. Views, split per `AGENTS.md` section 5: `Views/Settings/Index.cshtml` (header and the `<partial>`
   calls only) + `Views/Settings/_SystemDefaults.cshtml` + `_EmployeeOverrides.cshtml`.
   *(Built as `_SystemSettings.cshtml` + `_EmployeeSettings.cshtml`. The per-employee table was then
   **removed after the test round at the user's request** - a colleague's own choices are private - so
   `/settings` now carries the system defaults only. Recorded in `test-results.md` section 6.)*
3. Topbar: `Views/Shared/_LanguageSwitch.cshtml` and `Views/Shared/_ThemeSwitch.cshtml`, each a small
   form, included from `_Topbar.cshtml`; auto-submit on change through a `data-auto-submit` handler in
   `wwwroot/js/site.js` (no inline JavaScript).
4. `Helpers/Navigation.cs` + `Views/Shared/_Sidebar.cshtml`: the menu entry now carries a **resource
   key** and the sidebar renders `@L[item.TitleKey]`; the Settings item is Manager-only.
5. `_Layout.cshtml`: `<html lang="@CultureInfo.CurrentUICulture.TwoLetterISOLanguageName"
   data-theme="@theme">`, with the theme resolved from the cookie/DB, so the first paint is correct.

### Step 5 - move every M01 string to a resource key

Files to convert (visible text only): `Views/Shared/_Layout.cshtml`, `_Sidebar.cshtml`,
`_Topbar.cshtml`, `_HeadAssets.cshtml`, `Views/Shared/Error.cshtml`,
`Views/Shared/Components/NotificationBell/Default.cshtml`, `Views/Account/Login.cshtml`,
`Views/Home/Index.cshtml`, `Views/Home/AccessDenied.cshtml`, `Models/ViewModels/LoginViewModel.cs`
(`ErrorMessage` holds a key), and the messages produced in `Controllers/AccountController.cs` and
`Services/AccountService.cs`.

Both files get the same keys at the same time - Vietnamese in the neutral file, English in
`.en.resx`. A key that has no Vietnamese text yet is not written at all, so nothing half-translated
ships.

### Step 6 - Vietnamese sample data (`database/03_SampleData.sql`)

Text values only, in this order: `DEPARTMENT` names, `EMPLOYEE` full names, `CUSTOMER` names and
addresses and sources, `PRODUCT_SERVICE` names/descriptions, `CONTRACT` notes, `CONTACT` subjects and
content, `CARE` notes, `FEEDBACK` titles and content. Usernames, e-mail addresses, phone numbers,
codes, statuses, roles and the demo password stay untouched; every `NVARCHAR` literal keeps its `N`
prefix.

Verification: row counts per table identical before and after, the block at the end of the file still
prints `OK: sample data loaded, TotalValue and Tier match the triggers.`, and the UI shows the
accents correctly (no mojibake).

### Step 7 - theme in CSS

`wwwroot/css/site.css`:

1. Move the remaining raw colours into variables (`--bg-input`, `--bg-table-head`, `--on-success`,
   `--on-info`, `--on-warning`, `--on-danger`, `--on-neutral`, and a background/text pair for each of
   the four tier badges), so `.btn`, `.input`, `.tbl th` and the badge classes stop hard-coding white,
   `#F8FAFC` and the dark text colours.
2. Add `:root[data-theme="dark"] { ... }` with the palette from `requirements.md` 3.6.
3. Nothing else in the pages changes - no page gains a raw colour value.

### Step 8 - tests

Unit tests in `tests/CcmWeb.Tests` (added to the existing suite, which keeps its 22 cases):

- resource files: both carry the same key set, no empty value, and every `@L["Key"]` found in
  `src/CcmWeb/Views/**/*.cshtml` exists in both files;
- Vietnamese text round-trips through the localizer with its accents intact;
- `TranslateSqlError` returns the expected keys and `null` for an unknown code (the M01 tests change
  from English sentences to keys);
- money and date formatting in `vi` and `en` (`80.000.000 VNĐ`, `80,000,000 VND`, `dd/MM/yyyy`);
- real test database: the system default is read through `vw_SystemSettings`; a personal row beats it
  through `vw_EffectiveSettings`; an illegal value is refused (547) and mapped to a message key.

### Step 9 - verify and close the module

1. `dotnet build` and `dotnet test`.
2. Manual round against the real application pointing at `CustomerCareDB_Test`, in **both** languages:
   T01-T22 below, then `docs/tasks/m01b-language-settings/test-results.md`.
3. `docs/design.md`: section 2.3 money example, the new section 2.4 dark palette, and the section 9
   checklist line about English messages.
4. `docs/tasks/m01b-language-settings/report.md`.

## 2. Files to create

```
src/CcmWeb/SharedResource.cs
src/CcmWeb/Resources/SharedResource.resx                   (Vietnamese, neutral)
src/CcmWeb/Resources/SharedResource.en.resx                (English)
src/CcmWeb/Services/ISettingService.cs
src/CcmWeb/Services/SettingsService.cs
src/CcmWeb/Controllers/SettingsController.cs
src/CcmWeb/Models/ViewModels/SettingsViewModel.cs
src/CcmWeb/Models/ViewModels/SystemSettingViewModel.cs
src/CcmWeb/Views/Settings/Index.cshtml
src/CcmWeb/Views/Settings/_SystemDefaults.cshtml
src/CcmWeb/Views/Settings/_EmployeeOverrides.cshtml
src/CcmWeb/Views/Shared/_LanguageSwitch.cshtml
src/CcmWeb/Views/Shared/_ThemeSwitch.cshtml
tests/CcmWeb.Tests/LocalizationResourceTests.cs
tests/CcmWeb.Tests/SettingsTests.cs                        (real test DB)
docs/tasks/m01b-language-settings/test-results.md          (after the test round)
docs/tasks/m01b-language-settings/report.md                (after the test round)
```

## 3. Files to edit

```
database/01_CreateTables.sql                               + SETTINGS
database/02_Function_View_Proc_Trigger.sql                 + 2 views
database/03_SampleData.sql                                 + 2 seed rows, Vietnamese business data
src/CcmWeb/Program.cs                                      localization, middleware order
src/CcmWeb/Helpers/Utils.cs                                keys instead of English sentences, helpers
src/CcmWeb/Helpers/WorkConstants.cs                        Settings keys and values
src/CcmWeb/Helpers/Navigation.cs                           menu titles become resource keys
src/CcmWeb/Controllers/AccountController.cs                message keys
src/CcmWeb/Controllers/HomeController.cs                   error page resolves a key
src/CcmWeb/Services/AccountService.cs                      message keys
src/CcmWeb/Views/_ViewImports.cshtml                       @inject the localizer
src/CcmWeb/Views/Shared/_Layout.cshtml                     lang, data-theme
src/CcmWeb/Views/Shared/_Sidebar.cshtml                    localized titles
src/CcmWeb/Views/Shared/_Topbar.cshtml                     includes the two switches
src/CcmWeb/Views/Shared/Error.cshtml                       localized
src/CcmWeb/Views/Shared/Components/NotificationBell/Default.cshtml
src/CcmWeb/Views/Account/Login.cshtml
src/CcmWeb/Views/Home/Index.cshtml
src/CcmWeb/Views/Home/AccessDenied.cshtml
src/CcmWeb/Models/ViewModels/LoginViewModel.cs
src/CcmWeb/Models/Entities/*, Models/EF/ApplicationDbContext.cs   (scaffold output, generated)
src/CcmWeb/wwwroot/css/site.css                            variables + dark palette
src/CcmWeb/wwwroot/js/site.js                              data-auto-submit
tests/CcmWeb.Tests/UtilsTests.cs                           keys and per-culture formatting
tests/CcmWeb.Tests/LegacyHashTests.cs                      unchanged
docs/design.md                                             2.3, new 2.4, section 9 line
.agents/skills/ccm-database/references/schema.md           new table and views
```

`AGENTS.md` sections 1, 2, 5 and the `ccm-language` skill were already updated as part of this
request, before the module was written.

## 4. Planned test cases

The manual round runs against `CustomerCareDB_Test` (a fresh copy of the four SQL files with the
database name replaced, `sqlcmd -I -f 65001`), with the application pointed at it through User
Secrets.

| Id | Case | How | Expected |
|---|---|---|---|
| T01 | Clean build | `dotnet build` | 0 errors (the 3 known NU1902/NU1903 warnings may remain) |
| T02 | Fresh database | run files 01-04 on a new DB | `SETTINGS`, `vw_SystemSettings`, `vw_EffectiveSettings` exist; 2 seeded rows; file 03 prints `OK` |
| T03 | Resource files in step | unit test | same key set in both files, no empty value, every `@L["..."]` key in the views resolves |
| T04 | Vietnamese by default | open `/login` with no cookie | Vietnamese text, `<html lang="vi">`, no English left |
| T05 | Switch to English | topbar switch -> English | whole page English (sidebar, topbar, page, buttons), cookie set, reload stays English |
| T06 | Query string | `/login?culture=en`, then `?culture=fr` | English, then falls back to Vietnamese |
| T07 | Illegal value in the form | POST `/settings/language` with `culture=fr` | refused, no row written, stays Vietnamese |
| T08 | Signed in, personal row | switch to English, sign out, sign in | still English (row in `SETTINGS`) |
| T09 | Another browser | sign in from a clean browser | English is applied from the database row on the first page |
| T10 | Anonymous | no cookie, open the sign-in page | Vietnamese, and `SETTINGS` gains no row |
| T11 | Override beats default | SQL: system `Language='vi'` + personal `Language='en'` | `vw_EffectiveSettings` returns `en` with `IsOverridden = 1` |
| T12 | Manager changes the default | set the system `Theme='dark'` on `/settings` | an employee with no personal row gets dark; one with a personal row keeps their value |
| T13 | The database refuses a bad value | `UPDATE SETTINGS SET SettingValue='fr' WHERE SettingKey='Language' AND EmployeeID IS NULL` | CHECK violation; through the application a Vietnamese message with no SQL detail |
| T14 | Dark theme | switch to dark | whole screen dark with no white patch, badges and buttons readable, survives a reload |
| T15 | `/settings` permissions | Manager, then Sales, then Support | 200 for Manager; 403 page for the other two and no menu item |
| T16 | No hard-coded wording | grep `src/CcmWeb/Views` for visible text outside resource keys | nothing found in either language |
| T17 | Formatting per language | dates and money on the screen in both languages | `dd/MM/yyyy`, `80.000.000 VNĐ` / `80,000,000 VND` |
| T18 | Validation message | submit the sign-in form empty in both languages | message follows the chosen language, field keeps the entered data |
| T19 | M01 regression | login, logout, 403 page, 500 page, notification bell counts | all still work, now in the chosen language |
| T20 | Vietnamese sample data | open the customer/product data through the app and a query | accents are correct, row counts identical, triggers still compute `TotalValue`/`Tier` |
| T21 | No flash | view source of a page loaded with `data-theme="dark"` | `lang`/`data-theme` come from the server, no `localStorage`, no theme script |
| T22 | Settings are not audited | change a setting, then read `AUDIT_LOG` | no new row (only the contract and feedback triggers write it) |

A case that cannot be run is recorded as BLOCKED or NOT RUN with the reason, never ticked.

## 5. Risks and open points

1. **`@inject` in `_ViewImports.cshtml`** - if the Razor compiler refuses the directive there, the
   fallback is `@inject` at the top of each view (a one-line change per file, decided in Step 2 and
   reported).
2. **DataAnnotations messages** - `AddDataAnnotationsLocalization` must actually pick up
   `ErrorMessage` as a key; T18 proves it. If it does not, the fallback is to validate the message key
   in the ViewModel through the localizer before rendering.
3. **Re-scaffold** overwrites `Models/EF/ApplicationDbContext.cs` and `Models/Entities/*`; the
   trigger configuration and the interceptor live in other files, and Step 1 point 4 re-checks them
   (`AGENTS.md` section 4 - the error 334 trap).
4. **File 01 drops the database**, so the live demo hashes reset; only the dev database is affected
   and the test database is rebuilt anyway (`ccm-workflow`).
5. **Text-only sample data conversion** is the one step where a machine cannot decide the wording; it
   is reviewed by hand and the row counts are compared.
6. The 3 `NU1902`/`NU1903` warnings from the transitive `System.Security.Cryptography.Pkcs` 9.0.11
   stay untouched - fixing them needs a direct package reference, which needs the user's approval and
   is not part of this module.
7. Settings changes are **not** audited (no trigger, `AGENTS.md` section 4). If the user wants a trail
   for who changed a default, that is extra scope and a separate approval.
8. The dark palette is new content for `design.md`; if the user prefers different colours, section 2.4
   changes and the CSS follows.
