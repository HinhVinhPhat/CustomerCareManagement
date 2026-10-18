---
name: ccm-language
description: Language rules for the customer care management system - what stays English (code, SQL identifiers, table and column names, comments, docs, the coded values stored in the tables) and what is Vietnamese (everything shown on screen and the business data rows), the Vietnamese/English switch built on ASP.NET Core resource files (.resx) plus RequestLocalization and a cookie, the label mapping for every CHECK-constraint value, date and money formatting per language, and the English-Vietnamese glossary of the domain. Use this skill before writing or changing any visible text, any .resx file or any sample data row, when adding a page or a message, when translating between English and Vietnamese, and when the user mentions language, tiếng Việt, translation, resource, resx, culture, localization or EN/VI.
---

# Language: Vietnamese on screen, English under the hood

The application is used by Vietnamese employees, so **everything a user reads is Vietnamese by
default** and English is the second language the user can switch to. Everything the computer reads
stays English: identifiers, SQL names, comments and documents.

## 1. The split - decide by who reads it

| What | Language | Example |
|---|---|---|
| Class, method, variable, file names | English | `CustomerService`, `SearchCustomersAsync` |
| Table, column, view, procedure, trigger names | English | `CUSTOMER`, `CustomerName`, `vw_CareSchedule`, `sp_AddContract` |
| Coded values stored in the tables (CHECK constraints) | English | `'Active'`, `'Manager'`, `'Gold'`, `'Individual'` |
| Comments, XML docs, `docs/`, `README`, commit messages | English | `// Falls back to the system default` |
| **Everything shown on screen** (headers, labels, buttons, table columns, empty states, errors, validation, tooltips, page titles, confirmation dialogs, emails) | **Vietnamese** | `Khách hàng`, `Lưu`, `Hợp đồng đang hiệu lực` |
| **Business data rows in SQL** (names, addresses, notes, product names, feedback text, care notes) | **Vietnamese** | `Công ty TNHH An Phát`, `25 Lê Lợi, Quận 1` |
| `<html lang>` | current language | `lang="vi"` / `lang="en"` |

Do not mix inside one string. `Hợp đồng "Active"` is wrong:
show `Đang hiệu lực` (the translated label) and keep `Active` only in the database and the code.

The static build in `docs/mockups/static-mockups/` is a **visual** reference: it is written in
English because it was built before the switch existed. Copy its layout, never its wording.

## 2. Where the strings live - resource files, not the database

Two `.resx` files under `src/CcmWeb/Resources/`:

| File | Holds | Role |
|---|---|---|
| `SharedResource.resx` | **Vietnamese** | the default (neutral) file; anything unknown or missing falls back here |
| `SharedResource.en.resx` | English | the second language |

Vietnamese is the neutral file on purpose: a key that was forgotten shows Vietnamese, never English.
Both files must always contain the **same keys**.

Registration in `Program.cs` (the real wiring; `AddViewLocalization` is **not** used because there
are no per-language `.cshtml` files - every string comes from the localizer):

```csharp
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
       .AddDataAnnotationsLocalization(o => o.DataAnnotationLocalizerProvider =
           (_, _) => factory.Create(typeof(SharedResource)));   // ErrorMessage / Display values are KEYS
builder.Services.Configure<RequestLocalizationOptions>(options => { /* providers, vi default */ });
builder.Services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(UnicodeRanges.All));
// ...
app.UseRequestLocalization();   // after UseStaticFiles()
```

`HtmlEncoder.Create(UnicodeRanges.All)` keeps Vietnamese as UTF-8 in the HTML instead of numeric
entities; `&`, `<`, `>`, `"` and `'` stay escaped, so user data is still safe.

The localizer is injected **once**, in `Views/_ViewImports.cshtml`:

```razor
@inject IStringLocalizer<SharedResource> L
```

so a view only writes the keys:

```razor
<h1 class="text-2xl font-bold">@L["Customer_List_Title"]</h1>
<button class="btn btn-primary">@L["Common_Save"]</button>
<input asp-for="Username" placeholder="@L["Login_Username_Placeholder"]" />
```

Key naming - `Area_Thing_Part`: `Common_Save`, `Common_Cancel`, `Nav_Customers`,
`Customer_List_Title`, `Customer_Field_Phone`, `Contract_Status_Active`, `Error_Forbidden`,
`Validation_Required`. Reuse a key before inventing a near-duplicate.

In C# (Services, attributes, `BusinessException` messages) use `IStringLocalizer<SharedResource>`
injected through the constructor - a Service that produces a user-visible message takes the
localizer too. Never build a sentence by concatenating translated fragments; translate the whole
sentence with placeholders (`@L["Customer_Delete_Confirm", name]`).

## 3. Switching language

- `RequestLocalization` middleware, `SupportedCultures` `vi` and `en`, `DefaultRequestCulture`
  `vi`, providers in this order: **query string → cookie → the system default stored in the database
  → `Accept-Language`**. The database outranks the browser on purpose: a Manager's decision beats a
  guess about the visitor's machine.
- The switch is the partial `Views/Shared/_LanguageSwitch.cshtml` (and `_ThemeSwitch.cshtml`), two
  plain submit buttons that POST to `/preferences/language` and `/preferences/theme`. Both live in
  `PreferencesController`, which is `[AllowAnonymous]` because the sign-in page carries them too;
  nothing is stored until the request is signed in.
- The action validates the posted `value` against the supported list, writes the culture cookie
  (`.AspNetCore.Culture`, 1 year, made with `CookieRequestCultureProvider.MakeCookieValue`) and, when
  a user is signed in, saves the choice as **that employee's own row** in `SETTINGS`, so the choice
  follows the account to another browser. It then redirects back to the page the user came from.
- The theme is a separate cookie (`.CcmWeb.Theme`) with the same shape; the pair of partials and the
  layout get the current value from `ISettingService`.
- Anonymous visitors are served from the cookie; with no cookie the system default from
  `vw_SystemSettings` applies, and the last fallback is the hard-coded `vi` / `light`.
- On sign-in, `AccountController` fills both cookies from `vw_EffectiveSettings` before the first
  page is rendered, so a personal row wins immediately.
- Never append `?culture=` to a link the user sees; the query-string provider exists for manual
  checks and tests only. (A `?culture=` in the address bar beats the switch's cookie for that
  request - this is the open BUG-01 in `docs/tasks/m01b-language-settings/bug-report.md`.)

## 4. Labels for the coded values

The database keeps the English code (section 1) and the screen shows the label. Every one of these
comes from a resource key, so it is never hard-coded in a View:

| Table.Column | Code | Key suffix | Vietnamese |
|---|---|---|---|
| `ACCOUNT.Role` | `Manager` / `Sales` / `Support` | `Role_Manager` ... | Quản lý / Kinh doanh / Hỗ trợ |
| `EMPLOYEE.Position` | `Manager` / `Staff` | `Position_Manager` / `Position_Staff` | Quản lý / Nhân viên |
| `EMPLOYEE.Gender`, `CUSTOMER.Gender` | `Male` / `Female` / `Other` | `Gender_Male` ... | Nam / Nữ / Khác |
| `CUSTOMER.CustomerType` | `Individual` / `Business` | `CustomerType_Individual` / `CustomerType_Business` | Cá nhân / Doanh nghiệp |
| `CUSTOMER.Tier` | `Standard` / `Silver` / `Gold` / `Diamond` | `Tier_Standard` ... | Tiêu chuẩn / Bạc / Vàng / Kim cương |
| `PRODUCT_SERVICE.ProductType` | `Product` / `Service` | `ProductType_Product` / `ProductType_Service` | Sản phẩm / Dịch vụ |
| `CONTRACT.Status` | `Pending` / `Active` / `Expired` / `Cancelled` | `Contract_Status_Pending` ... | Chờ ký / Đang hiệu lực / Hết hạn / Đã huỷ |
| `CONTACT.Channel` | `Phone` / `Email` / `Zalo` / `InPerson` / `Other` | `Channel_Phone` ... | Điện thoại / Email / Zalo / Gặp trực tiếp / Khác |
| `CARE.CareType` | `Renewal` / `Upsell` / `Birthday` / `Survey` / `RenewalReminder` / `Other` | `CareType_Renewal` ... | Tái ký / Bán thêm / Sinh nhật / Khảo sát / Nhắc gia hạn / Khác |
| `FEEDBACK.FeedbackType` | `Complaint` / `Suggestion` / `GeneralFeedback` / `Compliment` | `FeedbackType_Complaint` ... | Khiếu nại / Đề xuất / Phản hồi chung / Khen ngợi |
| `FEEDBACK.Priority` | `1` / `2` / `3` | `Priority_1` ... | Cao / Trung bình / Thấp |
| `FEEDBACK.FeedbackStatus` | `New` / `InProgress` / `Resolved` / `Closed` | `FeedbackStatus_New` ... | Mới / Đang xử lý / Đã xử lý / Đã đóng |
| `vw_CareSchedule.ScheduleStatus` | `Overdue` / `Today` / `Upcoming` | `ScheduleStatus_Overdue` ... | Quá hạn / Hôm nay / Sắp tới |
| `SETTINGS.SettingKey` | `Language` / `Theme` | `Setting_Language` / `Setting_Theme` | Ngôn ngữ hiển thị / Chế độ hiển thị |
| `SETTINGS.SettingValue` for `Language` | `vi` / `en` | `Language_vi` / `Language_en` | Tiếng Việt / English |
| `SETTINGS.SettingValue` for `Theme` | `light` / `dark` | `Theme_light` / `Theme_dark` | Sáng / Tối |

`Helpers/Utils.cs` exposes the single place that turns a code into a label: the extension
`localizer.Label(group, code)` looks up `<group>_<code>` (`L.Label("Theme", "dark")` → `Theme_dark`).
Views use it directly (`@L.Label("Role", role)`, `@L.Label("Setting", row.Key)`), so adding a code
means adding one key per language file. The badge colours stay the same in both languages
(design.md 2.2) - a label changes, a colour does not.

## 5. Formatting

| | Vietnamese (`vi`) | English (`en`) |
|---|---|---|
| Date | `dd/MM/yyyy` | `dd/MM/yyyy` (unchanged, never `MM/dd`) |
| Time | `HH:mm` | `HH:mm` |
| Money | `80.000.000 VNĐ` | `80,000,000 VND` |
| Thousands separator | `.` | `,` |
| Decimal separator | `,` | `.` |

Dates keep `dd/MM/yyyy` in both languages so a Vietnamese user is never in doubt. Money goes through
`Utils.FormatMoney(value, culture)`, which uses the separators of the current UI culture and the
right suffix. The input side never changes: models are bound as `decimal`, `DateOnly`, `DateTime`;
`<input type="date">` still posts `yyyy-MM-dd`. A value is formatted only when it is displayed.

## 6. Data and messages coming from the database

- Sample data (`database/03_SampleData.sql`) holds **Vietnamese business data**: customer and
  employee names with diacritics, Vietnamese addresses and phone numbers (`0901234567`),
  product/service names, care notes, feedback text. Codes, statuses and roles stay English.
  Every `NVARCHAR` column already carries `N'...'` prefixes - keep them.
- The database raises business errors as English text (the code table is in the `ccm-database`
  skill). The application **never shows that text**: `Utils.TranslateSqlError` maps the error code
  (`50001`-`50015`, `547`, `2601`, `2627`...) to a resource key and the localizer renders it in the
  user's language. The English text from SQL is kept for the server log only.
- Column-level rules from the database also carry user-facing text (for example the 50004 feedback
  rule and its 547 equivalent); both codes must map to the **same** key so the message does not
  change with the code path.

## 7. Glossary - use these words everywhere

| English | Vietnamese | Notes |
|---|---|---|
| customer | khách hàng | |
| contact (person) | người liên hệ | `CONTACT` = thông tin liên hệ |
| contract | hợp đồng | |
| contract line / detail | chi tiết hợp đồng | |
| product / service | sản phẩm / dịch vụ | |
| care | chăm sóc | `CARE` = lịch sử chăm sóc |
| care schedule | lịch chăm sóc | |
| feedback | phản hồi | of a customer, not "ý kiến phản hồi" |
| assignee | người phụ trách | |
| department | phòng ban | |
| employee | nhân viên | |
| account | tài khoản | |
| tier | hạng khách hàng | |
| revenue | doanh thu | |
| source (of a customer) | nguồn khách hàng | |
| sign a contract | ký hợp đồng | |
| renew | gia hạn | |
| cancel | huỷ | |
| expire | hết hạn | |
| deactivate / reactivate | ngừng hoạt động / kích hoạt lại | never "xoá khách hàng" |
| overdue | quá hạn | |
| dashboard | tổng quan | |
| reports | báo cáo | |
| notification | thông báo | |
| settings | cài đặt | |
| dark mode / light mode | chế độ tối / chế độ sáng | |

Keep the wording consistent across pages: one English term maps to exactly one Vietnamese term, in
the resource files and in the sample data alike.

## 8. Checklist for a new or changed screen

1. Every visible string comes from `@L["..."]`; no Vietnamese and no English typed straight into a
   `.cshtml`.
2. The new keys exist in **both** `SharedResource.resx` and `SharedResource.en.resx`, with the same
   names, and reuse `Common_*` where possible.
3. Coded values from the database are shown through the label helper, not by printing the code.
4. Dates, times and money go through the `Utils` helpers.
5. The page was looked at in **both** languages (use the `VI`/`EN` switch in the topbar), in both
   themes, including the empty state, a validation error and a business error.
6. The server log and the HTTP responses contain no Vietnamese text that should have been a key.
7. `docs/design.md` and the skills still describe the page with English labels; a new label goes into
   the resource files, never into the spec.
