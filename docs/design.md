# UI design: Customer Care Management System

This document describes the shared layout, the colour palette, the list of pages and the content of each page. Every page states clearly which view / stored procedure in the `CustomerCareDB` database (version 2) it takes its data from, so the web app does not have to guess.

---

## 1. Users and roles

| Role (`ACCOUNT.Role`) | What they do on the web | Home page after login |
|---|---|---|
| **Manager** | View and edit everything, administer staff, accounts, products, view the audit log | Overview dashboard |
| **Sales** | Manage customers and contracts, record contacts, view revenue | Dashboard (contract- and revenue-oriented) |
| **Support** | Record contacts, care, handle feedback; only view customers and contracts | Dashboard (care schedule- and feedback-oriented) |

Principle: each person only sees the menu and the buttons their role is allowed to use. The server must still check permissions, not merely hide the button.

---

## 2. Style and colour palette

Style: light, clean, plenty of whitespace, favouring tables and figures that are easy to read. Strong colours are reserved for the primary button and for statuses.

### 2.1. Colour variables (CSS variables)

```css
:root {
  /* Brand */
  --primary:        #2563EB;
  --primary-hover:  #1D4ED8;
  --primary-soft:   #EFF6FF;
  --accent:         #0D9488;

  /* Background and text */
  --bg-page:        #F8FAFC;
  --bg-card:        #FFFFFF;
  --bg-sidebar:     #334155;
  --sidebar-fg:     #E2E8F0;
  --sidebar-fg-strong: #FFFFFF;
  --sidebar-hover:  rgba(255, 255, 255, .12);
  --sidebar-line:   rgba(255, 255, 255, .16);
  --bg-topbar:      #E2E8F0;
  --topbar-fg:      #0F172A;
  --topbar-line:    #CBD5E1;
  --bg-brand:       #334155;   /* the sign-in panel, same tone as the sidebar */
  --border:         #E2E8F0;
  --text:           #0F172A;
  --text-muted:     #475569;
  --text-on-dark:   #E2E8F0;

  /* Status */
  --success:        #16A34A;  --success-soft: #DCFCE7;
  --info:           #0284C7;  --info-soft:    #E0F2FE;
  --warning:        #D97706;  --warning-soft: #FEF3C7;
  --danger:         #DC2626;  --danger-soft:  #FEE2E2;
  --neutral:        #64748B;  --neutral-soft: #F1F5F9;
}
```

### 2.2. Badges by data (light background, bold text)

| Group | Value | Colour |
|---|---|---|
| Contract | Active | success |
| | Pending signature | info |
| | Expired | neutral |
| | Cancelled | danger |
| Feedback, priority level | 1 High / 2 Medium / 3 Low | danger / warning / neutral |
| Feedback, status | New | info |
| | In progress | warning |
| | Resolved | success |
| | Closed | neutral |
| Care schedule | Overdue / Today / Upcoming | danger / warning / info |
| Customer tier | Standard | `#64748B` |
| | Silver | `#94A3B8` |
| | Gold | `#F59E0B` |
| | Diamond | `#7C3AED` |

Rule: red is used only for errors, cancellations and overdue items. Colour is never the only signal; badges always carry text.

### 2.3. Typography and spacing

- Font: **Be Vietnam Pro** (the project's chosen font), fallback `Inter, system-ui, sans-serif`.
- Font sizes: page title 24px / bold; card title 16px / bold; body 14px; caption 12px.
- Spacing in multiples of 4px (8, 12, 16, 24, 32). Card corner radius 12px, buttons and inputs 8px. Very subtle shadow, preferring a 1px `--border` edge.
- Money: right-aligned, thousands separator, with the unit of the chosen language (`80.000.000 VNĐ` in Vietnamese, `80,000,000 VND` in English). Dates are displayed as `dd/MM/yyyy` in both languages, times as `HH:mm`.

### 2.4. Dark theme

The interface is Vietnamese by default with an English switch, and light by default with a dark theme
switch (both chosen from the topbar, both remembered per account). There is one palette: the same
variable names are re-declared under `[data-theme="dark"]`, so a page that only uses the variables and
the shared classes is dark-ready and no page carries a raw colour. The switch is one attribute on
`<html>`, decided on the server, so nothing flashes before the styles apply.

```css
[data-theme="dark"] {
  --primary: #60A5FA;  --primary-hover: #93C5FD;  --primary-soft: #1E3A5F;  --accent: #2DD4BF;
  --bg-page: #0B1220;  --bg-card: #151E31;  --bg-soft: #1C2740;
  --bg-sidebar: #1E293B;  --sidebar-fg: #E2E8F0;  --sidebar-fg-strong: #FFFFFF;
  --sidebar-hover: rgba(255, 255, 255, .08);  --sidebar-line: rgba(255, 255, 255, .10);
  --bg-topbar: #151E31;  --topbar-fg: #E8EEF8;  --topbar-line: #27334D;
  --bg-brand: #1E293B;
  --border: #27334D;
  --text: #E8EEF8;  --text-muted: #9FB0CC;  --text-on-dark: #E2E8F0;  --text-on-primary: #0B1220;

  --success: #4ADE80;  --success-soft: #14351F;  --success-ink: #BBF7D0;
  --info:    #38BDF8;  --info-soft:    #0C2C43;  --info-ink:    #BAE6FD;
  --warning: #FBBF24;  --warning-soft: #3A2A08;  --warning-ink: #FDE68A;
  --danger:  #F87171;  --danger-soft:  #3D1417;  --danger-ink:  #FECACA;
  --neutral: #94A3B8;  --neutral-soft: #1C2740;  --neutral-ink: #CBD5E1;
}
```

Rules for the dark theme:

- A "soft" badge background becomes a deep tint of its own hue and the text on it a light tint of the
  same hue (`--*-ink`), so a badge keeps its meaning and stays readable; the colour mapping of section
  2.2 does not change.
- The primary colour is lightened, and `--text-on-primary` flips to the dark page colour, so a primary
  button keeps a readable label.
- A status is still shown as text (or an icon) together with the colour, never colour alone, because
  the same colour reads differently on the two backgrounds.
- The frame is built from three named surfaces, and a page never carries a colour of its own: the
  sidebar (`--bg-sidebar` plus `--sidebar-fg` / `--sidebar-hover` / `--sidebar-line`), the topbar
  (`--bg-topbar` plus `--topbar-fg` / `--topbar-line`) and the sign-in panel (`--bg-brand` with
  `--text-on-dark`). Only these three places choose those tones, so the two bars can be re-tinted
  without touching a partial.
- Both bars are slate tones in each theme, one dark and one light: the sidebar `#334155` with the
  topbar `#E2E8F0` in the light theme, and the sidebar `#1E293B` (a raised panel rather than black)
  with the topbar `#151E31` in the dark theme. That keeps the frame recognisable across the two
  themes, and the page underneath keeps its own contrast.

---

## 3. Shared layout

### 3.1. Page frame (after login)

```
┌──────────────┬──────────────────────────────────────────────────┐
│              │  TOPBAR:  [☰] [🔍 Search customers...] 🔔3  👤 John ▾│
│   SIDEBAR    ├──────────────────────────────────────────────────┤
│   (menu)     │  Breadcrumb:  Home / Customers / ABC             │
│              │  PAGE TITLE                     [Action button]  │
│  Logo        │ ┌──────────────────────────────────────────────┐ │
│  ─────────   │ │                                              │ │
│  Dashboard   │ │             PAGE CONTENT                     │ │
│  Customers   │ │   (cards, filters, tables, forms...)         │ │
│  Contracts   │ │                                              │ │
│  Contacts    │ └──────────────────────────────────────────────┘ │
│  Care        │                                                  │
│  Feedback    │                                                  │
│  Reports     │                                                  │
│  ─────────   │                                                  │
│  Admin       │                                                  │
└──────────────┴──────────────────────────────────────────────────┘
```

### 3.2. Frame components

**Sidebar** (background `--bg-sidebar`, text `--sidebar-fg`, headings `--sidebar-fg-strong`)
- 256px wide, collapsing to 72px (icons only). Selected item: `--primary` background, white text.
- A slate tone (`#334155` light / `#1E293B` dark), separated from the page by a `--sidebar-line` edge.
- The menu is shown by role (see 5.2). The "Admin" item is visible only to Manager, separated by a divider line.
- At the bottom of the sidebar: the user's name and role, and a Log out button.

**Topbar** (64px tall, background `--bg-topbar`, text `--topbar-fg`, bottom border `--topbar-line`)
- The light counterpart of the sidebar tone: a grey bar (`#E2E8F0`) with a darker bottom edge
  (`#CBD5E1`) in the light theme, a dark one (`#151E31`) in the dark theme.
- The controls on it keep the card colour and take their outline from `--topbar-line`, so a chip stays
  visible on the bar in both themes.
- Sidebar collapse/expand button.
- **Quick search box** by customer name, email or phone (calls `sp_SearchCustomer`), showing up to 8 suggestions once you finish typing.
- **Notification bell** showing the total count; clicking it opens a grouped list:
  - Contracts about to expire (`vw_ExpiringContract`)
  - Overdue feedback (`vw_OpenFeedback` with `IsOverdue = 1`)
  - Care appointments overdue and due today (`vw_CareSchedule`)
- User menu: My profile, Change password, Log out.

**Content area**
- Background `--bg-page`, 24px padding, maximum width 1440px.
- First row: breadcrumb, page title and the primary action button (one button in `--primary`, secondary buttons with a grey outline).
- The content sits inside a white rounded card.

**Action notifications**: toast in the top-right corner (green for success, red for error), dismissed automatically after 4 seconds. A business error from the database is never shown as SQL text: the error code (`THROW 500xx`, a CHECK violation, a unique index) is translated into a resource key and the sentence the user reads is in the language that user chose.

### 3.3. Responsive

| Screen | Behaviour |
|---|---|
| ≥ 1280px | Sidebar expanded, tables show all columns |
| 768 – 1279px | Sidebar collapsed to icons, secondary columns hidden in tables |
| < 768px | Sidebar becomes a drawer (tap ☰), tables turn into card lists, forms are single-column |

### 3.4. Shared components (build once, use on many pages)

| Component | Description |
|---|---|
| KPI card | Large number, label, icon, clickable to open the related page |
| Data table | Column sorting, paging 10/20/50, row hover, action column at the end |
| Filter bar | Search box + a few select boxes + Clear filters button, filters remembered in the URL |
| Badge | Per the table in section 2.2 |
| Confirmation dialog | Used for dangerous actions (cancel a contract, deactivate a customer) |
| Form | Label above the input, `*` for required fields, error message right below the input |
| Empty state | Icon + description + action button ("No contracts yet, Add contract") |
| Loading state | Skeleton (dimmed grey block) instead of a full-page spinner |
| Timeline | Used for the customer's interaction history |

---

## 4. Page map (sitemap)

```
/login
/                       → Dashboard
├── /customers                      List
│   ├── /customers/create             Create
│   ├── /customers/{id}             Detail (tabs)
│   └── /customers/{id}/edit         Edit
├── /contracts                        List
│   ├── /contracts/create               Create
│   └── /contracts/{id}               Detail
├── /contacts                         Contact history
├── /care                        Care schedule and care log
├── /feedbacks                        List
│   └── /feedbacks/{id}               Handling detail
├── /reports                         Reports
├── /admin
│   ├── /admin/products           Products / services
│   ├── /admin/employees          Employees and departments
│   ├── /admin/accounts          Accounts
│   └── /admin/audit-logs            System audit log
├── /settings                         System defaults (Manager); language and theme per person
├── /preferences/language             Switch language (any visitor)
├── /preferences/theme                Switch light / dark (any visitor)
├── /profile                           Profile, change password
├── /access-denied                         Access denied (403)
└── 404 · 500
```

---

## 5. Per-page permissions

### 5.1. Matrix

Legend: **X** view, **S** create/edit, **–** no permission.

| Page | Manager | Sales | Support |
|---|---|---|---|
| Dashboard | X | X | X |
| Customers | X, S | X, S | X |
| Contracts | X, S | X, S | X |
| Contacts | X, S | X, add | X, S |
| Care | X, S | X | X, S |
| Feedback | X, S | – | X, S |
| Revenue report | X | X | – |
| Care report | X | – | X |
| Products | X, S | X | X |
| Employees, Accounts, Audit log | X, S | – | – |
| System settings `/settings` | X, S | – | – |
| Language and theme switch | X, S | X, S | X, S |

### 5.2. Menu by role

- **Manager**: Dashboard, Customers, Contracts, Contacts, Care, Feedback, Reports, Admin.
- **Sales**: Dashboard, Customers, Contracts, Contacts, Care (view only), Reports (revenue).
- **Support**: Dashboard, Customers (view only), Contacts, Care, Feedback, Reports (care).

Accessing a page you have no permission for redirects to the 403 page.

---

## 6. Page details

### 6.1. Login `/login`
- Two-column layout: on the left an image / `--primary` colour block with the system name, on the right the form.
- Fields: Username, Password (with a show/hide button). The "Log in" button spans the full width of the column.
- Data: the `ACCOUNT` table (verify the hash, `IsActive = 1`). After logging in, store `EmployeeID` in the session, and on every DB connection open call `sp_set_session_context N'EmployeeID', <EmployeeID>` so the audit log records the right person.
- Errors: "Incorrect username or password" (without saying which one is wrong). A locked account is reported separately.

### 6.2. Dashboard `/`
Purpose: see at a glance what has to be done today.

| Block | Content | Data source |
|---|---|---|
| KPI card row (4–6 cards) | Total customers, New customers in 30 days, Active contracts, Contracts expiring soon, Unresolved feedback (with the number overdue), Revenue this year | `sp_DashboardSummary` |
| Bar chart | Revenue for the 12 months of the selected year | `sp_MonthlyRevenue` |
| "Top customers" table | The 5 customers with the highest total value, with a tier badge | `vw_CustomerRevenue` |
| "Contracts expiring soon" list | Customer, end date, days remaining, Renew button | `vw_ExpiringContract` |
| "Feedback to handle" list | Priority, content, due date, assignee, overdue marker | `vw_OpenFeedback` |
| "Care schedule" list | Overdue / Today / Upcoming | `vw_CareSchedule` |

Displayed by role: Sales sees more contract and revenue blocks; Support sees more feedback and care schedule blocks; Manager sees everything. 12-column grid layout: KPI cards in the top row, the chart takes 8 columns on the left, the lists take 4 columns on the right.

### 6.3. Customer list `/customers`
- **Filters**: search box (name/email/phone), Customer tier, Assigned staff, "Include inactive customers" toggle, "My customers" toggle (filters by `OwnerEmployeeID` = the logged-in user).
- **Table**: Name (with an individual/business icon), Email, Phone, Tier (badge), Assigned staff, Total value, Created date, Actions (View, Edit).
- **Primary button**: "+ Add customer" (Manager, Sales).
- **Data**: `sp_SearchCustomer(@Keyword, @Tier, @OwnerEmployeeID, @ActiveOnly)`.

### 6.4. Add / edit customer
- Choose the **Customer type** first: Individual or Business. When Business is selected, **hide** Gender and Date of birth (matching the `CHK_Customer_Business` constraint).
- Fields: Full name / Company name*, Email, Phone, Gender, Date of birth, Address, Source, Assigned staff.
- Web-side validation: there must be at least an email or a phone number; the phone number contains digits only, 9–11 digits; the email is well-formed and not duplicated; the date of birth must be before today.
- **There is no Customer tier field**: the trigger assigns the tier from the total contract value.

### 6.5. Customer detail `/customers/{id}`
This is the most important page of a customer care system.

**Header (summary card)**: name, tier badge, type, email, phone, address, age (for individual customers, `fn_CustomerAge`), source, assigned staff, total contract value. Buttons: Edit, Add contact, Add care, Deactivate (`sp_SetCustomerStatus`, with a confirmation dialog).

**Tabs**:

| Tab | Content | Source |
|---|---|---|
| Overview | Number of contracts, average satisfaction score, open feedback, next appointment | Computed from the related tables |
| Contracts | The customer's contracts, "+ Add contract" button (customer pre-filled) | `vw_ContractInfo` filtered by `CustomerID` |
| Interaction history | Timeline merging contacts, care and feedback, each with its own icon and colour | `vw_InteractionHistory` filtered by `CustomerID`, sorted newest first |
| Feedback | The customer's feedback and its status | `FEEDBACK` filtered by `CustomerID` |

### 6.6. Contract list `/contracts`
- **Filters**: search by customer name, Status, Staff, Sign date range, quick filter button "Expiring soon (30 days)".
- **Table**: Contract no., Customer, Assigned staff, Sign date, End date (with "N days left", red if under 30 days), Value, Status (badge).
- **Data**: `vw_ContractInfo`; the quick filter uses `vw_ExpiringContract`.

### 6.7. Add contract `/contracts/create`
- General information: Customer* (searchable select box), Signing staff*, Sign date, End date*, Status (Active or Pending signature), Note.
- **Detail line** section: an input table where each row has Product/service (select), Quantity, Unit price (pre-filled from the current price, editable to give a discount), Line total. "+ Add line" button. Selecting the same product twice is not allowed.
- On the right or at the end of the form: **Total value** (computed client-side for preview) and the expected customer tier after saving.
- On Save, the web app sends the details as JSON, for example `[{"ProductID":1,"Quantity":2},{"ProductID":4,"Quantity":1,"UnitPrice":2500000}]`, calls `sp_AddContract`, then redirects to the detail page of the contract just created.
- Common errors to display: end date before the sign date, missing detail lines, duplicate product (error codes 50001, 50009, 50010, 50011, 50015).

### 6.8. Contract detail `/contracts/{id}`
- Info card: customer (click to open the profile), staff, sign date, end date, days remaining, status, note.
- Detail table: Product, Type, Quantity, Unit price, Line total, and a Total row at the bottom of the table (`vw_ContractDetail`).
- Action buttons by status:

| Current status | Buttons shown | Procedure |
|---|---|---|
| Pending signature | Sign contract, Cancel | `sp_SignContract`, `sp_CancelContract` |
| Active | Renew, Cancel | `sp_RenewContract`, `sp_CancelContract` |
| Expired | Renew | `sp_RenewContract` |
| Cancelled | None | – |

- Renew: a dialog to pick the new end date. Cancel: a dialog to enter the reason, red button.
- There should be a "Change history" block taken from `AUDIT_LOG` (TableName = `CONTRACT`).

### 6.9. Contacts `/contacts`
- **Filters**: customer, staff, channel, date range.
- **Table**: Time, Customer, Staff, Channel (phone, email, Zalo, in-person icons), Content, Result.
- **Button**: "+ Record contact" opens a dialog-style form: Customer*, Channel*, Content, Result. Staff and time are taken automatically from the logged-in user and the current time.
- Data: the `CONTACT` table.

### 6.10. Care `/care`
Split into two tabs.

**"Schedule to do" tab** (`vw_CareSchedule`): three groups Overdue (red), Today (orange), Upcoming 7 days (blue). Each row has the customer name, phone (tap to call on a phone), type, content, appointment date, and a "Mark as cared for" button (opens a form creating a new `CARE` record for that customer).

**"Care log" tab**: the `CARE` table with filters by customer, staff, type and date range.

**Add care form**: Customer*, Type* (Renewal, Upsell, Birthday, Survey, Renewal reminder, Other), Content, Result, Next appointment date. The **Satisfaction score (1–5, as 5 stars) field appears only when Type = Survey** (matching `CHK_Care_SatisfactionScore`). The appointment date must not be before the care date.

### 6.11. Feedback / complaints `/feedbacks`
- **Counter cards at the top**: New, In progress, Overdue (click to filter quickly).
- **Table**: Priority level, Customer, Type, Truncated content, Status, Assignee (or "Unassigned" with an Assign button), Due date (red if overdue).
- By default it shows unresolved feedback, sorted by priority level and then by due date (`vw_OpenFeedback`). A filter allows viewing "Resolved" and "Closed" as well.
- **Detail `/feedbacks/{id}`**: full content, customer information, assignee, due date, resolution date, note. Actions:
  - Assign a staff member (`sp_AssignFeedback`, automatically moves to "In progress")
  - Change status (`sp_UpdateFeedbackStatus`, the resolution date is filled in by the system)
  - Add a note
- Rule to tell the user: you cannot move to "In progress", "Resolved" or "Closed" before a staff member is assigned (error code 50004).
- New feedback form: Customer*, Type* (Complaint, Suggestion, Comment, Compliment), Content, Priority level, Due date.

### 6.12. Reports `/reports`
Split into tabs by permission:

| Tab | Content | Source |
|---|---|---|
| Revenue | Select a year, bar chart by month, table of contract counts and revenue per month, yearly total | `sp_MonthlyRevenue` |
| Customers | Ranking table by total value, filter by tier, pie chart of customer tier proportions | `vw_CustomerRevenue` |
| Care | Number of care visits by month and type, average satisfaction score | `sp_MonthlyCareStats` |

Each tab has an "Export Excel/CSV" button.

### 6.13. Admin (Manager only)

| Page | Content |
|---|---|
| Products / services | `PRODUCT_SERVICE` CRUD table: Name*, Type*, Unit price*, Description. Other roles are view-only. Do not delete a product that already appears in a contract; hide it instead if necessary. |
| Employees and departments | `EMPLOYEE` table with department and position. Each department has only 1 Head of department (show a friendly error if this is violated). |
| Accounts | Create an account for a staff member (one account each), assign a role, lock/unlock (`IsActive`), reset the password. Never display the hash. |
| System audit log | `AUDIT_LOG` table with filters by staff, table, action and time range. |
| System settings `/settings` | The system-wide defaults (`Language`, `Theme`) from `SETTINGS`, one save button per row, read through `vw_SystemSettings`. The value an individual employee chose for themselves is that person's own business and is **not** listed here; `vw_EffectiveSettings` is what tells the application what the current user gets. |

### 6.14. My profile `/profile`
View your own information, change password (enter the old password, the new password, then confirm). The new password is hashed with bcrypt or ASP.NET Identity, not SHA2 like the demo data.

### 6.15. Error pages
- **403** (`/access-denied`): "You do not have permission to access this page" with a button back to the Dashboard.
- **404**: "Page not found" with a button back to the Dashboard.
- **500**: a generic message that does not reveal technical details.

---

## 7. Main workflows (to check when demoing)

1. **New customer**: Customers, Create, Record contact, Add contract. The customer tier updates by itself from the contract value.
2. **Contract about to expire**: Dashboard or the notification bell, open the contract, Renew. The contract goes back to "Active".
3. **Complaint**: Receive feedback, Assign, In progress, Resolved (resolution date filled in automatically), review it in the customer's timeline.
4. **Periodic care**: Care schedule, Mark as cared for (set the next appointment date), satisfaction survey, see the score in the report.
5. **Permissions**: log in in turn as `john` (Manager), `mary` (Sales), `david` (Support) to show the different menus and permissions.

---

## 8. Suggested order of work

| Phase | Work | Note |
|---|---|---|
| 1 | Project skeleton, DB connection, login, permissions, shared layout (sidebar, topbar) | Do this first so every later page can reuse it |
| 2 | Shared components (table, filter bar, badge, form, toast) | Saves effort later |
| 3 | Customers: list, add/edit, detail | The central page |
| 4 | Contracts: list, create, detail, sign/renew/cancel | The most logic-heavy |
| 5 | Contacts, Care, Feedback | |
| 6 | Dashboard and Reports | Do last, once real data exists |
| 7 | Admin, audit log, error pages, responsive polish | |

---

## 9. Checklist before submission

- [ ] Every page checks permissions on the server side.
- [ ] Passwords hashed with bcrypt / Identity, never stored in plain text, no unsalted SHA2.
- [ ] Every query uses parameters (no SQL string concatenation) to avoid SQL injection.
- [ ] Every DB connection sets `SESSION_CONTEXT('EmployeeID')` so the audit log records the right person.
- [ ] Customer deletion uses "Deactivate" (`sp_SetCustomerStatus`), never a hard delete.
- [ ] SQL error messages are translated into a clear sentence in the language the user chose, never shown as raw SQL text or a constraint name.
- [ ] The interface is usable on a phone.

---

## 10. Architecture and API

> ⚠ **OUTDATED.** The application is now **a single ASP.NET Core MVC project**: controller actions replace REST endpoints, and the login cookie replaces JWT. This section is kept only as a reference for the role column and for error handling. See the `ccm-mvc-architecture` and `ccm-auth-security` skills.

### 10.1. Model

```
Browser ──► web (UI) ──► api (REST, JSON) ──► db (SQL Server)
                           │
                           └─ verify JWT + role, call views / stored procedures
```

- The UI **never** connects straight to the database; all data goes through the API.
- The API is the only place that checks permissions (see the matrix in section 5). The UI only hides buttons for tidiness.
- The API uses its own SQL login `app_ccm` (file `04_CreateAppUser.sql`), not `sa`.

### 10.2. Shared conventions

| Item | Convention |
|---|---|
| Base address | `/api/v1` |
| Format | JSON, camelCase field names, dates in ISO 8601 (`2026-10-13`), money as a number (not a string) |
| Paging | `?page=1&pageSize=20`, returns `{ items, total, page, pageSize }` |
| Sorting / filtering | `?sort=ngayKy:desc`, the filters are query string |
| Authentication | `Authorization: Bearer <JWT>`; the JWT contains `employeeID` and `vaiTro`, valid for 30–60 minutes |
| Errors | `{ "code": "50004", "message": "Feedback must be assigned...", "errors": { "tenTruong": ["..."] } }` |

**Mapping SQL errors to HTTP**

| DB error | HTTP | Meaning |
|---|---|---|
| 50001, 50008, 50009, 50010, 50011, 50012, 50015 | 400 | The submitted data is invalid |
| 50002, 50003, 50006, 50007, 50013 | 404 | Object not found |
| 50004, 50005, 50014 | 409 | The current status does not allow the operation |
| 547 (CHECK / foreign key violation) | 400 | The data violates a constraint |
| 2601, 2627 (duplicate unique key) | 409 | Duplicate data (email, phone, username...) |
| No token / expired | 401 | Login required |
| Wrong role | 403 | No permission |

**Important for the audit log**: every time a connection is taken from the pool, the API must run `EXEC sp_set_session_context N'EmployeeID', <employeeID from the JWT>` before executing any other statement. If it is forgotten, the `AUDIT_LOG` table records `EmployeeID = NULL`.

### 10.3. Endpoint list

The "Role" column: **MG** Manager, **SL** Sales, **SP** Support.

**Authentication**

| Method | Path | Role | Data source |
|---|---|---|---|
| POST | `/auth/login` | Public | `ACCOUNT` (verify the hash, `IsActive = 1`) |
| GET | `/auth/me` | All roles | Information about the logged-in user |
| PUT | `/auth/password` | All roles | Change password |

**Dashboard and notifications**

| Method | Path | Role | Data source |
|---|---|---|---|
| GET | `/dashboard/summary` | All roles | `sp_DashboardSummary` |
| GET | `/dashboard/revenue?year=` | MG, SL | `sp_MonthlyRevenue` |
| GET | `/dashboard/top-customers?limit=5` | MG, SL | `vw_CustomerRevenue` |
| GET | `/notifications` | All roles | Counts from `vw_ExpiringContract`, `vw_OpenFeedback`, `vw_CareSchedule` |

**Customers**

| Method | Path | Role | Data source |
|---|---|---|---|
| GET | `/customers?q=&group=&ownerId=&includeInactive=` | All roles | `sp_SearchCustomer` |
| GET | `/customers/{id}` | All roles | `CUSTOMER` + `fn_CustomerAge` |
| POST | `/customers` | MG, SL | Insert into `CUSTOMER` |
| PUT | `/customers/{id}` | MG, SL | Update `CUSTOMER` (`Tier` cannot be edited) |
| PATCH | `/customers/{id}/status` | MG, SL | `sp_SetCustomerStatus` |
| GET | `/customers/{id}/timeline` | All roles | `vw_InteractionHistory` |
| GET | `/customers/{id}/contracts` | All roles | `vw_ContractInfo` |

**Contracts**

| Method | Path | Role | Data source |
|---|---|---|---|
| GET | `/contracts?status=&staffId=&from=&to=&q=` | All roles | `vw_ContractInfo` |
| GET | `/contracts/expiring` | All roles | `vw_ExpiringContract` |
| GET | `/contracts/{id}` | All roles | `vw_ContractInfo` + `vw_ContractDetail` |
| POST | `/contracts` | MG, SL | `sp_AddContract` (the body has an `items` array, the API turns it into JSON for the procedure) |
| POST | `/contracts/{id}/sign` | MG, SL | `sp_SignContract` |
| POST | `/contracts/{id}/renew` | MG, SL | `sp_RenewContract` |
| POST | `/contracts/{id}/cancel` | MG, SL | `sp_CancelContract` |

**Contacts and care**

| Method | Path | Role | Data source |
|---|---|---|---|
| GET | `/contacts` | All roles | `CONTACT` |
| POST | `/contacts` | All roles | Insert into `CONTACT` (`employeeID` and the time come from the login session) |
| GET | `/care/schedule` | All roles | `vw_CareSchedule` |
| GET | `/care` | All roles | `CARE` |
| POST | `/care` | MG, SP | Insert into `CARE` |
| PUT | `/care/{id}` | MG, SP | Update `CARE` |

**Feedback**

| Method | Path | Role | Data source |
|---|---|---|---|
| GET | `/feedback?status=&priority=&overdue=` | MG, SP | `vw_OpenFeedback` or `FEEDBACK` |
| GET | `/feedback/{id}` | MG, SP | `FEEDBACK` |
| POST | `/feedback` | MG, SP | Insert into `FEEDBACK` |
| POST | `/feedback/{id}/assign` | MG, SP | `sp_AssignFeedback` |
| PATCH | `/feedback/{id}/status` | MG, SP | `sp_UpdateFeedbackStatus` |

**Reports**

| Method | Path | Role | Data source |
|---|---|---|---|
| GET | `/reports/revenue?year=` | MG, SL | `sp_MonthlyRevenue` |
| GET | `/reports/customers` | MG, SL | `vw_CustomerRevenue` |
| GET | `/reports/care?year=` | MG, SP | `sp_MonthlyCareStats` |

**Admin**

| Method | Path | Role | Data source |
|---|---|---|---|
| GET | `/products` | All roles | `PRODUCT_SERVICE` |
| POST, PUT | `/products`, `/products/{id}` | MG | `PRODUCT_SERVICE` |
| GET, POST, PUT | `/employees`, `/employees/{id}` | MG | `EMPLOYEE` |
| GET | `/departments` | All roles | `DEPARTMENT` |
| GET, POST | `/accounts` | MG | `ACCOUNT` (never return the hash) |
| POST | `/accounts/{id}/reset-password`, `/accounts/{id}/lock`, `/accounts/{id}/unlock` | MG | `ACCOUNT` |
| GET | `/audit-logs?staffId=&table=&from=&to=` | MG | `AUDIT_LOG` |

### 10.4. Security rules for the API

- Every query uses parameters, with no SQL string concatenation.
- CORS only allows the address of the web app (`CORS_ORIGIN`).
- Repeated failed logins lead to a temporary lockout (rate limited by IP and by account).
- Run HTTPS in production (put a reverse proxy such as Nginx or Caddy in front).
- Never write passwords, tokens or hashes to the log.

---

## 11. Docker

> ⚠ **OUTDATED.** There is no longer a split between `api` and `web`; only **one** MVC `web` service remains. See the `ccm-docker-deploy` skill.

### 11.1. Services

| Service | Role | Port (your machine) |
|---|---|---|
| `db` | SQL Server 2022, data stored in the `sqldata` volume | 1433 (dev only) |
| `db-init` | Runs once: creates the DB from the 4 SQL files, loads the sample data, creates the `app_ccm` user. Skipped if the DB already exists | none |
| `api` | REST API, waits for `db-init` to finish before starting | 5000 |
| `web` | UI, calls the API through `API_URL` | 8080 |

The skeleton lives in the `ccm-project/` folder:

```
ccm-project/
├── docker-compose.yml
├── .env.example          (copy to .env, change the password)
├── db/
│   ├── 01_CreateTables.sql               ← copied from the existing build
│   ├── 02_Function_View_Proc_Trigger.sql ← copied from the existing build
│   ├── 03_SampleData.sql                 ← copied from the existing build
│   ├── 04_CreateAppUser.sql
│   └── init.sh
├── api/   (Dockerfile + source code)
└── web/   (Dockerfile + source code)
```

### 11.2. Common commands

```bash
cp .env.example .env                  # then edit the password in .env
docker compose up -d --build          # build and run everything
docker compose logs -f db-init        # watch the DB initialisation
docker compose logs -f api            # watch the API log
docker compose down                   # stop (keep the data)
docker compose down -v                # stop and DELETE the data (to recreate the DB from scratch)
```

### 11.3. Important notes

- **The SA password must be strong** (at least 8 characters, with upper case, lower case, digits and symbols), otherwise the `db` container shuts itself down.
- **`sqlcmd` needs the `-I` flag** (already present in `init.sh`). `sqlcmd` disables `QUOTED_IDENTIFIER` by default, while file 01 creates filtered indexes (`WHERE`); without this flag it reports an error.
- **The `-f 65001` flag** so that the non-ASCII text in the SQL files is read correctly.
- **Windows**: `init.sh` must be saved with LF line endings, not CRLF, otherwise bash inside the container reports an error. Add a `.gitattributes` file containing `*.sh text eol=lf`.
- **Apple silicon Macs (M1/M2/M3...)**: the SQL Server image is x86 only, so Docker Desktop runs it through emulation (turn on "Use Rosetta" in the settings). It works but starts more slowly, and Docker needs at least 4 GB of RAM.
- The sample data computes dates at the moment `03_SampleData.sql` runs. To refresh the time anchors, run `docker compose down -v` and then `up` again.
- The DB server name inside the Docker network is `db` (not `localhost`). From your machine (SSMS, Azure Data Studio) use `localhost,1433`.
- In production: remove the `ports: 1433` line from `db`, do not commit `.env`, and add an HTTPS reverse proxy.

### 11.4. Additional checklist

- [ ] `docker compose up -d --build` runs cleanly on a new machine, with no manual steps.
- [ ] `docker compose logs db-init` shows the line "OK: sample data loaded".
- [ ] The API does not use the `sa` account.
- [ ] `.env` is in `.gitignore`.
- [ ] Every DB connection sets `SESSION_CONTEXT('EmployeeID')`.
- [ ] API documentation is available (Swagger/OpenAPI or the table in section 10.3) to submit with the project.
