# M06 - Quản trị và hồ sơ cá nhân - kế hoạch

Đi kèm `requirements.md`; **một lần duyệt** áp dụng cho cả hai tệp. Các bước chạy theo thứ tự; việc nằm
ngoài phạm vi hoặc thay đổi nghiệp vụ thì dừng lại và hỏi trước.

## Bước 0 - chốt quyết định

Bảy câu hỏi ở `requirements.md` mục 7 phải được trả lời trước khi code. Các bước dưới đây giả định
phương án đề xuất: (1) thêm 3 trigger nhật ký, (2) không thêm `IsActive` cho sản phẩm, (3) không xoá cứng
nhân viên/tài khoản, (4) mở `/admin/products` cho cả ba vai trò, (5) mật khẩu mới từ 8 ký tự, (6) chặn tự
khoá/tự hạ quyền và chặn mất Manager cuối, (7) phòng ban thêm/đổi tên, không xoá khi còn nhân viên.

Khác với đề xuất thì ảnh hưởng: (1) → bỏ Bước 1, Bước 6 và một nửa số ca test; (2) → thêm bước SQL,
re-scaffold `ProductService`, sửa dropdown hợp đồng ở M03; (3) → thêm bước SQL/thủ tục và đổi cả M02;
(4) → đổi `[Authorize]` ở Bước 5 và menu ở Bước 7; (5) → chỉ đổi hằng số ở Bước 2; (6) → bỏ một nhánh
kiểm tra ở Bước 4; (7) → bỏ partial phòng ban.

## Bước 1 - SQL

1. `database/02_Function_View_Proc_Trigger.sql`, thêm vào mục TRIGGERS:
   - `trg_Product_AuditLog` trên `PRODUCT_SERVICE`: Insert / Update / Delete, `Detail` ghi tên + đơn giá
     (mã số, không dịch), ví dụ `N'ProductName -> ProductName | 12000000 -> 13000000'`.
   - `trg_Employee_AuditLog` trên `EMPLOYEE`: Insert / Update / Delete, `Detail` ghi phòng ban và cấp bậc
     khi đổi.
   - `trg_Account_AuditLog` trên `ACCOUNT`: Insert / Delete / Update, `Detail` chỉ ghi `Username`, `Role`,
     `IsActive` - **tuyệt đối không** đưa `PasswordHash` vào. UPDATE chỉ đổi hash (nâng cấp SHA2 lúc đăng
     nhập) thì `WHERE` loại bỏ, không sinh dòng nhật ký.
   - Cả ba đọc `CAST(SESSION_CONTEXT(N'EmployeeID') AS INT)` như hai trigger sẵn có.
2. `database/01_CreateTables.sql`: thêm `CREATE INDEX IDX_AuditLog_LogTime ON AUDIT_LOG(LogTime DESC);`
   (trang nhật ký lọc và sắp theo thời gian).
3. Không đổi `03_SampleData.sql`.
4. Áp dụng: bộ test dựng lại `CustomerCareDB_Test` từ 01-03 nên tự có. Với `CustomerCareDB` thật, chạy
   **riêng** phần DDL mới (`DROP TRIGGER IF EXISTS` + `CREATE TRIGGER`, `CREATE INDEX`) bằng `sqlcmd` -
   không chạy lại `01` vì tệp đó xoá và tạo lại CSDL.
5. Cập nhật `.agents/skills/ccm-database/references/schema.md`: bảng trigger (6 dòng), index mới.

## Bước 2 - hằng số và helper

1. `Helpers/WorkConstants.cs`:
   - `Roles` giữ nguyên; thêm `Positions` (`Manager`, `Staff`, `All`).
   - `ProductTypes` (`Product`, `Service`, `All`), `EmployeeGenders` (`Male`, `Female`, `Other`, `All`) -
     cùng giá trị với `CHK_*`, không lặp lại chuỗi trong view.
   - `Accounts.PasswordMinLength = 8`, `PasswordMaxLength = 100`, `UsernameMaxLength = 50`.
   - `Audit.Tables` (`CONTRACT`, `FEEDBACK`, `PRODUCT_SERVICE`, `EMPLOYEE`, `ACCOUNT`),
     `Audit.Actions` (`Insert`, `StatusChange`, `Delete`, `Assign`, `Update`).
2. `Helpers/Navigation.cs`: `administration` → `Built = true`, thêm mục `profile` (`fa-user`, `/profile`,
   mọi vai trò, `Built = true`) và - nếu duyệt câu hỏi 4 - mục `products` cho Sales/Support. `IsCurrent`
   xử lý `/admin/products/5/edit` nhờ tiền tố `/admin/products`; riêng `/admin` không phải trang, nên
   mục Quản trị trỏ tới `/admin/products` để luôn có đích đến.
3. `Helpers/Badges.cs`: thêm `ForAccountState(bool isActive)` (`b-success` / `b-danger`) và
   `ForAuditAction(string)`; màu lấy từ biến CSS sẵn có.

## Bước 3 - ViewModels

`Models/ViewModels/` (mỗi trang một tệp, theo mẫu `CustomerListViewModel`):

1. `ProductViewModels.cs`: `ProductRow`, `ProductFilter` (Q, Type, Sort, Desc, Page, PageSize +
   `SortQuery`/`PageQuery`/`PageSizeQuery`), `ProductListViewModel` (`Rows`, `TotalRows`, `ErrorKey`,
   phân trang), `ProductFormViewModel` (Id, Name, Type, UnitPrice, Description, `InUseCount`).
2. `EmployeeViewModels.cs`: `EmployeeRow` (kèm `HasAccount`), `EmployeeFilter`, `EmployeeListViewModel`,
   `EmployeeFormViewModel` (kèm `Departments`, `DepartmentOptions`), `DepartmentRow` (Id, Name,
   `EmployeeCount`), `DepartmentFormModel`.
3. `AccountViewModels.cs`: `AccountRow`, `AccountFilter`, `AccountListViewModel`, `AccountCreateViewModel`
   (kèm `EmployeesWithoutAccount`), `AccountEditViewModel`, `ResetPasswordViewModel`.
4. `AuditLogViewModels.cs`: `AuditLogRow` (Time, EmployeeName, TableName, RecordID, Action, Detail),
   `AuditLogFilter` (Employee, Table, Action, From, To, Page, PageSize + query builders),
   `AuditLogListViewModel` (+ `Employees`, `ErrorKey`).
5. `ProfileViewModel.cs`: `ProfileViewModel` (thông tin đọc) và `ChangePasswordViewModel`
   (CurrentPassword, NewPassword, ConfirmPassword) với `[Required]`, `[StringLength]`,
   `[Compare]` để lỗi hiện đúng ô.

## Bước 4 - Services (`IxxxServices` + `xxxServices`)

1. `IProductService` / `ProductService`: `SearchAsync(filter)`, `GetAsync(id)`, `CreateAsync`,
   `UpdateAsync`, `DeleteAsync`, `CountUsageAsync(id)` (số dòng `CONTRACT_DETAIL`).
   Không trả `IQueryable` hay entity tracked; ném `BusinessException` với message key.
2. `IEmployeeService` / `EmployeeService`: `SearchAsync`, `GetAsync`, `CreateAsync`, `UpdateAsync`,
   `GetDepartmentsAsync`, `CreateDepartmentAsync`, `RenameDepartmentAsync`, `GetDepartmentUsageAsync`.
   Kiểm tra email/điện thoại trùng **trước** khi ghi; chặn cấp bậc Manager thứ hai trong một phòng ban.
3. `IAdminAccountService` / `AdminAccountService` (tách khỏi `IAccountService` đang phục vụ đăng nhập):
   `SearchAsync`, `CreateAsync` (hash bằng `IPasswordHasher<Account>`), `UpdateAsync` (role, IsActive),
   `ResetPasswordAsync`. Kiểm tra: nhân viên chưa có tài khoản, username chưa tồn tại, không tự khoá/tự hạ
   quyền, không làm mất Manager hoạt động cuối cùng.
4. `IAuditLogService` / `AuditLogService`: `SearchAsync(filter)` chỉ đọc + `GetEmployeesAsync` cho bộ lọc.
5. `IProfileService` / `ProfileService`: `GetAsync(employeeID)` và `ChangePasswordAsync(employeeID,
   current, new)`; xác thực mật khẩu cũ theo cùng đường với đăng nhập (SHA2 demo → nâng cấp), hash mật
   khẩu mới bằng `PasswordHasher`.
6. Đăng ký cả năm trong `Program.cs` cạnh các service hiện có; `ApplicationDbContext.Custom.cs` thêm
   `UseSqlOutputClause(false)` cho `ProductService`, `Employee`, `Account` vì ba bảng nay có trigger.
7. `Utils.TranslateConstraintErrorKey`: thêm nhánh cho `CHK_Employee_Gender`, `CHK_Employee_Email`,
   `CHK_Employee_Phone`, `CHK_Employee_Position`, `CHK_Product_Type`, `CHK_Product_UnitPrice`,
   `CHK_Account_Role`. `TranslateDuplicateErrorKey`: thêm nhánh theo tên constraint **đặt tường minh**
   (Bước 1 đặt tên cho unique index nếu cần) - nếu chỉ có tên hệ thống sinh thì dùng thông báo trùng chung.

## Bước 5 - Controller và route

1. `Controllers/AdminController.cs` (attribute routing theo `design.md` mục 4):
   - `[Authorize(Roles = Manager)]` ở mức controller; riêng `Products` (GET) mở `All` nếu duyệt câu hỏi 4.
   - `GET /admin` → chuyển tới `/admin/products`.
   - Sản phẩm: `GET /admin/products`, `GET|POST /admin/products/create`, `GET|POST /admin/products/{id}/edit`,
     `POST /admin/products/{id}/delete`.
   - Nhân viên: `GET /admin/employees`, `GET|POST /admin/employees/create`,
     `GET|POST /admin/employees/{id}/edit`, `POST /admin/employees/departments`,
     `POST /admin/employees/departments/{id}/rename`.
   - Tài khoản: `GET /admin/accounts`, `GET|POST /admin/accounts/create`,
     `GET|POST /admin/accounts/{id}/edit`, `GET|POST /admin/accounts/{id}/password`.
   - Nhật ký: `GET /admin/audit-logs`.
   - Mọi POST: kiểm `ModelState` → gọi Service → thành công thì `TempData["MessageKey"]` + redirect
     (PRG) → `BusinessException` thì render lại form với dữ liệu đã nhập và `ErrorKey`.
2. `Controllers/ProfileController.cs`: `[Authorize]`, `GET /profile`, `GET|POST /profile/password`;
   employeeID lấy từ `User.GetEmployeeID()`. Đổi mật khẩu thành công: toast + quay lại `/profile`.
3. Controller mỏng: không SQL, không luật nghiệp vụ; chỉ bind, kiểm quyền, gọi service, trả view.

## Bước 6 - Views và resource

1. `Views/Admin/`: `Products.cshtml` + `_ProductFilter`, `_ProductTable`, `_ProductForm`;
   `Employees.cshtml` + `_EmployeeFilter`, `_EmployeeTable`, `_EmployeeForm`, `_Departments`;
   `Accounts.cshtml` + `_AccountFilter`, `_AccountTable`, `_AccountForm`, `_ResetPasswordForm`;
   `AuditLogs.cshtml` + `_AuditFilter`, `_AuditTable`.
   Mỗi tệp trang chỉ có header và các `<partial>`; dùng lại `_Pager`, `_ConfirmDialog`, `_ActionMessage`,
   `_Badge` đã có, không copy khối.
2. `Views/Profile/Index.cshtml` + `_ProfileCard`, `_ChangePasswordForm`.
3. `Views/Shared/_Layout.cshtml`: tên nhân viên ở chân sidebar thành liên kết tới `/profile` (hoặc thêm
   mục menu) - chọn cách ít đụng nhất, giữ nguyên bố cục.
4. Resource: thêm cặp khóa VI/EN cho mọi chuỗi mới (`Nav_Profile`, `Nav_Products`, `Admin_*`, `Product_*`,
   `Employee_*`, `Department_*`, `Account_*`, `Audit_*`, `Profile_*`, `Position_*`, `Error_*` cho lỗi mới,
   `Common_*` nếu thiếu). Danh sách mã (`Position`, `ProductType`, `Role`, `AuditAction`, tên bảng) đi qua
   `L.Label(group, code)`.
5. Tiền tệ/ngày theo `ccm-language`: đơn giá canh phải, `VNĐ`/`VND`; ngày `dd/MM/yyyy`, giờ `HH:mm:ss`
   cho nhật ký; giá trị gửi xuống CSDL là `decimal`/`DateOnly`, không phải chuỗi đã định dạng.
6. Thông báo sau POST bằng toast; xoá sản phẩm và khoá tài khoản qua `_ConfirmDialog`; trạng thái rỗng có
   icon + câu giải thích; `mb-6`, `card`, `tbl`, `input`, `btn` theo `site.css`, không màu cứng.

## Bước 7 - chỉnh sửa nhỏ còn lại

- `Controllers/AccountController.cs`: cập nhật chú thích "profile thuộc M06" thành hiện thực.
- `Views/Account/Login.cshtml`: không đổi.
- `Views/Shared/_Sidebar.cshtml`: chỉ đổi nếu cần cho mục menu mới; mục chưa làm (`data-soon`) giữ nguyên
  cơ chế.

## Bước 8 - kiểm thử

1. Ca thuần (không CSDL), tệp mới `tests/CcmWeb.Tests/AdminRulesTests.cs`,
   `AccountRulesTests.cs`, `ProfileRulesTests.cs`: kiểm tra dữ liệu form, chính sách mật khẩu, quy tắc
   tự bảo vệ, bộ lọc nhật ký hợp lệ, và `LocalizationTests` bổ sung khóa mới cho cả hai ngôn ngữ.
2. Ca CSDL thật `AdminDatabaseTests.cs`: trigger nhật ký của ba bảng (Insert/Update/Delete, actor đúng,
   không có hash trong `Detail`, UPDATE chỉ đổi hash không sinh dòng), unique index phòng ban, trùng
   username/email/điện thoại, FK chặn xoá sản phẩm đang dùng, hash sau khi đổi mật khẩu khác SHA2 và
   `VerifyHashedPassword` trả `Success`.
3. Chạy `dotnet test` (bộ test dựng lại `CustomerCareDB_Test` từ 01-03, không đụng `CustomerCareDB`).
4. Kiểm thủ công trên `CustomerCareDB` thật bằng trình duyệt (đăng nhập `john` là Manager, `mary` là Sales
   để thử 403), ghi lại từng bước và kết quả thật vào `test-results.md`; ca không chạy ghi NOT RUN, không
   tick khống.
5. Lỗi phát hiện → `bug-report.md`, chờ duyệt cả lô trước khi sửa, sửa xong chạy lại đúng các ca bị ảnh
   hưởng.

## Bước 9 - tài liệu và bàn giao

- Cập nhật `bug-report.md`, `test-results.md`, viết `report.md` (việc đã làm, đường dẫn chính, bằng chứng
  đã chạy, mục BLOCKED/NOT RUN, bước chờ duyệt).
- Cập nhật `schema.md` và `AGENTS.md` nếu có quy tắc mới đáng ghi (ví dụ danh sách trigger nhật ký).
- Không commit trừ khi người dùng yêu cầu.

## Rủi ro và cách xử lý

| Rủi ro | Cách xử lý |
|---|---|
| Thêm trigger làm `SaveChanges` hỏng với lỗi 334 | Đặt `UseSqlOutputClause(false)` cho đúng ba thực thể **trước** khi thử ghi lần đầu, và có ca test ghi thật |
| Trigger ACCOUNT ghi rác mỗi lần đăng nhập nâng cấp hash | `WHERE` chỉ ghi khi `Username`/`Role`/`IsActive` đổi; có ca test đăng nhập không sinh dòng nhật ký |
| Chạy lại `database/01` trên máy thật sẽ xoá dữ liệu | Chỉ chạy phần DDL mới bằng `sqlcmd`; ghi rõ lệnh trong `report.md` |
| Tự khoá tài khoản Manager duy nhất | Kiểm tra ở Service (đếm Manager đang hoạt động) + ca test; thông báo nghiệp vụ, không phải 500 |
| Tên constraint UNIQUE do hệ thống sinh nên khó dịch lỗi | Kiểm tra trùng ở Service trước; nhánh dịch chỉ là lưới an toàn với thông báo chung |
| Nội dung tiếng Việt bị lệch mã hóa khi ghi tệp | Mọi tệp do công cụ ghi bằng UTF-8; kiểm tra nhanh chuỗi tiếng Việt trên trang sau khi build |
