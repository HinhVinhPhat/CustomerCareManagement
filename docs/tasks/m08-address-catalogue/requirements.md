# M08 - Vietnamese address catalogue

Status: **implemented on 11/11/2026 at the user's request** (the three notes below were the brief).
References: `docs/design.md` 6.4, `.agents/skills/ccm-database`, `.agents/skills/ccm-web-ui`.

## 1. Brief

| # | Note from the user | Meaning |
|---|---|---|
| 1 | Nguồn dữ liệu | Download `provinces.open-api.vn` API v2 **once**, keep the JSON in `database/`; the running app never depends on the external API. |
| 2 | Lưu ý dữ liệu | Administrative data keeps changing (e.g. Đồng Nai becoming a centrally-run city on 30/04/2026); provide a refresh script to run every few months. |
| 3 | Địa chỉ cũ | An old boarding-house address may have three levels. Keep the free-text "Số nhà, tên đường" field and never delete or rewrite the old data. |

## 2. Decisions

- **D1 - Local source.** `database/address/vn-provinces.json` is the raw API v2 response (34
  provinces/cities, 3,321 wards/communes at the time of download). It is committed, so a clone needs
  no network.
- **D2 - Database, not runtime JSON.** The JSON is loaded into `PROVINCE` and `WARD` by
  `database/05_AddressData.sql`; the app reads those tables through EF. The file is generated, never
  hand-edited, by `database/address/update-address-data.ps1`.
- **D3 - Two levels + free text.** Current data is province -> ward (the district level no longer
  exists). `CUSTOMER.ProvinceCode` and `CUSTOMER.WardCode` are nullable; `CUSTOMER.Address` stays the
  street detail and also holds the whole old three-level address on legacy rows.
- **D4 - No data loss on refresh.** The refresh marks every row `IsActive = 0`, then MERGEs the
  current rows back to `IsActive = 1`. A row removed from the source stays in the table, so an old
  customer still displays its ward/province name.
- **D5 - Ward belongs to province.** The dropdown limits the choice; the service checks the pair
  again before saving, so a hand-crafted POST cannot store a ward of another province.

## 3. Scope

In scope:

- `PROVINCE`, `WARD` tables and the two optional `CUSTOMER` columns/FKs.
- `05_AddressData.sql` (idempotent schema + data) and the PowerShell refresh script.
- Add/edit customer: Province/City and Ward/Commune selects, cascading through
  `GET /customers/wards`, plus the existing street field.
- Customer detail: the full address is composed from street + ward + province; legacy rows show their
  stored text unchanged.
- `init.sh` runs file 05 after 04 on every start, including an existing database.
- Resources in Vietnamese and English, database/design/skill documentation, test database rebuild.

Out of scope: changing old addresses automatically, geocoding, maps, district level, a settings screen
for the catalogue, and any external API call at run time.
