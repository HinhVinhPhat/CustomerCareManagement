# M05 - Tổng quan, báo cáo, thông báo - kết quả kiểm thử

Ngày chạy: **27/10/2026**, hai đợt trong cùng ngày: đợt 1 bằng dòng lệnh (HTTP + SQL), đợt 2 bằng
trình duyệt thật và một CSDL rỗng/lỗi. Mọi dòng dưới đây đã được chạy thật; việc gì chưa chạy thì ghi
rõ **NOT RUN**, không đánh dấu PASS. Lỗi tìm thấy nằm ở `bug-report.md`: B01 đã sửa và chạy lại; B02,
B03, B04 mới phát hiện ở đợt 2, **chưa sửa**, đang chờ người dùng duyệt.

## 1. Môi trường thực tế

| Mục | Giá trị |
|---|---|
| Hệ điều hành | Windows 11 Home Single Language, build 26300 |
| .NET SDK | 10.0.401 |
| Target framework | `net10.0` |
| EF Core | 10.0.13 (`Microsoft.EntityFrameworkCore.SqlServer` 10.0.13) |
| Cơ sở dữ liệu | Microsoft SQL Server 2025 (RTM) 17.0.1000.7, instance `.\SQLEXPRESS` |
| CSDL dùng để kiểm thử | `CustomerCareDB_Test`, do `TestDatabase` dựng lại từ `database/01`-`03` |
| Cách chạy ứng dụng | `dotnet run --project src/CcmWeb --no-build --urls http://localhost:5101`, biến môi trường `ConnectionStrings__DefaultConnection` trỏ tới `CustomerCareDB_Test` (Windows authentication) |
| Tài khoản | `john` (Quản lý), `mary` (Kinh doanh), `david` (Hỗ trợ) |
| Trình duyệt cho đợt 2 | Trình duyệt Chromium của bộ công cụ OpenCode, cửa sổ rộng 826 px, phục vụ tại `http://localhost:5104` |
| CSDL cho các ca rỗng / lỗi | `CustomerCareDB_Empty`, dựng từ `database/01` và `02` rồi thêm một phòng ban, một nhân viên, tài khoản `john` và ba dòng `SETTINGS`; đã xoá sau khi kiểm thử |

Ứng dụng chỉ đọc trong toàn bộ đợt kiểm thử. Không dùng EF InMemory hay SQLite. Ghi chú trung thực:
lần chạy thử đầu tiên (trước khi đặt biến môi trường) ứng dụng nối nhầm vào `CustomerCareDB` thật và
chạy `sp_ExpireContracts` lúc khởi động; log ghi **0 hợp đồng** bị đổi trạng thái. Toàn bộ đợt kiểm
thử sau đó chạy trên `CustomerCareDB_Test`. Sau đợt 2, bốn bảng `AUDIT_LOG` / `CONTRACT` / `CUSTOMER` /
`FEEDBACK` của `CustomerCareDB_Test` vẫn là 12 / 25 / 27 / 9, tức là mở trang và xuất tệp không ghi gì.

## 2. Kiểm thử tự động

```
dotnet build src\CcmWeb\CcmWeb.csproj
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```
dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj
Passed!  - Failed: 0, Passed: 159, Skipped: 0, Total: 159, Duration: 5 s - CcmWeb.Tests.dll (net10.0)
```

40 ca là của M05, nằm trong bốn tệp mới:

| Tệp | Số ca | Nội dung |
|---|---|---|
| `tests/CcmWeb.Tests/ReportRulesTests.cs` | 18 | chọn năm, danh sách năm, chiều cao cột, xoay dữ liệu chăm sóc, tỷ lệ hạng (kể cả tổng 0) |
| `tests/CcmWeb.Tests/CsvWriterTests.cs` | 12 | bọc nháy kép, nhân đôi nháy, chặn công thức `= + - @`, BOM UTF-8 |
| `tests/CcmWeb.Tests/DashboardRolesTests.cs` | 5 | khối nào của trang tổng quan thuộc vai trò nào |
| `tests/CcmWeb.Tests/ReportDatabaseTests.cs` | 5 | đọc `sp_MonthlyRevenue`, `sp_MonthlyCareStats`, `vw_CustomerRevenue`, danh sách năm trên SQL Server thật |

119 ca còn lại là bộ kiểm thử M01-M04, không đổi.

## 3. Các ca kiểm thử

| Id | Ca | Kết quả mong đợi | Thực tế | Trạng thái |
|---|---|---|---|---|
| T01 | Build sạch | 0 lỗi, 0 cảnh báo | `Build succeeded. 0 Warning(s), 0 Error(s)` | PASS |
| T02 | Toàn bộ kiểm thử tự động | tất cả xanh | 159 đạt, 0 lỗi, 0 bỏ qua | PASS |
| T03 | `ClampYear` / `AvailableYears` | năm sai về năm hiện tại; danh sách liên tục, mới nhất trước, có năm hiện tại | 6 dòng `InlineData` + 3 ca danh sách đều đạt | PASS |
| T04 | `PivotCareStats` | đủ 12 tháng, tháng thiếu bằng 0, điểm chỉ ở cột Khảo sát | 2 ca đạt; trang tháng 7/9/10 có điểm 4,0 / 5,0 / 4,0, các loại khác để trống | PASS |
| T05 | `TierShares` | tổng 100%, không chia cho 0 | 3 ca đạt; donut thật: 19+3+1+3 = 26 khách, 73%+12%+4%+11% = 100% | PASS |
| T06 | `BarHeights` | giá trị dương không cao 0, năm rỗng không NaN | 4 ca đạt | PASS |
| T07 | `CsvWriter` | ô có dấu phẩy/nháy/xuống dòng được bọc; công thức bị chặn; có BOM | 12 ca đạt; tệp tải về bắt đầu bằng `239,187,191` (EF BB BF) | PASS |
| T08 | Ma trận vai trò (unit) | Quản lý 6 thẻ, Kinh doanh 5 (không phản hồi), Hỗ trợ 5 (không doanh thu) | 5 ca đạt; đếm thật trên trang: john 6 thẻ, mary 5, david 5 | PASS |
| T09 | `john` mở `/` | sáu thẻ KPI bằng `sp_DashboardSummary` | 26 khách, 14 khách mới, 18 hợp đồng hiệu lực, 2 sắp hết hạn, 5 phản hồi mở (2 quá hạn), 185.000.000 VNĐ - khớp đúng thủ tục | PASS |
| T10 | Liên kết thẻ KPI | mỗi thẻ mở đúng trang, không dẫn tới 403 | Kinh doanh không có thẻ phản hồi (`/feedbacks` 403), Hỗ trợ không có thẻ doanh thu (`/reports/revenue` 403); các thẻ còn lại trỏ đúng trang | PASS |
| T11 | Biểu đồ doanh thu năm hiện tại | 12 cột bằng `sp_MonthlyRevenue @Year` | 0 / 45 / 0 / 12 / 0 / 0 / 0 / 12 / 0 / 116 / 0 / 0 triệu - khớp thủ tục. Đợt 3: nhãn đầu cột rút gọn theo triệu (`45 tr`), số đầy đủ vẫn ở `title` | PASS |
| T12 | KPI doanh thu so với bảng | hai số bằng nhau | 185.000.000 VNĐ ở cả hai chỗ | PASS |
| T13 | Đổi năm, năm không có hợp đồng | URL giữ `?year=`, biểu đồ đổi, năm rỗng hiện 12 cột 0 kèm lời nhắn | Năm ngoài khoảng dữ liệu (2020) quay về năm hiện tại theo `ClampYear`; dữ liệu mẫu chỉ có hợp đồng trong 2026 nên **chưa dựng được** một năm nằm trong khoảng mà rỗng | NOT RUN (phần năm rỗng) |
| T14 | Khách hàng hàng đầu | 5 dòng, chỉ khách đang hoạt động, giảm dần theo giá trị | 5 dòng: 128 / 128 / 100 / 60 / 48 triệu, đúng thứ tự `vw_CustomerRevenue`; dịch vụ lọc `IsActive` (có ca kiểm thử CSDL) | PASS |
| T15 | Hợp đồng sắp hết hạn | bằng `vw_ExpiringContract`; Quản lý/Kinh doanh có nút gia hạn, Hỗ trợ không | 2 dòng, còn 14 và 24 ngày, khớp view; john có 2 liên kết gia hạn, david 0 | PASS |
| T16 | Phản hồi cần xử lý | đúng thứ tự ưu tiên rồi hạn; có dấu quá hạn; Kinh doanh không thấy | 5 dòng theo `vw_OpenFeedback`; 2 dòng quá hạn có nhãn "Quá hạn"; mary không có khối này | PASS |
| T17 | Lịch chăm sóc | nhóm Quá hạn / Hôm nay / Sắp tới bằng view | Quá hạn 2, Sắp tới 2, Hôm nay 0 (nhóm rỗng không vẽ); khớp `GROUP BY ScheduleStatus` | PASS |
| T18 | Trang tổng quan của `mary` | không có thẻ/danh sách phản hồi, có doanh thu | 5 thẻ, không có khối phản hồi, có biểu đồ doanh thu và top khách hàng | PASS |
| T19 | Trang tổng quan của `david` | không có doanh thu/chart/top khách hàng, có phản hồi và chăm sóc | 5 thẻ, không có biểu đồ doanh thu và top khách hàng; có lịch chăm sóc và phản hồi | PASS |
| T20 | CSDL rỗng | mỗi khối hiện trạng thái rỗng, KPI bằng 0 thật | Đợt 2, trên `CustomerCareDB_Empty`: `sp_DashboardSummary` trả `0 0 0 0 0 0 .00 NULL`, sáu thẻ KPI đúng các số 0 đó; biểu đồ 12 cột 0 kèm "Năm 2026 chưa có doanh thu."; bốn khối danh sách hiện "Không có ... nào." / "Chưa có khách hàng nào."; ba tab báo cáo hiện bảng 0 dòng và tổng 0, ba tệp CSV chỉ có dòng tiêu đề | PASS |
| T21 | Một khối lỗi | chỉ khối đó báo lỗi, khối khác vẫn hiện, không lộ SQL | Đợt 2, đổi tên `sp_MonthlyRevenue` trong CSDL thử: khối doanh thu, top khách hàng và phản hồi **biến mất** thay vì hiện thông báo lỗi (B02); đổi tên `vw_OpenFeedback` thì cả trang trả 500 vì chuông ném lỗi (B03). Đã sửa B02, B03 và chạy lại ngày 28/10: trang trả 200, đúng khối hỏng hiện "Đã xảy ra lỗi, thao tác chưa hoàn tất." + "Vui lòng thử lại...", các khối khác vẫn hiện, chuông rụng đúng nhóm hỏng (còn 2 nhóm, tổng 4), `/reports/*` và `/customers` vẫn 200, HTML không chứa tên đối tượng SQL | PASS (sau khi sửa B02, B03) |
| T22 | `/reports` theo vai trò | john → doanh thu, mary → doanh thu, david → chăm sóc | 302 lần lượt tới `/reports/revenue`, `/reports/revenue`, `/reports/care` | PASS |
| T23 | Tab doanh thu | dòng bằng thủ tục; dòng tổng bằng `SUM`; chart bằng bảng | 12 dòng khớp; tổng 11 hợp đồng / 185.000.000 VNĐ; cột biểu đồ bằng bảng. Đợt 3 trên CSDL thật: 82.000.000 VNĐ, bảng và cột vẫn khớp | PASS |
| T24 | Tab khách hàng | xếp hạng bằng view, lọc hạng, sắp xếp, phân trang, donut bằng `COUNT(*)` | 26 khách đang hoạt động; lọc `tier=Gold` còn 1 dòng; trang 2 hiện 21-26; donut 19/3/1/3 khớp SQL | PASS |
| T25 | Tab chăm sóc | bảng bằng `sp_MonthlyCareStats` đã xoay; điểm bằng `AVG` của Survey; không có điểm tổng hợp | 11 lần chăm sóc trong 2026; tháng 10 có 3; điểm 4,0 / 5,0 / 4,0 chỉ ở cột Khảo sát | PASS |
| T26 | Quyền trên trang | david bị chặn doanh thu/khách hàng; mary bị chặn chăm sóc | david: 302 → `/access-denied` cho `/reports/revenue` và `/reports/customers`; mary: 302 → `/access-denied` cho `/reports/care` | PASS |
| T27 | Quyền trên tệp xuất | ba lời từ chối như trên; khách chưa đăng nhập về trang đăng nhập | mary xuất chăm sóc và david xuất doanh thu đều 302 → `/access-denied`; ẩn danh 302 → `/login?ReturnUrl=...` | PASS |
| T28 | Xuất doanh thu | tệp mở được, có BOM, tiêu đề theo ngôn ngữ, 12 dòng + tổng, số bằng bảng | `ccm-revenue-2026.csv`, BOM EF BB BF, tiêu đề "Tháng / Số hợp đồng / Doanh thu", 12 dòng + "Tổng cộng,11,185000000" | PASS |
| T29 | Xuất khách hàng có lọc hạng | đủ mọi dòng khớp lọc, không chỉ trang 1, cùng thứ tự | `ccm-customers-Gold.csv` có đúng 1 dòng Vàng đang hoạt động, không phân trang | PASS |
| T30 | Tên khách hàng chứa công thức | ô bị vô hiệu hoá/bọc nháy, tệp vẫn đúng số cột | Đợt 2, chèn bốn khách có tên `=HYPERLINK(""http://evil.example"",""bam vao"")`, `+SUM(1+1)`, `Cong ty A, B`, `-2+3` vào CSDL thử rồi xuất CSV: ba tên bắt đầu bằng `= + -` được chèn `'` phía trước; ô có nháy và dấu phẩy được bọc nháy kép và nhân đôi nháy; cả bốn dòng vẫn đủ 5 cột | PASS |
| T31 | Chuông của `john` | 3 nhóm, số đếm bằng view, tối đa 3 việc, có "xem tất cả" | 3 nhóm 2/2/2, badge tổng 6, mỗi nhóm 2 việc, có liên kết xem tất cả | PASS |
| T32 | Chuông của `mary` | không có nhóm phản hồi, tổng không tính phản hồi | 2 nhóm (hợp đồng, chăm sóc), badge tổng 4 | PASS |
| T33 | Chuông không có việc | không có badge đỏ, nhóm ghi "không có mục nào" | Đợt 2, trên CSDL rỗng: không có badge đỏ; bảng chuông hiện một dòng "Không có việc nào cần chú ý." thay cho danh sách nhóm (danh sách nhóm rỗng thì ẩn hẳn, đúng như thiết kế) | PASS |
| T34 | Tìm nhanh "nguy", "an", "công", "ABC" | tối đa 8 gợi ý, chỉ khách đang hoạt động, không phân biệt hoa/thường và dấu | `nguy` → 1 (Nguyễn Thị Thu); `an` → 6; `công` → 5; `ABC` → 1. Đợt 2 xem trên trình duyệt phát hiện B04 (in mã `Individual`); đã sửa và chạy lại 28/10: JSON có `customerTypeLabel` = "Cá nhân" (VI) / "Individual" (EN), danh sách hiện "Nguyễn Thị Thu · Cá nhân" | PASS (sau khi sửa B04) |
| T35 | Tìm nhanh 1 ký tự / chuỗi `<script>` | không gọi CSDL hoặc trả rỗng; không chạy mã | `q=n` → `[]`; `q=<script>alert(1)</script>` → `[]` | PASS |
| T36 | Tìm nhanh khi tắt JavaScript | Enter gửi `/customers?q=...` như cũ | **Chưa chạy**; trình duyệt của bộ công cụ không tắt được JavaScript | NOT RUN |
| T37 | Chuyển ngôn ngữ trên `/`, `/reports/*`, chuông | mọi chuỗi và tên tháng đổi; tiền `80.000.000 VNĐ` / `80,000,000 VND`; ngày `dd/MM/yyyy` | Đổi sang EN: "Revenue for 2026", "Contracts", "Total", "Feb", "185,000,000 VND"; dữ liệu khách hàng vẫn tiếng Việt. Lần chạy đầu phát hiện B01 (phụ đề in ra `{0}`), đã sửa và chạy lại: "Monthly revenue for 2026" | PASS (sau khi sửa B01) |
| T38 | Giao diện tối | biểu đồ, donut, badge, thẻ KPI đọc được; không màu cứng | Đợt 2, trên trình duyệt thật: bấm nút "Tối" đổi `data-theme="dark"`, nền `rgb(11, 18, 32)` chữ `rgb(232, 238, 248)`; ảnh chụp trang tổng quan, tab Doanh thu, tab Khách hàng đều đọc được; cột biểu đồ dùng `var(--primary)`, donut dùng `var(--neutral)`, `--silver-ink`, `--gold-ink`, `--diamond-ink`. Ghi chú: trong tối, các múi donut hơi nhạt nhưng vẫn phân biệt được nhờ chú giải kèm số | PASS (có ghi chú) |
| T39 | Khả năng truy cập cơ bản | mỗi biểu đồ có bảng hoặc số; trạng thái không chỉ bằng màu; tab/gợi ý dùng được bằng bàn phím | Đợt 2: gõ "nguy" rồi `ArrowDown` làm nổi mục đầu, `Enter` mở `/customers/3`; `Tab` tới nút chuông, `Enter` mở bảng, `aria-expanded` chuyển `false` → `true`; donut có `aria-label`, các nút đều có `aria-label`; badge luôn kèm chữ | PASS |
| T40 | An toàn dữ liệu khi chỉ đọc | mở trang và xuất tệp không ghi gì | Trước và sau khi mở dashboard, 3 tab và 3 tệp: `AUDIT_LOG` 12, `CONTRACT` 25, `CUSTOMER` 27, `FEEDBACK` 9 - không đổi | PASS |
| T41 | Giao diện điện thoại (< 768 px) | dùng được | **Chưa chạy**; bộ công cụ không đổi được kích thước khung nhìn (cửa sổ thử rộng 826 px), giống M02-M04 | NOT RUN |
| T42 | Trang 2 của bảng xếp hạng | phân trang sang trang 2 được | 26 khách, cỡ trang 20; `?page=2` hiện "21-26 trong 26 kết quả" | PASS |
| T43 | CSDL thật không bị đụng | không thêm dòng, không đổi schema | Đợt kiểm thử chạy trên `CustomerCareDB_Test`; không có thay đổi SQL nào. Lần chạy thử đầu nối nhầm CSDL thật và `sp_ExpireContracts` báo 0 hợp đồng đổi trạng thái (xem mục 1) | PASS (có ghi chú) |

## 4. Việc chưa kiểm chứng

- Tắt JavaScript (T36) và viewport điện thoại (T41).
- Một năm nằm trong khoảng dữ liệu nhưng không có hợp đồng (T13); dữ liệu mẫu chỉ có năm 2026.
- Chưa đo tải/chậm do chuông đọc thêm ba truy vấn mỗi trang; chưa thấy chậm bất thường khi mở trang.
- Chưa thử Excel thật trên máy có bộ gõ tiếng Việt; mới kiểm tra BOM và nội dung tệp.

## 4b. Lỗi sau đợt 2 và đợt 3

B02, B03, B04 đã được sửa ngày 28/10; B05, B06, B07 (ba lỗi giao diện người dùng báo qua ảnh chụp)
đã sửa trong cùng ngày. Chi tiết ở `bug-report.md`.

| Mã | Ca phát hiện | Trạng thái |
|---|---|---|
| B02 | T21 | Đã sửa; T21, T09, T18, T19 đạt |
| B03 | T21 | Đã sửa; T21, T31-T33 đạt |
| B04 | T34 | Đã sửa; T34, T37 đạt |
| B05 | Ảnh chụp ngày 28/10 | Đã sửa; T11, T23 đạt (12 cột vẫn khớp thủ tục, nhãn `45 tr` / `12 tr` không còn dính) |
| B06 | Ảnh chụp trang đăng nhập | Đã sửa; xem lại ảnh chụp 1440 px và 842 px, đăng nhập bằng `john` vẫn vào được |
| B07 | Ảnh chụp thẻ KPI | Đã sửa; T09, T18, T19 đạt (6 / 5 / 5 thẻ, thẻ doanh thu rộng 3/4 hàng) |

## 4c. Đợt 3 - chỉnh giao diện theo phản hồi người dùng (28/10)

Không thêm ca mới; chỉ chạy lại các ca bị ảnh hưởng bởi CSS và Razor.

| Ca | Kết quả đợt 3 |
|---|---|
| T11 | 12 cột vẫn theo `sp_MonthlyRevenue` (45 / 12 / 12 / 13 triệu). Nhãn đầu cột nay rút gọn theo triệu kèm dấu xấp xỉ (`≈45 tr`), số đầy đủ ở `title` — **PASS** |
| T23 | Bảng và cột vẫn khớp nhau; tổng 82.000.000 VNĐ trên CSDL thật — **PASS** |
| T09 / T18 / T19 | `john` 6 thẻ, `mary` 5 thẻ + thẻ doanh thu `md:col-span-4`, `david` 5 thẻ + thẻ phản hồi `md:col-span-4`; biểu tượng nằm bên trái con số, không thẻ nào tràn (`scrollWidth` = `clientWidth` ở cả 6 thẻ) — **PASS** |
| T38 | Chỉ xem lại giao diện sáng ở 1440 px và 842 px. **Chưa chạy lại chế độ tối trong đợt này** |
| T01 / T02 | `dotnet build` 0 lỗi / 0 cảnh báo; `dotnet test` 159/159 đạt — **PASS** |

## 5. Ghi chú khác

- Không có thay đổi nào trong `database/*.sql`; không scaffold lại entity.
- Tệp CSV dùng số không có dấu phân cách nghìn (ví dụ `185000000`), đúng như thiết kế để Excel đọc
  thành số.
- Năm ngoài khoảng dữ liệu được đưa về năm hiện tại (`ClampYear`); URL vẫn giữ `?year=` cũ, nhưng ô
  chọn năm hiển thị đúng năm đang xem.
- `dotnet test` dựng lại `CustomerCareDB_Test` từ `database/03`, mà dữ liệu mẫu tính theo ngày chạy,
  nên sau lần dựng lại ngày 28/10 doanh thu năm 2026 là 205.000.000 VNĐ (tháng 1 có thêm một hợp đồng)
  thay vì 185.000.000 như bảng số liệu ở trên. Các ca chạy lại chỉ kiểm tra hành vi (khối nào hiện, số
  có khớp thủ tục), không so với con số của ngày 27/10.
- Lần chạy lại B02/B03 có đổi tên `sp_MonthlyRevenue`, `vw_CustomerRevenue`, `vw_OpenFeedback` trong
  `CustomerCareDB_Test` rồi khôi phục ngay; đã kiểm tra `sys.objects` không còn tên `%broken%`.
