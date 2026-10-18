# M01b - Vietnamese UI, English/Vietnamese switch and settings

Status: **draft - waiting for approval on 18/10/2026**. Sits between M01 and M02; the M01-M07 split
(`ccm-workflow`) is unchanged, this is the follow-up work M01 needs before any other screen is
built on top of the shell.

Reference: `AGENTS.md` sections 1 and 5, skill `ccm-language` (written as part of this request),
`docs/design.md` sections 2 and 3, skill `ccm-database`.

## 1. Goal

The application is used by Vietnamese staff, so the interface is **Vietnamese by default** with
English as a second language, and each person can pick a **light or dark theme**. The choice belongs
to the account, so it follows the user to another computer; the sign-in page, which has no account
yet, still works from the browser cookie.

The rule "personal override wins over the system default" is a database rule, so it lives in a
`SETTINGS` table and the two views over it (`AGENTS.md` section 4: the database holds the business
logic, the application only calls into it).

At the end of M01b: every string M01 already shows comes from a resource file; the topbar carries a
language switch (VI/EN) and a theme switch (light/dark); the choice survives reload, logout, another
browser and another computer; a Manager can change the system-wide default on a new `/settings`
page; and `database/03_SampleData.sql` carries Vietnamese business data.

## 2. Scope

| # | Feature | Source |
|---|---|---|
| 1 | New `SETTINGS` table (system default = `EmployeeID IS NULL`, personal override per employee) plus `vw_SystemSettings` and `vw_EffectiveSettings`, with FK, UNIQUE, CHECK constraints, and 2 seeded system defaults (`Language='vi'`, `Theme='light'`) | user request; `AGENTS.md` 4 |
| 2 | `RequestLocalization` (query string -> cookie -> `Accept-Language`), supported cultures `vi` and `en`, default `vi` | `ccm-language` 3 |
| 3 | `Resources/SharedResource.resx` (Vietnamese - the neutral file) and `SharedResource.en.resx` (English), `IStringLocalizer` in Views and Services, DataAnnotations messages localized | user decision |
| 4 | Language switch in the topbar (`POST /settings/language`), working while signed out too | user request |
| 5 | Theme switch light/dark in the topbar (`POST /settings/theme`), `data-theme` on `<html>`, a dark value for every colour variable | user request |
| 6 | Persisting the choice: cookie for the current browser + one `SETTINGS` row for the account (only when signed in) | decision A (below) |
| 7 | `/settings` page (Manager only): the system defaults with an edit form. The read-only table of each employee's effective value was dropped at the user's request (a colleague's personal choices are their own); the rule itself stays in `vw_EffectiveSettings` | user request |
| 8 | Every visible string of M01 moved to resource keys: login, sidebar, topbar, landing page, 403, 500, notification bell, buttons, validation messages, business error messages | `ccm-language` |
| 9 | `Utils.TranslateSqlError` stops returning English text: it returns a resource key, and the message is rendered in the user's language | `ccm-language` 6 |
| 10 | `database/03_SampleData.sql`: business data written in Vietnamese | user decision |
| 11 | Re-scaffold the model, update `.agents/skills/ccm-database/references/schema.md`, and the wording in `docs/design.md` that still says "English" | `AGENTS.md` 4 |

### Explicitly NOT in M01b

- The shared list/form building blocks (filter bar, pager, `_Badge` partial, data table) - they arrive
  with the first screen that uses them (M02).
- Settings beyond `Language` and `Theme`. The table is built to take more keys (see 3.1), but seeding
  `PageSize` or anything else now would ship a setting no screen reads; `PageSize` arrives with M02.
- Wording for screens that do not exist yet (customers, contracts, contacts, care, feedback,
  dashboard, reports, admin) - M02-M06.
- The profile page and change password (design.md 6.14, M06).
- Translating `docs/mockups/static-mockups/` - it stays an English **visual** reference, per
  `ccm-language`.
- Docker (M07).

## 3. Design

### 3.1 `SETTINGS` table (new, in `database/01_CreateTables.sql`)

```sql
CREATE TABLE SETTINGS (
    SettingID     INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeID    INT NULL,                    -- NULL = the system-wide default
    SettingKey    NVARCHAR(50)  NOT NULL,      -- 'Language', 'Theme', ...
    SettingValue  NVARCHAR(100) NOT NULL,
    DataType      VARCHAR(20)   NOT NULL,      -- 'String' | 'Int' | 'Bool'
    Category      VARCHAR(30)   NOT NULL,      -- 'Localization' | 'Appearance' | 'General'
    Description   NVARCHAR(300) NULL,          -- shown as a hint on the settings page
    UpdatedDate   DATETIME      NOT NULL CONSTRAINT DF_Settings_UpdatedDate DEFAULT (GETDATE()),
    CONSTRAINT FK_Settings_Employee FOREIGN KEY (EmployeeID) REFERENCES EMPLOYEE(EmployeeID),
    CONSTRAINT UQ_Settings_Scope   UNIQUE (EmployeeID, SettingKey),
    CONSTRAINT CHK_Settings_Key       CHECK (SettingKey IN (N'Language', N'Theme')),
    CONSTRAINT CHK_Settings_DataType  CHECK (DataType  IN ('String', 'Int', 'Bool')),
    CONSTRAINT CHK_Settings_Category  CHECK (Category  IN ('Localization', 'Appearance', 'General')),
    CONSTRAINT CHK_Settings_Value     CHECK (
             (SettingKey = N'Language' AND SettingValue IN (N'vi', N'en'))
          OR (SettingKey = N'Theme'    AND SettingValue IN (N'light', N'dark')))
);
```

Notes the implementation must keep:

- `UQ_Settings_Scope (EmployeeID, SettingKey)`: SQL Server treats NULL as a value for a unique
  constraint, so there is at most **one** system default per key, and at most one personal row per
  employee and key - exactly the rule wanted, enforced by the engine.
- `CHK_Settings_Value` is the last line of defence: the application validates too, but a bad value
  cannot enter the table even from `sqlcmd`. The `CHECK` is extended when a new key is added in a
  later module (that is the price of getting real validation instead of a free-text key-value table).
- `DataType` and `Category` are read by the /settings page (grouping and the "what is this" hint);
  they are not decoration - `Category` drives the grouping of the page.

Two views (new, in `database/02_Function_View_Proc_Trigger.sql`):

| View | Returns |
|---|---|
| `vw_SystemSettings` | one row per key: `SettingKey`, `SettingValue`, `DataType`, `Category`, `Description` for `EmployeeID IS NULL` (what an anonymous visitor and the settings page read) |
| `vw_EffectiveSettings` | one row per employee and key: `EmployeeID`, `SettingKey`, `SettingValue`, `IsOverridden`, `Category`, `Description`; `SettingValue` is `COALESCE(personal, system)` so the fallback rule is not reimplemented in C# |

`vw_EffectiveSettings` is a cross join of `EMPLOYEE` with the system keys, left-joined to the
personal rows. `EMPLOYEE` has no `IsActive` column, so no active filter is applied.

Seed rows (in `database/03_SampleData.sql`, system scope):

| SettingKey | SettingValue | DataType | Category | Description |
|---|---|---|---|---|
| Language | `vi` | String | Localization | Ngôn ngữ hiển thị mặc định (vi / en) |
| Theme | `light` | String | Appearance | Chế độ hiển thị mặc định (light / dark) |

### 3.2 Localization

| Item | Decision |
|---|---|
| String store | `.resx` resource files (standard ASP.NET Core), **not** a database table of strings |
| Files | `src/CcmWeb/Resources/SharedResource.resx` = **Vietnamese** (the neutral file), `Resources/SharedResource.en.resx` = English |
| Marker class | `src/CcmWeb/SharedResource.cs` (empty class) so `IStringLocalizer<SharedResource>` finds `Resources/SharedResource*.resx` with `ResourcesPath = "Resources"` |
| Views | `@inject IStringLocalizer<SharedResource> L` in `Views/_ViewImports.cshtml`, used as `@L["Customer_List_Title"]` |
| Services | the localizer is injected like any other dependency; `BusinessException` carries a **key**, not a sentence |
| Validation | `AddDataAnnotationsLocalization` with `DataAnnotationLocalizerProvider` pointing at `SharedResource`; `ErrorMessage` holds the key |
| Key naming | `Area_Thing_Part`: `Common_Save`, `Nav_Customers`, `Customer_List_Title`, `Contract_Status_Active`, `Error_Forbidden`, `Validation_Required` |

A key missing from one of the two files falls back to the Vietnamese neutral file; a key missing
from both renders as the key itself (visible during development). A unit test asserts that both files
carry exactly the same key set, so this never ships silently.

### 3.3 Coded database values

Codes stay English in the database (`'Active'`, `'Manager'`, `'Gold'`, ...), which is what triggers,
views, procedures and the C# comparisons already rely on. Only the **label** is translated, through
`Utils` helpers backed by resource keys (`Contract_Status_Active`, `Tier_Gold`, `Role_Manager`, ...),
with the full mapping table in `ccm-language` section 4. Batch colours do not change with the
language (design.md 2.2).

### 3.4 Switching, and where the choice is remembered

| Situation | Behaviour |
|---|---|
| Signed in, has a personal row | personal value wins (`vw_EffectiveSettings`) |
| Signed in, no personal row | the system default applies; choosing a value writes a personal row |
| Signed out (sign-in page) | cookie only - there is no account to attach a row to |
| No cookie, no row | system default, then the hard fallback `vi` / `light` |

Flow: the topbar control posts to `/settings/language` (or `/settings/theme`) with the chosen value
and a local `returnUrl`; the action validates the value against the supported list, writes the
cookie, and - **only when the user is signed in** - saves the row through the Service; then it
redirects back to the page the user came from. Both actions are `[AllowAnonymous]` (the sign-in page
needs them) but the row is written only for an authenticated employee taken from the claim, never
from the form. `?culture=en` also works through the framework's query-string provider (handy for
tests) but is never the link the UI produces.

At sign-in the saved values are applied straight away, so the first rendered page is already in the
right language and theme. `<html lang>` and `<html data-theme>` are rendered server-side, so there is
no flash of the wrong language or theme and no `localStorage` copy to keep in step.

The cookie is the framework's own `.AspNetCore.Culture` (`CookieRequestCultureProvider`), so the
built-in provider reads back what the action wrote. The theme cookie is a plain `.CcmWeb.Theme`
cookie read by the layout.

### 3.5 `/settings` page (Manager only)

- System defaults: a table of `vw_SystemSettings` with an inline edit form for the value of each key
  (choice list for `Language` and `Theme`, never free text), a hint from `Description`, and the
  `UpdatedDate`.
- Each employee's effective value: a read-only table over `vw_EffectiveSettings`, grouped by employee
  with the overridden rows marked (a badge, not a colour alone).
- Per `AGENTS.md` section 5 the page is split into partials: the page file holds the header plus the
  `<partial>` calls; the two blocks are separate partials under `Views/Settings/`.
- Writing to `SETTINGS` is **not** audited: `AUDIT_LOG` is fed by the contract and feedback triggers,
  and M01b adds no trigger (`AGENTS.md` section 4). A `SETTINGS` change is not a business event.

### 3.6 Dark theme (new, design.md only defines a light palette)

design.md section 2 has a single light palette, so the dark palette is **new content** proposed here
and, once approved, written into design.md as section 2.4. Same hues, darkened surfaces:

| Variable | Dark value | Variable | Dark value |
|---|---|---|---|
| `--primary` | `#3B82F6` | `--success` | `#4ADE80` |
| `--primary-hover` | `#60A5FA` | `--success-soft` | `#14532D` |
| `--primary-soft` | `#1E3A8A` | `--info` | `#38BDF8` |
| `--accent` | `#2DD4BF` | `--info-soft` | `#0C4A6E` |
| `--bg-page` | `#0F172A` | `--warning` | `#FBBF24` |
| `--bg-card` | `#1E293B` | `--warning-soft` | `#78350F` |
| `--bg-sidebar` | `#020617` | `--danger` | `#F87171` |
| `--border` | `#334155` | `--danger-soft` | `#7F1D1D` |
| `--text` | `#E2E8F0` | `--neutral` | `#94A3B8` |
| `--text-muted` | `#94A3B8` | `--neutral-soft` | `#334155` |

`site.css` still holds a few raw colours outside the variables (`.btn`/`.input` background `#fff`,
`.tbl th` background `#F8FAFC`, the badge text colours `#166534`, `#075985`, `#92400E`, `#991B1B`,
`#334155`, and the four tier badge pairs). Dark mode needs them, so those values become variables
(`--bg-input`, `--bg-table-head`, `--on-success`, `--on-info`, `--on-warning`, `--on-danger`,
`--on-neutral`, and a background/text pair per tier) and every variable gets a dark value. No page
gains a raw colour value.

> **Built differently.** The table above is the proposal; the palette that was actually built has
> slightly different dark values and is written down in `design.md` section 2.4 and in `site.css`, which
> is the source of truth. The tone of the two bars was changed again on 18/10/2026 at the user's
> request (sidebar `#334155` / topbar `#E2E8F0` in the light theme, sidebar `#1E293B` in the dark
> theme) - see `report.md` section 9.

### 3.7 Sample data in Vietnamese (file 03)

Same rows, same counts, same relationships; only the text values change: employee and customer names
with diacritics, Vietnamese addresses and notes, product/service names, care notes, feedback text.
Codes, statuses, roles, usernames and the demo password stay English/unchanged, every `NVARCHAR`
value keeps its `N'...'` prefix, and the verification block at the end of the file must still print
`OK: sample data loaded, TotalValue and Tier match the triggers.`

## 4. Data used by this module

| Use | DB object |
|---|---|
| System defaults | `SETTINGS` (EmployeeID IS NULL) through `vw_SystemSettings` |
| Effective value for the signed-in user | `vw_EffectiveSettings` |
| Save a personal choice / a system default | INSERT / UPDATE on `SETTINGS` |
| Login (unchanged) | `ACCOUNT` joined to `EMPLOYEE` |
| Notification bell (unchanged) | `vw_ExpiringContract`, `vw_OpenFeedback`, `vw_CareSchedule` |

`docs/design.md` sections 10 and 11 stay outdated and are not touched; M01b only adds 2.4 and fixes
the wording noted in section 5.1.

## 5. Assumptions and items needing the user's approval

1. **The four answers already given** (recorded so the module is self-contained): the string store is
   `.resx`, not a database table; coded values stay English and only the label is translated;
   `SETTINGS` carries both the system default and the personal override in one table (scope A); M01
   is converted to Vietnamese now rather than from M02 onward.
2. **`docs/design.md` needs three small edits** to stop contradicting the approved language rule:
   2.3 (money example gains the Vietnamese form `80.000.000 VNĐ`), new 2.4 (the dark palette above),
   and section 9's checklist line "SQL error messages are translated into clear English messages" ->
   "in the user's language". Without these edits the spec and `AGENTS.md` section 1 disagree.
   `AGENTS.md` sections 1 and 5 were already updated when the `ccm-language` skill was written.
3. The dark palette in 3.6 is a proposal; only the light palette exists in design.md today.
4. Strings for screens that do not exist yet are not written now; each module adds its own keys.
5. `docs/mockups/static-mockups/` keeps its English text (visual reference only).
6. The language and theme switches reload the page (POST + redirect). A JavaScript-only toggle was
   rejected: it would need a second copy of the state in the browser and would not survive a fresh
   request, and the cookie has to be set server-side anyway.
7. `app_ccm` needs INSERT/UPDATE on `SETTINGS`. File `04` grants through the `role_Manager` database
   role; the implementation must confirm the grant covers the new table and extend file `04` if the
   role is granted per object rather than schema-wide.

## 6. Definition of done

1. `dotnet build` has no errors and `dotnet test` runs the whole suite (the M01 tests plus the new
   ones) with every case PASS on the real test database.
2. Running `database/01` -> `04` on a fresh database creates `SETTINGS`, `vw_SystemSettings`,
   `vw_EffectiveSettings`, seeds `Language='vi'` and `Theme='light'`, and file 03 still prints `OK`.
3. Instead of the four files being re-run, the model is re-scaffolded and
   `.agents/skills/ccm-database/references/schema.md` lists the new table, its constraints and the
   two views.
4. The sign-in page and every M01 screen are Vietnamese by default; a search of `src/CcmWeb/Views`
   finds no visible text typed straight into a `.cshtml` in either language.
5. Switching to English changes the whole screen (sidebar, topbar, page titles, buttons, empty
   states, validation messages, business errors) and switching back restores Vietnamese; an
   unsupported value (`?culture=fr`, `language=fr` in the form) falls back to Vietnamese and is never
   stored.
6. The choice survives a reload, a logout and sign-in, a second browser (cookie), and a second
   browser after sign-in (row in `SETTINGS`).
7. A personal row beats the system default, proven by a query on the test database.
8. A Manager sets the system default to `en` on `/settings`; an employee with no personal row then
   sees English, and an employee with a personal row still sees their own value.
9. The database rejects an illegal value written directly(`Language='fr'`, `Theme='blue'`) with a
   CHECK violation; through the application the user sees a Vietnamese message and no SQL detail.
10. Dark mode changes the whole screen with no white patch and stays after a reload; text, badges and
    buttons stay readable (`--text` on `--bg-card` and `--text-on-dark` on `--bg-sidebar`).
11. `/settings` is Manager-only: Sales and Support get the 403 page, and the menu item is hidden for
    them.
12. Dates show `dd/MM/yyyy` and money `80.000.000 VNĐ` (Vietnamese) / `80,000,000 VND` (English)
    through the helpers - no ambient server culture, no pre-formatted value reaching the database.
13. `docs/tasks/m01b-language-settings/test-results.md` records the round honestly (PASS / FAIL /
    BLOCKED / NOT RUN with evidence), and `report.md` closes the module.
