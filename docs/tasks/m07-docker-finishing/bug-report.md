# M07 - Docker và hoàn thiện - báo cáo lỗi

Đợt kiểm thử ngày **10/11/2026** trên Docker Desktop thật (Linux containers). Có **một lỗi (B01)** phát
hiện trong lúc dựng module, **đã sửa trong cùng đợt** vì `requirements.md` mục 3 đã duyệt trước việc
"sửa `init.sh` nếu chạy sai".

## B01 - `db-init` thoát với mã 1 khi chạy lại trên CSDL đã có (error 904), nên `web` không khởi động

| Mục | Nội dung |
|---|---|
| Mức độ | **Cao** - vi phạm đúng điều kiện hoàn thành: `docker compose down` rồi `up` phải giữ dữ liệu **và** chạy lại được. Thực tế dữ liệu còn nhưng cả stack đứng: `db-init` exit 1, `web` bị chặn bởi `depends_on` nên không lên. |
| Phát hiện ở | D07 (down/up) và D18 (chạy lại từ đầu theo README) |
| Hiện tượng | Lần `up` đầu tiên sau `down`: log `db-init` có `Skipping 01-03...`, rồi `Msg 904, Level 16, State 3, Line 12` `Database 5 cannot be autostarted during server shutdown or startup.`; `docker compose up -d` in `service "db-init" didn't complete successfully: exit 1`; `web` ở trạng thái `Created`, cổng 8080 không mở. `sqlcmd` đọc `CUSTOMER` vẫn thấy 10 dòng nên dữ liệu **không** mất. |
| Nguyên nhân | Healthcheck của `db` chỉ chạy `SELECT 1` trên `master`, nên nó báo healthy ngay khi SQL Server vừa nhận kết nối, trong khi các CSDL người dùng còn đang được autostart. `04_CreateAppUser.sql` mở đầu bằng `USE CustomerCareDB;` (dòng 12) nên gặp 904. Đã đo trực tiếp: sau `docker compose restart db`, câu `USE CustomerCareDB; SELECT 1` trả 904 ở giây 0 và thành công từ giây thứ 2 - tức là một cuộc đua vài giây, không phải sai mật khẩu hay thiếu quyền. |
| Cách sửa | `database/init.sh`: khi CSDL đã tồn tại, chờ tới khi mở được CSDL thật (`USE CustomerCareDB; SELECT 1`) tối đa 60 lần x 2 giây, in `CustomerCareDB is ready (1)`; và chạy file 04 trong vòng lặp thử lại (30 lần x 2 giây) vì cửa sổ 904 có thể vẫn còn sau khi probe vừa thành công. Hết lượt thì in `ERROR: could not create the app_ccm login` và `exit 1` - vẫn thất bại rõ ràng, không im lặng. Không đổi `01`-`04`, không đổi compose, không đổi ứng dụng. |
| Phạm vi ảnh hưởng | Chỉ đường chạy lại có CSDL sẵn (`db-init`). Lần chạy đầu (CSDL chưa có) không bị ảnh hưởng; đã chạy lại cả hai đường. |
| Ca chạy lại | D07 (down/up: dữ liệu + phiên đăng nhập còn, `db-init` báo `Skipping 01-03`, `CustomerCareDB is ready (1)`, `OK: app_ccm user is ready`), D08 (`down -v`/`up`: chạy `01`, nạp dữ liệu mẫu), D18 (`up -d --build` từ đầu theo README). |
| Ghi chú | Lỗi này có sẵn trong khung Docker từ M01, chưa từng chạy thật nên chưa ai thấy. `ccm-docker-deploy` đã thêm mục chẩn đoán 904. |
