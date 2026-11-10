# M06 - Quản trị - báo cáo lỗi

Đợt kiểm thử ngày **28/10/2026**, sửa và chạy lại ngày **10/11/2026**. Mỗi lỗi ghi rõ hiện tượng, nguyên
nhân, cách sửa, phạm vi ảnh hưởng và ca kiểm thử đã chạy lại. **B01 đã sửa và đã chạy lại T22-T26**
(mục "Kết quả sau khi sửa" ở cuối).

## B01 - Bấm "Lưu" ở trang sửa tài khoản và "Đặt lại mật khẩu" không làm gì, cũng không báo lỗi

Trạng thái: **đã sửa ngày 10/11/2026** (người dùng duyệt lô sửa).

| Mục | Nội dung |
|---|---|
| Mức độ | **Cao** - hai chức năng chính của M06 không dùng được: khoá/mở khoá tài khoản và đổi vai trò (nút Lưu), đặt lại mật khẩu cho nhân viên (nút Đặt lại mật khẩu) |
| Phát hiện ở | T22 (khoá tài khoản) và T24 (đặt lại mật khẩu); T23, T25, T26 bị chặn theo |
| Hiện tượng | Mở `/admin/accounts/7/edit`, bỏ chọn "Đang hoạt động", bấm **Lưu**: trang quay lại đúng biểu mẫu đó, ô vẫn như cũ, **không có toast, không có cảnh báo, không có lỗi tại ô nào**, `AUDIT_LOG` không có dòng nào, `ACCOUNT.IsActive` vẫn là 1. Làm y hệt ở `/admin/accounts/7/password` với mật khẩu mới: cũng không có thông báo, mật khẩu không đổi (đăng nhập bằng mật khẩu mới thất bại). Log máy chủ **không** ghi cảnh báo nào, vì đây không phải ngoại lệ. |
| Nguyên nhân | `AccountEditViewModel.Username`, `.EmployeeName` và `ResetPasswordViewModel.Username`, `.EmployeeName` là `required string` (kiểu tham chiếu không nullable) nhưng **chỉ để hiển thị**: biểu mẫu vẽ chúng bằng `<dd>` nên không gửi lên. ASP.NET Core tự thêm kiểm tra "bắt buộc" cho thuộc tính kiểu tham chiếu không nullable, nên `ModelState.IsValid` luôn `false` ngay khi bind. Controller `AdminAccountsController.Edit` / `Password` dựng lại model từ dữ liệu trong CSDL rồi mới kiểm `ModelState.IsValid`, nhưng `ModelState` vẫn giữ hai lỗi bind của `Username`/`EmployeeName`; hai ô này lại không có `<span asp-validation-for>` trong view nên không lỗi nào hiện ra. Kết quả: yêu cầu bị trả lại im lặng. |
| Bằng chứng | T27: POST đúng hai biểu mẫu đó nhưng gửi thêm `Username` và `EmployeeName` → toast "Đã lưu tài khoản." / "Đã đặt lại mật khẩu.", `AUDIT_LOG` có `ACCOUNT 7 Update: Role: Sales -> Support`, danh sách hiện vai trò mới. Như vậy Service, trigger và câu SQL đều đúng; lỗi nằm ở kiểm tra dữ liệu vào của controller/ViewModel. |
| Cách sửa đề xuất | Đánh dấu bốn thuộc tính chỉ để hiển thị là "không kiểm tra": thêm `[ValidateNever]` (`Microsoft.AspNetCore.Mvc.ModelBinding.Validation`) cho `AccountEditViewModel.Username`, `.EmployeeName` và `ResetPasswordViewModel.Username`, `.EmployeeName`, kèm chú thích vì sao. Không đổi view, không đổi Service, không đổi SQL, không thêm thư viện. |
| Phương án khác đã cân nhắc | (a) Đổi bốn thuộc tính sang `string?` - phải sửa thêm ba view để chịu giá trị null, ảnh hưởng rộng hơn; (b) đặt `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true` trong `AddControllersWithViews` - tắt kiểm tra cho **toàn bộ** ViewModel của dự án, có thể làm mất một kiểm tra đang có ích ở module khác; (c) `ModelState.Remove(...)` trong hai action - vá tại chỗ, người sau đọc dễ hiểu nhầm. Chọn `[ValidateNever]` vì phạm vi hẹp nhất và nói đúng ý định. |
| Phạm vi ảnh hưởng | Chỉ hai trang `/admin/accounts/{id}/edit` và `/admin/accounts/{id}/password`. Trang tạo tài khoản, trang nhân viên, sản phẩm, nhật ký, `/profile` không bị (mọi trường của chúng đều có trong biểu mẫu). |
| Ca chạy lại sau khi sửa | T22 (khoá qua form thật, có `AUDIT_LOG` `State: Active -> Locked`), T23 (đăng nhập bị chặn, có thông báo), T24 (đặt lại mật khẩu, đăng nhập bằng mật khẩu mới), T25, T26; chạy lại `dotnet test` (218 ca) và T27 để chắc chắn không đổi hành vi cũ. |

### Kết quả sau khi sửa (10/11/2026)

Đã thêm `[ValidateNever]` cho bốn thuộc tính chỉ để hiển thị trong
`src/CcmWeb/Models/ViewModels/AccountViewModels.cs` (`AccountEditViewModel.Username`,
`.EmployeeName`, `ResetPasswordViewModel.Username`, `.EmployeeName`), kèm chú thích trỏ về B01. Không
đổi view, Service, SQL hay thư viện.

Thêm bốn ca đơn vị trong `AdminRulesTests.A_field_the_form_only_shows_is_never_validated` để lần sau ai
bỏ thuộc tính đi thì `dotnet test` báo ngay.

Chạy lại trên `CustomerCareDB_Test` (ứng dụng trỏ vào CSDL thử bằng `ConnectionStrings__DefaultConnection`,
đăng nhập Windows, vì `app_ccm` không có quyền trên CSDL thử) bằng script HTTP thật - đăng nhập, tạo tài
khoản, gửi đúng những trường mà biểu mẫu gửi:

| Ca | Kết quả |
|---|---|
| T22 | PASS - `POST /admin/accounts/20/edit` chỉ gửi `Role` + `IsActive` → 302 về `/admin/accounts`, toast "Đã lưu tài khoản.", `ACCOUNT.IsActive = 0`, `AUDIT_LOG` có `ACCOUNT 20 Update State: Active -> Locked` với `EmployeeID = 4` (susan) |
| T23 | PASS - đăng nhập đúng mật khẩu của tài khoản đang khoá → ở lại `/login`, hiện "Tài khoản đã bị khoá. Vui lòng liên hệ quản trị viên." |
| T24 | PASS - `POST /admin/accounts/20/password` chỉ gửi `Password` + `ConfirmPassword` → 302, toast "Đã đặt lại mật khẩu.", mật khẩu cũ bị từ chối |
| T25 | PASS - `POST` sửa tài khoản với `IsActive = true`, `Role = Support` → 302, `ACCOUNT` là `Support 1`, `AUDIT_LOG` `Update Role: Sales -> Support; State: Locked -> Active` |
| T26 | PASS - đăng nhập bằng mật khẩu mới → 302 `/`; `/` và `/profile` trả 200, `/admin/accounts` vẫn chuyển `/access-denied` (vai trò Hỗ trợ) |
| T27 | PASS (không đổi) - gửi thêm `Username`/`EmployeeName` vẫn lưu bình thường; hai trường đó bị controller ghi đè từ CSDL nên không thể đổi tên tài khoản qua form |

`dotnet build`: 0 cảnh báo, 0 lỗi. `dotnet test`: **218/218 đạt** (214 ca cũ + 4 ca mới). Dữ liệu thử
(tài khoản `e2eb01`, mã 20) cùng ba dòng `AUDIT_LOG` của nó đã xoá; `ACCOUNT` trở lại 17 dòng,
`AUDIT_LOG` 53 dòng như trước đợt chạy lại.

## Ghi nhận khác (không phải lỗi)

- Ba ca `AdminRulesTests` đầu tiên FAIL vì kỳ vọng của người viết test sai, không phải lỗi ứng dụng
  (chi tiết ở `test-results.md` mục 2). Đã sửa test, không sửa nguồn.
- Xoá sản phẩm đang dùng hiện **khối cảnh báo** chứ không phải toast: đúng quy ước ở `AGENTS.md` mục 5
  (lỗi nghiệp vụ ở lại màn hình để người dùng đọc, chỉ thành công mới là toast).

## B02 - Ở bề rộng điện thoại, mọi trang bị tràn ngang vì thanh trên và biểu đồ doanh thu

Trạng thái: **đã sửa ngày 10/11/2026** (người dùng duyệt sửa). Phát hiện khi chạy N01.

| Mục | Nội dung |
|---|---|
| Mức độ | **Trung bình** - không sai dữ liệu, nhưng vi phạm yêu cầu "Responsive down to phone size" (`docs/design.md` mục 9, `AGENTS.md` mục 5): trên điện thoại phải vuốt ngang và một phần nút bị cắt khỏi màn hình. |
| Phát hiện ở | N01 (ảnh chụp 3 bề rộng) |
| Hiện tượng | Đặt khung nhìn 412 x 900: `document.documentElement.scrollWidth` = **510 px** ở `/admin/products`, `/admin/employees`, `/admin/accounts`, `/admin/audit-logs` và **738 px** ở `/` (tổng quan), trong khi `innerWidth` = 412. Ảnh `docs/tasks/m06-admin/screenshots/admin-accounts-phone.png` cho thấy nút "Đặt lại mật khẩu" và nút "Đăng xuất" bị cắt; ảnh `dashboard-phone.png` cho thấy thẻ doanh thu rộng hơn màn hình. Ở 768 px và 1280 px không tràn (`scrollWidth` = `innerWidth`). |
| Nguyên nhân | (1) `_Topbar.cshtml` dòng 22: nhóm bên phải `ml-auto ... shrink-0` chứa chuông + VI/EN + sáng/tối + avatar + nút chữ "Đăng xuất" rộng **378 px** và không co lại, cộng với nút mở menu và ô tìm kiếm thành 510 px. (2) `_RevenueChart.cshtml` dòng 6: thẻ `<section class="card p-5 lg:col-span-2">` là ô lưới, mặc định `min-width: auto`, nên lấy bề rộng tối thiểu của `.chart` (`min-width: 680 px` trong `site.css`) làm bề rộng cột; `overflow-x: auto` của `.chart-scroll` không phát huy tác dụng vì cột lưới đã rộng 722 px. |
| Cách sửa (đã làm) | (1) `_Topbar.cshtml`: thêm `min-w-0` cho form tìm kiếm và đổi form đăng xuất thành `class="shrink-0 hidden sm:block"` - nút đăng xuất bằng chữ vốn đã có bản biểu tượng ở chân sidebar, nên trên điện thoại không mất chức năng. (2) Thêm `min-w-0` cho sáu thẻ là ô lưới ở trang tổng quan (`_RevenueChart`, `_TopCustomers`, `_FeedbackToHandle`, `_ExpiringContracts`, `_CareSchedule`) để nội dung rộng tự cuộn bên trong thẻ thay vì kéo cả trang. (3) `site.css` (khối `@media (max-width: 767px)`): `td` của bảng dạng thẻ được `flex-wrap: wrap`, riêng ô thao tác cho nhãn chiếm trọn một dòng và hai nút nằm gọn bên phải - trước đó hai nút nằm trên một hàng flex không xuống dòng nên bị cắt mất chữ. Không đổi Service, SQL, ViewModel, không thêm thư viện. |
| Phạm vi ảnh hưởng | Chỉ giao diện; `_Topbar` và `.tbl-cards` dùng chung nên sửa một chỗ là hết tràn ở mọi trang. Bề rộng >= 640 px giữ nguyên giao diện hiện tại. |
| Kết quả sau khi sửa | Chạy lại N01 (15 ảnh mới, cùng bộ script): `document.documentElement.scrollWidth` = `innerWidth` ở **cả 15 ảnh** (1280 = 1280, 768 = 768, 412 = 412, trước đó 510/738). Ảnh `admin-accounts-phone.png` và `admin-products-phone.png`: nhãn "Thao tác" một dòng, hai nút "Sửa" + "Đặt lại mật khẩu" nằm gọn bên phải; `dashboard-phone.png`: biểu đồ doanh thu cuộn ngang trong thẻ, không kéo trang. Ảnh desktop không đổi (nút "Đăng xuất" vẫn còn ở thanh trên). `dotnet build` 0 cảnh báo 0 lỗi; `dotnet test` **218/218** đạt. |
