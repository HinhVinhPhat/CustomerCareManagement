# M07 - Docker và hoàn thiện - báo cáo bàn giao

Trạng thái: **đã triển khai và đã chạy thật trên Docker ngày 10/11/2026**. Còn một lỗi đã sửa trong
cùng đợt (B01, `init.sh`), xem `bug-report.md`. Chưa commit. Chi tiết từng ca ở `test-results.md`.

## 1. Đã làm

- **Đóng gói ứng dụng**: `src/CcmWeb/Dockerfile` hai tầng (`sdk:10.0` publish Release → `aspnet:10.0`),
  chạy bằng user `app`, tạo sẵn `/keys`, `EXPOSE 8080`; `src/CcmWeb/.dockerignore` loại `bin/`, `obj/`,
  `.vs/`... Ảnh `customercaremanagement-web:latest` nặng 456 MB.
- **Compose đủ ba dịch vụ**: bật `web` với `depends_on: db-init` (`service_completed_successfully`),
  host `db`, user `app_ccm` (không `sa`), cổng `8080:8080`, `restart: unless-stopped`, healthcheck mở
  cổng bằng bash (ảnh runtime không có `curl`).
- **Giữ đăng nhập qua restart**: `Program.cs` đọc `Security:DataProtectionKeys` và
  `PersistKeysToFileSystem`; compose đặt `/keys` + volume `keys`. `down`/`up` giữ phiên; `down -v` xoá
  khoá nên phiên cũ bị từ chối - đúng ý đồ.
- **HTTPS**: thêm `Security:RedirectToHttps` (mặc định `true`, giữ nguyên hành vi dev); compose đặt
  `false` nên bỏ cả `UseHttpsRedirection` và HSTS, log không còn "Failed to determine the https port".
- **`/health`**: endpoint anonymous, chạy `SELECT 1`; 200 `Healthy` hoặc 503, không lộ chi tiết lỗi.
- **Sửa B01**: `database/init.sh` chờ CSDL mở được và thử lại file 04, vì `db` báo healthy trước khi
  các CSDL người dùng autostart xong (error 904 khiến `db-init` exit 1 và `web` không lên).
- **Tài liệu**: `README.md` thêm mục "Run everything with Docker" (3 dịch vụ, cổng, `down`/`down -v`,
  đổi mật khẩu), `docs/design.md` mục 11 viết lại cho đúng một `web` (bỏ `api`), `.env.example` giải
  thích từng biến và ký tự cấm, `ccm-docker-deploy` ghi trạng thái đã kiểm chứng + mục chẩn đoán 904.

## 2. Đường dẫn chính

| Loại | Tệp |
|---|---|
| Đóng gói | `src/CcmWeb/Dockerfile`, `src/CcmWeb/.dockerignore` |
| Chạy | `docker-compose.yml`, `.env.example`, `database/init.sh` |
| Cấu hình ứng dụng | `src/CcmWeb/Program.cs` (`Security:RedirectToHttps`, `Security:DataProtectionKeys`, `/health`) |
| Tài liệu | `README.md`, `docs/design.md` (mục 11), `.agents/skills/ccm-docker-deploy/SKILL.md` |
| Kiểm thử | `docs/tasks/m07-docker-finishing/requirements.md`, `plan.md`, `test-results.md`, `bug-report.md` |

## 3. Đã kiểm chứng thật

- `docker compose up -d --build` từ trạng thái sạch (không volume): `db` healthy → `db-init` chạy
  `01`-`03` → `web` healthy; `/health` 200; đăng nhập `john`/`123456`; `/customers` 200.
- Ghi qua web trong container → `AUDIT_LOG` ghi `EmployeeID = 1` (john).
- `restart web`: phiên đăng nhập còn. `down` + `up`: dữ liệu và phiên còn, `db-init` báo `Skipping
  01-03`. `down -v` + `up`: CSDL dựng lại, dữ liệu mẫu mới, cookie cũ bị từ chối.
- `/health`: 200 → `stop db` → 503 → `start db` → 200.
- Phân quyền trong container: `mary` chỉ đọc `/admin/products`, `/admin/accounts` → `/access-denied`.
- Chữ tiếng Việt đi và về đúng (tạo sản phẩm `Sản phẩm kiểm tra`, đọc lại thấy `ả`/`ệ`).
- `docker compose exec web env`: chuỗi kết nối dùng `app_ccm`, không có `sa`; `git check-ignore .env`
  trả `.env`.
- `dotnet build` 0 cảnh báo 0 lỗi; `dotnet test` **218/218** đạt trên SQL Server thật.

## 4. Chưa đạt / chưa chạy

- **Đã chạy bổ sung ngày 10/11/2026**: ảnh chụp ba bề rộng (15 ảnh) và kiểm tra giao diện điện thoại -
  xem `docs/tasks/m06-admin/test-results.md` mục 5; lỗi tràn ngang phát hiện ở đó (B02, M06) đã sửa nên
  dòng "Giao diện dùng được trên điện thoại" của checklist `design.md` nay **PASS**.
- **NOT RUN**: chạy trên máy Apple-silicon hoặc một máy thứ hai thật (chỉ mô phỏng "máy sạch" bằng
  `down -v`); reverse proxy HTTPS; hành vi rate limiter sau proxy. Chi tiết ở `test-results.md` mục 5.

## 5. Bước đang chờ

Người dùng xem lại kết quả rồi đồng ý commit M07 (một commit cho module). Sau đó hệ thống đủ 7 module;
việc còn lại nếu muốn nộp bài là bài thuyết trình - nằm ngoài phạm vi đã duyệt.
