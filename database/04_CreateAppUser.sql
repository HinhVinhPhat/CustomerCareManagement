/* =====================================================================
   File 4/4: Create a dedicated SQL login for the web application
   (the application must NOT use sa).
   Run by init.sh as: sqlcmd -v APP_PASSWORD="..."
   Permissions: the application checks roles itself (cookie login), so this user is added
   to role_Manager.
   For stricter DB-level control: create three separate logins for the three roles and let
   the application pick a connection by role.
   The password must not contain a single quote (') and must be strong
   (>= 8 characters, upper/lower case, digits and symbols).
   ===================================================================== */
USE CustomerCareDB;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'app_ccm')
    CREATE LOGIN app_ccm WITH PASSWORD = N'$(APP_PASSWORD)', CHECK_POLICY = ON;
GO
IF DATABASE_PRINCIPAL_ID(N'app_ccm') IS NULL
    CREATE USER app_ccm FOR LOGIN app_ccm;
GO
ALTER ROLE role_Manager ADD MEMBER app_ccm;
GO
