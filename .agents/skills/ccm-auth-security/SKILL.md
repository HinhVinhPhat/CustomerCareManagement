---
name: ccm-auth-security
description: Login, role-based authorization (Manager, Sales, Support) and security for the ASP.NET Core MVC application of the customer care management system using the ACCOUNT table - cookie authentication, checking the demo SHA2 hash before moving to PasswordHasher, the employeeID and role claims, [Authorize], the access-denied page, account lockout, limiting failed logins, CSRF and XSS protection, account management and password reset. Use this skill when building the login page, protecting a controller or action, managing accounts, or when the user mentions login, password, permissions, role, cookie, 403 or security.
---

# Login, authorization, security (MVC, cookie)

The `ACCOUNT` table (Username, PasswordHash, Role, EmployeeID, IsActive) already exists. An MVC app
renders on the server, so use **cookie authentication**, not JWT. The page permission matrix is in
`docs/design.md` section 5 (section 10.3 about REST endpoints is outdated versus the MVC
architecture; use only its role column as a reference for the corresponding action). The code
structure is in the `ccm-mvc-architecture` skill.

## Configuration

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";                       // matches the sitemap in design.md section 4
        o.AccessDeniedPath = "/access-denied";                 // the "You do not have access" page (design.md 6.15)
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // on HTTPS deployment: Always
    });
builder.Services.AddAuthorization(o =>
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());   // login required by default
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
// app.UseAuthentication(); app.UseAuthorization();  (after UseRouting)
```

The fallback policy means every new page **requires login by default**; only the login action gets
`[AllowAnonymous]`. The anti-forgery (CSRF) filter applies to every POST/PUT/DELETE, and Razor
forms add the token automatically.

## Passwords: the demo hash is SHA2, move it to PasswordHasher

The sample data stores the password `123456` as `HASHBYTES('SHA2_256', N'123456')` for every
account so the SQL file runs on its own. That hash is fast and unsalted and **must not stay long
term**. Use `PasswordHasher<Account>` (package `Microsoft.Extensions.Identity.Core`). The login
flow:

1. The demo hash is a string of **exactly 64 hex characters**. A `PasswordHasher` hash is Base64
   and about 84 characters, so they cannot be confused.
2. Demo hash: compute the SHA-256 of the password **encoded as UTF-16 little-endian** (SQL Server
   hashes an `N'...'` nvarchar string), convert to hex, and compare **case-insensitively** using a
   constant-time comparison.
   - `Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(password)))` (`Encoding.Unicode` is UTF-16LE).
   - Test value: the SHA-256 of `123456` as UTF-16LE is `EC278A38901287B2771A13739520384D43E4B078F78AFFE702DEF108774CCE24` (the UTF-8 version gives `8D969EEF...`, which is wrong). Check against `SELECT PasswordHash FROM ACCOUNT WHERE Username = 'john'` on the real DB before trusting your own code.
3. On a match, **re-hash with `PasswordHasher`** and update `PasswordHash` (this table has no
   trigger, so use `ExecuteUpdateAsync`). If the hash is already a `PasswordHasher` hash, use
   `VerifyHashedPassword` and re-hash when it returns `SuccessRehashNeeded`.

The `PasswordHash` column is `VARCHAR(255)`, enough for both. A hash must never appear in a View,
a log or a ViewModel.

## Login flow

1. The form POST (with an anti-forgery token) sends the username and password.
2. Look up the account by `Username` (do not filter on `IsActive` in the query, so a locked account
   can still be distinguished).
3. No account or wrong password: show the **same** message "Incorrect username or password". When
   there is no account, still hash a dummy string so the timing matches and attackers cannot tell
   which accounts exist.
4. A **correct** password but `IsActive = 0`: only then report "This account has been locked".
   Report it only after the password is correct, otherwise anyone could probe which accounts are
   locked.
5. Success: create the claims and call `HttpContext.SignInAsync(...)`. Claims: `NameIdentifier` =
   AccountID, `Name` = FullName, `Role` = the role value, and a custom `employeeID` claim (the
   `GetEmployeeID()` helper in `Helpers/Utils.cs` reads it, and the audit-log interceptor uses it
   too). Then redirect to the role's landing page, allowing only a local `returnUrl`
   (`Url.IsLocalUrl`).
6. Sign out with a **POST** (`SignOutAsync`), never a GET link.

## Roles and authorization

- The DB role values are `Manager`, `Sales`, `Support`. Declare the matching constants in
  `Helpers/WorkConstants.cs` and map to them in **one** place when creating the claim. Concatenated
  constants still work in attributes: `[Authorize(Roles = WorkConstants.Roles.Manager + "," + WorkConstants.Roles.Sales)]`.
- Put `[Authorize(Roles = ...)]` on the **controller or action** per the matrix in design.md
  section 5. A wrong role goes to the access-denied page (403); not being logged in goes to the
  login page.
- Hiding menu items and buttons in a View with `User.IsInRole(...)` is only for a tidy UI. The user
  can still type a URL, so `[Authorize]` is the real protection.
- Data-level permission (the "My customers" toggle): filter by the `employeeID` from the claim in
  the Service; never trust a query-string parameter.
- The acting `employeeID` and role are always read from the authenticated `User`, never from a
  hidden form field.

## Account management (Manager only)

- Create an account linked to an employee without one (each employee has at most one; error 2627
  reports the duplicate).
- Reset password: generate a random temporary password, hash it, show it to the Manager **once** on
  screen and never log it. Encourage the user to change it immediately.
- Lock or unlock with `IsActive`. Do not allow locking yourself or locking the last Manager.
- Change password: require the old password, and a new password of at least 8 characters that
  differs from the old one.

## Protection against attacks

- **CSRF**: the global `AutoValidateAntiforgeryToken` filter (configured above). Never disable it
  for a write action.
- **XSS**: Razor HTML-encodes automatically when you use `@Model.Name`. **Never use `Html.Raw` with
  user-entered data** (customer names, feedback content, notes). When passing data to JavaScript,
  use `@Json.Serialize(...)`, never string concatenation.
- **SQL injection**: every query goes through EF/LINQ or `SqlParameter`, never string concatenation.
- **Repeated failed logins**: use `AddRateLimiter` (a fixed window per IP) on the login action, or
  count failures per username and block for a few minutes.
- **HTTPS**: in production enable `UseHttpsRedirection`, `UseHsts`, `Cookie.SecurePolicy = Always`.
  Never log passwords, hashes or cookies.
- In Docker the cookie encryption key (Data Protection) is lost when the container restarts and
  everyone is signed out. Persist the keys to a volume to keep sessions (see `ccm-docker-deploy`).

## Checks

1. Log in as `john`/`123456` the first time (goes through the SHA2 branch), confirm `PasswordHash`
   changed to a string longer than 64 characters, and logging in again still works.
2. A wrong password and a wrong username show the same message.
3. Locked account: the correct password reports the lock, a wrong password reports bad credentials.
4. Log in as `mary` (Sales) and open a URL for a Support-only page: you land on the access-denied
   page. Open an internal page while logged out: you go to the login page.
5. Enter the customer name `<img src=x onerror=alert(1)>` and view the list, detail and dashboard:
   only the text shows, no script runs.
6. Submit a POST form without the anti-forgery token: it is rejected.
