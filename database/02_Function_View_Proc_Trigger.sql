/* =====================================================================
   File 2/4: Functions, Views, Stored Procedures, Triggers, Roles
   Run after 01_CreateTables.sql and BEFORE 03_SampleData.sql. Requires SQL Server 2016+.
   Customer tier rule (total contract value, excluding "Cancelled" and "Pending"):
        >= 100 million: Diamond | >= 50 million: Gold | >= 20 million: Silver | else: Standard

   Error codes used with THROW in this file:
     50001 Contract has no detail lines     50009 Duplicate product in the details
     50002 Feedback not found               50010 Product does not exist
     50003 Employee not found               50011 Invalid quantity
     50004 Feedback not assigned            50012 Invalid feedback status
     50005 Customer still has contracts     50013 Contract not found
     50006 Invalid customer                 50014 Contract status forbids the action
     50007 Invalid employee                 50015 Invalid date
     50008 Invalid detail JSON
   ===================================================================== */
USE CustomerCareDB;
GO

/* ====================== FUNCTIONS ====================== */

-- Total value of a customer's "effective" contracts
CREATE FUNCTION dbo.fn_CustomerContractTotal (@CustomerID INT)
RETURNS DECIMAL(18,2)
AS
BEGIN
    RETURN ISNULL((SELECT SUM(TotalValue)
                   FROM CONTRACT
                   WHERE CustomerID = @CustomerID
                     AND Status IN (N'Active', N'Expired')), 0);
END;
GO

-- Rank a customer by total value
CREATE FUNCTION dbo.fn_CustomerTier (@TotalValue DECIMAL(18,2))
RETURNS NVARCHAR(50)
AS
BEGIN
    RETURN CASE
        WHEN @TotalValue >= 100000000 THEN N'Diamond'
        WHEN @TotalValue >=  50000000 THEN N'Gold'
        WHEN @TotalValue >=  20000000 THEN N'Silver'
        ELSE N'Standard'
    END;
END;
GO

-- Age of an individual customer (NULL if there is no date of birth)
CREATE FUNCTION dbo.fn_CustomerAge (@CustomerID INT)
RETURNS INT
AS
BEGIN
    DECLARE @DateOfBirth DATE, @Age INT;
    SELECT @DateOfBirth = DateOfBirth FROM CUSTOMER WHERE CustomerID = @CustomerID;
    IF @DateOfBirth IS NULL RETURN NULL;
    SET @Age = DATEDIFF(YEAR, @DateOfBirth, GETDATE());
    IF DATEADD(YEAR, @Age, @DateOfBirth) > CAST(GETDATE() AS DATE) SET @Age -= 1;
    RETURN @Age;
END;
GO

-- Days remaining on a contract (negative = already overdue)
CREATE FUNCTION dbo.fn_ContractDaysRemaining (@ContractID INT)
RETURNS INT
AS
BEGIN
    DECLARE @EndDate DATE;
    SELECT @EndDate = EndDate FROM CONTRACT WHERE ContractID = @ContractID;
    IF @EndDate IS NULL RETURN NULL;
    RETURN DATEDIFF(DAY, CAST(GETDATE() AS DATE), @EndDate);
END;
GO

/* ====================== VIEWS ====================== */

-- Full contract information (contract list page)
CREATE VIEW dbo.vw_ContractInfo
AS
SELECT h.ContractID, h.CustomerID, k.CustomerName, h.EmployeeID, n.FullName, pb.DepartmentName,
       h.SignDate, h.EndDate,
       DATEDIFF(DAY, CAST(GETDATE() AS DATE), h.EndDate) AS DaysRemaining,
       h.TotalValue, h.Status, h.Note
FROM CONTRACT h
JOIN CUSTOMER k ON k.CustomerID = h.CustomerID
JOIN EMPLOYEE  n ON n.EmployeeID = h.EmployeeID
LEFT JOIN DEPARTMENT pb ON pb.DepartmentID = n.DepartmentID;
GO

-- One row per contract line (contract detail page)
CREATE VIEW dbo.vw_ContractDetail
AS
SELECT c.ContractID, c.ProductID, sp.ProductName, sp.ProductType, c.Quantity, c.UnitPrice,
       c.Quantity * c.UnitPrice AS LineTotal
FROM CONTRACT_DETAIL c
JOIN PRODUCT_SERVICE sp ON sp.ProductID = c.ProductID;
GO

-- Revenue per customer (dashboard + reports)
CREATE VIEW dbo.vw_CustomerRevenue
AS
SELECT k.CustomerID, k.CustomerName, k.CustomerType, k.Tier, k.OwnerEmployeeID, k.IsActive,
       COUNT(h.ContractID)             AS ContractCount,
       ISNULL(SUM(h.TotalValue), 0)  AS TotalValue
FROM CUSTOMER k
LEFT JOIN CONTRACT h ON h.CustomerID = k.CustomerID
                    AND h.Status IN (N'Active', N'Expired')
GROUP BY k.CustomerID, k.CustomerName, k.CustomerType, k.Tier, k.OwnerEmployeeID, k.IsActive;
GO

-- Active contracts expiring in the next 30 days
CREATE VIEW dbo.vw_ExpiringContract
AS
SELECT h.ContractID, h.CustomerID, k.CustomerName, k.Phone, h.EmployeeID, n.FullName,
       h.EndDate,
       DATEDIFF(DAY, CAST(GETDATE() AS DATE), h.EndDate) AS DaysRemaining,
       h.TotalValue
FROM CONTRACT h
JOIN CUSTOMER k ON k.CustomerID = h.CustomerID
JOIN EMPLOYEE  n ON n.EmployeeID = h.EmployeeID
WHERE h.Status = N'Active'
  AND h.EndDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 30, CAST(GETDATE() AS DATE));
GO

-- Unresolved feedback (queries should ORDER BY Priority, DueDate)
CREATE VIEW dbo.vw_OpenFeedback
AS
SELECT p.FeedbackID, p.CustomerID, k.CustomerName, p.FeedbackType, p.Content,
       p.Priority,
       CASE p.Priority WHEN 1 THEN N'High' WHEN 2 THEN N'Medium' ELSE N'Low' END AS PriorityName,
       p.FeedbackStatus, p.EmployeeID, n.FullName, p.CreatedTime, p.DueDate,
       CASE WHEN p.DueDate < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END AS IsOverdue
FROM FEEDBACK p
JOIN CUSTOMER k ON k.CustomerID = p.CustomerID
LEFT JOIN EMPLOYEE n ON n.EmployeeID = p.EmployeeID
WHERE p.FeedbackStatus IN (N'New', N'InProgress');
GO

-- Care schedule: take the appointment of each customer's most recent care record (active customers only),
-- showing overdue, today and the next 7 days
CREATE VIEW dbo.vw_CareSchedule
AS
SELECT c.CareID, c.CustomerID, k.CustomerName, k.Phone, c.EmployeeID, n.FullName,
       c.CareType, c.Content, c.NextAppointmentDate,
       CASE WHEN c.NextAppointmentDate <  CAST(GETDATE() AS DATE) THEN N'Overdue'
            WHEN c.NextAppointmentDate =  CAST(GETDATE() AS DATE) THEN N'Today'
            ELSE N'Upcoming' END AS ScheduleStatus
FROM CARE c
JOIN CUSTOMER k ON k.CustomerID = c.CustomerID AND k.IsActive = 1
JOIN EMPLOYEE  n ON n.EmployeeID = c.EmployeeID
WHERE c.NextAppointmentDate IS NOT NULL
  AND c.NextAppointmentDate <= DATEADD(DAY, 7, CAST(GETDATE() AS DATE))
  AND NOT EXISTS (SELECT 1 FROM CARE c2          -- c is the latest care record (same day -> higher CareID wins)
                  WHERE c2.CustomerID = c.CustomerID
                    AND (c2.CareDate > c.CareDate OR (c2.CareDate = c.CareDate AND c2.CareID > c.CareID)));
GO

-- Customer interaction timeline (contacts + care + feedback) for the customer detail page
CREATE VIEW dbo.vw_InteractionHistory
AS
SELECT t.CustomerID, t.LogTime, t.Category, t.SubType, t.Content, t.Result, t.EmployeeID, n.FullName
FROM (
    SELECT CustomerID, ContactTime AS LogTime, N'Contact' AS Category, Channel AS SubType,
           Content AS Content, Result AS Result, EmployeeID
    FROM CONTACT
    UNION ALL
    SELECT CustomerID, CAST(CareDate AS DATETIME), N'Care', CareType, Content, Result, EmployeeID
    FROM CARE
    UNION ALL
    SELECT CustomerID, CreatedTime, N'Feedback', FeedbackType, Content, FeedbackStatus, EmployeeID
    FROM FEEDBACK
) t
LEFT JOIN EMPLOYEE n ON n.EmployeeID = t.EmployeeID;
GO

-- Application settings at system scope (EmployeeID IS NULL) - what an anonymous visitor and the
-- settings page read. At most one row per key, guaranteed by UQ_Settings_Scope.
CREATE VIEW dbo.vw_SystemSettings
AS
SELECT s.SettingKey, s.SettingValue, s.DataType, s.Category, s.Description, s.UpdatedDate
FROM SETTINGS s
WHERE s.EmployeeID IS NULL;
GO

-- The settings an employee really gets: the personal row wins, the system default is the fallback.
-- EMPLOYEE x (every system key) left-joined to the personal rows, so the fallback rule lives here
-- and the application never re-implements it. IsOverridden marks a personal choice (a key that has
-- no system default is not listed - every key always has one).
CREATE VIEW dbo.vw_EffectiveSettings
AS
SELECT n.EmployeeID, n.FullName, n.DepartmentID,
       d.SettingKey,
       ISNULL(s.SettingValue, d.SettingValue) AS SettingValue,
       CASE WHEN s.SettingID IS NULL THEN 0 ELSE 1 END AS IsOverridden,
       d.DataType, d.Category, d.Description,
       CASE WHEN s.SettingID IS NULL THEN d.UpdatedDate ELSE s.UpdatedDate END AS UpdatedDate
FROM EMPLOYEE n
CROSS JOIN (SELECT * FROM SETTINGS WHERE EmployeeID IS NULL) d
LEFT JOIN SETTINGS s ON s.EmployeeID = n.EmployeeID AND s.SettingKey = d.SettingKey;
GO

/* ====================== STORED PROCEDURES ====================== */

-- Add a contract + its detail lines in one transaction.
-- @DetailJson example: N'[{"ProductID":1,"Quantity":2},{"ProductID":4,"Quantity":1,"UnitPrice":2500000}]'
-- If UnitPrice is omitted the product's current price is used. A duplicate ProductID in one contract is rejected.
-- @Status accepts only 'Active' or 'Pending'.
CREATE PROCEDURE dbo.sp_AddContract
    @CustomerID         INT,
    @EmployeeID         INT,
    @EndDate   DATE,
    @DetailJson  NVARCHAR(MAX),
    @SignDate       DATE = NULL,
    @Note       NVARCHAR(500) = NULL,
    @Status    NVARCHAR(30) = N'Active',
    @ContractID         INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        IF @SignDate IS NULL SET @SignDate = CAST(GETDATE() AS DATE);

        IF NOT EXISTS (SELECT 1 FROM CUSTOMER WHERE CustomerID = @CustomerID AND IsActive = 1)
            THROW 50006, N'Customer does not exist or is no longer active.', 1;
        IF NOT EXISTS (SELECT 1 FROM EMPLOYEE WHERE EmployeeID = @EmployeeID)
            THROW 50007, N'Employee does not exist.', 1;
        IF @Status NOT IN (N'Active', N'Pending')
            THROW 50014, N'A new contract may only be created as "Active" or "Pending".', 1;
        IF @EndDate < @SignDate
            THROW 50015, N'The end date must be on or after the sign date.', 1;
        IF ISJSON(@DetailJson) <> 1
            THROW 50008, N'The contract details are not valid JSON.', 1;

        DECLARE @details TABLE (ProductID INT, Quantity INT, UnitPrice DECIMAL(18,2));
        INSERT INTO @details (ProductID, Quantity, UnitPrice)
        SELECT ProductID, Quantity, UnitPrice
        FROM OPENJSON(@DetailJson)
             WITH (ProductID INT '$.ProductID', Quantity INT '$.Quantity', UnitPrice DECIMAL(18,2) '$.UnitPrice');

        IF NOT EXISTS (SELECT 1 FROM @details)
            THROW 50001, N'A contract must have at least one detail line.', 1;
        IF EXISTS (SELECT 1 FROM @details WHERE Quantity IS NULL OR Quantity <= 0)
            THROW 50011, N'Quantity must be a positive integer.', 1;
        IF EXISTS (SELECT ProductID FROM @details GROUP BY ProductID HAVING COUNT(*) > 1)
            THROW 50009, N'A product may appear only once in a contract.', 1;
        IF EXISTS (SELECT 1 FROM @details c LEFT JOIN PRODUCT_SERVICE sp ON sp.ProductID = c.ProductID WHERE sp.ProductID IS NULL)
            THROW 50010, N'One of the products does not exist in the catalogue.', 1;

        BEGIN TRANSACTION;

        INSERT INTO CONTRACT (CustomerID, EmployeeID, SignDate, EndDate, TotalValue, Status, Note)
        VALUES (@CustomerID, @EmployeeID, @SignDate, @EndDate, 0, @Status, @Note);
        SET @ContractID = SCOPE_IDENTITY();

        INSERT INTO CONTRACT_DETAIL (ContractID, ProductID, Quantity, UnitPrice)
        SELECT @ContractID, c.ProductID, c.Quantity, ISNULL(c.UnitPrice, sp.UnitPrice)
        FROM @details c
        JOIN PRODUCT_SERVICE sp ON sp.ProductID = c.ProductID;

        COMMIT TRANSACTION;   -- TotalValue and Tier have already been updated by the triggers
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- Move a "Pending" contract to "Active"
CREATE PROCEDURE dbo.sp_SignContract
    @ContractID   INT,
    @SignDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @curStatus NVARCHAR(30), @curEndDate DATE;
    SELECT @curStatus = Status, @curEndDate = EndDate FROM CONTRACT WHERE ContractID = @ContractID;
    IF @curStatus IS NULL THROW 50013, N'Contract not found.', 1;
    IF @curStatus <> N'Pending' THROW 50014, N'Only a "Pending" contract can be signed.', 1;
    IF @SignDate IS NULL SET @SignDate = CAST(GETDATE() AS DATE);
    IF @curEndDate < @SignDate THROW 50015, N'The sign date cannot be after the end date.', 1;

    UPDATE CONTRACT SET SignDate = @SignDate, Status = N'Active' WHERE ContractID = @ContractID;
END;
GO

-- Renew a contract (Active / Expired) to a new date and set it back to "Active"
CREATE PROCEDURE dbo.sp_RenewContract
    @ContractID          INT,
    @NewEndDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @curStatus NVARCHAR(30), @curEndDate DATE;
    SELECT @curStatus = Status, @curEndDate = EndDate FROM CONTRACT WHERE ContractID = @ContractID;
    IF @curStatus IS NULL THROW 50013, N'Contract not found.', 1;
    IF @curStatus NOT IN (N'Active', N'Expired')
        THROW 50014, N'Only an "Active" or "Expired" contract can be renewed.', 1;
    IF @NewEndDate <= @curEndDate OR @NewEndDate < CAST(GETDATE() AS DATE)
        THROW 50015, N'The new end date must be after the current end date and not in the past.', 1;

    UPDATE CONTRACT SET EndDate = @NewEndDate, Status = N'Active' WHERE ContractID = @ContractID;
END;
GO

-- Cancel a contract (Active / Pending), appending the reason to Note
CREATE PROCEDURE dbo.sp_CancelContract
    @ContractID INT,
    @Reason NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @curStatus NVARCHAR(30);
    SELECT @curStatus = Status FROM CONTRACT WHERE ContractID = @ContractID;
    IF @curStatus IS NULL THROW 50013, N'Contract not found.', 1;
    IF @curStatus NOT IN (N'Active', N'Pending')
        THROW 50014, N'Only an "Active" or "Pending" contract can be cancelled.', 1;

    UPDATE CONTRACT
    SET Status = N'Cancelled',
        Note = LEFT(ISNULL(Note + N' | ', N'') + N'Cancelled: ' + ISNULL(@Reason, N'(no reason given)'), 500)
    WHERE ContractID = @ContractID;
END;
GO

-- Search customers by keyword (name / email / phone), tier, owning employee; a NULL parameter is ignored
-- @ActiveOnly = 1 (default): active customers only; 0: include deactivated ones
CREATE PROCEDURE dbo.sp_SearchCustomer
    @Keyword       NVARCHAR(100) = NULL,
    @Tier       NVARCHAR(50)  = NULL,
    @OwnerEmployeeID INT           = NULL,
    @ActiveOnly  BIT           = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT k.CustomerID, k.CustomerName, k.CustomerType, k.Email, k.Phone, k.Address, k.Tier, k.CreatedDate,
           k.OwnerEmployeeID, n.FullName AS OwnerName, k.IsActive,
           dbo.fn_CustomerContractTotal(k.CustomerID) AS TotalValue
    FROM CUSTOMER k
    LEFT JOIN EMPLOYEE n ON n.EmployeeID = k.OwnerEmployeeID
    WHERE (@Keyword IS NULL OR k.CustomerName LIKE N'%' + @Keyword + N'%'
                           OR k.Email LIKE N'%' + @Keyword + N'%'
                           OR k.Phone    LIKE N'%' + @Keyword + N'%')
      AND (@Tier IS NULL OR k.Tier = @Tier)
      AND (@OwnerEmployeeID IS NULL OR k.OwnerEmployeeID = @OwnerEmployeeID)
      AND (@ActiveOnly = 0 OR k.IsActive = 1)
    ORDER BY k.CustomerName;
END;
GO

-- Soft delete / restore a customer (used instead of DELETE on the web)
CREATE PROCEDURE dbo.sp_SetCustomerStatus
    @CustomerID     INT,
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM CUSTOMER WHERE CustomerID = @CustomerID)
        THROW 50006, N'Customer not found.', 1;
    UPDATE CUSTOMER SET IsActive = @IsActive WHERE CustomerID = @CustomerID;
END;
GO

-- Assign an employee to handle a feedback item, moving it to "InProgress"
CREATE PROCEDURE dbo.sp_AssignFeedback
    @FeedbackID INT,
    @EmployeeID INT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM FEEDBACK WHERE FeedbackID = @FeedbackID)
        THROW 50002, N'Feedback not found.', 1;
    IF NOT EXISTS (SELECT 1 FROM EMPLOYEE WHERE EmployeeID = @EmployeeID)
        THROW 50003, N'Employee not found.', 1;

    UPDATE FEEDBACK
    SET EmployeeID = @EmployeeID,
        FeedbackStatus = CASE WHEN FeedbackStatus = N'New' THEN N'InProgress' ELSE FeedbackStatus END
    WHERE FeedbackID = @FeedbackID;
END;
GO

-- Change the feedback status (ResolvedDate is filled/cleared by a trigger)
CREATE PROCEDURE dbo.sp_UpdateFeedbackStatus
    @FeedbackID      INT,
    @Status NVARCHAR(50),
    @Note    NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Status NOT IN (N'New', N'InProgress', N'Resolved', N'Closed')
        THROW 50012, N'Invalid feedback status.', 1;
    IF NOT EXISTS (SELECT 1 FROM FEEDBACK WHERE FeedbackID = @FeedbackID)
        THROW 50002, N'Feedback not found.', 1;
    IF @Status <> N'New'
       AND EXISTS (SELECT 1 FROM FEEDBACK WHERE FeedbackID = @FeedbackID AND EmployeeID IS NULL)
        THROW 50004, N'Feedback must be assigned to an employee before moving to a processing status.', 1;

    UPDATE FEEDBACK
    SET FeedbackStatus = @Status,
        Note = ISNULL(@Note, Note)
    WHERE FeedbackID = @FeedbackID;
END;
GO

-- Monthly revenue for a year (by sign date, excluding cancelled / pending contracts)
CREATE PROCEDURE dbo.sp_MonthlyRevenue
    @Year INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.Month, COUNT(h.ContractID) AS ContractCount, ISNULL(SUM(h.TotalValue), 0) AS Revenue
    FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS m(Month)
    LEFT JOIN CONTRACT h ON h.SignDate >= DATEFROMPARTS(@Year, 1, 1)
                        AND h.SignDate <  DATEFROMPARTS(@Year + 1, 1, 1)
                        AND MONTH(h.SignDate) = m.Month
                        AND h.Status IN (N'Active', N'Expired')
    GROUP BY m.Month
    ORDER BY m.Month;
END;
GO

-- Care statistics by month and type, with the average satisfaction score
CREATE PROCEDURE dbo.sp_MonthlyCareStats
    @Year INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MONTH(CareDate) AS Month, CareType, COUNT(*) AS VisitCount,
           AVG(CAST(SatisfactionScore AS DECIMAL(4,2))) AS AvgSatisfaction
    FROM CARE
    WHERE CareDate >= DATEFROMPARTS(@Year, 1, 1) AND CareDate < DATEFROMPARTS(@Year + 1, 1, 1)
    GROUP BY MONTH(CareDate), CareType
    ORDER BY Month, CareType;
END;
GO

-- Overall dashboard metrics (one row)
CREATE PROCEDURE dbo.sp_DashboardSummary
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    SELECT
        (SELECT COUNT(*) FROM CUSTOMER WHERE IsActive = 1)                                   AS TotalCustomers,
        (SELECT COUNT(*) FROM CUSTOMER WHERE IsActive = 1 AND CreatedDate >= DATEADD(DAY,-30,@Today)) AS NewCustomers30Days,
        (SELECT COUNT(*) FROM CONTRACT WHERE Status = N'Active')                    AS ActiveContracts,
        (SELECT COUNT(*) FROM vw_ExpiringContract)                                             AS ExpiringContracts,
        (SELECT COUNT(*) FROM vw_OpenFeedback)                                              AS OpenFeedback,
        (SELECT COUNT(*) FROM vw_OpenFeedback WHERE IsOverdue = 1)                             AS OverdueFeedback,
        (SELECT ISNULL(SUM(TotalValue), 0) FROM CONTRACT
          WHERE Status IN (N'Active', N'Expired')
            AND SignDate >= DATEFROMPARTS(YEAR(@Today), 1, 1)
            AND SignDate <  DATEFROMPARTS(YEAR(@Today) + 1, 1, 1))                                 AS RevenueThisYear,
        (SELECT AVG(CAST(SatisfactionScore AS DECIMAL(4,2))) FROM CARE
          WHERE SatisfactionScore IS NOT NULL AND CareDate >= DATEADD(DAY, -180, @Today))                AS AvgSatisfaction180Days;
END;
GO

-- Move overdue contracts to "Expired" (should run daily through a SQL Agent job)
CREATE PROCEDURE dbo.sp_ExpireContracts
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE CONTRACT
    SET Status = N'Expired'
    WHERE Status = N'Active'
      AND EndDate < CAST(GETDATE() AS DATE);
    SELECT @@ROWCOUNT AS UpdatedContracts;
END;
GO

/* ====================== TRIGGERS ====================== */

-- 1. Insert/update/delete a contract detail line -> recompute the contract TotalValue
CREATE TRIGGER dbo.trg_ContractDetail_UpdateTotal
ON CONTRACT_DETAIL
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE h
    SET TotalValue = ISNULL((SELECT SUM(c.Quantity * c.UnitPrice)
                         FROM CONTRACT_DETAIL c WHERE c.ContractID = h.ContractID), 0)
    FROM CONTRACT h
    WHERE h.ContractID IN (SELECT ContractID FROM inserted UNION SELECT ContractID FROM deleted);
END;
GO

-- 2. A contract changed -> re-rank the customer tier
--    (requires 'nested triggers' = ON, the SQL Server default, because trigger 1 updates CONTRACT,
--     which in turn fires this trigger)
CREATE TRIGGER dbo.trg_Contract_UpdateTier
ON CONTRACT
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE k
    SET Tier = dbo.fn_CustomerTier(dbo.fn_CustomerContractTotal(k.CustomerID))
    FROM CUSTOMER k
    WHERE k.CustomerID IN (SELECT CustomerID FROM inserted UNION SELECT CustomerID FROM deleted);
END;
GO

-- 3. Feedback: fill ResolvedDate when "Resolved"/"Closed"; clear it when reopened
--    (the assignee requirement has moved to the CHECK constraint CHK_Feedback_Assignee)
CREATE TRIGGER dbo.trg_Feedback_FillResolvedDate
ON FEEDBACK
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE p
    SET ResolvedDate = CASE WHEN i.FeedbackStatus IN (N'Resolved', N'Closed')
                        THEN CAST(GETDATE() AS DATE) ELSE NULL END
    FROM FEEDBACK p
    JOIN inserted i ON i.FeedbackID = p.FeedbackID
    WHERE (i.FeedbackStatus IN (N'Resolved', N'Closed') AND p.ResolvedDate IS NULL)
       OR (i.FeedbackStatus NOT IN (N'Resolved', N'Closed') AND p.ResolvedDate IS NOT NULL);
END;
GO

-- 4. Reject deleting a customer that still has contracts; otherwise delete the related care data too
--    (the web app should use sp_SetCustomerStatus for a soft delete instead of DELETE)
CREATE TRIGGER dbo.trg_Customer_PreventDelete
ON CUSTOMER
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM CONTRACT h JOIN deleted d ON d.CustomerID = h.CustomerID)
        THROW 50005, N'Cannot delete a customer that still has contracts. Use sp_SetCustomerStatus to deactivate instead.', 1;

    DELETE FROM CONTACT  WHERE CustomerID IN (SELECT CustomerID FROM deleted);
    DELETE FROM CARE WHERE CustomerID IN (SELECT CustomerID FROM deleted);
    DELETE FROM FEEDBACK WHERE CustomerID IN (SELECT CustomerID FROM deleted);
    DELETE FROM CUSTOMER WHERE CustomerID IN (SELECT CustomerID FROM deleted);
END;
GO

-- 5. Contract audit log: insert / status change / delete (the actor is read from SESSION_CONTEXT 'EmployeeID')
CREATE TRIGGER dbo.trg_Contract_AuditLog
ON CONTRACT
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @actorID INT = TRY_CAST(SESSION_CONTEXT(N'EmployeeID') AS INT);

    INSERT INTO AUDIT_LOG (EmployeeID, TableName, RecordID, Action, Detail)
    SELECT @actorID, N'CONTRACT', i.ContractID, N'Insert', N'Customer ' + CAST(i.CustomerID AS NVARCHAR(10))
    FROM inserted i
    WHERE NOT EXISTS (SELECT 1 FROM deleted d WHERE d.ContractID = i.ContractID);

    INSERT INTO AUDIT_LOG (EmployeeID, TableName, RecordID, Action, Detail)
    SELECT @actorID, N'CONTRACT', i.ContractID, N'StatusChange', d.Status + N' -> ' + i.Status
    FROM inserted i
    JOIN deleted d ON d.ContractID = i.ContractID
    WHERE i.Status <> d.Status;

    INSERT INTO AUDIT_LOG (EmployeeID, TableName, RecordID, Action, Detail)
    SELECT @actorID, N'CONTRACT', d.ContractID, N'Delete', N'Customer ' + CAST(d.CustomerID AS NVARCHAR(10))
    FROM deleted d
    WHERE NOT EXISTS (SELECT 1 FROM inserted i WHERE i.ContractID = d.ContractID);
END;
GO

-- 6. Feedback audit log: status change or assignee change
CREATE TRIGGER dbo.trg_Feedback_AuditLog
ON FEEDBACK
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @actorID INT = TRY_CAST(SESSION_CONTEXT(N'EmployeeID') AS INT);

    INSERT INTO AUDIT_LOG (EmployeeID, TableName, RecordID, Action, Detail)
    SELECT @actorID, N'FEEDBACK', i.FeedbackID,
           CASE WHEN ISNULL(i.EmployeeID, 0) <> ISNULL(d.EmployeeID, 0) THEN N'Assign' ELSE N'StatusChange' END,
           d.FeedbackStatus + N' -> ' + i.FeedbackStatus
             + N' | Assignee: ' + ISNULL(CAST(d.EmployeeID AS NVARCHAR(10)), N'none')
             + N' -> ' + ISNULL(CAST(i.EmployeeID AS NVARCHAR(10)), N'none')
    FROM inserted i
    JOIN deleted d ON d.FeedbackID = i.FeedbackID
    WHERE i.FeedbackStatus <> d.FeedbackStatus OR ISNULL(i.EmployeeID, 0) <> ISNULL(d.EmployeeID, 0);
END;
GO

/* ====================== ROLES ====================== */
-- Note: if the web app uses one shared login for the whole application, the real authorization lives at
-- the web layer via ACCOUNT.Role; the roles below are for demoing DB-level authorization or for the case
-- where each role has its own login.
CREATE ROLE role_Manager;
CREATE ROLE role_Sales;
CREATE ROLE role_Support;
GO

-- Manager: full data and execute permissions
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO role_Manager;

-- Sales: manage customers and contracts; read contacts/care; write contacts
GRANT SELECT, INSERT, UPDATE ON CUSTOMER    TO role_Sales;
GRANT SELECT, INSERT, UPDATE ON CONTRACT      TO role_Sales;
GRANT SELECT, INSERT, UPDATE ON CONTRACT_DETAIL    TO role_Sales;
GRANT SELECT, INSERT         ON CONTACT       TO role_Sales;
GRANT SELECT ON CARE                      TO role_Sales;
GRANT SELECT ON PRODUCT_SERVICE                   TO role_Sales;
GRANT SELECT ON EMPLOYEE                     TO role_Sales;
GRANT SELECT ON DEPARTMENT                     TO role_Sales;
GRANT SELECT ON vw_ContractInfo            TO role_Sales;
GRANT SELECT ON vw_ContractDetail             TO role_Sales;
GRANT SELECT ON vw_CustomerRevenue          TO role_Sales;
GRANT SELECT ON vw_ExpiringContract           TO role_Sales;
GRANT SELECT ON vw_InteractionHistory             TO role_Sales;
GRANT EXECUTE ON sp_AddContract               TO role_Sales;
GRANT EXECUTE ON sp_SignContract                 TO role_Sales;
GRANT EXECUTE ON sp_RenewContract             TO role_Sales;
GRANT EXECUTE ON sp_CancelContract                TO role_Sales;
GRANT EXECUTE ON sp_SearchCustomer              TO role_Sales;
GRANT EXECUTE ON sp_SetCustomerStatus     TO role_Sales;
GRANT EXECUTE ON sp_MonthlyRevenue         TO role_Sales;
GRANT EXECUTE ON sp_DashboardSummary         TO role_Sales;

-- Support: write contacts, care, feedback; read-only customers and contracts
GRANT SELECT ON CUSTOMER                    TO role_Support;
GRANT SELECT ON CONTRACT                      TO role_Support;
GRANT SELECT ON CONTRACT_DETAIL                    TO role_Support;
GRANT SELECT ON PRODUCT_SERVICE                   TO role_Support;
GRANT SELECT ON EMPLOYEE                     TO role_Support;
GRANT SELECT ON DEPARTMENT                     TO role_Support;
GRANT SELECT, INSERT, UPDATE ON CONTACT       TO role_Support;
GRANT SELECT, INSERT, UPDATE ON CARE      TO role_Support;
GRANT SELECT, INSERT, UPDATE ON FEEDBACK      TO role_Support;
GRANT SELECT ON vw_ContractInfo            TO role_Support;
GRANT SELECT ON vw_ContractDetail             TO role_Support;
GRANT SELECT ON vw_OpenFeedback            TO role_Support;
GRANT SELECT ON vw_CareSchedule                TO role_Support;
GRANT SELECT ON vw_ExpiringContract           TO role_Support;
GRANT SELECT ON vw_InteractionHistory             TO role_Support;
GRANT EXECUTE ON sp_SearchCustomer              TO role_Support;
GRANT EXECUTE ON sp_AssignFeedback           TO role_Support;
GRANT EXECUTE ON sp_UpdateFeedbackStatus   TO role_Support;
GRANT EXECUTE ON sp_MonthlyCareStats   TO role_Support;
GRANT EXECUTE ON sp_DashboardSummary         TO role_Support;
-- ACCOUNT and AUDIT_LOG are not granted to Sales / Support (Manager only)
GO

/* Example of creating a login/user and assigning a role (uncomment to run when demoing authorization):
CREATE LOGIN lg_sales WITH PASSWORD = 'Abc@12345', CHECK_POLICY = OFF;
CREATE USER  us_sales FOR LOGIN lg_sales;
ALTER ROLE role_Sales ADD MEMBER us_sales;
*/

/* ====================== QUICK TEST (run after loading 03_SampleData.sql) ======================
EXEC sp_set_session_context N'EmployeeID', 2;      -- pretend "mary" is logged in (so the audit log records the right person)

-- Add a new contract for customer 9 through the procedure
DECLARE @id INT;
DECLARE @endDate DATE = DATEADD(YEAR, 1, GETDATE());
EXEC sp_AddContract @CustomerID = 9, @EmployeeID = 2, @EndDate = @endDate,
     @Note = N'Test', @DetailJson = N'[{"ProductID":1,"Quantity":2},{"ProductID":4,"Quantity":1}]',
     @ContractID = @id OUTPUT;
SELECT * FROM CONTRACT WHERE ContractID = @id;      -- TotalValue = 13,000,000 computed by the trigger
SELECT * FROM CUSTOMER WHERE CustomerID = 9;      -- 12m + 13m = 25m -> Tier automatically becomes "Silver"
SELECT * FROM AUDIT_LOG;                        -- there is an "Insert" row for CONTRACT with EmployeeID = 2

EXEC sp_DashboardSummary;
SELECT * FROM vw_ExpiringContract;
SELECT * FROM vw_OpenFeedback ORDER BY Priority, DueDate;
SELECT * FROM vw_CareSchedule ORDER BY NextAppointmentDate;
SELECT * FROM vw_CustomerRevenue ORDER BY TotalValue DESC;
SELECT * FROM vw_InteractionHistory WHERE CustomerID = 1 ORDER BY LogTime DESC;
DECLARE @year INT = YEAR(GETDATE());
EXEC sp_MonthlyRevenue @Year = @year;
EXEC sp_SearchCustomer @Keyword = N'Company';
EXEC sp_AssignFeedback @FeedbackID = 9, @EmployeeID = 3;  -- feedback without an assignee -> "InProgress"
EXEC sp_UpdateFeedbackStatus @FeedbackID = 9, @Status = N'Resolved';   -- ResolvedDate filled automatically
DELETE FROM CUSTOMER WHERE CustomerID = 1;         -- must raise error 50005 because it still has contracts
===================================================================== */
