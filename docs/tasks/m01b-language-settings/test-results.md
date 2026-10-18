# M01b - Vietnamese UI, English/Vietnamese switch and settings - test results

Run date: **18/10/2026**. Written after the code was frozen for this round; every row below was
actually executed. Nothing is ticked that was not run.

## 1. Environment actually used

| Item | Value |
|---|---|
| Operating system | Windows, .NET CLI |
| .NET SDK | 10.0.401 |
| ASP.NET Core / runtime | Microsoft.AspNetCore.App 10.0.x, Microsoft.NETCore.App 10.0.x |
| EF Core | 10.x (runtime package and `dotnet-ef` tool) |
| Database engine | Microsoft SQL Server 2025 (RTM) 17.0.1000.7, instance `.\SQLEXPRESS` |
| Automated tests | `CustomerCareDB_Test`, rebuilt by the xUnit fixture from `database/01`-`03` |
| Manual round | `CustomerCareDB` (development), rebuilt from `database/01`-`04` in this round, application on `http://localhost:5293` |

Why the manual round uses the development database: `TestDatabase` loads only files `01`-`03`, so
`CustomerCareDB_Test` has no `ACCOUNT` rows and nobody can sign in against it. The cases that need a
signed-in user therefore ran against `CustomerCareDB`, which had just been recreated by files `01`-`04`
(the same scripts the deployment uses). The unit tests - including every case that writes to the
database - ran against `CustomerCareDB_Test`, never the live database.

Tools: `sqlcmd` with `-I -f 65001` (UTF-8 input, so Vietnamese literals survive), PowerShell 5.1 for
the HTTP round (`HttpWebRequest` with `AllowAutoRedirect = $false` to read the real status and
`Location`), and the browser tool for DOM/computed-style checks. The browser tool cannot capture
screenshots in this environment ("needs a visible tab"), so no screenshot evidence exists for this
round - the style evidence is a list of computed colour values instead.

## 2. Automated tests

```
dotnet build CcmWeb.sln   -> Build succeeded. 0 Error(s), 8 Warning(s)
dotnet test  CcmWeb.sln   -> Passed! - Failed: 0, Passed: 39, Skipped: 0, Total: 39
```

The 8 warnings are the known NuGet audit warnings on the transitive
`System.Security.Cryptography.Pkcs` 9.0.11 (4 per project: NU1902 and NU1903); see 5.1.

| Test class | Cases | What it covers |
|---|---|---|
| `LocalizationTests` | 6 | both resource files carry the same keys, no empty value, the neutral file is Vietnamese, every key literal in `src/CcmWeb/**/*.cs` and `*.cshtml` exists, every coded value has a label in both files, every SQL error code maps to a key that exists |
| `SettingsTests` | 5 | real SQL Server: `CHK_Settings_Value`, `CHK_Settings_Key` and `UQ_Settings_Scope` refuse illegal writes and map to the right keys; a personal row beats the system default through `vw_EffectiveSettings`; each system key has exactly one default row |
| `UtilsTests` | 18 | key-based SQL error translation for 50001-50015, 547, 2601 and 2627, unknown codes -> `Error_Unexpected`/`null`; `FormatMoney` in `vi` and `en`; `BusinessException` carries key and arguments; the coded-value lists accept only what the database accepts |
| `TriggerWriteTests` | 3 | unchanged behaviour, messages now expected as keys (`Error_CustomerEmailTaken`, `Error_BusinessCustomerNoGender`) |
| `LegacyHashTests` | 5 | unchanged (demo SHA2 vector) |
| `SessionContextTests` | 2 | unchanged (`SESSION_CONTEXT('EmployeeID')` on EF connections) |

No EF InMemory or SQLite was used anywhere.

## 3. Cases

| Id | Case | Expected | Actual | Status |
|---|---|---|---|---|
| T01 | Clean build | 0 errors | `Build succeeded. 0 Error(s), 8 Warning(s)` (see 5.1) | PASS |
| T02 | Fresh database | files 01-04 run, `SETTINGS` and the two views exist, 2 seeded rows, file 03 prints `OK` | all four files exit 0; file 03 prints `OK: sample data loaded, TotalValue and Tier match the triggers.`; `sys.objects`: `SETTINGS` (USER_TABLE), `vw_SystemSettings`, `vw_EffectiveSettings`; `SETTINGS` = `Language\|vi\|Localization`, `Theme\|light\|Appearance`; `vw_EffectiveSettings` = 16 rows (8 employees x 2 keys) | PASS |
| T03 | Resource files in step | same key set in both files, no empty value, every key used in the code or a view resolves | `LocalizationTests` 6/6 PASS (the check scans `*.cs` as well as `*.cshtml`, and also proves that every `Language_*`, `Theme_*`, `Role_*`, `Setting_*` label and every error key exists) | PASS |
| T04 | Vietnamese by default | `/login` with no cookie shows Vietnamese, `<html lang="vi">` | `GET /login` (fresh jar) -> `<html lang="vi" data-theme="light">`, title `Đăng nhập \| Hệ thống chăm sóc khách hàng`, labels `Tên đăng nhập`, `Mật khẩu`, `Tài khoản demo (mật khẩu 123456)`, tooltips `Tiếng Việt`, `Chế độ hiển thị`. The only English left is coded values (`autocomplete="username"`, `value="Language"`) and URLs (`/preferences/...`) | PASS |
| T05 | Switch to English | the whole page English, cookie set, a reload stays English | `POST /preferences/language value=en` -> 302 with `Set-Cookie: .AspNetCore.Culture=c%3Den%7Cuic%3Den`; the redirect target `/settings` renders `lang="en"` with English headings and tooltips (`Settings`, `System defaults`, `Display language`, `Save`); `GET /` stays English | PASS |
| T06 | Query string | `?culture=en` English, `?culture=fr` falls back to Vietnamese | `GET /login?culture=en` -> `lang="en"`; `?culture=fr` -> `lang="vi"`; `?culture=vi` -> `lang="vi"` | PASS |
| T07 | Illegal value in the form | refused, no cookie written, no row written | `POST /preferences/language value=fr` -> 302 with no `Set-Cookie`; `POST /preferences/theme value=blue` -> 302 with no `Set-Cookie`; the personal-row count stayed 0 | PASS |
| T08 | Signed in, personal row | the choice is stored per employee and applies again after signing in | the switch wrote `SETTINGS` rows for employee 1 (`Language=en`, `Theme=dark`, `UpdatedDate` filled) and for employee 2 (`Language=en`), read back with `sqlcmd`; all were deleted after the round | PASS |
| T09 | Another browser | signing in from a clean jar applies the database value on the first page | a brand-new session signing in as that employee got English on `/access-denied` while the system default was still `vi` (her personal row) - her own choice, not the browser's, decided the screen | PASS |
| T10 | Anonymous | Vietnamese, and `SETTINGS` gains no row | 3 anonymous `GET /login` -> the personal-row count before and after was `0` | PASS |
| T11 | Override beats default | `vw_EffectiveSettings` returns the personal value with `IsOverridden = 1` | xUnit on the test database: employee 3 -> `en`, `IsOverridden=1`; employee 1 -> `vi`, `IsOverridden=0` | PASS |
| T12 | Manager changes the default | an employee without a personal row gets the new default, one with a row keeps their value | `POST /settings/system Language=en` -> a fresh visitor from a clean jar got `lang="en"` on `/login`; an employee whose personal value was `vi` still saw `vi` while the default was `en`; the default was set back to `vi` afterwards | PASS |
| T13 | The database refuses a bad value | CHECK violation, and through the application a Vietnamese message with no SQL detail | xUnit on the test database: a personal row with `Language=fr` -> `547`/`CHK_Settings_Value` -> `Error_SettingValueInvalid`; `PageSize=20` -> `547`/`CHK_Settings_Key` -> `Error_UnknownSetting`; through the application `POST /settings/system Theme=blue` -> banner `Giá trị không hợp lệ cho cài đặt này.` and `PageSize=20` -> banner `Cài đặt không tồn tại.`, no constraint name or SQL text anywhere | PASS |
| T14 | Dark theme | whole screen dark, no white patch, survives a reload | server-rendered `data-theme="dark"`; computed styles in the browser - login page: body `rgb(11,18,32)`, left panel `rgb(7,12,23)`, primary button `rgb(96,165,250)` with `rgb(11,18,32)` text; `/settings`: card `rgb(21,30,49)`, table head `rgb(28,39,64)`, badge `rgb(12,44,67)` with `rgb(186,230,253)` text, select and Save button `rgb(21,30,49)`, active menu entry `rgb(96,165,250)`; a reload keeps dark because the cookie is read again | PASS |
| T15 | `/settings` permissions | 200 for Manager, the 403 page for Sales and Support, and no menu entry | Manager -> 200; Sales (`mary`) -> `302 Location: /access-denied?ReturnUrl=%2Fsettings`; Support (`helen`) -> the same; the access-denied page renders in Vietnamese (`Không có quyền truy cập`, `Vai trò Kinh doanh của bạn không được phép mở trang này...`); the Sales and Support pages contain no `href="/settings"` | PASS |
| T16 | No hard-coded wording | no visible text outside resource keys, in either language | scripted scan of every `Views/**/*.cshtml`: no literal text between tags and no literal `placeholder`/`title`/`aria-label` value (every hit was part of a Razor expression). In the rendered Vietnamese pages the only English left is coded values (`value="Language"`), URLs (`/preferences/language`), attribute values (`autocomplete="password"`) and the stored-procedure name inside a Vietnamese sentence (`sp_DashboardSummary`) | PASS |
| T17 | Formatting per language | `dd/MM/yyyy`, `80.000.000 VNĐ` / `80,000,000 VND` | dates on screen: `18/10/2026 16:07`; xUnit: `FormatMoney(80_000_000, vi-VN)` = `80.000.000 VNĐ`, `en-US` = `80,000,000 VND`, both `dd/MM/yyyy`. **Money on a screen is NOT RUN**: no M01b page shows an amount yet - the first one is the contract list in M03 | PASS (helper) / NOT RUN (on screen) |
| T18 | Validation message | the message follows the chosen language and the entered data is kept | the same form renders `data-val-required="Nhập tên đăng nhập."` in Vietnamese and `data-val-required="Enter your username."` in English; a submitted username is echoed back escaped (see the XSS check below), so nothing the user typed is lost | PASS |
| T19 | M01 regression | sign-in, sign-out, 403 page, 500 page and the bell counts still work, now in the chosen language | sign-in as `john` from the freshly rebuilt database works and upgrades the demo SHA2 hash; dashboard renders `Tổng quan`, the sidebar in Vietnamese and the bell with `2 / 2 / 2`, which equals `vw_ExpiringContract` = 2, `vw_OpenFeedback WHERE IsOverdue` = 2, `vw_CareSchedule WHERE Overdue/Today` = 2; `POST /logout` -> the next `GET /` -> `302 /login?ReturnUrl=%2F`; the 403 page works (T15). **The 500 page is NOT RUN**: in Development the developer exception page handles errors, so `/error` cannot be reached without changing the environment | PASS / NOT RUN (500 page) |
| T20 | Vietnamese sample data | accents correct, row counts unchanged, triggers still compute `TotalValue`/`Tier` | row counts `DEPARTMENT 4, EMPLOYEE 8, ACCOUNT 6, CUSTOMER 10, PRODUCT_SERVICE 8, CONTRACT 16, CONTRACT_DETAIL 39, CONTACT 17, CARE 12, FEEDBACK 9, SETTINGS 2` - identical to the M01 baseline; accented values read back (`Nguyễn Văn An`, `Công ty TNHH ABC`, `12 Lê Lợi, Quận 1, TP.HCM`, `Phần mềm quản lý cơ bản`, `Lỗi phần mềm: không đăng nhập được`); `CONTRACT.TotalValue` and `CUSTOMER.Tier` are still computed by the triggers (`80000000.00` -> `Diamond`, `13000000.00` -> `Standard`) | PASS |
| T21 | No flash | `lang`/`data-theme` come from the server, no `localStorage`, no theme script | both attributes are in the server HTML of every page checked; no `localStorage`, `sessionStorage`, `matchMedia` or `document.documentElement.dataset` anywhere in the views, `site.js` or `site.css` | PASS |
| T22 | Settings are not audited | no new `AUDIT_LOG` row after changing a setting | `AUDIT_LOG` was 0 rows before and 0 rows after the whole round, which included several system and personal setting writes | PASS |

Extra checks run in the same round (not part of the 22 cases):

| Check | Expected | Actual | Status |
|---|---|---|---|
| CSRF | a POST without the anti-forgery token is refused | `POST /login` without a token -> `400` | PASS |
| XSS | user input is escaped, even though the encoder now passes Vietnamese through as UTF-8 | a username of `<script>alert('xss')</script>` renders as `&lt;script&gt;alert...`; the raw `<script>` string never appears in the page | PASS |
| Switches without JavaScript | the language and theme switches still work | both switches are ordinary submit buttons inside a form, so they work with JavaScript disabled; nothing in the switch depends on `site.js` | PASS |

## 4. Bugs found during this round

**BUG-01 - the language switch does nothing on a URL that carries `?culture=`** (found by T06 + T05
together, low severity). **Fixed after the round** with the user's approval - see `bug-report.md` for
the fix and the re-verification, and section 7 below.

One problem was found and fixed **while preparing this round**, before any case above was run, and the
whole round was then run on the final build: Razor HTML-encoded every Vietnamese diacritic as a
numeric entity (`Đăng nhập` arrived as `&#x110;&#x103;ng nh&#x1EAD;p`). The page is correct in a
browser either way, but the source is unreadable and any tool that reads the HTML sees entities. It
was fixed by registering `HtmlEncoder.Create(UnicodeRanges.All)` in `Program.cs` - the characters that
matter for HTML (`&`, `<`, `>`, `"`, `'`) are still escaped, which the XSS check above confirms. This
is not counted as a test-round bug because it was fixed before the round started; it is listed in
`report.md` as an implementation issue.

## 5. Known issues and things that are not done

### 5.1 NuGet audit warnings (fixed after the round)

`NU1902` (moderate) and `NU1903` (high) on the transitive `System.Security.Cryptography.Pkcs` 9.0.11 -
two per project, eight in a solution build. Nothing in this project uses that package directly: it
arrives through `Microsoft.Data.SqlClient` 6.1.7, which EF Core's SQL Server provider pulls in, and
NuGet resolves the **lowest** version any dependency asks for, which is the vulnerable one.

Both projects now pin the patched build with a direct reference
(`System.Security.Cryptography.Pkcs` 10.0.13, the version the advisory names for the 10.0 line, matching
EF Core 10.0.13). The build is warning-free and the whole suite still passes against real SQL Server;
see section 7.

### 5.2 Deliberate limitations of M01b

- Only two settings exist (`Language`, `Theme`). `PageSize` was dropped from the module because no
  screen is paged yet; it belongs to M02.
- The Settings screen is read-mostly: an employee changes their own language and theme from the
  topbar, and only a Manager can change a system default. There is no screen where a Manager sets a
  value **for** another employee, and (since the change in section 6) no screen that shows a
  colleague's own choice either.
- The two-letter `VI` / `EN` buttons carry the language code as their visible text; the sentence a
  user reads (tooltip, screen reader) comes from the resource files. A three-letter-code language
  would need a wider button, which is a layout decision for a later module.
- The money format is implemented and unit-tested but shown on no screen yet (T17).
- Responsive/mobile layout was not re-checked in this round: the browser tool cannot capture a
  screenshot here, and M01b added no new page layout - only the settings page, which uses the same
  card/table classes as the rest.

### 5.3 Deviations from the approved plan

| Planned | Built | Why |
|---|---|---|
| switches at `/settings/language` and `/settings/theme` in `SettingsController`, `culture=fr` as the parameter | `PreferencesController`, `/preferences/language` and `/preferences/theme`, parameter `value` | The settings screen is Manager-only while a display preference belongs to every role, including visitors on the sign-in page. Keeping them in one controller would have meant a Manager-only class with two `[AllowAnonymous]` holes in it |
| `data-auto-submit` handler in `site.js` for the switches | two plain submit buttons per switch, no JavaScript | A `<select>` needs JavaScript to submit itself; a button group does not. The switches now work with JavaScript disabled, and no speculative JS was added |
| the theme cookie `HttpOnly` | `HttpOnly = false` | It holds no secret and `site.js` may one day read it to paint the page before the server answers. The language cookie is the framework's own |
| providers: query string -> cookie -> `Accept-Language` | query string -> cookie -> **system default from the database** -> `Accept-Language` | The system default is a decision a Manager took; it must outrank a guess about the browser. `Accept-Language` is now only used when the database answers nothing |
| `AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)` | not used | There are no per-language `.cshtml` files. Every string comes from `IStringLocalizer`, injected once in `Views/_ViewImports.cshtml` |
| `_SystemDefaults.cshtml`, `_EmployeeOverrides.cshtml`, `SystemSettingViewModel.cs` | `_SystemSettings.cshtml`, `_EmployeeSettings.cshtml`, `SettingsViewModel.cs` (with the row records) | Same split, clearer names. **Later changed:** the per-employee table was removed after this round at the user's request (see section 6) |
| `Program.cs` - no `HtmlEncoder` change | `HtmlEncoder.Create(UnicodeRanges.All)` | See section 4; Vietnamese would otherwise be sent as numeric entities |
| `HomeController.cs`, `AccountService.cs` - message keys | not changed | Neither holds a user-visible string: the controller only returns views, and the service reports through an enum plus log entries |

### 5.4 Effect on the live development database

Files `01`-`04` were re-run, so `CustomerCareDB` was dropped and recreated: the demo `PasswordHash`
values are back to the SHA2 form (`john` re-hashes on the next sign-in) and `AUDIT_LOG` is empty. The
`SETTINGS` rows the round wrote for employees 1 and 2 were deleted afterwards, leaving the two seeded
system rows. `CustomerCareDB_Test` is rebuilt by the test fixture on every run and was never written
to by the UI round.

## 6. Change after the round - the per-employee table was removed

Requested by the user after the round: the settings page must not list what each employee gets, because
a colleague's own language and theme are that person's business.

| Item | Before | After |
|---|---|---|
| `Views/Settings/_EmployeeSettings.cshtml` | read-only table of every employee's effective value with a `Riêng` / `Mặc định` badge | file deleted |
| `/settings` | two cards: system defaults + effective value per employee | one card: the system defaults |
| `SettingsViewModel` | `SystemSettings` + `EmployeeSettings` (with `EmployeeSettingsRow`, `EffectiveSettingRow`) | `SystemSettings` only |
| `ISettingService` / `SettingsService` | also `GetEmployeeSettingsAsync` | removed; `GetEffectiveValueAsync` stays, it is what the theme and the culture provider read for **the signed-in employee only** |
| `SharedResource{,.en}.resx` | six keys for that table | removed from both files, so the two files stay in step |

The database side is unchanged on purpose: `vw_EffectiveSettings` still holds the fallback rule, and the
`SETTINGS` personal row is still what makes a choice follow the account. Only the screen got smaller.

Re-verified after the change (same round's environment):

| Check | Expected | Actual | Status |
|---|---|---|---|
| Build | 0 errors | `Build succeeded. 0 Error(s), 8 Warning(s)` (the known audit warnings) | PASS |
| Automated tests | all green | `Failed: 0, Passed: 39` - including the localization tests, which prove no view still references a removed key | PASS |
| `/settings` as Manager, Vietnamese | one table, no employee rows | one table `Mặc định của hệ thống`, columns `Cài đặt \\| Giá trị \\| Cập nhật`, 2 data rows; the strings `Nhân viên`, `Nguồn` and `Giá trị hiệu lực` no longer appear anywhere on the page (only the hint text still says "mọi nhân viên") | PASS |
| `/settings` as Manager, English (via the switch) | the same page in English | `lang="en"`, section `System defaults`, columns `Setting \\| Value \\| Updated`; no `Employee`, `Personal`, `Source` or `Effective value` anywhere | PASS |
| `SETTINGS` after the round | two system rows, no personal row | `(system) Language=vi`, `(system) Theme=light`; personal rows deleted; `AUDIT_LOG` still 0 rows | PASS |
| The theme switch writes only the employee's row | the system default must not move | with the system `Theme=light`, `POST /preferences/theme value=dark` left the system row at `light` and created `EmployeeID 1 Theme=dark`; the settings page then still showed `light` selected | PASS |
| A fresh visitor | still Vietnamese, light | `<html lang="vi" data-theme="light">` | PASS |

## 7. Second batch of changes after the round (18/10/2026)

Approved with "ok làm tiếp": fix BUG-01, bring `docs/design.md` in line with what was built, and clear
the NuGet audit warnings.

| Item | Change |
|---|---|
| BUG-01 | `Helpers/Utils.cs` gained `ReturnUrlWithoutCulture(HttpRequest)`; both switch partials call it instead of `Context.Request.Path + Context.Request.QueryString`, so a `?culture=` in the address bar is dropped from the return URL while every other parameter survives |
| Unit tests | four cases for the helper in `UtilsTests` (culture dropped, both culture forms dropped, other parameters kept, no query unchanged, values escaped again) |
| NuGet | `System.Security.Cryptography.Pkcs` 10.0.13 pinned directly in `src/CcmWeb/CcmWeb.csproj` and `tests/CcmWeb.Tests/CcmWeb.Tests.csproj`, both with a comment saying why. The transitive 9.0.11 (GHSA-2fmv-rv2c-59x7, GHSA-846f-f764-76mx / CVE-2026-84008) is gone from the graph |
| `docs/design.md` | 2.3 money example now shows both languages and `HH:mm`; new **2.4 Dark theme** with the palette as built and the rules for badges, the primary colour and status signals; 3.2 toast text says the code is translated instead of showing the SQL message verbatim; 4 page map gained `/settings` and `/preferences/*`; 5.1 permission matrix gained the settings page and the switches; 6.13 describes the settings page (system defaults only, a personal choice is private); 9 checklist line now says the message is in the language the user chose |

Re-verification after this batch:

| Check | Expected | Actual | Status |
|---|---|---|---|
| Build | no warnings | `Build succeeded. 0 Warning(s), 0 Error(s)` | PASS |
| Automated tests | all green | `Failed: 0, Passed: 43` (39 + the 4 new helper cases) | PASS |
| Package graph | the patched build | `System.Security.Cryptography.Pkcs 10.0.13` (top level), `Microsoft.Data.SqlClient 6.1.7` unchanged; the test suite exercises SqlClient against real SQL Server, so the override is proven at runtime for our paths | PASS |
| `GET /login?culture=en` | return URL without the parameter | `lang=en`, `returnUrl=/login` | PASS |
| `GET /login?culture=en&q=abc` | the other parameter survives | `returnUrl=/login?q=abc` | PASS |
| `GET /login?culture=en&page=2&ui-culture=en` | both culture forms dropped | `returnUrl=/login?page=2` | PASS |
| Switching to `VI` on `/login?culture=en` | the page really turns Vietnamese | `lang=vi`, Vietnamese title - the bug is gone | PASS |
| `GET /settings?culture=en&x=1` signed in as Manager | `returnUrl=/settings?x=1` | as expected | PASS |
| The app after the package change | signs in and renders | dashboard, bell counts and `/settings` all fine on `http://localhost:5293` | PASS |
