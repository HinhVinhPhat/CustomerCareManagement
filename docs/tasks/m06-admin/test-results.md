# M06 - Quản trị - kết quả kiểm thử

Ngày chạy: **28/10/2026**, hai đợt trong cùng ngày (đợt 1 trước khi dọn dữ liệu, đợt 2 sau khi thêm
ba trigger audit vào `CustomerCareDB` thật), cộng **đợt 3 ngày 10/11/2026** chạy lại T22-T26 sau khi
sửa B01, cộng **đợt 4 ngày 10/11/2026** chạy N01-N05 bằng trình duyệt thật (mục 5). Mọi dòng dưới đây
đã được chạy thật; việc gì chưa chạy thì ghi rõ **NOT RUN**, không đánh dấu PASS. Lỗi tìm thấy nằm ở
`bug-report.md`: **B01 và B02 đã sửa và đã chạy lại**.

## 1. Môi trường thực tế

| Mục | Giá trị |
|---|---|
| Hệ điều hành | Windows 11 Home Single Language, build 26300 |
| .NET SDK | 10.0.401 |
| Target framework | `net10.0` |
| Cơ sở dữ liệu | Microsoft SQL Server 2025 (RTM) 17.0.1000.7, instance `.\SQLEXPRESS` |
| CSDL dùng để kiểm thử tự động | `CustomerCareDB_Test`, do `TestDatabase` dựng lại từ `database/01`-`03` mỗi lần chạy |
| CSDL dùng cho kiểm thử tay | `CustomerCareDB` thật, đăng nhập `app_ccm` (ứng dụng), `sqlcmd -E` (chỉ để dọn dữ liệu) |
| Cách chạy ứng dụng | `dotnet run --project src/CcmWeb --no-build`, `http://localhost:5293` |
| Tài khoản | `john` (Quản lý), `mary` (Kinh doanh), `david` (Hỗ trợ), mật khẩu `123456` |
| Cách kiểm tra giao diện | `Invoke-WebRequest` + đọc HTML trả về (công cụ chụp ảnh trình duyệt báo lỗi "Screenshot needs a visible tab") |

Trước đợt kiểm thử, ba trigger `trg_Product_AuditLog`, `trg_Employee_AuditLog`, `trg_Account_AuditLog`
và index `IDX_AuditLog_LogTime` đã được áp lên `CustomerCareDB` thật bằng `sqlcmd` (chỉ phần DDL mới,
không chạy lại `01_CreateTables.sql`). Sau khi áp, `SELECT name FROM sys.triggers` trả **9 trigger** và
`sys.indexes` của `AUDIT_LOG` trả **3 index**.

## 2. Kiểm thử tự động

```
dotnet build src\CcmWeb\CcmWeb.csproj
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```
dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj
Passed!  - Failed: 0, Passed: 218, Skipped: 0, Total: 218, Duration: 27 s - CcmWeb.Tests.dll (net10.0)
```

(Lần chạy 10/11/2026 sau khi sửa B01: 218 ca, gồm 214 ca cũ và 4 ca mới
`A_field_the_form_only_shows_is_never_validated`.)

59 ca mới của M06, trong bốn tệp và một lớp trợ giúp:

| Tệp | Số ca | Nội dung |
|---|---|---|
| `AdminRulesTests.cs` | 22 | Giới hạn độ dài mật khẩu; chuỗi truy vấn của liên kết sắp xếp/lọc/phân trang; số dòng và số trang của `_Pager`; danh sách giá trị mã hoá khớp CHECK constraint; bốn trường chỉ để hiển thị phải mang `[ValidateNever]` (B01) |
| `AdminDatabaseTests.cs` | 23 | Sản phẩm, nhân viên, phòng ban, tài khoản, nhật ký trên `CustomerCareDB_Test`; mỗi ca đi qua Service nên chứng minh cả trigger audit ghi đúng người thực hiện |
| `AccountRulesTests.cs` | 7 | `PasswordRules.Verify`: hash `PasswordHasher`, hash SHA2 demo (kèm yêu cầu đổi hash), mật khẩu sai, mật khẩu rỗng, nhận dạng hash demo |
| `ProfileRulesTests.cs` | 7 | `/profile` đọc đúng người và vai trò; sai mật khẩu hiện tại; mật khẩu mới quá ngắn; trùng mật khẩu cũ; đổi thành công; nâng cấp hash demo; nhân viên chưa có tài khoản |
| `AdminTestSupport.cs` | - | Context có claim `employeeID` (để `SESSION_CONTEXT` chạy), email/điện thoại duy nhất cho mỗi ca |

Bốn ca SQL là chỗ dễ hỏng nhất và đều đã chạy thật: `The_last_active_manager_cannot_be_locked` (bọc
trong transaction rồi rollback để không khoá mất Manager mẫu của các ca sau),
`A_second_manager_in_one_department_is_refused` (unique index `UX_Employee_DepartmentManager`),
`A_product_that_a_contract_already_uses_is_not_deleted`, `The_audit_screen_reads_newest_first_and_filters_in_the_database`.

Trong lúc viết ca, ba ca đầu tiên của `AdminRulesTests` đã **FAIL** vì kỳ vọng sai của người viết test
(không phải lỗi ứng dụng): `ProductFilter.SortQuery` với cột đang sắp xếp trả `?desc=true` chứ không
trả chuỗi rỗng, `PageQuery(1)` vẫn ghi `page=1` khi bộ lọc khác rỗng, và `EmployeeFilter.Department` là
`int?` nên giá trị "không lọc" là `null` chứ không phải `0`. Ba ca đã được sửa lại cho đúng hành vi
thật rồi chạy lại; lần chạy cuối 214/214 đạt.

## 3. Kiểm thử tay qua HTTP (CSDL thật)

Đợt 1 - đọc và ghi trên `CustomerCareDB`, tài khoản `john`:

| Mã | Ca | Kết quả |
|---|---|---|
| T03 | `GET /admin` chuyển tới `/admin/products`; `GET` 13 địa chỉ (`/admin/products`, `/create`, `/{id}/edit`, `/admin/employees`, `/create`, `/{id}/edit`, `/admin/accounts`, `/create`, `/{id}/edit`, `/{id}/password`, `/admin/audit-logs`, `/profile`) | PASS - tất cả 200, đúng `<h1>` từng trang |
| T04 | Không lộ resource key thô trên cả 13 trang | PASS - không có chuỗi dạng `Product_Name` |
| T05 | Chuyển sang tiếng Anh rồi mở lại 5 trang | PASS - `Products`, `Employees`, `Accounts`, `Audit log`, `My profile`; không lộ key thô |
| T06 | Quay lại tiếng Việt | PASS - nhãn trở lại tiếng Việt |
| T07 | Phân trang: chữ "Hiển thị 1-10 trong 10 kết quả" (VI) và "Showing 1-10 of 10 results" (EN) | PASS |
| T08 | Badge `Position`, `AccountState`, `AuditAction`, `TableName` | PASS - "Trưởng phòng", "Hoạt động", "Tạo mới", "Cập nhật", "Sản phẩm" |
| T09 | Sidebar có liên kết `/profile` | PASS |
| T10 | Không lộ hash mật khẩu trong HTML của trang tài khoản | PASS - không có chuỗi `AQAAAA` |
| T11 | Tạo sản phẩm qua form | PASS - toast "Đã thêm sản phẩm.", sản phẩm hiện trong danh sách |
| T12 | Tạo sản phẩm trùng tên | PASS - ở lại form, lỗi ngay dưới ô: "Tên sản phẩm này đã có." |
| T13 | Tạo nhân viên qua form | PASS - toast "Đã thêm nhân viên." |
| T14 | Thêm phòng ban | PASS - toast "Đã thêm phòng ban.", phòng mới hiện trong danh sách |
| T15 | Đổi mật khẩu của chính mình với mật khẩu hiện tại sai | PASS - lỗi tại ô: "Mật khẩu hiện tại không đúng." |
| T16 | Trang sửa tài khoản của chính mình | PASS - ô vai trò và ô "Đang hoạt động" bị `disabled`, có dòng giải thích, có hidden field để POST vẫn đủ giá trị |
| T17 | `mary` (Kinh doanh) và `david` (Hỗ trợ): `/admin/products` đọc-only; `/admin/employees`, `/admin/accounts`, `/admin/audit-logs` | PASS - sản phẩm 200, không nút thêm/xoá, không tab quản trị; ba trang kia chuyển tới `/access-denied` (HTTP 200 của trang từ chối) |
| T18 | Menu của `mary` | PASS - chỉ có "Sản phẩm" trong nhóm quản trị |

Đợt 2 - vòng đầy đủ "nhân viên → tài khoản → đăng nhập → khoá → đặt lại mật khẩu → mở khoá":

| Mã | Ca | Kết quả |
|---|---|---|
| T19 | Tạo nhân viên mới qua `/admin/employees/create` | PASS - toast "Đã thêm nhân viên.", mã 10 |
| T20 | Tạo tài khoản cho nhân viên đó qua `/admin/accounts/create` (vai trò Kinh doanh) | PASS - toast "Đã tạo tài khoản.", mã 7 |
| T21 | Đăng nhập bằng tài khoản mới | PASS - vào được `/profile`, menu có "Sản phẩm", không có "Nhân viên" |
| T22 | Khoá tài khoản bằng form sửa tài khoản thật (chỉ gửi `Role`, bỏ chọn `IsActive`) | **PASS** (chạy lại 10/11/2026 sau khi sửa B01) - 302 về `/admin/accounts`, toast "Đã lưu tài khoản.", `ACCOUNT.IsActive = 0`, `AUDIT_LOG` có `ACCOUNT 20 Update State: Active -> Locked` với `EmployeeID = 4` (susan) |
| T23 | Đăng nhập khi tài khoản đã bị khoá | **PASS** (chạy lại 10/11/2026) - ở lại `/login` (HTTP 200), hiện "Tài khoản đã bị khoá. Vui lòng liên hệ quản trị viên."; khoá chỉ báo sau khi mật khẩu đúng nên không dò được tài khoản nào đang khoá |
| T24 | Đặt lại mật khẩu bằng form thật (chỉ gửi `Password`, `ConfirmPassword`) | **PASS** (chạy lại 10/11/2026) - 302, toast "Đã đặt lại mật khẩu.", mật khẩu cũ bị từ chối |
| T25 | Mở khoá tài khoản | **PASS** (chạy lại 10/11/2026) - `Role = Support`, `IsActive = true`, `AUDIT_LOG` `Update Role: Sales -> Support; State: Locked -> Active` |
| T26 | Đăng nhập bằng mật khẩu mới sau khi đặt lại | **PASS** (chạy lại 10/11/2026) - 302 `/`; `/` và `/profile` trả 200; `/admin/accounts` vẫn chuyển `/access-denied` với vai trò Hỗ trợ |
| T27 | Chứng minh nguyên nhân B01: POST cùng hai form nhưng gửi thêm `Username` và `EmployeeName` | PASS - toast "Đã lưu tài khoản." / "Đã đặt lại mật khẩu."; `AUDIT_LOG` có dòng `ACCOUNT 7 Update: Role: Sales -> Support`; danh sách hiện vai trò mới. Hành vi không đổi sau khi sửa B01 |
| T28 | Tạo sản phẩm qua form | PASS - toast "Đã thêm sản phẩm.", mã 11 |
| T29 | Sửa sản phẩm (đổi tên, loại, đơn giá) | PASS - toast "Đã lưu sản phẩm."; `AUDIT_LOG` ghi `Name: … | Type: Service -> Product | UnitPrice: 1500000 -> 1750000` |
| T30 | Xoá sản phẩm chưa dùng | PASS - toast "Đã xoá sản phẩm.", sản phẩm biến khỏi danh sách, `AUDIT_LOG` có dòng `Delete` |
| T31 | Xoá sản phẩm đang nằm trong hợp đồng (mã 1) | PASS - quay lại danh sách, khối cảnh báo "Sản phẩm đã có trong hợp đồng nên không thể xoá.", sản phẩm còn nguyên |
| T32 | Sửa nhân viên với đầy đủ trường như form gửi | PASS - toast "Đã lưu nhân viên." |
| T33 | Đổi tên phòng ban rồi đổi lại | PASS - toast "Đã đổi tên phòng ban." cả hai lần |
| T34 | Lọc nhật ký `table=ACCOUNT&action=Update` trong hôm nay | PASS - 2 dòng, có badge "Cập nhật" |
| T35 | Lọc nhật ký khoảng ngày tương lai | PASS - trạng thái rỗng "Không tìm thấy kết quả nào" |
| T36 | Phân trang nhật ký `pageSize=2`, trang 2, và `page=99` | PASS - "Hiển thị 1-2 trong 13 kết quả", liên kết `page=2`, trang 99 hiện "Trang này không còn dữ liệu" |
| T37 | Lọc nhân viên theo từ khoá không tồn tại | PASS - trạng thái "Không tìm thấy kết quả nào" |
| T38 | Trigger audit ghi đúng người thực hiện khi thao tác qua web | PASS - các dòng `Insert/Update/Delete` của sản phẩm, nhân viên, tài khoản đều có `EmployeeID = 1` (john) |

Ghi chú trung thực: đợt 2 (28/10/2026) T22-T26 không chạy được vì B01; đợt 3 ngày 10/11/2026 đã sửa B01
và chạy lại cả năm ca qua HTTP thật. Đợt 3 chạy trên `CustomerCareDB_Test` (đặt
`ConnectionStrings__DefaultConnection` cho ứng dụng, đăng nhập Windows) thay vì `CustomerCareDB` thật, vì
`app_ccm` không có quyền trên CSDL thử; tài khoản `e2eb01` (mã 20) và ba dòng `AUDIT_LOG` của nó đã xoá
sau khi ghi kết quả, `ACCOUNT` trở lại 17 dòng và `AUDIT_LOG` 53 dòng (đúng số dòng trước đợt 3).

Giới hạn đăng nhập: chính sách `LoginRateLimitPolicy` cho 10 lần đăng nhập / 5 phút cho mỗi IP. Ở đợt 1
script kiểm thử đăng nhập quá nhiều lần nên nhận **429 Too Many Requests**; đợt 2 chạy lại sau khi khởi
động lại ứng dụng (bộ đếm nằm trong bộ nhớ) và giữ số lần đăng nhập dưới ngưỡng. Đây là hành vi đúng
của chính sách, không phải lỗi.

## 4. Dữ liệu thử đã dọn

Mọi thao tác ghi ở trên nằm trên `CustomerCareDB` thật, nên dữ liệu thử đã được xoá bằng `sqlcmd` sau
khi ghi lại kết quả:

| Bảng | Trước khi dọn | Sau khi dọn |
|---|---|---|
| `PRODUCT_SERVICE` | 8 sản phẩm mẫu + 3 sản phẩm thử | 8 |
| `EMPLOYEE` | 8 nhân viên mẫu + 2 nhân viên thử | 8 |
| `DEPARTMENT` | 4 phòng ban mẫu + 1 phòng thử | 4 |
| `ACCOUNT` | 6 tài khoản mẫu + 1 tài khoản thử | 6 |
| `AUDIT_LOG` | 15 dòng | 15 (giữ nguyên: nhật ký là append-only, các dòng thử chứng minh trigger chạy thật) |

`AUDIT_LOG` còn 15 dòng; 9 dòng đầu do đợt kiểm thử M06 sinh ra (`PRODUCT_SERVICE` 9-11,
`EMPLOYEE` 9-10, `ACCOUNT` 7, cùng các dòng `Delete` khi dọn). Hai dòng `Delete` cuối có `EmployeeID`
NULL vì việc dọn chạy bằng `sqlcmd` không đặt `SESSION_CONTEXT` - đúng như thiết kế, và cũng là bằng
chứng rằng đường ghi qua ứng dụng mới có người thực hiện.

## 5. Chạy bổ sung bằng trình duyệt thật - đợt 10/11/2026 (N01-N05)

Ba ca N01, N02, N03 và một phần N05 trước đây để **NOT RUN** vì thiếu công cụ chụp ảnh. Đợt này chạy
trên ứng dụng thật (`http://localhost:5293`, CSDL `CustomerCareDB` trên `.\SQLEXPRESS`) bằng Chromium
chạy không giao diện, điều khiển qua DevTools Protocol: đăng nhập `john`/`123456`, đặt bề rộng khung
nhìn (`Emulation.setDeviceMetricsOverride`) rồi chụp ảnh bằng `Page.captureScreenshot`.

| Mã | Ca | Kết quả | Bằng chứng |
|---|---|---|---|
| N01 | Ảnh 4 trang quản trị + tổng quan ở 3 bề rộng | **PASS** (sau khi sửa B02) | Đủ 15 ảnh trong `docs/tasks/m06-admin/screenshots/` (5 trang x 1280/768/412). Đo sau khi sửa: `document.documentElement.scrollWidth` = `innerWidth` ở **cả 15 ảnh** (1280/768/412), không còn tràn ngang; bảng dạng thẻ ở 412 px hiện nhãn "Thao tác" một dòng và hai nút nằm gọn bên phải. Trước khi sửa: 510 px (4 trang quản trị) và 738 px (tổng quan) - lỗi **B02** ở `bug-report.md`. |
| N02 | Lọc tài khoản theo vai trò và trạng thái | **PASS** | Chọn `Vai trò = Hỗ trợ` -> `?role=Support&state=`, 2 dòng `david`, `helen`, "Hiển thị 1-2 trong 2 kết quả"; thêm `Trạng thái = Đã khoá` -> 0 dòng, hiện "Không tìm thấy kết quả nào" (đúng: `SELECT Role, IsActive FROM ACCOUNT` cho 2/2/2 và cả 6 đều `IsActive = 1`); `Vai trò = Quản lý` + `Hoạt động` -> 2 dòng `john`, `susan`; bấm "Xoá bộ lọc" -> về `/admin/accounts`, 6 dòng. |
| N03 | Dòng nhật ký không có người thực hiện hiện "Hệ thống" | **PASS** | `/admin/audit-logs` có 6 dòng chữ "Hệ thống", khớp đúng `SELECT COUNT(*) FROM AUDIT_LOG WHERE EmployeeID IS NULL` = 6; ví dụ dòng đầu: `10/11/2026 18:34 | Hệ thống | Hợp đồng | 3 | Đổi trạng thái | Active -> Expired` (do job hết hạn hợp đồng ghi, không có người dùng). |
| N05 | Tự khoá / tự hạ quyền trên giao diện | **PASS một phần** | `/admin/accounts/1/edit` (tài khoản của chính `john`): `select[name=Role]` và `input[name=IsActive]` đều `disabled`, kèm chú thích "Đây là tài khoản bạn đang đăng nhập, nên không thể tự khoá hay tự đổi vai trò."; vẫn có hidden input để giữ giá trị khi lưu. Nhánh "Manager hoạt động cuối cùng" **không chạy được qua giao diện** vì muốn chạm tới phải hạ quyền tài khoản kia trước, mà tài khoản đang đăng nhập bị chặn - chỉ kiểm bằng ca CSDL `AdminDatabaseTests`. |

### Còn để mở

| Mã | Ca | Lý do |
|---|---|---|
| N05b | "Manager hoạt động cuối cùng" bấm trên giao diện | Như trên: giao diện chỉ chặn được nhánh tự hạ quyền; nhánh cuối cùng đã có ca CSDL, không dựng lại được bằng thao tác hợp lệ trên UI |

Sau khi sửa B02: `dotnet build src\CcmWeb\CcmWeb.csproj` **0 cảnh báo, 0 lỗi**;
`dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj` **218/218 đạt**.
