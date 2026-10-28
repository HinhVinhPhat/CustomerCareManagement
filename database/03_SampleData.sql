/* =====================================================================
   File 3/4: Sample data
   Run AFTER 01_CreateTables.sql and 02_Function_View_Proc_Trigger.sql.
   - Contract TotalValue and customer Tier are NOT written by hand: the triggers compute
     them from CONTRACT_DETAIL.
   - Every date is relative to "today" (a negative number of days = past, positive = future),
     so whenever you run it the dashboard has data: expiring contracts, care appointments
     that are overdue / today / upcoming, overdue feedback... Run the three files again to
     refresh the timeline.
   ===================================================================== */
USE CustomerCareDB;
GO
SET NOCOUNT ON;
-- Record the audit log as employee 1 while loading (the loading log is cleared at the end of this file)
EXEC sp_set_session_context @key = N'EmployeeID', @value = 1;
GO

-- 1. DEPARTMENT
INSERT INTO DEPARTMENT (DepartmentName) VALUES
(N'Phòng Kinh doanh'),
(N'Phòng Chăm sóc khách hàng'),
(N'Phòng Kỹ thuật'),
(N'Phòng Marketing');
GO

-- 2. EMPLOYEE
INSERT INTO EMPLOYEE (FullName, Email, Phone, Gender, Position, DepartmentID) VALUES
(N'Nguyễn Văn An',    'john.smith@customercare.com',    '0901111111', N'Male',   N'Manager', 1),
(N'Trần Thị Mai',     'mary.johnson@customercare.com',  '0902222222', N'Female', N'Staff',   1),
(N'Lê Minh Đức',      'david.brown@customercare.com',   '0903333333', N'Male',   N'Staff',   2),
(N'Phạm Thu Hà',      'susan.davis@customercare.com',   '0904444444', N'Female', N'Manager', 2),
(N'Hoàng Văn Phúc',   'peter.wilson@customercare.com',  '0905555555', N'Male',   N'Staff',   3),
(N'Vũ Thị Lan',       'laura.moore@customercare.com',   '0906666666', N'Female', N'Staff',   4),
(N'Đặng Quốc Khánh',  'kevin.taylor@customercare.com',  '0907777777', N'Male',   N'Staff',   1),
(N'Bùi Thị Hồng',     'helen.anderson@customercare.com','0908888888', N'Female', N'Staff',   2);
GO

-- 3. ACCOUNT (demo password: 123456, SHA2-256 without salt for demo purposes only.
--    A real web app should use bcrypt / ASP.NET Identity and re-hash on first login)
INSERT INTO ACCOUNT (Username, PasswordHash, Role, EmployeeID) VALUES
(N'john',   CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'123456'), 2), N'Manager', 1),
(N'mary',   CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'123456'), 2), N'Sales',   2),
(N'kevin',  CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'123456'), 2), N'Sales',   7),
(N'david',  CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'123456'), 2), N'Support', 3),
(N'susan',  CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'123456'), 2), N'Manager', 4),
(N'helen',  CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'123456'), 2), N'Support', 8);
GO

-- 4. APPLICATION SETTINGS
--    EmployeeID = NULL is the system-wide default. A personal row (EmployeeID = that employee) is
--    created by the application when somebody picks a different language or theme, and it wins over
--    the default. The values are guarded by CHK_Settings_Value, so only 'vi'/'en', 'light'/'dark'
--    and a whole number 1..100 for PageSize can be stored.
--    vw_EffectiveSettings (file 02) reads the fallback rule.
INSERT INTO SETTINGS (EmployeeID, SettingKey, SettingValue, DataType, Category, Description) VALUES
(NULL, N'Language', N'vi',    'String', 'Localization', N'Ngôn ngữ hiển thị mặc định (vi / en)'),
(NULL, N'Theme',    N'light', 'String', 'Appearance',   N'Chế độ hiển thị mặc định (light / dark)'),
(NULL, N'PageSize', N'20',    'Int',    'General',      N'Số dòng mỗi trang của các danh sách (1..100)');
GO

-- 5. PRODUCT / SERVICE
INSERT INTO PRODUCT_SERVICE (ProductName, ProductType, UnitPrice, Description) VALUES
(N'Phần mềm quản lý cơ bản',  N'Product', 5000000,  N'Phần mềm quản lý khách hàng cơ bản'),
(N'Phần mềm quản lý nâng cao', N'Product', 15000000, N'Phần mềm quản lý khách hàng nâng cao'),
(N'Gói tư vấn triển khai',     N'Service',  8000000, N'Dịch vụ tư vấn và triển khai hệ thống'),
(N'Gói bảo trì hằng năm',      N'Service',  3000000, N'Dịch vụ bảo trì và hỗ trợ kỹ thuật'),
(N'Phần mềm ERP',              N'Product', 50000000, N'Hệ thống hoạch định nguồn lực doanh nghiệp'),
(N'Gói đào tạo nhân viên',     N'Service',  2000000, N'Đào tạo sử dụng phần mềm cho nhân viên'),
(N'Phần mềm quản lý nhân sự',  N'Product', 20000000, N'Phần mềm quản lý nguồn nhân lực'),
(N'Gói hỗ trợ 24/7',           N'Service',  5000000, N'Dịch vụ hỗ trợ kỹ thuật 24/7');
GO

/* ---- Tables with dates: use the anchor @today = 00:00 today and add/subtract days ---- */
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @today DATETIME = CAST(CAST(GETDATE() AS DATE) AS DATETIME);

-- 6. CUSTOMER (Tier keeps its default; the trigger ranks it once contracts exist)
INSERT INTO CUSTOMER (CustomerName, CustomerType, Email, Phone, Gender, DateOfBirth, Address, Source, CreatedDate, OwnerEmployeeID)
SELECT v.CustomerName, v.CustomerType, v.Email, v.Phone, v.Gender, CAST(v.DateOfBirth AS DATE), v.Address, v.Source,
       DATEADD(DAY, v.CreatedOffset, @today), v.OwnerEmployeeID
FROM (VALUES
 (1,  N'Công ty TNHH ABC',  N'Business',   N'abc@company.com',        N'0281111111', NULL,     NULL,         N'12 Lê Lợi, Quận 1, TP.HCM',                  N'Website',    -600, 1),
 (2,  N'Công ty Cổ phần XYZ', N'Business', N'xyz@company.com',        N'0282222222', NULL,     NULL,         N'34 Nguyễn Huệ, Quận 1, TP.HCM',              N'Giới thiệu', -570, 2),
 (3,  N'Nguyễn Thị Thu',    N'Individual', N'emily.clark@mail.com',   N'0911111111', N'Female', '1990-05-12', N'56 Trần Hưng Đạo, Quận 5, TP.HCM',           N'Facebook',   -540, 2),
 (4,  N'Trần Quốc Bảo',     N'Individual', N'daniel.harris@mail.com', N'0922222222', N'Male',   '1985-08-20', N'78 Điện Biên Phủ, Quận 3, TP.HCM',           N'Điện thoại', -500, 7),
 (5,  N'Công ty TNHH DEF',  N'Business',   N'def@company.com',        N'0283333333', NULL,     NULL,         N'90 Võ Văn Tần, Quận 3, TP.HCM',              N'Sự kiện',    -590, 7),
 (6,  N'Lê Ngọc Diệp',      N'Individual', N'sophia.lewis@mail.com',  N'0933333333', N'Female', '1992-11-30', N'102 Cách Mạng Tháng 8, Quận 10, TP.HCM',     N'Website',    -420, 2),
 (7,  N'Công ty Cổ phần GHI', N'Business', N'ghi@company.com',        N'0284444444', NULL,     NULL,         N'14 Pasteur, Quận 1, TP.HCM',                 N'Giới thiệu', -580, 1),
 (8,  N'Phạm Minh Tuấn',    N'Individual', N'matthew.walker@mail.com', N'0944444444', N'Male',   '1988-03-07', N'26 Nguyễn Trãi, Quận 5, TP.HCM',             N'Sự kiện',    -450, 2),
 (9,  N'Công ty TNHH JKL',  N'Business',   N'jkl@company.com',        N'0285555555', NULL,     NULL,         N'38 Lê Lợi, Quận 1, TP.HCM',                  N'Facebook',   -300, 7),
 (10, N'Đỗ Khánh Linh',     N'Individual', N'olivia.young@mail.com',  N'0955555555', N'Female', '1995-07-15', N'50 Bà Huyện Thanh Quan, Quận 3, TP.HCM',     N'Website',    -200, 2)
) v(Seq, CustomerName, CustomerType, Email, Phone, Gender, DateOfBirth, Address, Source, CreatedOffset, OwnerEmployeeID)
ORDER BY v.Seq;

-- 7. CONTRACT (TotalValue starts at 0, the trigger computes it from the details;
--    SignOffset/EndOffset are days relative to today)
--    The status matches the dates: EndOffset < 0 -> "Expired". Contracts 3 and 10 are always
--    "expiring soon" (15 / 25 days left).
INSERT INTO CONTRACT (CustomerID, EmployeeID, SignDate, EndDate, Status, Note)
SELECT v.CustomerID, v.EmployeeID, DATEADD(DAY, v.SignOffset, @today), DATEADD(DAY, v.EndOffset, @today), v.Status, v.Note
FROM (VALUES
 (1,  1, 1,  -570, -205, N'Expired', N'Hợp đồng phần mềm cơ bản + tư vấn + đào tạo'),
 (2,  1, 7,  -400,  -35, N'Expired', N'Hợp đồng phần mềm nâng cao'),
 (3,  1, 1,  -330,   15, N'Active',  N'Hợp đồng ERP - sắp hết hạn'),
 (4,  2, 2,  -550,  180, N'Active',  N'Hợp đồng phần mềm nâng cao'),
 (5,  4, 7,  -420,  200, N'Active',  N'Hợp đồng phần mềm cơ bản'),
 (6,  3, 2,  -500,  250, N'Active',  N'Gói tư vấn triển khai'),
 (7,  5, 7,  -540, -180, N'Expired', N'Hợp đồng ERP'),
 (8,  6, 2,     7,  372, N'Pending', N'Chờ ký kết (dự kiến sau 7 ngày)'),
 (9,  8, 2,  -300,  400, N'Active',  N'Hợp đồng HRM'),
 (10, 7, 1,  -340,   25, N'Active',  N'Hợp đồng ERP gia hạn - sắp hết hạn'),
 (11, 7, 7,  -560, -380, N'Expired', N'Hợp đồng ERP + bảo trì + hỗ trợ'),
 (12, 5, 1,  -250,  480, N'Active',  N'Hợp đồng ERP mở rộng'),
 (13, 2, 2,  -520,  210, N'Active',  N'Hợp đồng phần mềm nâng cao + đào tạo + hỗ trợ'),
 (14, 3, 7,  -200,  520, N'Active',  N'Tư vấn + bảo trì + đào tạo'),
 (15, 9, 7,   -60,  305, N'Active',  N'Hợp đồng phần mềm cơ bản + bảo trì + đào tạo'),
 (16, 10, 2,  -20,  345, N'Active',  N'Tư vấn + hỗ trợ 24/7')
) v(Seq, CustomerID, EmployeeID, SignOffset, EndOffset, Status, Note)
ORDER BY v.Seq;

-- 8. CONTRACT DETAIL  -> the trigger adds up TotalValue and ranks the customer Tier
INSERT INTO CONTRACT_DETAIL (ContractID, ProductID, Quantity, UnitPrice) VALUES
(1, 1, 1, 5000000), (1, 3, 1, 8000000), (1, 6, 5, 2000000),                          -- 23m
(2, 2, 1, 15000000), (2, 4, 1, 3000000), (2, 8, 1, 5000000), (2, 6, 1, 2000000),      -- 25m
(3, 5, 1, 50000000), (3, 3, 1, 8000000), (3, 6, 1, 2000000), (3, 8, 4, 5000000),      -- 80m
(4, 2, 1, 15000000), (4, 4, 1, 3000000),                                              -- 18m
(5, 1, 1, 5000000),                                                                   -- 5m
(6, 3, 1, 8000000),                                                                   -- 8m
(7, 5, 1, 50000000), (7, 6, 1, 2000000), (7, 4, 1, 3000000),                          -- 55m
(8, 3, 1, 8000000), (8, 6, 1, 2000000),                                               -- 10m
(9, 7, 1, 20000000),                                                                  -- 20m
(10, 5, 1, 50000000), (10, 8, 2, 5000000),                                            -- 60m
(11, 5, 1, 50000000), (11, 4, 1, 3000000), (11, 8, 3, 5000000),                       -- 68m
(12, 5, 1, 45000000),                                                                 -- 45m (discounted)
(13, 2, 1, 15000000), (13, 6, 1, 2000000), (13, 8, 1, 5000000), (13, 3, 1, 8000000), -- 30m
(14, 3, 1, 8000000), (14, 4, 1, 3000000), (14, 6, 1, 1000000),                        -- 12m (training discounted)
(15, 1, 1, 5000000), (15, 6, 2, 2000000), (15, 4, 1, 3000000),                        -- 12m
(16, 3, 1, 8000000), (16, 8, 1, 5000000);                                             -- 13m
-- Expected Tier: C1 128m Diamond | C2 48m Silver | C3 20m Silver | C4 5m Standard
-- C5 100m Diamond | C6 0 (pending contract) Standard | C7 128m Diamond | C8 20m Silver | C9 12m, C10 13m Standard

-- 9. CONTACT HISTORY (DayOffset = days relative to today, Hour = hour of the day)
INSERT INTO CONTACT (CustomerID, EmployeeID, ContactTime, Channel, Content, Result)
SELECT v.CustomerID, v.EmployeeID, DATEADD(HOUR, v.Hour, DATEADD(DAY, v.DayOffset, @today)), v.Channel, v.Content, v.Result
FROM (VALUES
 (1,  1, -600,  9, N'Phone',    N'Tư vấn gói phần mềm cơ bản',              N'Khách hàng quan tâm'),
 (1,  1, -598, 10, N'Email',    N'Gửi báo giá phần mềm',                    N'Đã gửi báo giá'),
 (2,  2, -570, 14, N'InPerson', N'Demo phần mềm quản lý nâng cao',          N'Khách hàng đồng ý ký hợp đồng'),
 (3,  3, -540,  9, N'Zalo',     N'Hỏi về gói tư vấn triển khai',            N'Đã trao đổi chi tiết'),
 (5,  2, -590, 11, N'Phone',    N'Tư vấn hệ thống ERP',                     N'Đã hẹn demo'),
 (5,  2, -587, 14, N'InPerson', N'Demo ERP tại văn phòng khách hàng',       N'Khách hàng đồng ý triển khai'),
 (7,  1, -580,  9, N'Email',    N'Giới thiệu gói ERP',                      N'Khách hàng phản hồi tích cực'),
 (4,  7, -500, 10, N'Phone',    N'Tư vấn phần mềm quản lý cơ bản',          N'Khách hàng đồng ý'),
 (8,  2, -450, 15, N'InPerson', N'Demo phần mềm quản lý nhân sự',           N'Khách hàng cần thêm thời gian cân nhắc'),
 (6,  3, -420,  9, N'Zalo',     N'Hỏi về gói tư vấn triển khai',            N'Đang chờ duyệt ngân sách'),
 (9,  3, -290,  9, N'Email',    N'Giới thiệu sản phẩm phần mềm',            N'Khách hàng chưa có nhu cầu'),
 (10, 4, -190, 10, N'Phone',    N'Tư vấn gói phần mềm phù hợp',             N'Khách hàng đang cân nhắc'),
 (9,  7,  -65, 10, N'Phone',    N'Giới thiệu chương trình khuyến mãi',      N'Khách hàng đồng ý mua gói cơ bản'),
 (1,  3,  -60, 10, N'Phone',    N'Nhắc gia hạn hợp đồng',                   N'Khách hàng đồng ý gia hạn'),
 (7,  1,  -30, 14, N'InPerson', N'Tư vấn gia hạn hợp đồng ERP',             N'Khách hàng cần thêm thời gian'),
 (2,  3,   -2, 10, N'Phone',    N'Hỏi thăm sau khảo sát',                   N'Khách hàng hài lòng'),
 (6,  2,   -1, 15, N'Zalo',     N'Nhắc ký hợp đồng',                        N'Khách hàng sẽ ký vào tuần sau')
) v(CustomerID, EmployeeID, DayOffset, Hour, Channel, Content, Result)
ORDER BY v.DayOffset, v.CustomerID;

-- 10. CUSTOMER CARE
--    EndOffset = days relative to today for the next appointment (NULL = none). vw_CareSchedule only
--    takes each customer's MOST RECENT care record:
--    C7 appointment today | C1 upcoming (+2) | C2 upcoming (+3) | C5 overdue (-3)
INSERT INTO CARE (CustomerID, EmployeeID, CareDate, CareType, Content, Result, SatisfactionScore, NextAppointmentDate)
SELECT v.CustomerID, v.EmployeeID, DATEADD(DAY, v.CareOffset, @today), v.CareType, v.Content, v.Result, v.Score, DATEADD(DAY, v.EndOffset, @today)
FROM (VALUES
 (1, 3, -300, N'Survey',          N'Khảo sát mức độ hài lòng sau 5 tháng',        N'Khách hàng hài lòng',      5,    -270),
 (2, 3, -280, N'Upsell',          N'Đề xuất nâng cấp lên gói ERP',                N'Khách hàng đang cân nhắc', NULL, -250),
 (5, 4, -260, N'Upsell',          N'Đề xuất mở rộng các phân hệ ERP',             N'Khách hàng đồng ý mở rộng', NULL, -240),
 (7, 3, -200, N'Renewal',         N'Tư vấn gia hạn hợp đồng ERP',                 N'Đang thương lượng giá',    NULL,    0),
 (3, 4, -150, N'Birthday',        N'Gửi lời chúc sinh nhật và phiếu quà tặng',    N'Khách hàng cảm ơn',        NULL, NULL),
 (1, 3, -120, N'Upsell',          N'Tư vấn nâng cấp lên gói ERP',                 N'Khách hàng quan tâm',      NULL, -100),
 (8, 4, -100, N'Survey',          N'Khảo sát sau 4 tháng sử dụng phần mềm HRM',   N'Khách hàng hài lòng',      4,    NULL),
 (4, 3,  -60, N'Upsell',          N'Giới thiệu gói hỗ trợ 24/7',                  N'Khách hàng từ chối',       NULL, NULL),
 (1, 4,  -40, N'Survey',          N'Khảo sát cuối kỳ',                            N'Khách hàng rất hài lòng',  5,    NULL),
 (2, 3,  -20, N'Survey',          N'Khảo sát chất lượng phần mềm nâng cao',        N'Khách hàng hài lòng',      4,      3),
 (5, 4,   -8, N'Renewal',         N'Đề xuất gia hạn hợp đồng ERP đã hết hạn',     N'Khách hàng đang cân nhắc', NULL,   -3),
 (1, 3,   -5, N'RenewalReminder', N'Nhắc gia hạn hợp đồng ERP sắp hết hạn',       N'Khách hàng sẽ phản hồi',   NULL,    2)
) v(CustomerID, EmployeeID, CareOffset, CareType, Content, Result, Score, EndOffset)
ORDER BY v.CareOffset, v.CustomerID;

-- 11. FEEDBACK / COMPLAINTS  (DueOffset / ResolvedOffset = days relative to today)
INSERT INTO FEEDBACK (CustomerID, EmployeeID, CreatedTime, FeedbackType, Content, Priority, FeedbackStatus, DueDate, ResolvedDate, Note)
SELECT v.CustomerID, v.EmployeeID, DATEADD(HOUR, v.Hour, DATEADD(DAY, v.CreatedOffset, @today)), v.FeedbackType, v.Content, v.Priority, v.FeedbackStatus,
       DATEADD(DAY, v.DueOffset, @today), DATEADD(DAY, v.ResolvedOffset, @today), v.Note
FROM (VALUES
 (1, 5,    -320, 10, N'Complaint',       N'Lỗi phần mềm: không đăng nhập được',           1, N'Resolved',   -319, -320, N'Đã sửa trong ngày'),
 (2, 5,    -270, 14, N'GeneralFeedback', N'Giao diện nên thân thiện hơn',                 3, N'InProgress', -263, NULL, N'Đã chuyển cho nhóm phát triển (quá hạn)'),
 (5, 5,    -250,  9, N'Complaint',       N'Phân hệ báo cáo ERP xuất sai số liệu',         1, N'Resolved',   -249, -248, N'Đã vá lỗi'),
 (7, 5,    -200, 15, N'Complaint',       N'Mất dữ liệu ERP sau khi cập nhật',             1, N'Resolved',   -199, -199, N'Đã phục hồi từ bản sao lưu'),
 (3, 3,    -180, 11, N'Suggestion',      N'Đề xuất thêm tính năng nhắc tự động',          2, N'InProgress', -173, NULL, N'Đã đưa vào kế hoạch (quá hạn)'),
 (4, 3,    -100, 10, N'Compliment',      N'Nhân viên hỗ trợ rất nhiệt tình',              3, N'Closed',      -93, -100, N'Đã ghi nhận lời khen'),
 (8, 3,     -20, 14, N'GeneralFeedback', N'Phần mềm HRM chạy chậm trên máy cấu hình thấp', 2, N'InProgress',    5, NULL, N'Đang phân tích'),
 (1, NULL,   -3,  9, N'Suggestion',      N'Đề xuất tích hợp với Zalo OA',                 2, N'New',          4, NULL, N'Chưa phân công'),
 (2, NULL,   -1, 16, N'Complaint',       N'Chưa nhận được hóa đơn điện tử',               1, N'New',          1, NULL, N'Chưa phân công')
) v(CustomerID, EmployeeID, CreatedOffset, Hour, FeedbackType, Content, Priority, FeedbackStatus, DueOffset, ResolvedOffset, Note)
ORDER BY v.CreatedOffset, v.CustomerID;

COMMIT TRANSACTION;
GO

-- Clear the audit log produced while loading the sample data so AUDIT_LOG starts empty
DELETE FROM AUDIT_LOG;
DBCC CHECKIDENT ('AUDIT_LOG', RESEED, 0) WITH NO_INFOMSGS;
EXEC sp_set_session_context @key = N'EmployeeID', @value = NULL;
GO

-- Consistency check: TotalValue = SUM(details) and Tier follows the ranking rule
IF EXISTS (SELECT 1 FROM CONTRACT h
           WHERE h.TotalValue <> ISNULL((SELECT SUM(c.Quantity * c.UnitPrice) FROM CONTRACT_DETAIL c WHERE c.ContractID = h.ContractID), 0))
   OR EXISTS (SELECT 1 FROM CUSTOMER k
              WHERE k.Tier <> dbo.fn_CustomerTier(dbo.fn_CustomerContractTotal(k.CustomerID)))
    PRINT N'WARNING: TotalValue or Tier does not match. Re-check the file run order and the nested-triggers option.';
ELSE
    PRINT N'OK: sample data loaded, TotalValue and Tier match the triggers.';
GO

-- Quick look at the resulting ranking
SELECT CustomerID, CustomerName, Tier, dbo.fn_CustomerContractTotal(CustomerID) AS TotalValue FROM CUSTOMER ORDER BY CustomerID;
GO
