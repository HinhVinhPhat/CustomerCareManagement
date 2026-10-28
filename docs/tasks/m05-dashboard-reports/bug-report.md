# M05 - Tổng quan, báo cáo, thông báo - báo cáo lỗi

Đợt kiểm thử ngày **27-28/10/2026**. Mỗi lỗi ghi rõ hiện tượng, nguyên nhân, cách sửa, phạm vi ảnh
hưởng và ca kiểm thử đã chạy lại. B01-B07 đã sửa; B05-B07 là các lỗi giao diện người dùng báo ngày
28/10 và đã sửa trong cùng ngày.

## B01 - Phụ đề tab Doanh thu in ra `{0}` thay vì năm

| Mục | Nội dung |
|---|---|
| Mức độ | Thấp (chỉ sai chữ, không sai số liệu) |
| Phát hiện ở | T37 - chuyển sang tiếng Anh rồi mở `/reports/revenue?year=2026` |
| Hiện tượng | Dưới tiêu đề trang hiện nguyên văn `Monthly revenue for {0}`; bản tiếng Việt cũng sẽ là `Doanh thu theo tháng của năm {0}` |
| Nguyên nhân | `Views/Reports/Revenue.cshtml` gọi `L["Reports_Revenue_Subtitle"]` mà không truyền tham số, trong khi giá trị resource có placeholder `{0}` dành cho năm. Các phụ đề khác (Khách hàng, Chăm sóc) không có placeholder nên không lộ |
| Cách sửa | Truyền năm đang xem: `L["Reports_Revenue_Subtitle", Model.Year]` (một dòng, không đổi resource key) |
| Phạm vi ảnh hưởng | Chỉ dòng phụ đề của tab Doanh thu; không ảnh hưởng bảng, biểu đồ, tệp CSV hay quyền |
| Chạy lại | T01 (build 0 lỗi/0 cảnh báo), T02 (159/159 đạt), T37 (phụ đề hiện `Monthly revenue for 2026` ở EN và `Doanh thu theo tháng của năm 2026` ở VI) |

Không còn lỗi nào khác trong đợt 1.

## B02 - Khối trang tổng quan biến mất khi truy vấn hỏng

| Mục | Nội dung |
|---|---|
| Mức độ | Trung bình (người dùng không biết khối bị lỗi, tưởng là trang không có mục đó) |
| Phát hiện ở | T21 đợt 2: đổi tên `sp_MonthlyRevenue` thành `sp_MonthlyRevenue_broken` trong CSDL thử rồi mở `/` |
| Hiện tượng | Ba khối "Doanh thu theo tháng", "Khách hàng hàng đầu", "Phản hồi cần xử lý" mất hẳn khỏi trang, không có thông báo lỗi; các khối còn lại vẫn hiện và không lộ chi tiết SQL |
| Nguyên nhân | `DashboardService` để danh sách là `null` khi truy vấn hỏng và đặt khóa lỗi vào `RevenueErrorKey` / `TopCustomersErrorKey` / `FeedbackErrorKey`, nhưng ba partial `_RevenueChart.cshtml`, `_TopCustomers.cshtml`, `_FeedbackToHandle.cshtml` lại mở đầu bằng `@if (Model.Revenue is not null)` (tương tự cho hai khối kia). Danh sách `null` nên cả khối không được vẽ, nhánh hiện `_BlockError` không bao giờ chạy. Hai khối còn lại (`_ExpiringContracts`, `_CareSchedule`) không bị vì `DashboardService` khởi tạo danh sách rỗng |
| Cách sửa | Đổi điều kiện vẽ khối thành "vai trò được xem khối này" (`DashboardRules.SeesRevenue(role)` / `SeesFeedback(role)` truyền qua `DashboardViewModel`, hoặc thêm cờ `ShowRevenue` / `ShowTopCustomers` / `ShowFeedback`), rồi vẽ `_BlockError` khi khóa lỗi khác `null` và danh sách khi không lỗi. Sửa ba tệp partial, không đụng Service hay SQL |
| Phạm vi ảnh hưởng | Chỉ ba khối của trang tổng quan; ba tab báo cáo vẫn hiện thông báo lỗi chung đúng |
| Cách sửa đã làm | Thêm hai cờ `ShowRevenue` / `ShowFeedback` vào `DashboardViewModel`, gán từ `DashboardRules` trong `DashboardService`; ba partial đổi điều kiện ngoài cùng sang cờ đó và thêm nhánh dự phòng khi danh sách `null` mà không có khóa lỗi |
| Chạy lại sau khi sửa (28/10) | T21 đạt: đổi tên lần lượt `sp_MonthlyRevenue`, `vw_CustomerRevenue`, `vw_OpenFeedback` thì trang trả 200, đúng khối hỏng hiện "Đã xảy ra lỗi, thao tác chưa hoàn tất." + "Vui lòng thử lại...", bốn khối còn lại vẫn hiện, không lộ tên đối tượng SQL. T09 (6 thẻ), T18 (Kinh doanh 5 thẻ, không khối phản hồi), T19 (Hỗ trợ 5 thẻ, không khối doanh thu) vẫn đúng; khôi phục tên thì trang trở lại 6 thẻ, 0 lỗi |

## B03 - Chuông thông báo ném lỗi làm cả trang trả 500

| Mục | Nội dung |
|---|---|
| Mức độ | Cao (mất cả trang, không chỉ một khối) |
| Phát hiện ở | T21 đợt 2: đổi tên `vw_OpenFeedback` thành `vw_OpenFeedback_broken` rồi mở `/` |
| Hiện tượng | Trang trả HTTP 500 và hiện trang lỗi chung; log máy chủ ghi `Error Number:208` (invalid object name). Đổi lại tên view thì trang chạy bình thường |
| Nguyên nhân | `NotificationService.GetSummaryAsync` không có `try/catch`, còn `NotificationBellViewComponent` gọi nó trong `_Layout`, nên một view hỏng ở chuông làm hỏng mọi trang có layout (trang tổng quan, ba tab báo cáo, danh sách khách hàng...) trong khi các khối khác vẫn đọc được |
| Cách sửa | Bọc từng nhóm trong `NotificationService` bằng `try/catch` riêng, ghi log và trả nhóm rỗng (hoặc ẩn nhóm) khi hỏng; giữ nguyên ba view và cách tính số đếm. Không đổi SQL |
| Phạm vi ảnh hưởng | Mọi trang dùng `_Layout`; hành vi bình thường không đổi vì chỉ thêm nhánh lỗi |
| Cách sửa đã làm | Thêm `ILogger<NotificationService>` và hàm `LoadGroupAsync` bọc từng nhóm; nhóm lỗi được ghi log và trả `null` nên chỉ nhóm đó biến mất, không tính vào tổng |
| Chạy lại sau khi sửa (28/10) | T21 đạt: `vw_OpenFeedback` hỏng thì `/`, `/reports/*` và `/customers` đều trả 200; chuông còn 2 nhóm, tổng 4 thay vì 6, và trang không lộ chi tiết SQL. Khôi phục view: chuông trở lại 3 nhóm, tổng 6. T31 (6), T32 (4), T33 (không badge) không đổi |

## B04 - Gợi ý tìm nhanh in mã tiếng Anh thay vì nhãn tiếng Việt

| Mục | Nội dung |
|---|---|
| Mức độ | Thấp (chỉ là chữ, không sai dữ liệu) |
| Phát hiện ở | T34 đợt 2, xem danh sách gợi ý trên trình duyệt |
| Hiện tượng | Mỗi gợi ý hiện `Nguyễn Thị ThuIndividual`, `Công ty TNHH ABCBusiness`: loại khách hàng in đúng mã lưu trong CSDL chứ không phải nhãn hiển thị |
| Nguyên nhân | `wwwroot/js/site.js` gán thẳng `type.textContent = item.customerType`, mà `customerType` là giá trị mã hóa `Individual` / `Business` của CHECK constraint. Quy tắc ngôn ngữ yêu cầu giá trị mã hóa phải đi qua `L.Label(...)` |
| Cách sửa | Cho `CustomerSuggestionViewModel` trả thêm nhãn đã dịch (ví dụ trường `typeLabel`) hoặc dịch ở `CustomerService.SuggestAsync` bằng `IStringLocalizer.Label("CustomerType", ...)`, rồi JS dùng trường đó. Không đổi `sp_SearchCustomer` |
| Phạm vi ảnh hưởng | Chỉ danh sách gợi ý; JSON trả về có thêm một trường, không phá vỡ chỗ khác vì chỉ `site.js` đọc |
| Cách sửa đã làm | `CustomerSuggestion` thêm `CustomerTypeLabel`; `CustomerService` nhận `IStringLocalizer<SharedResource>` và điền nhãn bằng `localizer.Label("CustomerType", row.CustomerType)`; `site.js` dùng `item.customerTypeLabel` (vẫn lùi về `customerType` nếu thiếu) |
| Chạy lại sau khi sửa (28/10) | T34/T37 đạt: `/customers/suggest?q=nguy` trả `customerTypeLabel: "Cá nhân"` ở VI và `"Individual"` ở EN; trên trình duyệt danh sách hiện "Nguyễn Thị Thu · Cá nhân", không còn dính chữ |

## B05 - Nhãn số tiền của hai cột biểu đồ cạnh nhau chồng lên nhau

| Mục | Nội dung |
|---|---|
| Mức độ | Thấp (chỉ là chữ, số vẫn đúng và vẫn có trong `title` của cột) |
| Phát hiện ở | Ảnh chụp trang tổng quan ngày 28/10, khi tháng 1 và tháng 2 cùng có doanh thu |
| Hiện tượng | Hai nhãn dính thành `20.000.00045.000.000` rồi `VNĐ VNĐ`; nhãn rộng hơn cột nên tràn sang cột bên cạnh |
| Nguyên nhân | `.chart-value` trong `site.css` không giới hạn bề rộng và không cho phép xuống dòng, còn `.chart-col` đặt `min-width: 0`; mười hai cột chia đều một hàng nên mỗi cột chỉ rộng khoảng 55 px, trong khi `45.000.000 VNĐ` ở cỡ 11 px rộng hơn thế |
| Cách sửa đã làm | Bốn thay đổi trong `site.css` và partial `Charts/_BarChart.cshtml`: (1) `.chart-value` cao cố định 20 px, cỡ 10,5 px, `white-space: nowrap` nên mọi cột bắt đầu ở cùng một đường; (2) nhãn tiền rút gọn theo triệu kèm dấu xấp xỉ (`≈45 tr` / `≈45 M`) qua resource key mới `Chart_MoneyUnit`, số đầy đủ vẫn nằm ở `title` của cột và ở bảng trên tab Doanh thu; (3) biểu đồ có `min-width: 680px` trong một khung cuộn ngang nên 12 cột không bị ép; (4) `.chart-bar` thêm `max-width: 56px` để cột không phình khi thẻ rộng |
| Phạm vi ảnh hưởng | Cả biểu đồ cột của trang tổng quan và tab Doanh thu, cả hai ngôn ngữ |
| Chạy lại sau khi sửa (28/10) | T11, T23: 12 cột vẫn khớp `sp_MonthlyRevenue` (45 / 12 / 12 / 13 triệu); ảnh chụp trang tổng quan và `/reports/revenue` cho thấy nhãn nằm gọn trong cột, không còn dính nhau, khoảng cách từ dòng "Năm 2026" tới nhãn cột là 30 px. T38 chưa chạy lại trong đợt này |

## B06 - Khung đăng nhập lệch và nút ngôn ngữ / sáng-tối nằm quá thấp

| Mục | Nội dung |
|---|---|
| Mức độ | Thấp (chỉ là bố cục) |
| Phát hiện ở | Ảnh chụp trang `/login` do người dùng gửi ngày 28/10 |
| Hiện tượng | Panel thương hiệu bên trái bị dàn theo `justify-between` nên chữ nằm rời rạc, form bên phải trôi nổi không có khung; hai nút VI/EN và Sáng/Tối nằm ngay trên form, cách mép trên cả màn hình |
| Nguyên nhân | `Views/Account/Login.cshtml` đặt phần chọn ngôn ngữ / giao diện trong cột form thay vì ở góc trang, và form không có `.card` nên không có đường bao |
| Cách sửa đã làm | Đưa hai partial lên `absolute top-5 right-5` của `<body>`; bọc form trong `.card p-8 max-w-md`; panel trái đổi sang `flex-col` với khối tiêu đề căn giữa theo chiều dọc (`flex-1 justify-center`) để chữ không bị rời |
| Phạm vi ảnh hưởng | Chỉ trang đăng nhập; các partial ngôn ngữ / giao diện không đổi |
| Chạy lại sau khi sửa (28/10) | Ảnh chụp 1440 px và 842 px: panel trái kín chiều cao, form là một thẻ giữa cột phải, hai nút nằm ở góc trên bên phải; đăng nhập bằng `john` vẫn vào được trang tổng quan |

## B07 - Số "Doanh thu năm nay" to, dễ tràn khỏi thẻ KPI

| Mục | Nội dung |
|---|---|
| Mức độ | Thấp (chỉ là bố cục) |
| Phát hiện ở | Ảnh chụp trang tổng quan ngày 28/10, thẻ doanh thu nằm cùng hàng sáu thẻ nên rất hẹp |
| Hiện tượng | `82.000.000 VNĐ` ở cỡ 22 px trong một ô rộng ~200 px, chạm sát hai mép thẻ và sẽ tràn khi số lớn hơn |
| Nguyên nhân | `_KpiCards.cshtml` vẽ tất cả thẻ trong một lưới `xl:grid-cols-6`, mọi thẻ cùng cỡ |
| Cách sửa đã làm | Chia làm hai hàng: hàng trên bốn thẻ đếm (`md:grid-cols-4`), hàng dưới là doanh thu và phản hồi. Doanh thu chiếm 3/4 hàng (`md:col-span-3`) với cỡ chữ 30 px (`.kpi-wide`), phản hồi 1/4; vai trò chỉ có một thẻ dưới (Kinh doanh chỉ doanh thu, Hỗ trợ chỉ phản hồi) thì thẻ đó chiếm cả hàng (`md:col-span-4`). `.kpi-value` thêm `overflow-wrap: anywhere` để không bao giờ tràn. Biểu tượng đặt trong ô vuông nền nhạt **bên trái con số** (`.kpi-head`) thay vì nằm trên, theo yêu cầu người dùng ngày 28/10 |
| Phạm vi ảnh hưởng | Chỉ khối thẻ KPI của trang tổng quan; số liệu và liên kết từng thẻ không đổi |
| Chạy lại sau khi sửa (28/10) | HTML của `john` có 4 thẻ hàng trên + `md:col-span-3 kpi-wide` (doanh thu) + 1 thẻ 1/4 (phản hồi); `mary` và `david` có 5 thẻ với thẻ dưới `md:col-span-4`; ảnh chụp 1440 px và 842 px đều không tràn |
