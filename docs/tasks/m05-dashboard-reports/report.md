# M05 - Tổng quan, báo cáo, thông báo - báo cáo bàn giao

Trạng thái: **đã triển khai và đã chạy đợt kiểm thử trên SQL Server thật ngày 27/10/2026**, đang chờ
người dùng xem lại. Chi tiết từng ca ở `test-results.md`; lỗi tìm thấy và cách sửa ở `bug-report.md`.

## 1. Đã làm

- **Trang tổng quan `/`** đọc `sp_DashboardSummary`, `sp_MonthlyRevenue` và bốn view; mỗi khối là một
  partial riêng, mỗi khối có khóa lỗi riêng nên một truy vấn hỏng không làm trắng cả trang và không
  hiện số 0 giả. Khối nào vai trò không được xem thì không vẽ (Kinh doanh không có phản hồi, Hỗ trợ
  không có doanh thu).
- **Ba tab báo cáo** `/reports/revenue`, `/reports/customers`, `/reports/care`; `/reports` chuyển tới
  tab đầu tiên của vai trò. Tab Khách hàng lọc theo hạng, sắp xếp, phân trang; donut tỷ lệ hạng lấy
  từ `vw_CustomerRevenue` (chỉ khách đang hoạt động). Tab Chăm sóc xoay `sp_MonthlyCareStats` thành 12
  tháng, điểm hài lòng chỉ ở cột Khảo sát.
- **Xuất CSV** cho cả ba tab, UTF-8 có BOM, giữ đúng bộ lọc và thứ tự đang xem, chặn công thức
  `= + - @`, tên tệp chỉ ghép từ giá trị đã kiểm tra.
- **Chuông thông báo** dạng nhóm: số việc, tối đa 3 việc mỗi nhóm, liên kết "xem tất cả"; nhóm vai
  trò không mở được thì ẩn và không tính vào tổng.
- **Tìm nhanh khách hàng** trên thanh trên cùng: gợi ý sau 250 ms, tối thiểu 2 ký tự, tối đa 8 khách,
  chỉ trả mã/tên/loại; vẫn dùng được form gửi thường khi không có JavaScript.
- **Biểu đồ** vẽ bằng CSS (cột và donut), không thêm gói nào; màu lấy từ biến CSS của `site.css` nên
  chế độ tối dùng lại được.
- **Tài nguyên** cả hai file `.resx` có cùng bộ khóa; nhãn giá trị mã hoá đi qua `L.Label(...)`; ngày
  `dd/MM/yyyy`, tiền theo ngôn ngữ.
- **Kiểm thử**: thêm 40 ca (quy tắc thuần, CSV, ma trận vai trò, đọc CSDL thật).

## 2. Đường dẫn chính

| Loại | Tệp |
|---|---|
| Trang | `src/CcmWeb/Views/Home/Index.cshtml`, `Views/Home/Dashboard/*`, `Views/Reports/*` |
| Biểu đồ dùng chung | `src/CcmWeb/Views/Shared/Charts/_BarChart.cshtml`, `_Donut.cshtml` |
| Lỗi khối dùng chung | `src/CcmWeb/Views/Shared/_BlockError.cshtml` |
| Chuông, tìm nhanh | `Views/Shared/Components/NotificationBell/Default.cshtml`, `Views/Shared/_Topbar.cshtml`, `wwwroot/js/site.js` |
| Giao diện | `wwwroot/css/site.css` (`.kpi`, `.chart-*`, `.donut*`, `.suggest*`) |
| Điều khiển | `Controllers/HomeController.cs`, `Controllers/ReportsController.cs`, `Controllers/CustomersController.cs` (`GET /customers/suggest`) |
| Dịch vụ | `Services/DashboardService.cs`, `ReportService.cs`, `NotificationService.cs`, `CustomerService.SuggestAsync` |
| Quy tắc thuần | `Helpers/ReportRules.cs`, `DashboardRules`, `CsvWriter.cs`, `WorkConstants.Reports/Dashboard/Bell/Search` |
| Kiểm thử | `tests/CcmWeb.Tests/ReportRulesTests.cs`, `CsvWriterTests.cs`, `DashboardRolesTests.cs`, `ReportDatabaseTests.cs` |

## 3. Đã kiểm chứng thật

- `dotnet build src/CcmWeb/CcmWeb.csproj`: **0 lỗi, 0 cảnh báo**.
- `dotnet test tests/CcmWeb.Tests/CcmWeb.Tests.csproj`: **159/159 đạt** (40 ca mới của M05).
- Chạy ứng dụng với `CustomerCareDB_Test`, đăng nhập `john`, `mary`, `david`:
  - KPI, biểu đồ, bảng, donut, chuông, tìm nhanh, CSV đều khớp truy vấn SQL trực tiếp (T09, T11, T12,
    T14-T19, T23-T25, T28-T29, T31-T32, T34-T35).
  - Quyền máy chủ đúng: 403 cho trang và tệp xuất sai vai trò, ẩn danh về trang đăng nhập (T26-T27).
  - Chuyển ngôn ngữ VI/EN: nhãn, tên tháng, tiền đúng (T37); phát hiện và sửa B01.
  - Mở dashboard, ba tab và ba tệp xuất không làm đổi số dòng `AUDIT_LOG`, `CONTRACT`, `CUSTOMER`,
    `FEEDBACK` (T40).
- Không đổi `database/*.sql`, không scaffold lại entity, không thêm gói nào.
- **Đợt 2 trên trình duyệt thật** (Chromium của bộ công cụ, cửa sổ 826 px):
  - CSDL rỗng: sáu thẻ KPI bằng 0 đúng `sp_DashboardSummary`, mọi khối hiện lời nhắn rỗng, ba tab báo
    cáo hiện bảng 0 dòng, ba tệp CSV chỉ có dòng tiêu đề (T20); chuông không có badge đỏ, bảng chuông
    ghi "Không có việc nào cần chú ý." (T33).
  - Tên khách chứa công thức CSV: ba tên `= + -` được chèn `'`, ô có nháy/phẩy được bọc đúng, tệp vẫn
    đủ cột (T30).
  - Chế độ tối: đổi được bằng nút trên thanh trên, ảnh chụp trang tổng quan và hai tab báo cáo đọc
    được, màu lấy từ biến CSS (T38).
  - Bàn phím: `ArrowDown` + `Enter` trên danh sách gợi ý mở đúng hồ sơ, `Enter` trên nút chuông mở
    bảng và đổi `aria-expanded` (T39).
- **Đợt 2 phát hiện ba lỗi, đã sửa ngày 28/10 và chạy lại**: B02 (khối tổng quan biến mất khi truy vấn
  hỏng), B03 (chuông ném lỗi làm cả trang trả 500), B04 (gợi ý tìm nhanh in mã `Individual` /
  `Business`). Sau khi sửa: build 0 lỗi/0 cảnh báo, `dotnet test` 159/159, T21, T34, T37, T09, T18,
  T19, T31-T33 đều đạt. Chi tiết ở `bug-report.md`.
- **Đợt 3 - chỉnh giao diện theo phản hồi người dùng (28/10)**: sửa B05 (nhãn tiền hai cột dính nhau),
  B06 (khung đăng nhập lệch, nút ngôn ngữ/sáng-tối quá thấp), B07 (số doanh thu dễ tràn thẻ KPI).
  - Thẻ KPI chia hai hàng: bốn thẻ đếm ở trên, doanh thu là hình chữ nhật 3/4 hàng ở dưới, phản hồi 1/4;
    vai trò chỉ có một thẻ dưới thì thẻ đó chiếm cả hàng.
  - Biểu đồ: nhãn tiền rút gọn theo triệu kèm dấu xấp xỉ (`≈45 tr` / `≈45 M`) để không ai đọc thành số
    tròn, số đầy đủ ở `title` và ở bảng; cột cao cố định nên các cột thẳng hàng, khung cuộn ngang khi hẹp,
    cột tối đa 56 px khi thẻ rộng; khoảng cách từ dòng "Năm 2026" tới nhãn cột là 30 px.
  - Thẻ KPI: biểu tượng nằm trong ô vuông nền nhạt bên trái con số (theo yêu cầu sau của người dùng).
  - Trang đăng nhập: form là một thẻ giữa cột phải, panel thương hiệu kín chiều cao, hai nút VI/EN và
    Sáng/Tối ở góc trên bên phải.
  - Đã chạy lại: `dotnet build` 0 lỗi/0 cảnh báo, `dotnet test` 159/159, T11, T23, T09, T18, T19; xem
    ảnh chụp ở 1440 px và 842 px. **Chưa chạy lại chế độ tối (T38)** trong đợt này.

## 4. Chưa chạy / còn thiếu

- Tắt JavaScript (T36) và giao diện điện thoại dưới 768 px (T41): bộ công cụ không tắt được JS và
  không đổi được kích thước khung nhìn.
- Năm nằm trong khoảng dữ liệu nhưng không có hợp đồng (T13): dữ liệu mẫu chỉ có năm 2026.
- Chưa mở tệp CSV bằng Excel thật; mới kiểm tra BOM, tiêu đề và nội dung.
- Chưa đo tải khi chuông đọc thêm ba truy vấn mỗi trang.

## 5. Đang chờ người dùng

- Xem lại ba chỗ vừa chỉnh (thẻ KPI hai hàng, biểu đồ, trang đăng nhập) và báo nếu muốn khác đi.
- Xem lại báo cáo và `test-results.md`; nếu đồng ý thì M05 được coi là hoàn tất.
- Chưa commit gì; các thay đổi của M05 vẫn nằm trong working tree chờ người dùng quyết định.
