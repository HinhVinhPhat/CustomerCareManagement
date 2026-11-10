# M06 - Quản trị và hồ sơ cá nhân

Trạng thái: **chờ người dùng duyệt** (chưa code). Bảy câu hỏi ở mục 7 cần được trả lời; mục 7 ghi kèm
phương án đề xuất để duyệt nhanh.

Tài liệu tham chiếu: `docs/design.md` mục 4 (sitemap `/admin/*`, `/profile`), 5.1 (ma trận quyền),
5.2 (menu theo vai trò), 6.13-6.15, giai đoạn 7 của mục 8; `AGENTS.md` mục 3, 4, 5;
`.agents/skills/ccm-database/references/schema.md`; `.agents/skills/ccm-auth-security`;
`.agents/skills/ccm-mvc-architecture`; `.agents/skills/ccm-web-ui`; `.agents/skills/ccm-language`.

## 1. Mục tiêu

Hoàn tất phần quản trị mà một mình vai trò Manager dùng: quản lý sản phẩm/dịch vụ, nhân viên và phòng
ban, tài khoản đăng nhập, và tra cứu nhật ký hệ thống. Kèm theo là trang hồ sơ cá nhân `/profile` cho
**mọi** nhân viên, nơi họ xem thông tin của mình và tự đổi mật khẩu.

Nguyên tắc giữ nguyên từ các module trước: CSDL giữ luật nghiệp vụ, ứng dụng chỉ gọi vào; mọi chữ trên
màn hình đi qua resource key; mọi POST có anti-forgery token; mật khẩu chỉ tồn tại dưới dạng hash
`PasswordHasher` và không bao giờ hiện ra hay ghi log.

## 2. Phạm vi

### 2.1. Trong phạm vi

| Trang | Đường dẫn | Vai trò |
|---|---|---|
| Sản phẩm / dịch vụ | `/admin/products` (+ `/create`, `/{id}/edit`) | Manager: xem, thêm, sửa, xoá. Sales, Support: chỉ xem (xem câu hỏi 4) |
| Nhân viên và phòng ban | `/admin/employees` (+ `/create`, `/{id}/edit`) | Manager |
| Tài khoản | `/admin/accounts` (+ `/create`, `/{id}/edit`, `/{id}/password`) | Manager |
| Nhật ký hệ thống | `/admin/audit-logs` | Manager |
| Hồ sơ cá nhân | `/profile` (+ `/profile/password`) | Mọi vai trò, chỉ dữ liệu của chính mình |

Kèm theo: bật mục menu `administration` trong `Helpers/Navigation.cs` (hiện `Built = false`), thêm mục
menu cho `/profile` (truy cập từ tên nhân viên ở chân sidebar), và bổ sung tài liệu `docs/tasks/m06-admin/`.

### 2.2. Ngoài phạm vi (không làm nếu không được duyệt riêng)

- Đăng ký tài khoản tự do, quên mật khẩu qua email, xác thực hai lớp, đổi ảnh đại diện.
- Phân quyền chi tiết theo từng chức năng (vẫn là 3 vai trò cố định trong `ACCOUNT.Role`).
- Xoá cứng nhân viên hoặc tài khoản (xem câu hỏi 3).
- Sửa `EMPLOYEE.Position` thành danh sách mới, thêm bảng quyền, thêm cột `IsActive` cho `EMPLOYEE`.
- Thay đổi nội dung các trang M02-M05, trừ việc dùng lại partial/badge đã có.

## 3. Hiện trạng đã kiểm tra (sự thật, không phải giả định)

- `DEPARTMENT(DepartmentID, DepartmentName)`; `EMPLOYEE(EmployeeID, FullName, Email, Phone, Gender,
  Position, DepartmentID)`; `ACCOUNT(AccountID, Username, PasswordHash, Role, EmployeeID, IsActive)`;
  `PRODUCT_SERVICE(ProductID, ProductName, ProductType, UnitPrice, Description)`;
  `AUDIT_LOG(LogID, LogTime, EmployeeID, TableName, RecordID, Action, Detail)`.
- Ràng buộc đáng chú ý: `CHK_Employee_Position (Manager|Staff)`, `CHK_Employee_Gender`,
  `CHK_Employee_Email`, `CHK_Employee_Phone` (chỉ số, 9-11 ký tự), `CHK_Account_Role`,
  `CHK_Product_Type (Product|Service)`, `CHK_Product_UnitPrice (>= 0)`;
  `UX_Employee_DepartmentManager` (mỗi phòng ban tối đa **một** nhân viên `Position = 'Manager'`);
  `ACCOUNT.EmployeeID` UNIQUE (mỗi nhân viên tối đa một tài khoản); `ACCOUNT.Username` UNIQUE;
  `EMPLOYEE.Email`, `EMPLOYEE.Phone`, `PRODUCT_SERVICE.ProductName`, `DEPARTMENT.DepartmentName` UNIQUE.
- **Không có** trigger `AUDIT_LOG` cho PRODUCT_SERVICE, EMPLOYEE, ACCOUNT. Hiện chỉ
  `trg_Contract_AuditLog` và `trg_Feedback_AuditLog` ghi nhật ký; bảng `AUDIT_LOG` đang có **0 dòng**.
- `AUDIT_LOG` chỉ có index `(TableName, RecordID)`, chưa có index cho `LogTime` (trang nhật ký sắp theo
  thời gian giảm dần).
- `PRODUCT_SERVICE` không có cột `IsActive`; `CONTRACT_DETAIL.ProductID` là FK tới nó, nên xoá một sản
  phẩm đã dùng sẽ bị CSDL chặn bằng lỗi 547.
- `EMPLOYEE` được tham chiếu bởi ACCOUNT, CUSTOMER.OwnerEmployeeID, CONTRACT, CONTACT, CARE, FEEDBACK,
  AUDIT_LOG - xoá cứng gần như luôn thất bại.
- `EMPLOYEE.Position` (Manager/Staff - cấp bậc công việc) **khác** `ACCOUNT.Role`
  (Manager/Sales/Support - quyền truy cập). Hai cột độc lập, không ràng buộc lẫn nhau.
- `IAccountService` hiện chỉ có `AuthenticateAsync`; việc đổi mật khẩu khi đăng nhập (SHA2 demo →
  `PasswordHasher`) đã có sẵn trong `AccountService.RehashAsync`.
- `Utils.TranslateConstraintErrorKey` đã có `Error_DepartmentHasManager` cho
  `UX_Employee_DepartmentManager`; `AuditAction_*`, `ProductType_*`, `Role_*` đã có trong resource.
- Dữ liệu thật `CustomerCareDB`: 8 sản phẩm, 8 nhân viên, 4 phòng ban, 6 tài khoản, 0 tài khoản bị khoá,
  0 dòng nhật ký.

## 4. Chi tiết từng trang

### 4.1. Sản phẩm / dịch vụ `/admin/products`

- Danh sách: bộ lọc từ khóa (tên, mô tả), lọc loại (Product/Service), sắp xếp theo tên hoặc đơn giá
  (hai chiều), phân trang 10/20/50. Bộ lọc nằm trong URL như các danh sách M02-M05.
- Cột: tên, loại (badge + nhãn dịch), đơn giá (canh phải, `80.000.000 VNĐ` / `80,000,000 VND`),
  mô tả (cắt ngắn), cột thao tác.
- Thêm/sửa: một form, thẻ riêng; `ProductName`*, `ProductType`*, `UnitPrice`*, `Description`.
  Đơn giá nhập bằng số, không cho âm; mô tả tối đa 500 ký tự (đếm ở client và chặn ở server).
- Xoá: chỉ hiện nút khi sản phẩm **chưa** xuất hiện trong hợp đồng nào; luôn qua `_ConfirmDialog`.
  Server kiểm tra lại trước khi xoá; nếu vẫn bị FK chặn thì hiện thông báo nghiệp vụ, không lộ SQL.
- Không có trường "đang dùng / ngừng dùng" (xem câu hỏi 2).

### 4.2. Nhân viên và phòng ban `/admin/employees`

- Danh sách: từ khóa (họ tên, email, điện thoại), lọc phòng ban, lọc cấp bậc (Manager/Staff), phân trang.
  Cột: họ tên, email, điện thoại, phòng ban, cấp bậc, có tài khoản hay chưa (đọc từ `ACCOUNT`).
- Thêm/sửa: `FullName`*, `Email`*, `Phone`*, `Gender`*, `Position`*, `DepartmentID`*.
  Kiểm tra trùng email/điện thoại ở server trước khi ghi và báo lỗi ngay cạnh ô nhập; CSDL vẫn là chốt
  chặn cuối (lỗi 2601/2627 → thông báo chung, không lộ tên constraint).
- Cấp bậc `Manager` cho phòng ban đã có người phụ trách → chặn với thông báo thân thiện
  (`Error_DepartmentHasManager`), không phải trang lỗi 500.
- Không có nút xoá (xem câu hỏi 3). Thẻ phụ "Phòng ban": bảng tên phòng ban + số nhân viên, form thêm
  phòng ban mới, nút đổi tên từng dòng (qua dialog), không cho xoá phòng ban còn nhân viên.
- Sửa nhân viên không đụng tới `ACCOUNT`; việc cấp/khoá tài khoản nằm ở trang Tài khoản.

### 4.3. Tài khoản `/admin/accounts`

- Danh sách: từ khóa (username, họ tên nhân viên), lọc vai trò, lọc trạng thái (đang dùng / đã khoá),
  phân trang. Cột: username, nhân viên, vai trò, trạng thái (badge), thao tác. **Không** có cột hash.
- Tạo: chọn nhân viên **chưa có** tài khoản (dropdown đã lọc sẵn), `Username`*, `Role`*, mật khẩu ban
  đầu* + xác nhận. Mật khẩu hash bằng `PasswordHasher<Account>` trước khi ghi.
- Sửa: đổi `Role`, khoá/mở khoá `IsActive` (khoá phải qua `_ConfirmDialog`).
- Đặt lại mật khẩu: form riêng, mật khẩu mới + xác nhận, ghi hash mới; không hiển thị, không ghi log mật
  khẩu; nhật ký chỉ ghi "đặt lại mật khẩu", không ghi nội dung.
- Chặn tự khoá và tự hạ quyền chính mình, và chặn làm mất **Manager đang hoạt động cuối cùng** (câu hỏi 6).
- Không có nút xoá tài khoản; khoá là cách vô hiệu hoá (xem câu hỏi 3).

### 4.4. Nhật ký hệ thống `/admin/audit-logs`

- Chỉ đọc. Bộ lọc: nhân viên (dropdown), bảng (CONTRACT / FEEDBACK / PRODUCT_SERVICE / EMPLOYEE /
  ACCOUNT), hành động (Insert / StatusChange / Delete / Assign / Update), khoảng ngày (từ - đến),
  phân trang; sắp xếp mặc định thời gian mới nhất trước.
- Cột: thời gian (`dd/MM/yyyy HH:mm:ss`), nhân viên (hoặc "Không xác định" khi `EmployeeID` NULL),
  bảng (nhãn dịch), mã bản ghi, hành động (badge), chi tiết.
- Cột `Detail` do trigger ghi bằng mã tiếng Anh (`Active -> Expired`, `Assignee: 3 -> 5`), hiển thị
  nguyên văn như một cột kỹ thuật; bảng và hành động thì dịch qua `L.Label`.
- Trang rỗng phải nói "chưa có bản ghi nào" kèm gợi ý, không phải bảng trắng.

### 4.5. Hồ sơ cá nhân `/profile`

- Mọi vai trò, chỉ dữ liệu của chính người đang đăng nhập (lấy từ claim `EmployeeID`, không bao giờ từ form).
- Thẻ 1: thông tin chỉ đọc - họ tên, email, điện thoại, giới tính, cấp bậc, phòng ban, username, vai trò.
  Không sửa được ở đây (việc sửa nhân viên là của Manager ở 4.2).
- Thẻ 2: đổi mật khẩu - mật khẩu hiện tại*, mật khẩu mới*, xác nhận*. Sai mật khẩu hiện tại → lỗi cạnh ô
  đầu; mật khẩu mới không hợp lệ → lỗi cạnh ô thứ hai. Đổi xong vẫn giữ phiên đăng nhập, hiện toast
  thành công (không đăng xuất, không đổi cookie).
- Không hiện hash, không ghi mật khẩu vào log.

## 5. Quy tắc nghiệp vụ và bảo mật

1. **Quyền**: `[Authorize(Roles = "Manager")]` trên `AdminController` (trừ action sản phẩm nếu câu hỏi 4
   mở cho Sales/Support) và trên từng action ghi; `[Authorize]` cho `/profile`. Ẩn nút không đủ - server
   phải chặn, và truy cập trái quyền đi về trang 403 đã có.
2. **Người thực hiện** lấy từ claim (`User.GetEmployeeID()`), không từ trường ẩn của form, để
   `AUDIT_LOG` ghi đúng người.
3. **Mật khẩu**: chỉ `PasswordHasher`; so khớp mật khẩu cũ chấp nhận cả hash SHA2 demo (dùng lại
   `Utils.IsLegacySha256Hash` / `VerifyLegacySha256`) rồi nâng cấp ngay sang `PasswordHasher` như luồng
   đăng nhập. Không có mật khẩu mặc định dùng chung.
4. **Kiểm tra trước, CSDL chốt sau**: trùng email/điện thoại/username/tên sản phẩm/tên phòng ban và
   "mỗi phòng ban một Manager" được kiểm ở Service để báo lỗi đúng ô; nếu vẫn lọt thì `Utils` dịch mã lỗi
   thành thông báo tiếng Việt, không lộ chi tiết SQL.
5. **Không xoá cứng** nhân viên, tài khoản; sản phẩm chỉ xoá khi chưa dùng (chờ duyệt câu hỏi 2, 3).
6. **EF Core và trigger**: nếu thêm trigger cho PRODUCT_SERVICE/EMPLOYEE/ACCOUNT (câu hỏi 1) thì ba thực
   thể đó phải `UseSqlOutputClause(false)` trong `ApplicationDbContext.Custom.cs`, nếu không mọi
   `SaveChanges` sẽ hỏng với lỗi 334.
7. **Ghi nhật ký**: trigger đọc `SESSION_CONTEXT('EmployeeID')` do interceptor đặt; nếu không có thì
   `EmployeeID` NULL - chấp nhận được cho thao tác ngoài ứng dụng (sqlcmd), không được xảy ra với thao tác
   trên web (kiểm bằng test).
8. **Giao diện**: mỗi trang chia thành partial (`_FilterBar`, `_Table`, `_Form`, `_Departments`...),
   không nhồi vào một tệp; trạng thái rỗng/lỗi/thành công riêng; thông báo sau POST bằng toast
   (`window.ccmToast`), lỗi từng ô hiện cạnh ô; menu quản trị chỉ hiện với Manager; responsive tới điện thoại.

## 6. Thay đổi CSDL dự kiến (phụ thuộc câu hỏi 1 và 2)

| Tệp | Thay đổi |
|---|---|
| `database/02_Function_View_Proc_Trigger.sql` | 3 trigger `trg_Product_AuditLog`, `trg_Employee_AuditLog`, `trg_Account_AuditLog` ghi `AUDIT_LOG` cho Insert/Update/Delete. Trigger trên ACCOUNT **bỏ qua** lần UPDATE chỉ đổi `PasswordHash` (do luồng đăng nhập nâng cấp hash) và **không** ghi hash vào `Detail` |
| `database/01_CreateTables.sql` | thêm `IDX_AuditLog_LogTime ON AUDIT_LOG(LogTime DESC)` cho trang nhật ký. Nếu duyệt thêm cột `IsActive` cho `PRODUCT_SERVICE` (câu hỏi 2) thì thêm cả cột đó + mặc định 1 |
| `database/03_SampleData.sql` | không đổi, trừ khi duyệt `IsActive` cho sản phẩm |
| `.agents/skills/ccm-database/references/schema.md` | cập nhật bảng trigger, index, và danh sách mã lỗi nếu có thêm |

Không re-scaffold nếu không thêm cột: các thực thể `ProductService`, `Employee`, `Department`, `Account`,
`AuditLog` đã có sẵn từ M01.

## 7. Câu hỏi cần duyệt

1. **Nhật ký cho bảng quản trị.** Hiện `AUDIT_LOG` chỉ có trigger cho CONTRACT và FEEDBACK. Có thêm 3
   trigger cho PRODUCT_SERVICE, EMPLOYEE, ACCOUNT không?
   *Đề xuất: có* - đúng nguyên tắc "CSDL giữ luật", và trigger ACCOUNT bỏ qua lần đổi hash lúc đăng nhập
   để nhật ký không bị nhiễu.
2. **Xoá sản phẩm.** `PRODUCT_SERVICE` không có cột `IsActive`, nên "ẩn đi" như `design.md` gợi ý là
   không làm được nếu không đổi schema.
   *Đề xuất: không thêm cột* - chỉ cho xoá sản phẩm chưa dùng, sản phẩm đã dùng thì chỉ sửa được
   (đơn giá mới không làm thay đổi hợp đồng cũ vì `CONTRACT_DETAIL` đã chốt giá).
   Phương án khác: thêm `IsActive` + ẩn khỏi dropdown hợp đồng, hiện badge "Ngừng dùng".
3. **Xoá nhân viên / tài khoản.** *Đề xuất: không có nút xoá* - nhân viên đã có lịch sử (hợp đồng, chăm
   sóc, phản hồi, nhật ký) nên xoá cứng luôn bị FK chặn; tài khoản thì dùng khoá (`IsActive`).
4. **Sản phẩm cho Sales/Support.** Ma trận 5.1 ghi Sales/Support được **xem** sản phẩm, nhưng menu 5.2
   chỉ cho Manager vào nhóm Admin.
   *Đề xuất: mở `/admin/products` cho cả ba vai trò (Sales/Support chỉ đọc) và thêm một mục menu
   "Sản phẩm" riêng cho hai vai trò đó*, giữ nhóm "Quản trị" cho Manager.
   Phương án khác: chỉ Manager, và ghi lại việc lệch ma trận.
5. **Chính sách mật khẩu.** *Đề xuất: tối thiểu 8 ký tự, tối đa 100, không bắt ký tự đặc biệt, mật khẩu
   mới phải khác mật khẩu cũ; mật khẩu do Manager đặt lại là mật khẩu tạm, không bắt đổi ở lần đăng nhập
   kế tiếp* (muốn bắt buộc thì phải thêm cột vào `ACCOUNT`).
   Lưu ý: tài khoản demo `123456` vẫn đăng nhập được (luồng đăng nhập không đổi), chỉ mật khẩu **mới** phải
   từ 8 ký tự.
6. **Bảo vệ chính mình.** *Đề xuất: chặn tự khoá, tự hạ vai trò, và chặn thao tác làm mất Manager đang
   hoạt động cuối cùng* - nếu không sẽ tự khoá mình khỏi hệ thống và không ai vào sửa được nữa.
7. **Phòng ban.** *Đề xuất: cho thêm và đổi tên phòng ban, không cho xoá khi còn nhân viên* (FK sẽ chặn
   và thông báo nghiệp vụ). Không thêm cột "trưởng phòng" vì `design.md` chỉ nói mỗi phòng ban có một
   người `Position = 'Manager'`, đã có unique index lo.

## 8. Tiêu chí nghiệm thu

- Manager mở được cả 4 trang quản trị từ menu; Sales/Support không thấy nhóm Quản trị và bị chặn (403) khi
  gõ URL, trừ `/admin/products` nếu câu hỏi 4 duyệt phương án mở.
- Thêm/sửa sản phẩm, nhân viên, phòng ban, tài khoản đều qua được; dữ liệu sai bị chặn với thông báo
  tiếng Việt đúng ô, dữ liệu đã nhập còn nguyên.
- Mỗi phòng ban chỉ một `Position = 'Manager'`: thử vi phạm → thông báo thân thiện, CSDL không đổi.
- Mỗi nhân viên chỉ một tài khoản, username không trùng: thử vi phạm → thông báo thân thiện.
- Tạo tài khoản xong đăng nhập được bằng mật khẩu vừa đặt; khoá tài khoản → đăng nhập bị từ chối với
  thông báo "tài khoản đã bị khoá"; mở khoá → đăng nhập lại được.
- `/profile` đổi mật khẩu: sai mật khẩu cũ bị từ chối; đổi thành công thì đăng xuất rồi đăng nhập bằng mật
  khẩu mới được; hash trong CSDL đổi và không phải SHA2.
- Sau khi thêm/sửa/xoá ở ba bảng quản trị (nếu duyệt câu hỏi 1), `AUDIT_LOG` có dòng mới đúng
  `EmployeeID` của Manager đang đăng nhập, đúng `TableName`, `Action`, và **không** chứa hash.
- Trang nhật ký lọc theo nhân viên/bảng/hành động/khoảng ngày đúng, phân trang đúng, sắp mới nhất trước.
- Không hiện hash ở bất kỳ màn hình nào; không có mật khẩu trong log ứng dụng.
- Chuông thông báo, tìm nhanh và các trang M02-M05 vẫn chạy như trước (hồi quy).
- `dotnet build` 0 lỗi / 0 cảnh báo; `dotnet test` xanh toàn bộ (159 ca hiện có + ca mới).

## 9. Kiểm thử dự kiến

- **Ca thuần (không CSDL)**: kiểm tra dữ liệu form sản phẩm/nhân viên/tài khoản, chính sách mật khẩu,
  quy tắc "không tự khoá mình", danh sách bảng/hành động hợp lệ của bộ lọc nhật ký, và mã resource có đủ
  hai ngôn ngữ.
- **Ca trên SQL Server thật** (`CustomerCareDB_Test` do bộ test dựng lại từ `database/01`-`03`):
  trigger nhật ký mới, unique index phòng ban, trùng username/email/điện thoại, FK chặn xoá sản phẩm đã
  dùng, `PASSWORDHASH` sau khi đổi mật khẩu, `SESSION_CONTEXT` ghi đúng người.
- **Ca thủ công trên `CustomerCareDB`**: bấm qua từng trang bằng trình duyệt, kiểm tra toast, dialog xác
  nhận, trạng thái rỗng, và responsive ở bề rộng điện thoại.
- Kết quả ghi vào `test-results.md`; lỗi ghi vào `bug-report.md` và chờ duyệt theo lô trước khi sửa.
