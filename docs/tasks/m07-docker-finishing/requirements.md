# M07 - Docker và hoàn thiện - yêu cầu

Trạng thái: **chờ người dùng duyệt** (cùng `plan.md`). Chưa viết code.

## 1. Mục tiêu

Đưa ứng dụng chạy được **trọn gói bằng Docker** trên một máy khác chỉ với `docker compose up -d --build`
(SQL Server + nạp dữ liệu một lần + web MVC), dữ liệu không mất khi khởi động lại, và rà soát lần cuối
(checklist `docs/design.md` mục 9, README, tài liệu) trước khi bàn giao.

Hiện trạng: `docker-compose.yml` mới bật `db` và `db-init`; dịch vụ `web` còn bị chú thích; `src/CcmWeb`
chưa có `Dockerfile`; chưa có `.dockerignore`; các tệp này **chưa từng chạy trên máy thật** (chính
`ccm-docker-deploy` ghi như vậy). M06 đã xong và đã commit (`dcfae12`).

Nguồn: `docs/design.md` mục 8 (thứ tự công việc), mục 9 (checklist), mục 11 (Docker - **đã lỗi thời**:
còn `api` + `web`, thực tế chỉ còn một MVC `web`); skill `ccm-docker-deploy`, `ccm-mvc-architecture`,
`ccm-database`.

## 2. Phạm vi

| # | Việc | Ghi chú |
|---|---|---|
| 1 | `src/CcmWeb/Dockerfile` hai tầng: `sdk:10.0` publish Release → `aspnet:10.0` chạy | Đúng `net10.0` trong `.csproj`; không đổi framework |
| 2 | `src/CcmWeb/.dockerignore` | Loại `bin/`, `obj/`, `.vs/`, `logs/`, tệp cục bộ |
| 3 | Bật dịch vụ `web` trong `docker-compose.yml` | `depends_on: db-init` với `service_completed_successfully`, cổng `8080:8080`, `ASPNETCORE_URLS=http://+:8080`, chuỗi kết nối dùng `app_ccm` (**không** `sa`) |
| 4 | Giữ đăng nhập qua lần khởi động lại container | Data Protection: lưu khoá vào `/keys`, volume `keys`; đường dẫn lấy từ cấu hình, không có cấu hình thì giữ mặc định của framework |
| 5 | Tắt chuyển hướng HTTPS khi chạy trong compose (không có chứng chỉ) | Bằng một khoá cấu hình, mặc định vẫn bật như hiện nay; compose đặt tắt |
| 6 | Điểm kiểm tra sức khoẻ `/health` (anonymous) cho `depends_on`/healthcheck của `web` | Trả 200 khi ứng dụng và CSDL trả lời được; không lộ thông tin nội bộ |
| 7 | `README.md`: mục Docker đầy đủ (3 dịch vụ, cổng, lần chạy đầu, `down -v`) + rà lại các mục đã cũ sau M05/M06 | Không viết lại toàn bộ README |
| 8 | `.env.example`: ghi rõ biến nào dùng ở đâu, cảnh báo ký tự cấm | Không đổi giá trị mẫu |
| 9 | `docs/design.md` mục 11: sửa cho đúng thực tế (một `web`, không `api`) | Chỉ mục 11; mục 10 giữ nguyên dấu "OUTDATED" |
| 10 | `docs/tasks/m07-docker-finishing/`: `test-results.md`, `bug-report.md`, `report.md` | Theo `ccm-workflow` |

**Không làm**: không thêm dịch vụ mới (nginx, Redis, Seq...), không đổi nghiệp vụ, không đổi schema,
không đổi framework/package, không thêm tính năng ngoài `design.md`, không commit `.env`.

## 3. Cơ sở dữ liệu

Không đổi `database/*.sql`. `database/init.sh` đã đúng nguyên tắc "chỉ chạy 01-03 khi CSDL chưa có,
04 luôn chạy"; M07 chỉ **kiểm chứng** nó trên Docker thật (LF, `-I -f 65001 -C`, chờ `db` healthy) và
sửa nếu chạy sai.

## 4. Giả định

1. Máy kiểm thử có Docker Desktop + WSL2 (máy hiện tại: Docker CLI 29.7.2, daemon chưa bật; tôi đã mở
   Docker Desktop - nếu daemon không lên được thì các ca Docker ghi **BLOCKED**, không đánh PASS).
2. Ảnh `mcr.microsoft.com/dotnet/sdk:10.0` và `aspnet:10.0` dùng được (đúng `net10.0` của dự án).
3. Chạy trong compose là **HTTP sau này sẽ có reverse proxy**: không bật HTTPS/HSTS trong container,
   cookie để `SameAsRequest` như hiện tại.
4. `app_ccm` là tài khoản duy nhất ứng dụng dùng; `sa` chỉ tồn tại trong `db-init`.
5. `/keys` là volume riêng, không chứa trong image; quyền ghi của user `app` trong container được xử lý
   bằng `chown` trong Dockerfile hoặc volume có sẵn.

## 5. Định nghĩa hoàn thành

- `docker compose up -d --build` trên máy sạch chạy **không cần thao tác tay**; `db-init` báo đã nạp dữ liệu
  (hoặc "skipped" khi CSDL đã có) và kết thúc; `web` healthy; mở `http://localhost:8080/login` đăng nhập
  được bằng `john` / `123456`.
- `docker compose down` rồi `up` lại: **dữ liệu còn nguyên** và **phiên đăng nhập còn** (không bị đá ra
  `/login`). `down -v` rồi `up`: CSDL được dựng lại từ đầu, dữ liệu mẫu mới.
- `AUDIT_LOG` sau một thao tác qua web trong container có `EmployeeID` khác `NULL`.
- Không có `sa` trong chuỗi kết nối của `web`; `.env` không được commit (`git check-ignore .env`).
- `dotnet build` 0 cảnh báo 0 lỗi; `dotnet test` toàn bộ 218 ca đạt trên SQL Server thật.
- Tài liệu: README + `design.md` mục 11 khớp thực tế; `test-results.md` ghi đúng việc đã chạy, có
  `bug-report.md` cho lỗi phát sinh và `report.md` bàn giao.

## 6. Rủi ro

| Rủi ro | Cách xử lý |
|---|---|
| Docker daemon/WSL2 không lên trên máy này | Ghi BLOCKED cho các ca Docker, vẫn hoàn tất phần code/tài liệu và nêu rõ trong báo cáo |
| Ảnh SQL Server 2022 nặng (~1.5 GB), lần đầu chậm | Chờ healthcheck (start_period 30s, 20 lần), không rút ngắn |
| Quyền ghi `/keys` trong image `aspnet` (user `app`) | `USER root` tạo thư mục + `chown`, rồi `USER app`; kiểm bằng log "keys persisted" sau khi restart |
| Rate limiter theo IP: trong container mọi người cùng IP của proxy | Chỉ ghi chú giới hạn, **không** sửa chính sách trong M07 (ngoài phạm vi, cần duyệt riêng) |
| `UseHttpsRedirection` trong container báo "Failed to determine the https port" | Bước 5 của phạm vi: tắt bằng cấu hình, compose đặt tắt |

## 7. Câu hỏi cần duyệt

Đề xuất dưới đây là mặc định trong `plan.md`; trả lời khác thì các bước tương ứng đổi theo.

1. **Data Protection**: lưu khoá vào volume `/keys` để giữ đăng nhập sau khi restart? (đề xuất: **có**)
2. **HTTPS trong container**: thêm khoá cấu hình `Security:RedirectToHttps` (mặc định `true`, compose đặt
   `false`)? (đề xuất: **có**) Hay giữ nguyên `UseHttpsRedirection` và chấp nhận cảnh báo trong log?
3. **`/health`**: thêm endpoint anonymous trả 200/503 có kiểm tra CSDL để compose healthcheck `web`?
   (đề xuất: **có**)
4. **`design.md` mục 11**: sửa lại cho đúng một `web` (bỏ `api`)? (đề xuất: **có**)
5. **README**: chỉ cập nhật mục Docker và các câu đã lỗi thời, hay viết lại toàn bộ cho gọn? (đề xuất:
   **chỉ cập nhật chỗ cần**)
6. Có cần thêm **healthcheck cho `web`** trong compose (dùng `/health`) không? (đề xuất: **có**)
