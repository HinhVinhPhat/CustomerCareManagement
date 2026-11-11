# M08 - Danh mục địa chỉ Việt Nam - kết quả kiểm thử

Ngày chạy: **11/11/2026**. Tài liệu viết sau khi mã đã đóng băng cho vòng này; mọi dòng dưới đây
đều đã thực sự chạy. Không đánh dấu PASS cho trường hợp chưa chạy.

## 1. Môi trường thực tế

| Hạng mục | Giá trị |
|---|---|
| Hệ điều hành | Windows, .NET CLI |
| .NET SDK | 10.0.401 |
| Cơ sở dữ liệu | Microsoft SQL Server 2025 (RTM) 17.0.1000.7, instance `.\SQLEXPRESS` |
| CSDL kiểm thử thủ công | `CustomerCareDB_AddressTest` (tạo mới bằng `database/01` -> `05`) |
| CSDL kiểm thử tự động | `CustomerCareDB_Test`, dựng lại bởi fixture xUnit từ `database/01` -> `05` |
| Ứng dụng | `http://localhost:5293`, trỏ vào `CustomerCareDB_AddressTest` |
| Công cụ | `sqlcmd` (`-I -f 65001` khi nạp tệp), `dotnet build` / `dotnet test`, trình duyệt tích hợp của công cụ |

Lý do dùng CSDL riêng cho vòng thủ công: `CustomerCareDB_Test` không có dữ liệu `ACCOUNT` nên không
thể đăng nhập. Vòng thủ công vì vậy chạy trên `CustomerCareDB_AddressTest` - một CSDL mới, không phải
CSDL phát triển `CustomerCareDB`. Bộ xUnit vẫn chỉ ghi vào `CustomerCareDB_Test`.

## 2. Kiểm thử tự động

```
dotnet build CcmWeb.sln  -> Build succeeded. 0 Warning(s), 0 Error(s)
dotnet test  CcmWeb.sln  -> Passed! - Failed: 0, Passed: 218, Skipped: 0, Total: 218 (13 s)
```

Bộ kiểm thử hiện có không thêm lớp test riêng cho địa chỉ; phần địa chỉ được kiểm chứng ở vòng thủ
công bên dưới, còn bộ xUnit xác nhận thay đổi schema/EF không làm hỏng hành vi cũ (bao gồm
`TestDatabase` nay nạp thêm tệp `05`).

## 3. Kết quả theo trường hợp

| ID | Trường hợp | Kết quả | Bằng chứng / ghi chú |
|---|---|---|---|
| T01 | Chạy `01` -> `05` trên CSDL mới | **PASS** | Cả 5 tệp thoát mã 0; `SELECT COUNT(*)` cho `PROVINCE` = 34, `WARD` = 3.321. |
| T02 | Chạy lại `05` lần hai | **PASS** | Vẫn 34 / 3.321, không lỗi khóa trùng; `MERGE` cập nhật tại chỗ, không nhân bản dòng. |
| T03 | Các dòng khách hàng mẫu cũ | **PASS** | 10 khách hàng mẫu vẫn có `ProvinceCode`/`WardCode` NULL và giữ nguyên văn bản địa chỉ cũ (`SELECT COUNT(*) ... WHERE ProvinceCode IS NULL AND WardCode IS NULL` = 10). |
| T04 | Tạo khách hàng với tỉnh 79 + phường 26740 + "12 Lê Lợi" | **PASS** | Tạo được `CustomerID = 11`; DB lưu `ProvinceCode = 79`, `WardCode = 26740`, `Address = '12 Lê Lợi'`; trang chi tiết hiển thị "12 Lê Lợi, Phường Sài Gòn, Thành phố Hồ Chí Minh". |
| T05 | Mở khách hàng cũ (id 1) | **PASS** | Ô tỉnh/phường để trống, văn bản địa chỉ cũ giữ nguyên, gợi ý địa chỉ cũ hiển thị trên biểu mẫu. |
| T06 | Đổi tỉnh trên biểu mẫu | **PASS** | Trong trình duyệt đã đăng nhập: `GET /customers/wards?provinceCode=79` trả 200 và 168 phường/xã, khớp `SELECT COUNT(*) FROM WARD WHERE ProvinceCode = 79`; danh sách chỉ còn một dòng placeholder sau khi đặt lại. Gọi endpoint khi chưa đăng nhập bị chuyển về trang đăng nhập (đúng `[Authorize]`). |
| T07 | Gửi phường của tỉnh khác | **PASS** | Lỗi hiển thị tại ô phường/xã: "Phường/xã không thuộc tỉnh/thành phố đã chọn."; kiểm tra DB không có dòng nào được ghi. |
| T08 | Gửi phường mà không có tỉnh | **PASS** | Xóa `ProvinceCode`, giữ `WardCode = 26740` rồi gửi: lỗi tại ô phường/xã "Phải chọn tỉnh/thành phố trước khi chọn phường/xã."; DB của khách hàng 11 vẫn là 79 / 26740, không thay đổi. |
| T09 | Build và bộ xUnit hiện có | **PASS** | Build 0 lỗi, 0 cảnh báo; `dotnet test` 218/218 đạt trên SQL Server thật. |

## 4. Ghi chú và phần chưa làm

- Không có bài kiểm thử tự động mới riêng cho `CustomerService` phần địa chỉ (kiểm tra cặp
  tỉnh/phường, dựng địa chỉ đầy đủ). Việc này nằm ngoài bảng T01-T09 đã duyệt; nếu cần, đề xuất bổ
  sung ở vòng sau.
- Chưa kiểm tra trên trình duyệt di động thật; giao diện hai ô chọn dùng lại lưới hiện có và đã xem
  ở kích thước cửa sổ hẹp bằng công cụ, không phải thiết bị thật.
- Chưa chạy `docker compose` cho thay đổi `init.sh` trong vòng này; phần Docker mới chỉ được rà soát
  bằng mã. Đây là hạng mục **NOT RUN**, không phải PASS.
- CSDL `CustomerCareDB_AddressTest` được giữ lại để đối chiếu sau bàn giao; có thể xóa bằng
  `DROP DATABASE CustomerCareDB_AddressTest` khi không cần nữa.
