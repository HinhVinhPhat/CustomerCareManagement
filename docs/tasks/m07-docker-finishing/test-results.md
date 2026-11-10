# M07 - Docker và hoàn thiện - kết quả kiểm thử

Ngày chạy: **10/11/2026**. Mọi dòng dưới đây được chạy thật; chưa chạy thì ghi **NOT RUN**, không đánh
dấu PASS. Lỗi phát hiện và đã sửa: **B01** trong `bug-report.md`.

## 1. Môi trường

| Mục | Giá trị |
|---|---|
| Hệ điều hành | Windows 11 Home Single Language, build 26300 |
| Docker | Docker Desktop, CLI 29.7.2, Linux containers (SQL Server 2022 chạy native amd64) |
| .NET | SDK 10.0.401, target `net10.0`; ảnh `sdk:10.0` và `aspnet:10.0` |
| Ảnh SQL Server | `mcr.microsoft.com/mssql/server:2022-latest` (đã có sẵn trên máy) |
| Ảnh ứng dụng | `customercaremanagement-web:latest`, **456 MB** (nền `aspnet` 342 MB) |
| Cổng | web `8080`, db `1433` (chỉ dev) |
| Tệp cấu hình | `.env` tạo từ `.env.example` (không commit); `git check-ignore .env` in ra `.env` |
| Công cụ kiểm tra giao diện | `Invoke-WebRequest` / `HttpClient` + `docker compose exec` |

## 2. Kiểm thử tự động (trên máy chủ, sau khi sửa `Program.cs`)

```
dotnet build src\CcmWeb\CcmWeb.csproj
Build succeeded.  0 Warning(s)  0 Error(s)

dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj
Passed!  - Failed: 0, Passed: 218, Skipped: 0, Total: 218
```

Không thêm ca đơn vị cho `Security:RedirectToHttps` và `Security:DataProtectionKeys`: cả hai chỉ là dây
nối cấu hình của framework, và đã được chứng minh bằng ca Docker thật D06 (phiên đăng nhập sống qua
restart) và D12 (không còn cảnh báo HTTPS). Ghi rõ ở đây thay vì thêm ca hình thức.

## 3. Kiểm thử Docker

| Mã | Ca | Kết quả |
|---|---|---|
| D01 | `docker compose build` | **PASS** - build hai tầng thành công, ảnh 456 MB, publish Release từ `sdk:10.0` chạy trên `aspnet:10.0` |
| D02 | `docker compose up -d --build` trên máy chưa có volume | **PASS** - `db` healthy, `db-init` chạy xong, `web` healthy, cổng 8080 mở; cả một lệnh, không thao tác tay |
| D03 | Log `db-init` lần đầu | **PASS** - `Running 01_CreateTables.sql`, `Running 02...`, `Running 03...`, `OK: sample data loaded`, `OK: app_ccm user is ready` |
| D04 | Đăng nhập `john`/`123456` qua `http://localhost:8080` | **PASS** - POST `/login` -> 302 `/`; dashboard 200, có form đăng xuất; `/customers` 200 |
| D05 | Ghi qua web trong container rồi đọc `AUDIT_LOG` | **PASS** - tạo sản phẩm qua `/admin/products/create` (302), sản phẩm hiện trong danh sách; `AUDIT_LOG` có `PRODUCT_SERVICE 9 Insert emp=1 Docker test product | Product | 1500000.00` (người thực hiện = john) |
| D06 | `docker compose restart web` rồi mở lại bằng cookie cũ | **PASS** - sau restart, `/` 200 và vẫn đăng nhập; `/customers` 200 (khoá Data Protection nằm ở volume `keys`) |
| D07 | `docker compose down` + `up -d` | **PASS** (sau khi sửa B01) - `db-init` báo `Skipping 01-03` + `CustomerCareDB is ready (1)` + `OK: app_ccm user is ready`; `CUSTOMER` vẫn 10 dòng, sản phẩm thử vẫn còn; phiên đăng nhập cũ **vẫn dùng được** |
| D08 | `down -v` + `up -d` | **PASS** - `db-init` chạy lại `01`, `OK: sample data loaded`; 10 khách hàng mẫu; sản phẩm thử **biến mất**; cookie cũ bị từ chối (302 về `/login`) vì volume `keys` đã xoá; đăng nhập lại được |
| D09 | Biến môi trường của `web` | **PASS** - `ConnectionStrings__DefaultConnection=...User Id=app_ccm...`, không có `SA_PASSWORD`/`sa` |
| D10 | `.env` | **PASS** - `git check-ignore .env` in `.env`; `git status` không thấy `.env` |
| D11 | `/health` | **PASS** - không đăng nhập: 200 `Healthy`; `docker compose stop db` -> 503; `start db` -> 200 trở lại |
| D12 | Log `web` có cảnh báo HTTPS | **PASS** - không còn `Failed to determine the https port`; log chỉ có `Now listening on: http://[::]:8080` |
| D13 | Ngôn ngữ và chữ tiếng Việt trong container | **PASS** - mặc định tiếng Việt (nhãn "Thêm khách hàng" có trên trang), `?culture=en` chuyển sang nhãn tiếng Anh; tạo sản phẩm tên `Sản phẩm kiểm tra` qua HTTP -> danh sách hiện đúng `ả` (U+1EA3) và `ệ` (U+1EC7) |
| D14 | Phân quyền trong container | **PASS** - `mary` (Kinh doanh): `/admin/products` 200, **không** có liên kết `/admin/products/create`; `/admin/accounts` -> `/access-denied?ReturnUrl=/admin/accounts` |
| D15 | Background service hết hạn hợp đồng | **PASS** - log `EXEC dbo.sp_ExpireContracts`, `Contract expiry job finished: 0 contract(s) moved to Expired` |
| D16 | Kiểm thử tự động trên máy chủ | **PASS** - build 0 cảnh báo/0 lỗi, 218/218 ca đạt |
| D17 | Checklist `design.md` mục 9 | **PASS một phần** - xem mục 4 bên dưới; dòng "giao diện dùng được trên điện thoại" vẫn **NOT RUN** (không chụp ảnh được, xem N01) |
| D18 | Làm đúng từng bước README trên trạng thái sạch | **PASS** - `down -v`, rồi `up -d --build`: `db`+`web` healthy, `db-init` chạy 01-03, `/health` 200, đăng nhập và `/customers` hiện `ABC` |

## 4. Checklist `design.md` mục 9 (D17)

| Dòng | Bằng chứng |
|---|---|
| Mọi trang kiểm quyền phía server | Ca M01-M06 + D14 (`mary` bị chặn `/admin/accounts`) |
| Mật khẩu băm bằng `PasswordHasher`, không plain text/không SHA2 không muối | `AccountRulesTests` (7 ca), hash SHA2 demo được nâng cấp khi đăng nhập (M06) |
| Mọi truy vấn có tham số | Toàn bộ dữ liệu đi qua EF Core; không có câu SQL nối chuỗi trong `Services/` (đã rà khi viết M02-M06) |
| `SESSION_CONTEXT('EmployeeID')` cho mọi kết nối | `SessionContextTests` + D05 (`AUDIT_LOG` ghi `emp=1`) |
| Xoá khách hàng = ngưng hoạt động, không xoá cứng | Ca M02 + `sp_SetCustomerStatus` |
| Lỗi SQL được dịch thành câu người dùng hiểu | `TranslateSqlError` có ca đơn vị (M01, M06) |
| Giao diện dùng được trên điện thoại | **NOT RUN** - chỉ kiểm được markup (`tbl-cards`, `md:grid-cols-*`); máy này không chụp ảnh trình duyệt được |

## 5. Chưa chạy (NOT RUN)

| Mã | Ca | Lý do |
|---|---|---|
| N01 | Ảnh chụp 4 trang quản trị và dashboard ở ba bề rộng | Công cụ chụp ảnh báo "Screenshot needs a visible tab" (giống M06) |
| N02 | Chạy trên máy Apple-silicon hoặc trên một máy khác thật | Chỉ có một máy Windows 11; phần "máy sạch" được mô phỏng bằng `down -v` |
| N03 | Reverse proxy HTTPS (Nginx/Caddy) trước `web` | Ngoài phạm vi M07; đã ghi hướng dẫn trong README và `design.md` mục 11 |
| N04 | Rate limiter sau proxy (mọi người cùng IP) | Chỉ ghi chú giới hạn, không sửa chính sách trong M07 |

## 6. Dọn dẹp

Sau khi ghi kết quả: `docker compose down -v` -> không còn container/volume `customercaremanagement_*`;
`docker compose ps -a` trống. Ảnh `customercaremanagement-web:latest` và ảnh nền `dotnet`/SQL Server còn
trong máy để lần sau chạy nhanh. `CustomerCareDB` trên SQL Server `.\SQLEXPRESS` của máy **không** bị
đụng tới trong đợt này (mọi ca Docker chạy trên CSDL trong container).
