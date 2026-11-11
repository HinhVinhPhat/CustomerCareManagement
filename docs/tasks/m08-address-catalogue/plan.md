# M08 - Plan and verification

## 1. Implementation

| Step | Work | Files |
|---|---|---|
| 1 | Download API v2 once, keep the raw JSON | `database/address/vn-provinces.json` |
| 2 | Generator: validate the response, escape SQL strings, batch the ward MERGE (500 rows per batch because a table-value constructor stops at 1,000), write UTF-8 without BOM | `database/address/update-address-data.ps1` |
| 3 | Schema for a fresh DB, and the same schema for an existing DB | `database/01_CreateTables.sql`, generated `database/05_AddressData.sql` |
| 4 | Always merge the catalogue after the login file | `database/init.sh` |
| 5 | EF: entities, `DbSet`s and the relationships in the custom partial (scaffolded files untouched) | `Models/Entities/Province.cs`, `Ward.cs`, `Customer.Address.cs`, `Models/EF/ApplicationDbContext.Custom.cs` |
| 6 | Form fields, lists and validation | `CustomerFormViewModel.cs`, `ICustomerService.cs`, `CustomerService.cs` |
| 7 | Cascading endpoint + re-fill after a failed POST | `CustomersController.cs` |
| 8 | Two selects inside a `display: contents` wrapper, street field, legacy hint | `Views/Customers/_FormFields.cshtml` |
| 9 | Separate JS initialiser; loads `/customers/wards`, keeps one placeholder on reset | `wwwroot/js/site.js` |
| 10 | Vietnamese + English strings | both `.resx` files |
| 11 | Documentation and the test DB | `docs/design.md`, `README.md`, `AGENTS.md`, skills, `Tests/TestDatabase.cs` |

## 2. How to refresh the catalogue

```powershell
powershell -ExecutionPolicy Bypass -File database/address/update-address-data.ps1
```

Commit `database/address/vn-provinces.json` and `database/05_AddressData.sql`. On a deployed machine,
run file 05 (or restart `db-init`): it is idempotent and does not touch customer rows.

## 3. Test cases

| ID | Case | Expected |
|---|---|---|
| T01 | Run 01 -> 05 on a fresh test database | all exit 0; 34 provinces, 3,321 wards |
| T02 | Run 05 a second time | same counts, no duplicate keys |
| T03 | Existing sample rows | still 10 customers with `ProvinceCode`/`WardCode` NULL and their old text |
| T04 | Create a customer with province 79 + ward 26740 + "12 Lê Lợi" | row stores 79/26740; detail shows "12 Lê Lợi, Phường Sài Gòn, Thành phố Hồ Chí Minh" |
| T05 | Open an old customer (id 1) | province/ward empty, old text unchanged, legacy hint shown |
| T06 | Change province on the form | `GET /customers/wards?provinceCode=...` returns only that province's wards; placeholder stays single |
| T07 | Post a ward of another province | business error on the ward field, no row written |
| T08 | Post a ward with no province | validation error on the ward field |
| T09 | Build and existing xUnit suite | build 0 errors; suite passes on the real SQL Server |

Results are recorded in `test-results.md`.
