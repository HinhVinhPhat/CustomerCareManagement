# CustomerCareDB schema reference (v2)

Contents: Tables | Relationships | Views | Stored procedures | Functions | Triggers | Error codes | SQL roles

Source: `database/01` to `database/04`. If the SQL files disagree with this document, the SQL
files win, and fix this document.

Sample data (file 03) carries **Vietnamese** business values - names, addresses, notes, product
names, feedback text. Coded values (statuses, roles, tiers, types, channels) stay English; see the
`ccm-language` skill for how they are shown on screen.

## Tables

**DEPARTMENT** (DepartmentID PK, DepartmentName unique)

**EMPLOYEE** (EmployeeID PK, FullName, Email unique, Phone unique, Gender, Position, DepartmentID FK; all not null)
- Position has only 2 values; unique index: each department has at most 1 "Manager".
- Email looks like `x@y.z`; phone is digits only, 9-11 long.

**ACCOUNT** (AccountID PK, Username unique, PasswordHash varchar(255), Role, EmployeeID unique FK, IsActive bit default 1)
- Each employee has at most 1 account.

**CUSTOMER** (CustomerID PK, CustomerName, CustomerType, Email null, Phone null, Gender null, DateOfBirth null, Address null, Source null, CreatedDate default today, Tier default Standard, OwnerEmployeeID null FK EMPLOYEE, IsActive bit default 1)
- `IsActive = 0` means no longer cared for (soft delete).
- Email is unique when not NULL (many customers without email are fine). Must have an email or a phone number.
- `DateOfBirth < CreatedDate`. A business has no Gender/DateOfBirth.
- Source is free text; sample values: Website, Giới thiệu, Sự kiện, Facebook, Điện thoại.
- `Tier` is owned by a trigger; never edit it by hand.

**PRODUCT_SERVICE** (ProductID PK, ProductName unique, ProductType, UnitPrice >= 0, Description null)

**CONTRACT** (ContractID PK, CustomerID FK, EmployeeID FK, SignDate default today, EndDate not null, TotalValue, Status default Active, Note null)
- `EndDate >= SignDate`. `TotalValue` is computed by a trigger. EmployeeID is the employee who signs / owns the contract.

**CONTRACT_DETAIL** (ContractID + ProductID is the composite PK, Quantity > 0, UnitPrice >= 0)
- `UnitPrice` is the price at signing time and may differ from the current price (discount). Deleting a contract cascades to its details.

**CONTACT** (ContactID PK, CustomerID FK, EmployeeID FK, ContactTime datetime default now, Channel, Content, Result)

**CARE** (CareID PK, CustomerID FK, EmployeeID FK, CareDate default today, CareType, Content, Result, SatisfactionScore null, NextAppointmentDate null)

**FEEDBACK** (FeedbackID PK, CustomerID FK, EmployeeID null FK, CreatedTime datetime, FeedbackType, Content, Priority default 2, FeedbackStatus default New, DueDate null, ResolvedDate null, Note)
- EmployeeID NULL = not assigned. `CHK_Feedback_Assignee`: only "New" may have no EmployeeID.
- DueDate and ResolvedDate may not be before the CreatedTime date.

**AUDIT_LOG** (LogID bigint PK, LogTime datetime2(0), EmployeeID null FK, TableName, RecordID int, Action, Detail)
- Action: Insert | StatusChange | Delete | Assign. Written only for CONTRACT and FEEDBACK. Only triggers write it; the app only reads it.

**SETTINGS** (SettingID PK, EmployeeID null FK, SettingKey, SettingValue, DataType, Category, Description null, UpdatedDate default now)
- `EmployeeID IS NULL` = the system-wide default (changed by a Manager); `EmployeeID = <id>` = that
  employee's own choice, which wins over the default.
- `UQ_Settings_Scope (EmployeeID, SettingKey)`: SQL Server treats NULL as a value here, so at most
  **one** default per key and one personal row per employee and key.
- `CHK_Settings_Key`: `Language`, `Theme`, `PageSize`. `CHK_Settings_Value` ties the value **and** its
  declared `DataType` to the key, so not even sqlcmd can store a nonsense row: `Language` is
  `vi`/`en` with `String`, `Theme` is `light`/`dark` with `String`, `PageSize` is a whole number
  1-100 with `Int` (extend the CHECK when a new key is added). `DataType` is `String`/`Int`/`Bool`;
  `Category` is `Localization`/`Appearance`/`General`.
- Seeded defaults: `Language = vi`, `Theme = light`, `PageSize = 20`. Nothing writes here except the
  application; there is **no trigger**, so a settings change is not audited.

## Relationships

DEPARTMENT 1-N EMPLOYEE; EMPLOYEE 1-1 ACCOUNT; EMPLOYEE 1-N CUSTOMER (through OwnerEmployeeID);
CUSTOMER 1-N CONTRACT, CONTACT, CARE, FEEDBACK; EMPLOYEE 1-N CONTRACT, CONTACT, CARE, FEEDBACK,
AUDIT_LOG, SETTINGS (personal rows); CONTRACT N-N PRODUCT_SERVICE through CONTRACT_DETAIL.

## Views

| View | Columns | Notes |
|---|---|---|
| vw_ContractInfo | ContractID, CustomerID, CustomerName, EmployeeID, FullName, DepartmentName, SignDate, EndDate, DaysRemaining, TotalValue, Status, Note | Contract list |
| vw_ContractDetail | ContractID, ProductID, ProductName, ProductType, Quantity, UnitPrice, LineTotal | Detail lines |
| vw_CustomerRevenue | CustomerID, CustomerName, CustomerType, Tier, OwnerEmployeeID, IsActive, ContractCount, TotalValue | Top customers, reports. Counts only Active and Expired contracts |
| vw_ExpiringContract | ContractID, CustomerID, CustomerName, Phone, EmployeeID, FullName, EndDate, DaysRemaining, TotalValue | "Active" contracts expiring in the next 30 days |
| vw_OpenFeedback | FeedbackID, CustomerID, CustomerName, FeedbackType, Content, Priority, PriorityName, FeedbackStatus, EmployeeID, FullName, CreatedTime, DueDate, IsOverdue (0/1) | Status New or InProgress. Should be `ORDER BY Priority, DueDate` |
| vw_CareSchedule | CareID, CustomerID, CustomerName, Phone, EmployeeID, FullName, CareType, Content, NextAppointmentDate, ScheduleStatus (Overdue / Today / Upcoming) | The appointment of each active customer's **most recent** care record, due within 7 days |
| vw_InteractionHistory | CustomerID, LogTime, Category (Contact / Care / Feedback), SubType, Content, Result, EmployeeID, FullName | A customer's timeline, `ORDER BY LogTime DESC` |
| vw_SystemSettings | SettingKey, SettingValue, DataType, Category, Description, UpdatedDate | The system-wide defaults (`EmployeeID IS NULL`): what an anonymous visitor and the settings page read |
| vw_EffectiveSettings | EmployeeID, FullName, DepartmentID, SettingKey, SettingValue, IsOverridden (0/1), DataType, Category, Description, UpdatedDate | The value each employee really gets: personal row wins, system default is the fallback. 1 row per employee and key; the app must not re-implement this rule |

## Stored procedures

| Procedure | Parameters | Returns | Errors it may throw |
|---|---|---|---|
| sp_AddContract | @CustomerID, @EmployeeID, @EndDate, @DetailJson, @SignDate=NULL (today), @Note=NULL, @Status='Active' (or 'Pending'), @ContractID OUTPUT | ContractID through OUTPUT | 50006, 50007, 50014, 50015, 50008, 50001, 50011, 50009, 50010 |
| sp_SignContract | @ContractID, @SignDate=NULL | none | 50013, 50014, 50015 |
| sp_RenewContract | @ContractID, @NewEndDate | none | 50013, 50014, 50015 |
| sp_CancelContract | @ContractID, @Reason=NULL | none (the reason is appended to Note) | 50013, 50014 |
| sp_SearchCustomer | @Keyword=NULL, @Tier=NULL, @OwnerEmployeeID=NULL, @ActiveOnly=1, @PageNumber=1, @PageSize=20, @SortBy=N'Name' (`Name`/`CreatedDate`/`TotalValue`), @SortDesc=0 | CustomerID, CustomerName, CustomerType, Email, Phone, Address, Tier, CreatedDate, OwnerEmployeeID, OwnerName, IsActive, TotalValue, TotalRows | none; paging and ordering happen inside, `@PageSize` is clamped to 1-100, an unknown `@SortBy` falls back to `Name`, and `TotalRows` (`COUNT(*) OVER ()`) counts the matches before paging - an empty result carries no row at all |
| sp_SetCustomerStatus | @CustomerID, @IsActive | none | 50006 |
| sp_AssignFeedback | @FeedbackID, @EmployeeID | none (moves New to InProgress) | 50002, 50003 |
| sp_UpdateFeedbackStatus | @FeedbackID, @Status, @Note=NULL | none | 50012, 50002, 50004 |
| sp_MonthlyRevenue | @Year | 12 rows: Month, ContractCount, Revenue | none |
| sp_MonthlyCareStats | @Year | Month, CareType, VisitCount, AvgSatisfaction | none |
| sp_DashboardSummary | none | 1 row: TotalCustomers, NewCustomers30Days, ActiveContracts, ExpiringContracts, OpenFeedback, OverdueFeedback, RevenueThisYear, AvgSatisfaction180Days | none |
| sp_ExpireContracts | none | UpdatedContracts | none |

`@DetailJson` is a JSON array whose property names are **case-sensitive**:
`[{"ProductID":1,"Quantity":2},{"ProductID":4,"Quantity":1,"UnitPrice":2500000}]`. Omit
`UnitPrice` (or set it null) to use the product's current price. No duplicate ProductID;
Quantity must be a positive integer.

Allowed contract status transitions: Pending -> Active (sign) | Cancelled; Active -> renew
(stays Active) | Cancelled; Expired -> renew (back to Active); Cancelled -> nowhere. Renewal:
the new date must be after the current end date and not in the past.

## Functions (scalar)

`dbo.fn_CustomerContractTotal(@CustomerID)`, `dbo.fn_CustomerTier(@TotalValue)`,
`dbo.fn_CustomerAge(@CustomerID)` (NULL when there is no date of birth),
`dbo.fn_ContractDaysRemaining(@ContractID)` (negative = already overdue).

## Triggers

| Trigger | Table | Job |
|---|---|---|
| trg_ContractDetail_UpdateTotal | CONTRACT_DETAIL | Recompute CONTRACT.TotalValue |
| trg_Contract_UpdateTier | CONTRACT | Re-rank the customer Tier (needs nested triggers = ON, the default) |
| trg_Feedback_FillResolvedDate | FEEDBACK | Fill ResolvedDate on Resolved/Closed; clear it when reopened |
| trg_Customer_PreventDelete | CUSTOMER (INSTEAD OF DELETE) | Block deletion while contracts remain (50005); otherwise delete the related contacts, care and feedback |
| trg_Contract_AuditLog | CONTRACT | Write AUDIT_LOG: Insert, StatusChange, Delete |
| trg_Feedback_AuditLog | FEEDBACK | Write AUDIT_LOG: StatusChange, Assign |

## Custom error codes (SqlException.Number)

| Code | Meaning |
|---|---|
| 50001 | Contract has no detail lines |
| 50002 | Feedback not found |
| 50003 | Employee not found |
| 50004 | Feedback has no assigned employee |
| 50005 | Customer still has contracts, cannot delete |
| 50006 | Invalid customer (does not exist or is no longer active) |
| 50007 | Invalid employee |
| 50008 | Invalid contract detail JSON |
| 50009 | Duplicate product in the details |
| 50010 | Product does not exist |
| 50011 | Invalid quantity |
| 50012 | Invalid feedback status |
| 50013 | Contract not found |
| 50014 | Contract status forbids the action |
| 50015 | Invalid date |

Common system errors: 547 (CHECK or foreign-key violation, the constraint name is in the
message), 2601 and 2627 (unique violation: email, phone, username, product name, department
name, department manager, setting scope).

## SQL roles

`role_Manager` (full control in schema dbo), `role_Sales`, `role_Support`. The web app's login
`app_ccm` is a member of `role_Manager`, so **the real authorization lives in the application**
using the role in the login (cookie) claim. The other three roles exist to demo DB-level
authorization.
