# M06 - Quản trị - báo cáo bàn giao

Trạng thái: **đã triển khai, đã chạy kiểm thử trên SQL Server thật ngày 28/10/2026; lỗi B01 đã sửa và
chạy lại T22-T26 ngày 10/11/2026**. Chưa commit. Chi tiết từng ca ở `test-results.md`; lỗi và cách sửa ở
`bug-report.md`.

## 1. Đã làm

- **Danh mục sản phẩm `/admin/products`** cho cả ba vai trò (Quản lý được thêm/sửa/xoá, Kinh doanh và
  Hỗ trợ chỉ đọc). Lọc theo từ khoá và loại, sắp xếp theo tên/loại/giá, phân trang; cột "đang dùng"
  đếm số dòng hợp đồng để biết sản phẩm nào xoá được. Sản phẩm đã nằm trong hợp đồng chỉ sửa được,
  không xoá được (`Error_ProductInUse`). Sửa giá không viết lại giá cũ trong `CONTRACT_DETAIL`.
- **Nhân viên và phòng ban `/admin/employees`**: danh sách lọc theo phòng ban, chức danh, từ khoá;
  thêm/sửa nhân viên; thẻ phòng ban thêm mới và đổi tên tại chỗ, kèm sĩ số và trưởng phòng. Không xoá
  cứng nhân viên; mỗi phòng ban tối đa một Trưởng phòng (unique index `UX_Employee_DepartmentManager`).
  Nhân viên chưa có tài khoản có liên kết "Tạo tài khoản" mở sẵn đúng người đó.
- **Tài khoản `/admin/accounts`**: danh sách lọc theo vai trò, trạng thái, từ khoá; tạo tài khoản cho
  nhân viên chưa có; sửa vai trò và khoá/mở khoá; đặt lại mật khẩu. Chặn tự khoá/tự hạ quyền và chặn
  khoá Quản lý đang hoạt động cuối cùng (`Error_SelfLockout`, `Error_LastManager`).
- **Nhật ký hệ thống `/admin/audit-logs`** chỉ đọc: lọc theo người thực hiện, bảng, hành động, khoảng
  ngày; mới nhất trước; giá trị mã hoá hiện qua badge, chi tiết giữ nguyên giá trị trigger ghi.
- **Hồ sơ cá nhân `/profile`** cho mọi vai trò: xem thông tin của mình (chỉ đọc, sửa thì nhờ Quản lý) và
  đổi mật khẩu của mình (phải nhập mật khẩu hiện tại).
- **Mật khẩu**: quy tắc 8-100 ký tự gom vào `Helpers/PasswordRules.cs`, dùng chung cho đăng nhập, trang
  tài khoản và `/profile`; tài khoản còn hash SHA2 của dữ liệu mẫu được nâng lên `PasswordHasher` ngay
  lần đăng nhập hoặc đổi mật khẩu đầu tiên. Hash không bao giờ rời khỏi CSDL.
- **Cơ sở dữ liệu**: thêm ba trigger audit `trg_Product_AuditLog`, `trg_Employee_AuditLog`,
  `trg_Account_AuditLog` (ghi `Insert`/`Update`/`Delete`, ghi giá trị chứ không ghi câu, không bao giờ
  ghi hash, bỏ qua lần cập nhật chỉ đổi hash) và index `IDX_AuditLog_LogTime ON AUDIT_LOG(LogTime DESC)`.
  Cả hai đã áp lên `CustomerCareDB` thật; `schema.md` đã cập nhật. `UseSqlOutputClause(false)` được bật
  cho ba bảng có trigger để EF không vấp lỗi 334.
- **Giao diện**: bốn trang quản trị dùng chung `Views/Admin/` và các partial (tab, bộ lọc, bảng, form,
  phân trang `_Pager`, trạng thái rỗng `_ListEmptyState`); mỗi trang chỉ còn phần đầu trang và các lệnh
  `<partial>`. Menu theo vai trò: Quản lý thấy bốn mục, Kinh doanh/Hỗ trợ chỉ thấy "Sản phẩm"; tên trong
  sidebar mở `/profile`.
- **Tài nguyên**: 145 khoá mới cho cả `SharedResource.resx` và `SharedResource.en.resx` (612 khoá mỗi
  tệp, không lệch, không trùng). Không còn chuỗi tiếng Anh nào viết thẳng trong view.
- **Kiểm thử**: 59 ca mới trong bốn tệp, tổng 218 ca đạt trên `CustomerCareDB_Test`.

## 2. Đường dẫn chính

| Loại | Tệp |
|---|---|
| Trang | `src/CcmWeb/Views/Admin/` (Products, ProductCreate, ProductEdit, Employees, EmployeeCreate, EmployeeEdit, Accounts, AccountCreate, AccountEdit, AccountPassword, AuditLogs, `_AdminTabs`, `_ProductForm`, `_ProductFilter`, `_ProductTable`, `_EmployeeForm`, `_EmployeeFilter`, `_EmployeeTable`, `_Departments`, `_AccountFilter`, `_AccountTable`, `_AuditFilter`, `_AuditTable`) |
| Trang | `src/CcmWeb/Views/Profile/Index.cshtml`, `_ProfileCard.cshtml`, `_ChangePasswordForm.cshtml` |
| Dùng chung | `src/CcmWeb/Views/Shared/_Pager.cshtml`, `_ListEmptyState.cshtml`, `_Sidebar.cshtml` |
| Controller | `Controllers/AdminProductsController.cs`, `AdminEmployeesController.cs`, `AdminAccountsController.cs`, `AdminAuditLogsController.cs`, `AdminController.cs`, `ProfileController.cs` |
| Service | `Services/ProductCatalogService.cs`, `EmployeeService.cs`, `AdminAccountService.cs`, `AuditLogService.cs`, `ProfileService.cs` |
| Model | `Models/ViewModels/ProductViewModels.cs`, `EmployeeViewModels.cs`, `AccountViewModels.cs`, `AuditLogViewModels.cs`, `ProfileViewModel.cs`, `PagedListViewModel.cs` |
| Trợ giúp | `Helpers/WorkConstants.cs`, `ListQuery.cs`, `PasswordRules.cs`, `Badges.cs`, `Navigation.cs`, `Utils.cs` |
| Cấu hình | `src/CcmWeb/Program.cs` (5 service, `AddRazorOptions` thêm `/Views/Admin/{0}.cshtml`) |
| SQL | `database/01_CreateTables.sql` (index), `database/02_Function_View_Proc_Trigger.sql` (mục 7-9) |
| Kiểm thử | `tests/CcmWeb.Tests/AdminRulesTests.cs`, `AdminDatabaseTests.cs`, `AccountRulesTests.cs`, `ProfileRulesTests.cs`, `AdminTestSupport.cs` |
| Tài liệu | `docs/tasks/m06-admin/requirements.md`, `plan.md`, `test-results.md`, `bug-report.md` |

## 3. Đã kiểm chứng thật

- `dotnet build src\CcmWeb\CcmWeb.csproj` - 0 cảnh báo, 0 lỗi (chạy lại 10/11/2026 sau khi sửa B01).
- `dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj` - 218/218 đạt trên `CustomerCareDB_Test`
  (SQL Server thật, không dùng InMemory/SQLite), gồm 4 ca mới cho B01.
- 13 địa chỉ GET của bốn trang quản trị, các form, `/profile` - 200, không lộ resource key thô, không
  lộ hash; bản tiếng Anh hiện đúng nhãn.
- Vòng ghi qua web trên `CustomerCareDB`: tạo/sửa/xoá sản phẩm, tạo nhân viên, sửa nhân viên, thêm và
  đổi tên phòng ban, tạo tài khoản, đăng nhập bằng tài khoản mới, xoá sản phẩm đang dùng bị chặn kèm
  cảnh báo, nhật ký ghi đúng người thực hiện.
- Vòng tài khoản chạy lại 10/11/2026 trên `CustomerCareDB_Test`: tạo tài khoản, khoá qua form (có
  `AUDIT_LOG` `State: Active -> Locked`), đăng nhập bị chặn kèm thông báo, đặt lại mật khẩu, mật khẩu cũ
  hết hiệu lực, mở khoá + đổi vai trò, đăng nhập bằng mật khẩu mới. Dữ liệu thử đã xoá.
- Phân quyền: `mary`, `david` chỉ đọc `/admin/products`, ba trang quản trị còn lại chuyển
  `/access-denied`, menu chỉ có "Sản phẩm".
- Dữ liệu thử đã dọn: 8 sản phẩm, 8 nhân viên, 4 phòng ban, 6 tài khoản; `AUDIT_LOG` giữ 15 dòng.

## 4. Chưa đạt / chưa chạy

- **B01 đã sửa** (`[ValidateNever]` cho bốn trường chỉ để hiển thị) và T22-T26 đã chạy lại đạt; xem
  `bug-report.md`.
- **Đã chạy bổ sung ngày 10/11/2026** bằng trình duyệt thật: N02 (lọc tài khoản) **PASS**, N03 (dòng
  nhật ký "Hệ thống") **PASS**, N05 (tự khoá/tự hạ quyền) **PASS một phần**, N01 (15 ảnh ba bề rộng)
  **PASS sau khi sửa B02** - B02 là lỗi tràn ngang ở bề rộng điện thoại, đã sửa trong cùng đợt. Chi
  tiết ở `test-results.md` mục 5, ảnh ở `screenshots/`.
- **Còn để mở**: nhánh "Manager hoạt động cuối cùng" chỉ kiểm bằng ca CSDL vì giao diện chặn thao tác
  tự hạ quyền trước khi chạm tới nhánh đó.

## 5. Bước đang chờ

Người dùng xem lại kết quả sau khi sửa B01, B02 và đồng ý commit phần bổ sung này (M06 gốc đã commit ở
`dcfae12`).
