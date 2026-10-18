/* =====================================================================
   CUSTOMER CARE MANAGEMENT SYSTEM  -  VERSION 2
   File 1/4: Create database + tables + indexes
   RUN ORDER: 01_CreateTables -> 02_Function_View_Proc_Trigger -> 03_SampleData -> 04_CreateAppUser
   (logic is created before data so that loading the sample data fires the triggers that
    compute TotalValue and Tier)
   Safe to run repeatedly (drops the old database first).
   Note: save this file as UTF-8 with BOM; if you use sqlcmd add -f 65001.
   ===================================================================== */
USE master;
GO
IF DB_ID(N'CustomerCareDB') IS NOT NULL
BEGIN
    ALTER DATABASE CustomerCareDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE CustomerCareDB;
END
GO
CREATE DATABASE CustomerCareDB;
GO
USE CustomerCareDB;
GO

-- 1. DEPARTMENT
CREATE TABLE DEPARTMENT (
    DepartmentID   INT IDENTITY(1,1) PRIMARY KEY,
    DepartmentName  NVARCHAR(100) NOT NULL UNIQUE
);
GO

-- 2. EMPLOYEE
CREATE TABLE EMPLOYEE (
    EmployeeID        INT IDENTITY(1,1) PRIMARY KEY,
    FullName     NVARCHAR(100) NOT NULL,
    Email     NVARCHAR(100) NOT NULL UNIQUE,
    Phone        NVARCHAR(15) NOT NULL UNIQUE,
    Gender    NVARCHAR(10) NOT NULL,
    Position      NVARCHAR(50) NOT NULL,
    DepartmentID  INT NOT NULL,
    CONSTRAINT FK_Employee_Department FOREIGN KEY (DepartmentID) REFERENCES DEPARTMENT(DepartmentID),
    CONSTRAINT CHK_Employee_Gender CHECK (Gender IN (N'Male', N'Female', N'Other')),
    CONSTRAINT CHK_Employee_Position   CHECK (Position IN (N'Manager', N'Staff')),
    CONSTRAINT CHK_Employee_Email    CHECK (Email LIKE N'_%@_%._%'),
    CONSTRAINT CHK_Employee_Phone CHECK (Phone NOT LIKE N'%[^0-9]%' AND LEN(Phone) BETWEEN 9 AND 11)
);
GO

-- 3. ACCOUNT (web login; each employee has at most one account)
CREATE TABLE ACCOUNT (
    AccountID          INT IDENTITY(1,1) PRIMARY KEY,
    Username   NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash   VARCHAR(255) NOT NULL,       -- only the hash is stored (bcrypt/Identity ~60-100 chars), never the raw password
    Role        NVARCHAR(30) NOT NULL,       -- Manager | Sales | Support
    EmployeeID          INT NOT NULL UNIQUE,
    IsActive      BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Account_Employee FOREIGN KEY (EmployeeID) REFERENCES EMPLOYEE(EmployeeID),
    CONSTRAINT CHK_Account_Role CHECK (Role IN (N'Manager', N'Sales', N'Support'))
);
GO

-- 4. CUSTOMER
CREATE TABLE CUSTOMER (
    CustomerID          INT IDENTITY(1,1) PRIMARY KEY,
    CustomerName       NVARCHAR(100) NOT NULL,          -- personal name or company name
    CustomerType        NVARCHAR(20)  NOT NULL DEFAULT N'Individual',
    Email       NVARCHAR(100),                   -- conditional unique, see index UX_Customer_Email below
    Phone          NVARCHAR(15),
    Gender      NVARCHAR(10),
    DateOfBirth      DATE,
    Address        NVARCHAR(255),
    Source       NVARCHAR(50),                    -- Website | Referral | Event | Facebook | Phone
    CreatedDate       DATE NOT NULL DEFAULT GETDATE(),
    Tier        NVARCHAR(50) NOT NULL DEFAULT N'Standard',   -- set by trigger, DO NOT edit manually
    OwnerEmployeeID  INT NULL,                        -- owning employee (powers the "my customers" screen)
    IsActive      BIT NOT NULL DEFAULT 1,          -- soft delete: 0 = no longer cared for
    CONSTRAINT FK_Customer_Owner FOREIGN KEY (OwnerEmployeeID) REFERENCES EMPLOYEE(EmployeeID),
    CONSTRAINT CHK_Customer_Type   CHECK (CustomerType IN (N'Individual', N'Business')),
    CONSTRAINT CHK_Customer_Gender CHECK (Gender IN (N'Male', N'Female', N'Other')),
    CONSTRAINT CHK_Customer_Tier   CHECK (Tier IN (N'Standard', N'Silver', N'Gold', N'Diamond')),
    CONSTRAINT CHK_Customer_Phone     CHECK (Phone IS NULL OR (Phone NOT LIKE N'%[^0-9]%' AND LEN(Phone) BETWEEN 9 AND 11)),
    CONSTRAINT CHK_Customer_Email    CHECK (Email IS NULL OR Email LIKE N'_%@_%._%'),
    CONSTRAINT CHK_Customer_DateOfBirth CHECK (DateOfBirth IS NULL OR DateOfBirth < CreatedDate),
    CONSTRAINT CHK_Customer_Contact  CHECK (Email IS NOT NULL OR Phone IS NOT NULL),   -- must have at least one contact method
    CONSTRAINT CHK_Customer_Business CHECK (CustomerType = N'Individual' OR (Gender IS NULL AND DateOfBirth IS NULL))  -- businesses have no gender/date of birth
);
GO

-- 5. PRODUCT / SERVICE
CREATE TABLE PRODUCT_SERVICE (
    ProductID      INT IDENTITY(1,1) PRIMARY KEY,
    ProductName     NVARCHAR(150) NOT NULL UNIQUE,
    ProductType    NVARCHAR(50)  NOT NULL,
    UnitPrice  DECIMAL(18,2) NOT NULL DEFAULT 0,
    Description      NVARCHAR(500),
    CONSTRAINT CHK_Product_Type   CHECK (ProductType IN (N'Product', N'Service')),
    CONSTRAINT CHK_Product_UnitPrice CHECK (UnitPrice >= 0)
);
GO

-- 6. CONTRACT
CREATE TABLE CONTRACT (
    ContractID         INT IDENTITY(1,1) PRIMARY KEY,
    CustomerID         INT NOT NULL,
    EmployeeID         INT NOT NULL,
    SignDate       DATE NOT NULL DEFAULT GETDATE(),
    EndDate   DATE NOT NULL,
    TotalValue       DECIMAL(18,2) NOT NULL DEFAULT 0,   -- auto-updated by trigger from CONTRACT_DETAIL
    Status  NVARCHAR(30) NOT NULL DEFAULT N'Active',
    Note       NVARCHAR(500),
    CONSTRAINT FK_Contract_Customer FOREIGN KEY (CustomerID) REFERENCES CUSTOMER(CustomerID),
    CONSTRAINT FK_Contract_Employee  FOREIGN KEY (EmployeeID) REFERENCES EMPLOYEE(EmployeeID),
    CONSTRAINT CHK_Contract_Status CHECK (Status IN (N'Active', N'Expired', N'Cancelled', N'Pending')),
    CONSTRAINT CHK_Contract_TotalValue    CHECK (TotalValue >= 0),
    CONSTRAINT CHK_Contract_Dates      CHECK (EndDate >= SignDate)
);
GO

-- 7. CONTRACT DETAIL
CREATE TABLE CONTRACT_DETAIL (
    ContractID      INT NOT NULL,
    ProductID      INT NOT NULL,
    Quantity   INT NOT NULL DEFAULT 1,
    UnitPrice  DECIMAL(18,2) NOT NULL,          -- price at signing time
    CONSTRAINT PK_ContractDetail PRIMARY KEY (ContractID, ProductID),
    CONSTRAINT FK_ContractDetail_Contract FOREIGN KEY (ContractID) REFERENCES CONTRACT(ContractID) ON DELETE CASCADE,
    CONSTRAINT FK_ContractDetail_Product FOREIGN KEY (ProductID) REFERENCES PRODUCT_SERVICE(ProductID),
    CONSTRAINT CHK_ContractDetail_Quantity CHECK (Quantity > 0),
    CONSTRAINT CHK_ContractDetail_UnitPrice  CHECK (UnitPrice >= 0)
);
GO

-- 8. CONTACT HISTORY
CREATE TABLE CONTACT (
    ContactID        INT IDENTITY(1,1) PRIMARY KEY,
    CustomerID        INT NOT NULL,
    EmployeeID        INT NOT NULL,
    ContactTime  DATETIME NOT NULL DEFAULT GETDATE(),
    Channel    NVARCHAR(50) NOT NULL,
    Content   NVARCHAR(1000),
    Result    NVARCHAR(200),
    CONSTRAINT FK_Contact_Customer FOREIGN KEY (CustomerID) REFERENCES CUSTOMER(CustomerID),
    CONSTRAINT FK_Contact_Employee  FOREIGN KEY (EmployeeID) REFERENCES EMPLOYEE(EmployeeID),
    CONSTRAINT CHK_Contact_Channel CHECK (Channel IN (N'Phone', N'Email', N'Zalo', N'InPerson', N'Other'))
);
GO

-- 9. CUSTOMER CARE
CREATE TABLE CARE (
    CareID         INT IDENTITY(1,1) PRIMARY KEY,
    CustomerID         INT NOT NULL,
    EmployeeID         INT NOT NULL,
    CareDate       DATE NOT NULL DEFAULT GETDATE(),
    CareType       NVARCHAR(80) NOT NULL,
    Content    NVARCHAR(1000),
    Result     NVARCHAR(200),
    SatisfactionScore  TINYINT NULL,                 -- 1-5, only for the "Survey" type
    NextAppointmentDate  DATE,
    CONSTRAINT FK_Care_Customer FOREIGN KEY (CustomerID) REFERENCES CUSTOMER(CustomerID),
    CONSTRAINT FK_Care_Employee  FOREIGN KEY (EmployeeID) REFERENCES EMPLOYEE(EmployeeID),
    CONSTRAINT CHK_Care_Type  CHECK (CareType IN (N'Renewal', N'Upsell', N'Birthday', N'Survey', N'RenewalReminder', N'Other')),
    CONSTRAINT CHK_Care_SatisfactionScore  CHECK (SatisfactionScore IS NULL OR (SatisfactionScore BETWEEN 1 AND 5 AND CareType = N'Survey')),
    CONSTRAINT CHK_Care_NextAppointment   CHECK (NextAppointmentDate IS NULL OR NextAppointmentDate >= CareDate)
);
GO

-- 10. FEEDBACK / COMPLAINTS
CREATE TABLE FEEDBACK (
    FeedbackID         INT IDENTITY(1,1) PRIMARY KEY,
    CustomerID         INT NOT NULL,
    EmployeeID         INT NULL,                     -- NULL = not assigned yet
    CreatedTime   DATETIME NOT NULL DEFAULT GETDATE(),
    FeedbackType       NVARCHAR(50) NOT NULL,
    Content    NVARCHAR(2000),
    Priority  TINYINT NOT NULL DEFAULT 2,   -- 1: High | 2: Medium | 3: Low
    FeedbackStatus  NVARCHAR(50) NOT NULL DEFAULT N'New',
    DueDate      DATE NULL,
    ResolvedDate     DATE NULL,                    -- trigger fills this when "Resolved"/"Closed"
    Note       NVARCHAR(500),
    CONSTRAINT FK_Feedback_Customer FOREIGN KEY (CustomerID) REFERENCES CUSTOMER(CustomerID),
    CONSTRAINT FK_Feedback_Employee  FOREIGN KEY (EmployeeID) REFERENCES EMPLOYEE(EmployeeID),
    CONSTRAINT CHK_Feedback_Type      CHECK (FeedbackType IN (N'Complaint', N'Suggestion', N'GeneralFeedback', N'Compliment')),
    CONSTRAINT CHK_Feedback_Priority    CHECK (Priority IN (1, 2, 3)),
    CONSTRAINT CHK_Feedback_Status CHECK (FeedbackStatus IN (N'New', N'InProgress', N'Resolved', N'Closed')),
    CONSTRAINT CHK_Feedback_DueDate   CHECK (DueDate IS NULL OR DueDate >= CAST(CreatedTime AS DATE)),
    CONSTRAINT CHK_Feedback_ResolvedDate  CHECK (ResolvedDate IS NULL OR ResolvedDate >= CAST(CreatedTime AS DATE)),
    -- Moved from a trigger to a CHECK: only "New" may be left without an assignee
    CONSTRAINT CHK_Feedback_Assignee CHECK (FeedbackStatus = N'New' OR EmployeeID IS NOT NULL)
);
GO

-- 11. SYSTEM AUDIT LOG (who did what, when). The web app calls:
--     EXEC sp_set_session_context N'EmployeeID', <logged-in EmployeeID>;   -- right after opening the connection
CREATE TABLE AUDIT_LOG (
    LogID       BIGINT IDENTITY(1,1) PRIMARY KEY,
    LogTime   DATETIME2(0) NOT NULL DEFAULT SYSDATETIME(),
    EmployeeID       INT NULL,                       -- NULL = the acting user could not be determined
    TableName     NVARCHAR(50) NOT NULL,
    RecordID  INT NOT NULL,
    Action   NVARCHAR(30) NOT NULL,          -- Insert | StatusChange | Delete | Assign
    Detail    NVARCHAR(500) NULL,
    CONSTRAINT FK_AuditLog_Employee FOREIGN KEY (EmployeeID) REFERENCES EMPLOYEE(EmployeeID)
);
GO

-- 12. APPLICATION SETTINGS. One row per key and scope:
--       EmployeeID IS NULL  -> the system-wide default, changed by a Manager
--       EmployeeID = <id>   -> that employee's own choice, which wins over the default
--     The value a user really gets is read from vw_EffectiveSettings (file 02).
CREATE TABLE SETTINGS (
    SettingID      INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeID     INT NULL,
    SettingKey     NVARCHAR(50)  NOT NULL,
    SettingValue   NVARCHAR(100) NOT NULL,
    DataType       VARCHAR(20)   NOT NULL,
    Category       VARCHAR(30)   NOT NULL,
    Description    NVARCHAR(300) NULL,
    UpdatedDate    DATETIME      NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Settings_Employee FOREIGN KEY (EmployeeID) REFERENCES EMPLOYEE(EmployeeID),
    -- One default per key (SQL Server treats NULL as a value in a UNIQUE constraint) and
    -- one personal row per employee and key - the rule is enforced by the engine, not by the app
    CONSTRAINT UQ_Settings_Scope     UNIQUE (EmployeeID, SettingKey),
    CONSTRAINT CHK_Settings_Key      CHECK (SettingKey IN (N'Language', N'Theme')),
    CONSTRAINT CHK_Settings_DataType CHECK (DataType IN ('String', 'Int', 'Bool')),
    CONSTRAINT CHK_Settings_Category CHECK (Category IN ('Localization', 'Appearance', 'General')),
    -- The value must be legal for its key, so not even sqlcmd can store garbage here.
    -- Adding a key in a later module means extending this CHECK as well.
    CONSTRAINT CHK_Settings_Value    CHECK (
             (SettingKey = N'Language' AND SettingValue IN (N'vi', N'en'))
          OR (SettingKey = N'Theme'    AND SettingValue IN (N'light', N'dark')))
);
GO

-- Conditional UNIQUE: many customers without email are fine (a plain UNIQUE allows only one NULL row)
CREATE UNIQUE INDEX UX_Customer_Email ON CUSTOMER(Email) WHERE Email IS NOT NULL;
-- Each department has at most one manager
CREATE UNIQUE INDEX UX_Employee_DepartmentManager ON EMPLOYEE(DepartmentID) WHERE Position = N'Manager';

-- Indexes for foreign keys / frequently filtered or sorted columns
CREATE INDEX IDX_Employee_DepartmentID  ON EMPLOYEE(DepartmentID);
CREATE INDEX IDX_Customer_Owner ON CUSTOMER(OwnerEmployeeID);
CREATE INDEX IDX_ContractDetail_ProductID            ON CONTRACT_DETAIL(ProductID);
CREATE INDEX IDX_Contract_EmployeeID         ON CONTRACT(EmployeeID);
CREATE INDEX IDX_Contract_CustomerID_Status ON CONTRACT(CustomerID, Status) INCLUDE (TotalValue);   -- for fn_CustomerContractTotal
CREATE INDEX IDX_Contract_Status_EndDate ON CONTRACT(Status, EndDate);            -- for the expiring-contract view
CREATE INDEX IDX_Contract_SignDate       ON CONTRACT(SignDate);                                  -- for revenue reports
CREATE INDEX IDX_Contact_EmployeeID          ON CONTACT(EmployeeID);
CREATE INDEX IDX_Contact_CustomerID_ContactTime ON CONTACT(CustomerID, ContactTime);
CREATE INDEX IDX_Care_EmployeeID         ON CARE(EmployeeID);
CREATE INDEX IDX_Care_CustomerID_CareDate  ON CARE(CustomerID, CareDate);                           -- for vw_CareSchedule
CREATE INDEX IDX_Care_NextAppointmentDate  ON CARE(NextAppointmentDate);
CREATE INDEX IDX_Feedback_EmployeeID         ON FEEDBACK(EmployeeID);
CREATE INDEX IDX_Feedback_CustomerID         ON FEEDBACK(CustomerID);
CREATE INDEX IDX_Feedback_Status    ON FEEDBACK(FeedbackStatus);
CREATE INDEX IDX_AuditLog_Table_Record     ON AUDIT_LOG(TableName, RecordID);
GO
