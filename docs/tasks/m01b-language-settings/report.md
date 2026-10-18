# M01b - Vietnamese UI, English/Vietnamese switch and settings - report

Status: **code, SQL and tests are complete and verified; every item that was waiting has been handled
except the `PageSize` setting, which moves to M02.** Nothing was committed.

## 1. What was done

| Part | Result |
|---|---|
| Database | `SETTINGS` table (`EmployeeID IS NULL` = system default, a personal row wins) with three CHECKs and `UQ_Settings_Scope`; views `vw_SystemSettings` and `vw_EffectiveSettings` keep the "personal beats default" rule inside SQL; 2 seeded rows; all four scripts re-run successfully |
| Vietnamese data | `03_SampleData.sql` now seeds Vietnamese business data (departments, employees, customers, products, contract notes, contacts, care, feedback). Codes, e-mails, phones, usernames, statuses and the demo password are unchanged; row counts are identical to M01 |
| Localization | `Resources/SharedResource.resx` (Vietnamese, neutral) + `SharedResource.en.resx` (English), ~100 keys each, kept in step by unit tests; a missing key therefore falls back to Vietnamese. Every visible string of M01 now comes from a key - no page types its own text |
| Language switch | Cookie -> system default from the database -> (only then) the browser's `Accept-Language`; `?culture=` still works for a single request. The switch writes the framework's culture cookie and stores the choice as the employee's own row, so it follows the account, not the browser |
| Theme switch | `.CcmWeb.Theme` cookie plus a personal row, and a real dark palette in `site.css` (`[data-theme="dark"]`). No page holds a raw colour; the theme is decided on the server, so there is no flash and no `localStorage` |
| Settings screen | `/settings` (Manager only): the system defaults with a save button per row. What each employee chose for themselves is private and is **not** listed (the per-employee table was removed at your request - see section 7) |
| Sign-in | After signing in, the language and theme cookies are filled from `vw_EffectiveSettings`, so the account's own choice applies on the first page |
| Error messages | `TranslateSqlError` returns a resource **key** (50001-50015, 547, 2601/2627); the user reads a translated sentence and never the text SQL Server produced |
| Tests | 39 xUnit cases (17 new), all green against the real SQL Server test database |

## 2. Main paths

```
database/01_CreateTables.sql                          + SETTINGS
database/02_Function_View_Proc_Trigger.sql            + vw_SystemSettings, vw_EffectiveSettings
database/03_SampleData.sql                            Vietnamese business data + 2 settings rows
src/CcmWeb/SharedResource.cs                          marker type for IStringLocalizer
src/CcmWeb/Resources/SharedResource.resx              Vietnamese (neutral)
src/CcmWeb/Resources/SharedResource.en.resx           English
src/CcmWeb/Services/ISettingService.cs                contract (system defaults, effective values, saves)
src/CcmWeb/Services/SettingsService.cs                reads the views, writes the table, resolves the theme
src/CcmWeb/Services/SystemDefaultCultureProvider.cs   last culture provider: the database default
src/CcmWeb/Controllers/SettingsController.cs          GET /settings, POST /settings/system (Manager)
src/CcmWeb/Controllers/PreferencesController.cs       POST /preferences/language, /preferences/theme
src/CcmWeb/Models/ViewModels/SettingsViewModel.cs     the two tables of /settings
src/CcmWeb/Views/Settings/{Index,_SystemSettings}.cshtml
src/CcmWeb/Views/Shared/{_LanguageSwitch,_ThemeSwitch}.cshtml
src/CcmWeb/Views/**/*.cshtml                          every M01 string moved to a resource key
src/CcmWeb/Helpers/{WorkConstants,Utils,Navigation}.cs
src/CcmWeb/wwwroot/css/site.css                       variables + [data-theme="dark"]
docs/tasks/m01b-language-settings/*                   requirements, plan, test results, this report
tests/CcmWeb.Tests/{LocalizationTests,SettingsTests,UtilsTests,TriggerWriteTests}.cs
```

## 3. Verification that was actually run

- `dotnet build` -> 0 errors, 8 warnings (only the known NuGet audit ones, see `test-results.md` 5.1).
- `dotnet test` -> **39/39 PASS** on the real SQL Server database `CustomerCareDB_Test`, no InMemory
  or SQLite.
- `database/01`-`04` re-run from scratch: all exit 0, file 03 prints its `OK` line, and the row counts
  match the M01 baseline (4/8/10/8/16/39/17/12/9, plus 6 accounts and 2 settings rows).
- The 22 planned cases T01-T22 were executed against the running application over HTTP plus the
  database; the evidence for each one is in `test-results.md` section 3. Two rows are split
  honestly: money is formatted and unit-tested but shown on no screen yet, and the 500 page cannot be
  reached in Development.
- Extra security checks: a POST without the anti-forgery token -> `400`; a username of
  `<script>alert('xss')</script>` comes back escaped; both switches work with JavaScript disabled.
- Dark theme verified through the browser's computed styles (body, card, table head, badge, input,
  active menu entry), not just by the attribute.
- `tools/check-skills.ps1` -> 7 skills, 0 errors, 0 warnings.

## 4. BLOCKED / NOT RUN

| Item | Why |
|---|---|
| T17, money on a screen | No M01b page shows an amount; the helper is unit-tested and the first screen with money is the contract list (M03) |
| T19, the 500 page | In Development the developer exception page answers instead of `/error`; reaching it would mean changing the environment, which is out of scope |
| Screenshots of the light/dark screens | The browser tool cannot capture in this environment ("needs a visible tab"); the theme was verified by computed colour values instead |
| Mobile layout re-check | Same tool limitation, and M01b added no new layout - `/settings` reuses the card/table classes |

The 8 `NU1902`/`NU1903` warnings are no longer on this list: they were cleared in the second batch
(section 8).

## 5. Decisions taken with you

1. **BUG-01** - **fixed** in the second batch (section 8): the switch no longer keeps a `?culture=` in
   its return URL, and four unit tests cover the helper.
2. **`docs/design.md`** - **updated** in the second batch: the money example in 2.3, a new 2.4 with the
   dark palette as built, the page map, the permission matrix, the settings page in 6.13, the toast
   wording in 3.2 and the checklist line in 9. The palette itself lives in `site.css`; say the word if
   you want different colours and both places change together.
3. **The NuGet audit warnings** - **fixed** in the second batch by pinning the patched
   `System.Security.Cryptography.Pkcs` 10.0.13 in both projects.
4. **The per-employee table on `/settings`** - **removed** at your request (section 7): a colleague's own
   choice is private.
5. **`PageSize`** stays out of M01b and moves to M02, where the first paged list appears. Nothing else is
   waiting.

Deviations from the approved plan that are already built (switch controller and URLs, no
`data-auto-submit`, `HttpOnly = false` for the theme cookie, provider order, no `AddViewLocalization`,
the `HtmlEncoder` change, file names) are listed with a reason in `test-results.md` section 5.3 - say
the word if you would rather have any of them as planned.

## 6. How to see it

```powershell
Get-ChildItem database\*.sql | Sort-Object Name | ForEach-Object {
  if ($_.Name -like '04*') { sqlcmd -S ".\SQLEXPRESS" -E -C -I -f 65001 -b -v APP_PASSWORD="App_Str0ng!Pass2026" -i $_.FullName }
  else { sqlcmd -S ".\SQLEXPRESS" -E -C -I -f 65001 -b -i $_.FullName }
}
cd src\CcmWeb
dotnet run --launch-profile http      # http://localhost:5293
```

Sign in as `john` / `123456` (Manager) for `/settings`, or as `mary` (Sales) to see that the page is
refused while her own language and theme switches still work.

## 7. Change after the test round (18/10/2026)

At your request the settings page **no longer lists every employee's effective value**: a colleague's
own language and theme are private, so only the system defaults are shown on `/settings`, to Managers.
The rule itself is untouched - `vw_EffectiveSettings` still decides what each employee gets, and the
language and theme switches still store a personal row.

What changed:

| File | Change |
|---|---|
| `Views/Settings/_EmployeeSettings.cshtml` | deleted |
| `Views/Settings/Index.cshtml` | the `<partial name="_EmployeeSettings" ... />` call removed |
| `Models/ViewModels/SettingsViewModel.cs` | `EmployeeSettings`, `EmployeeSettingsRow` and `EffectiveSettingRow` removed; the doc comment now says why |
| `Controllers/SettingsController.cs` | no longer calls `GetEmployeeSettingsAsync` |
| `Services/ISettingService.cs`, `SettingsService.cs` | `GetEmployeeSettingsAsync` removed (`GetEffectiveValueAsync` stays - the theme and the culture provider need it) |
| `Resources/SharedResource{,.en}.resx` | the six keys used only by that table removed (`Settings_EmployeeTitle`, `Settings_EmployeeHint`, `Settings_Column_Employee`, `Settings_Column_Source`, `Settings_Source_Personal`, `Settings_Source_Default`) |

Re-verified after the change: `dotnet build` -> 0 errors / 8 known warnings; `dotnet test` -> **39/39
PASS** (the localization tests prove both resource files still carry the same keys and that no view
references a removed key); over HTTP, `/settings` as Manager renders exactly **one** table in both
languages (`Mặc định của hệ thống` / `System defaults`, columns `Cài đặt | Giá trị | Cập nhật`), the
columns and badges of the removed table are gone, and the personal-row count in `SETTINGS` was 0
before and after. A fresh visitor still gets `<html lang="vi" data-theme="light">`.

Also checked while doing this: the theme **switch** writes only the employee's own row (the system
default stayed `light` while the employee's row became `dark`), so the switch and the settings page
never touch the same row. The system default of `Theme` had been left at `dark` by a save from the
settings page; it is back to the seeded `light`.

## 8. Second batch of changes (18/10/2026)

Approved with "ok làm tiếp".

1. **BUG-01 fixed.** `Helpers/Utils.cs` gained `ReturnUrlWithoutCulture(HttpRequest)`; the language and
   theme switch partials call it, so a `?culture=` in the address bar is dropped from the return URL
   while the rest of the query survives. Four new unit tests cover it (`UtilsTests`), and the case that
   started it all now behaves: `GET /login?culture=en` offers `returnUrl=/login`, and pressing `VI`
   really turns the page Vietnamese. Details in `bug-report.md`.
2. **The NuGet audit warnings are gone.** Both projects pin `System.Security.Cryptography.Pkcs`
   10.0.13 (the version the advisory names for the 10.0 line) with a comment explaining that
   `Microsoft.Data.SqlClient` 6.1.7 would otherwise pull the vulnerable 9.0.11. A solution build is now
   `0 Warning(s), 0 Error(s)`.
3. **`docs/design.md` is in line with the built interface.** Section 2.3 shows the money and time format
   of both languages; a new **2.4 Dark theme** carries the dark palette as it exists in `site.css` plus
   the rules (a soft badge becomes a deep tint with a light counterpart text, the primary is lightened,
   a status is never colour alone); 3.2 no longer claims the SQL message is shown verbatim; the page map
   (4) and the permission matrix (5.1) list `/settings` and `/preferences/*`; 6.13 describes the
   settings page and says a personal choice is private; the checklist line in 9 now asks for a message in
   the language the user chose.

Verification of this batch: `dotnet build CcmWeb.sln` -> **0 errors, 0 warnings**; `dotnet test` ->
**43/43 PASS** against real SQL Server (so the package override is proven on the SqlClient paths the app
uses); the package graph shows `System.Security.Cryptography.Pkcs 10.0.13` with
`Microsoft.Data.SqlClient 6.1.7` unchanged; and over HTTP the switch now works on a `?culture=` address
while other parameters are preserved. The M01b functionality itself did not change in this batch.

## 9. Frame tones (18/10/2026, user request)

The two bars were re-tinted on request: the sidebar a lighter tone and the topbar a deeper one, in the
**same slate family** rather than near-black next to white. The value was picked from a five-variant
preview page rendered by the running application (a temporary file under `wwwroot`, deleted afterwards),
because the browser here can show a page but the user has to look at it.

| Surface | Light theme | Dark theme |
|---|---|---|
| Sidebar | `#334155` (slate-700) with `#E2E8F0` text | `#1E293B` (slate-800, a raised panel, no longer `#070C17`) |
| Topbar | `#E2E8F0` (slate-200) with `#0F172A` text and a `#CBD5E1` bottom edge | `#151E31` unchanged |
| Sign-in panel | `#334155` (follows the sidebar) | `#1E293B` |
| Hover / edges | `--sidebar-hover` white 12%, `--sidebar-line` white 16% | unchanged from the built dark palette |

Two structural points came out of it, and both are the reason the change stayed small:

- The three surfaces have their own names and text colours (`--bg-sidebar` / `--sidebar-fg` /
  `--sidebar-hover` / `--sidebar-line`, `--bg-topbar` / `--topbar-fg` / `--topbar-line`, `--bg-brand`),
  so no partial carries a colour and each bar can be re-tinted by editing `site.css` alone. The sidebar
  footer and the brand row no longer use `text-white` and `border-white/10`.
- The controls on the bar take their outline from `--topbar-line` (`.topbar .btn, .topbar .input`), or a
  card-coloured chip would disappear into the grey bar of the light theme.

The first attempt **swapped** the two surfaces (white sidebar, dark topbar); it was reverted the moment
the user said that was not the intent.

Verified in the browser (computed styles, both themes): light - page `#F8FAFC`, sidebar `rgb(51,65,85)`
with `rgb(226,232,240)` text and a white-16% right edge, topbar `rgb(226,232,240)` with `rgb(15,23,42)`
text and a `rgb(203,213,225)` bottom edge, a white chip on it edged `rgb(203,213,225)`; dark - page
`#0B1220`, sidebar `rgb(30,41,59)`, topbar `rgb(21,30,49)`, chip edged `rgb(39,51,77)`; the theme cookie
was put back to `light` afterwards. `docs/design.md` (2.1, 2.4, 3.2) carries the new values.
`dotnet build` 0 errors / 0 warnings, `dotnet test` 43/43 PASS, and the app renders both themes.
