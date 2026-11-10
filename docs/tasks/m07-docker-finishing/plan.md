# M07 - Docker và hoàn thiện - kế hoạch

Đi kèm `requirements.md`; **một lần duyệt** áp dụng cho cả hai tệp. Các bước chạy theo thứ tự; việc
ngoài phạm vi hoặc thay đổi nghiệp vụ thì dừng lại và hỏi trước.

Các bước dưới đây giả định câu trả lời ở `requirements.md` mục 7 là phương án đề xuất (có `keys`,
có khoá cấu hình HTTPS, có `/health`, sửa `design.md` mục 11, cập nhật README tại chỗ, có healthcheck
cho `web`).

## Bước 0 - kiểm tra môi trường (không sửa gì)

1. `docker version` + `docker info` (daemon phải lên). Nếu không lên: ghi BLOCKED, vẫn làm Bước 1-3 và 6,
   dừng trước Bước 4-5 để hỏi.
2. `git check-ignore .env` phải in `.env`; `.gitattributes` phải có `*.sh text eol=lf`; `database/init.sh`
   phải là LF (`git ls-files --eol database/init.sh`).
3. Ghi lại số dòng `AUDIT_LOG` và dữ liệu mẫu trên `CustomerCareDB` thật để so sánh sau (không dùng làm
   nguồn cho ca Docker - ca Docker chạy trên CSDL trong container).

## Bước 1 - đóng gói ứng dụng

1. `src/CcmWeb/Dockerfile` (hai tầng, `net10.0`):
   - tầng build: `mcr.microsoft.com/dotnet/sdk:10.0`, copy `.csproj` → `dotnet restore` → copy toàn bộ →
     `dotnet publish src/CcmWeb.csproj -c Release -o /app/publish --no-restore`;
   - tầng chạy: `mcr.microsoft.com/dotnet/aspnet:10.0`, `WORKDIR /app`, tạo `/keys` và `chown app`,
     copy publish, `USER app`, `EXPOSE 8080`, `ENTRYPOINT ["dotnet","CcmWeb.dll"]`.
   - Không COPY `appsettings.Development.json` bí mật, không nhúng chuỗi kết nối; không đổi `net10.0`.
2. `src/CcmWeb/.dockerignore`: `bin/`, `obj/`, `.vs/`, `*.user`, `appsettings.*.local.json`, `.git`.
3. `docker build` thử ngay ở máy để bắt lỗi restore/publish trước khi ghép compose.

## Bước 2 - cấu hình ứng dụng cho container (`Program.cs`)

1. Data Protection: đọc `Security:DataProtectionKeys` (đường dẫn). Có giá trị thì
   `AddDataProtection().PersistKeysToFileSystem(...)` + `SetApplicationName("CcmWeb")`; không có thì giữ
   mặc định (máy dev không đổi hành vi). Không tạo thư mục trong mã nếu thiếu quyền - ghi cảnh báo.
2. HTTPS: đọc `Security:RedirectToHttps` (mặc định `true`). `false` thì bỏ `UseHttpsRedirection()` và ghi
   một dòng log; `true` giữ nguyên như hiện tại.
3. `/health`: một action tối giản ở controller riêng (`HealthController`) hoặc `MapGet` trong `Program.cs`,
   `[AllowAnonymous]`, mở kết nối EF và chạy `SELECT 1`; OK → 200 `Healthy`, lỗi → 503. Không trả chi tiết
   lỗi, không trả số phiên bản/chuỗi kết nối.
4. HSTS: chỉ bật khi `RedirectToHttps` bật (để container không gửi HSTS trên HTTP).
5. Không đổi login, phân quyền, rate limiter, background service.

## Bước 3 - compose, biến môi trường, tài liệu

1. `docker-compose.yml`:
   - bật `web`: `build: ./src/CcmWeb`, `depends_on: db-init: service_completed_successfully`,
     `environment`: `ConnectionStrings__DefaultConnection` (host `db`, user `app_ccm`),
     `ASPNETCORE_URLS=http://+:8080`, `Security__RedirectToHttps=false`,
     `Security__DataProtectionKeys=/keys`; `volumes: keys:/keys`; `ports: 8080:8080`;
     `healthcheck` gọi `/health`; `restart: unless-stopped`.
   - thêm volume `keys`; `db`/`db-init` giữ nguyên.
2. `.env.example`: chú thích thêm cho `web` (không dùng `;`, `$`, `'`), nhắc `down -v` khi đổi mật khẩu.
3. `README.md`: mục Docker (3 dịch vụ, `http://localhost:8080`, lần chạy đầu, `down`/`down -v`), sửa các câu
   đã cũ (đường dẫn trang sau M05/M06, số ca test).
4. `docs/design.md` mục 11: sửa cây thư mục và bảng dịch vụ cho đúng một `web`; bỏ `api`, `API_URL`.
5. `.agents/skills/ccm-docker-deploy/SKILL.md`: sau khi chạy thật, đổi các câu "chưa từng chạy" thành trạng
   thái đã kiểm chứng, ghi cổng và lệnh đúng.

## Bước 4 - chạy thật trên Docker

1. `docker compose build` rồi `docker compose up -d`; theo dõi `docker compose logs -f db-init` tới khi
   kết thúc; `docker compose ps` thấy `db` healthy, `web` healthy.
2. Lần đầu (chưa có volume): log `db-init` phải có `Running 01/02/03`, `OK: sample data loaded`,
   `OK: app_ccm user is ready`.
3. `http://localhost:8080/login` → đăng nhập `john` / `123456`; mở dashboard; xem một hợp đồng; kiểm tra
   `AUDIT_LOG` qua `sqlcmd` trong container `db` (dùng `app_ccm`, không `sa`).
4. `docker compose restart web` và `docker compose down` + `up -d`: dữ liệu còn, phiên đăng nhập còn,
   `db-init` log "Skipping 01-03".
5. `docker compose down -v` + `up -d`: dữ liệu mẫu dựng lại, ngày trong `03_SampleData.sql` tính theo lúc
   nạp.
6. Kiểm tra không có `sa` trong biến môi trường của `web`; `.env` không vào git.

## Bước 5 - rà soát hoàn thiện (checklist `design.md` mục 9)

Đi từng dòng checklist trên ứng dụng thật (localhost và trong container), ghi PASS/FAIL kèm bằng chứng;
không sửa nguồn trong lúc chạy ca. Mục nào đã có ca từ M01-M06 thì dẫn lại mã ca cũ thay vì chạy lại.

## Bước 6 - kiểm thử tự động cuối

1. `dotnet build src\CcmWeb\CcmWeb.csproj` - 0 cảnh báo, 0 lỗi.
2. `dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj` - kỳ vọng 218 ca đạt; nếu thêm ca cho `/health`
   hoặc cấu hình mới thì ghi rõ số ca tăng.
3. Ca mới dự kiến (đơn vị, không cần Docker): đọc `Security:RedirectToHttps` mặc định `true`, đọc
   `Security:DataProtectionKeys` rỗng thì không đổi hành vi. Nếu không tách được thành ca đơn vị gọn thì
   kiểm bằng ca tay, ghi rõ.

## Bước 7 - tài liệu bàn giao

`docs/tasks/m07-docker-finishing/test-results.md` (mỗi ca: mã, môi trường, thao tác, kỳ vọng, thực tế,
PASS/FAIL/BLOCKED/NOT RUN, bằng chứng), `bug-report.md` (nếu có lỗi, chờ duyệt lô sửa), `report.md`
(đã làm, đường dẫn chính, đã chạy thật, BLOCKED/NOT RUN, bước chờ duyệt).

## Ca kiểm thử dự kiến

| Mã | Ca | Kỳ vọng |
|---|---|---|
| D01 | `docker build` ảnh `web` | Build thành công, image chạy được |
| D02 | `compose up` từ máy chưa có volume | `db` healthy, `db-init` kết thúc 0, `web` healthy, cổng 8080 mở |
| D03 | Log `db-init` lần đầu | `Running 01/02/03`, `OK: sample data loaded`, `OK: app_ccm user is ready` |
| D04 | Đăng nhập `john`/`123456` qua `http://localhost:8080` | Vào dashboard, menu đủ vai trò Quản lý |
| D05 | Thao tác ghi qua web trong container (ví dụ đổi mật khẩu hoặc sửa sản phẩm) | `AUDIT_LOG` có dòng, `EmployeeID` của john, không NULL |
| D06 | `docker compose restart web` rồi mở lại trang | Vẫn đang đăng nhập (khoá Data Protection ở `/keys`), dữ liệu còn |
| D07 | `down` + `up -d` | Dữ liệu còn nguyên, `db-init` báo "Skipping 01-03" |
| D08 | `down -v` + `up -d` | CSDL dựng lại từ đầu, dữ liệu mẫu mới, đăng nhập được |
| D09 | Biến môi trường và chuỗi kết nối của `web` | Không có `sa`, chỉ `app_ccm` |
| D10 | `.env` | `git check-ignore .env` in ra `.env`; `git status` không thấy `.env` |
| D11 | `/health` | 200 khi CSDL trả lời; 503 khi dừng `db` (kiểm bằng cách stop `db` rồi gọi lại) |
| D12 | Cảnh báo HTTPS trong log `web` | Không còn "Failed to determine the https port" |
| D13 | Ngôn ngữ và định dạng trong container | Mặc định tiếng Việt, đổi EN/VI được, ngày `dd/MM/yyyy`, tiền `VNĐ` |
| D14 | Phân quyền trong container | `mary`/`david` chỉ đọc `/admin/products`, ba trang quản trị còn lại `/access-denied` |
| D15 | `sp_ExpireContracts` trong container | Log background service chạy lúc khởi động, không lỗi |
| D16 | Kiểm thử tự động trên máy chủ | `dotnet build` 0 lỗi, `dotnet test` 218/218 (hoặc số mới) |
| D17 | Checklist `design.md` mục 9 | Từng dòng PASS kèm bằng chứng, hoặc ghi rõ NOT RUN |
| D18 | README làm đúng từng bước trên máy sạch | Không phải sửa gì ngoài giá trị `.env` |

## Rủi ro và cách xử lý

| Rủi ro | Cách xử lý |
|---|---|
| Docker daemon không lên (WSL2) | Dừng ở Bước 4, ghi BLOCKED, báo ngay để người dùng bật Docker rồi chạy tiếp |
| Lần pull ảnh SQL Server đầu tiên rất chậm | Chờ, không rút ngắn healthcheck; ghi thời gian thật vào `test-results.md` |
| `/keys` không ghi được (user `app` trong image) | `chown` trong Dockerfile; kiểm bằng log và ca D06 |
| `/health` làm lộ thông tin | Chỉ trả trạng thái, không trả exception/chuỗi kết nối |
| Sửa `Program.cs` ảnh hưởng đường chạy dev | Mặc định giữ nguyên hành vi; ca D16 + chạy lại ứng dụng trên máy (localhost) một vòng đọc/ghi |
