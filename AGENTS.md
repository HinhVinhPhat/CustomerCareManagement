# Agent Guide - Customer Care Management System

## 1. Context and required reading

Talk to the user in Vietnamese (their working language). In the repository the split is:
**code, SQL identifiers, comments and documents are English, while everything shown on screen and
the business data rows are Vietnamese**. The application carries an English/Vietnamese switch,
defaults to Vietnamese and keeps English as the second language. The exact split, the resource
files and the glossary are in the `ccm-language` skill - read it before writing any visible text
or sample data. This is a SQL Server / Database course project: an ASP.NET Core MVC web
application plus SQL Server, built by a single developer, module by module. All paths below are
relative to the project root.

The key point of the project: **the database holds the business logic** (triggers,
procedures, views, audit log). The application only calls into it and never re-implements it.

| When | Must read |
|---|---|
| Starting a work session, before writing requirements/plan for a module | `docs/design.md` (sections 1-9: pages, permissions, UI), skill `ccm-workflow` |
| Before coding any module or feature | The matching part of `docs/design.md`, `.agents/skills/ccm-database/references/schema.md` |
| Before designing or changing a View, layout or theme | `docs/design.md` section 2 (colours, fonts, badges), `docs/mockups/static-mockups/` to see the reference look |
| Before writing any user-visible text, resource key or sample data row | skill `ccm-language` |
| Before changing SQL or reading data | `database/*.sql` and `.agents/skills/ccm-database/SKILL.md` |

Note: `docs/design.md` section 10 (REST API, JWT) and section 11 (Docker with `api` +
`web`) are **outdated** because the application is now a single MVC project and controller
actions replace endpoints. The files under `docs/mockups/static-mockups/` are a visual
reference only (static HTML, fake data), not the real application. When documents
contradict each other, point out the contradiction and ask the user instead of choosing on
your own. Do not treat a procedure, field or page as "already there" unless you have seen
it in `database/*.sql`.

## 2. Local skills and how to pick them

Skills live in `.agents/skills/` as plain markdown files. Tools that support this folder
discover them automatically; otherwise **read `SKILL.md` directly using the path in the
table**, together with the files under `references/` that it mentions. Load only the skill
that fits the task, but once loaded read it in full.

| Skill | Apply when | Expected outcome |
|---|---|---|
| `ccm-database` (`.agents/skills/ccm-database/SKILL.md`) | Writing any query, Service or screen that touches data; understanding tables, views, procedures, triggers, error codes; running the 4 SQL files | Use the right DB objects, never write columns owned by triggers |
| `ccm-mvc-architecture` (`.agents/skills/ccm-mvc-architecture/SKILL.md`) | Creating the project, scaffolding the DbContext, writing controllers, Services, entities, ViewModels, Program.cs; `SaveChanges` errors | Correct folder structure, EF database-first, audit log records the right user, SQL errors become business messages in the user's language |
| `ccm-auth-security` (`.agents/skills/ccm-auth-security/SKILL.md`) | Login, passwords, permissions, accounts, security | Cookie auth, move SHA2 hashing to PasswordHasher, `[Authorize]` on the right roles, CSRF/XSS protection |
| `ccm-web-ui` (`.agents/skills/ccm-web-ui/SKILL.md`) | Creating or editing `.cshtml`, `_Layout`, `site.css`; porting a page from the reference | Razor UI matching `design.md`, role-based menu, forms matching the DB constraints |
| `ccm-language` (`.agents/skills/ccm-language/SKILL.md`) | Writing or changing any visible text, a resource key, sample data, or the language switch | Vietnamese on screen and in the data, English in the code, both `.resx` files in step |
| `ccm-docker-deploy` (`.agents/skills/ccm-docker-deploy/SKILL.md`) | Docker, compose, `database/init.sh`, running on another machine | Restart without data loss, the app never uses `sa` |
| `ccm-workflow` (`.agents/skills/ccm-workflow/SKILL.md`) | Starting a big module, planning, testing, bugs, reporting | Requirements + plan approved, tested on real SQL Server, honest `test-results.md` |

This workflow is added to the system/tool instructions; if they conflict, follow the
higher-level instruction and state clearly which step needs adjusting.

### Module workflow (summary, details in `ccm-workflow`)

- Split into M01-M07 (foundation, customers, products and contracts, contacts/care/feedback,
  dashboard and reports, administration, Docker and finishing). Work on one module at a time.
- Write `requirements.md` and `plan.md` for the module under `docs/tasks/mXX-<slug>/` and get
  the user to approve them **once**. No approval, no code.
- Test only after implementation; test on real SQL Server with a separate test DB, and store
  `test-results.md`. Record FAIL honestly, never tick a case you did not run, never edit
  source during the test round.
- Record bugs in `bug-report.md`; the user approves the whole batch before you fix them and
  re-run the affected cases. A new bug outside an approved batch must be raised for approval.
- Anything out of scope or any business change: stop and ask for approval of the extra work.
  Small, obvious tasks do not need the whole workflow.

## 3. Architecture and code rules

One ASP.NET Core MVC project under `src/`, structured like the WebBanSach sample:

```
src/CcmWeb/
  Controllers/     thin controllers
  Helpers/         Utils.cs, WorkConstants.cs
  Models/EF/       ApplicationDbContext (scaffolded) + ApplicationDbContext.Custom.cs (partial)
  Models/Entities/ entities and views generated by the scaffold
  Models/ViewModels/
  Services/        IxxxServices + xxxServices
  Views/           Razor, Shared/_Layout.cshtml
  wwwroot/
tests/CcmWeb.Tests/  xUnit
```

- URLs follow the sitemap in `docs/design.md` section 4 (`/customers`, `/contracts/create`...),
  using attribute routing.
- Controller: receive the request, check permission and `ModelState`, call a Service, return a
  View/Redirect. No SQL, no business rules.
- Service: use `ApplicationDbContext`, call views/procedures, normalise input data, translate
  SQL errors into English `BusinessException` messages. Never return `IQueryable` or tracked
  entities to the controller. Views contain no queries or business logic.
- EF Core **database-first**: generate models with `dotnet ef dbcontext scaffold`, **no
  migrations, no `EnsureCreated`**. Do not hand-edit scaffolded files; put customisations in
  `ApplicationDbContext.Custom.cs` and partial classes.
- Do not add on your own: generic repository, UnitOfWork, empty interfaces, scaffolding for
  future modules, Dapper, AutoMapper, MediatR, a separate API server. Ask the user before
  adding a new library.
- Use **.NET 10 (LTS)** for `src/CcmWeb` (EF Core 8 or later is needed for
  `UseSqlOutputClause` and `SqlQueryRaw`; the project uses EF Core 10); once a `.csproj`/`global.json`
  exists, follow that file and do not change the framework on your own. Configure with User Secrets
  (dev) or environment variables; the connection string is named `DefaultConnection`, uses the login
  `app_ccm`, and **never `sa`**.
- Data types: money is `decimal`; DATE columns are `DateOnly`; DATETIME is `DateTime`; phone
  numbers are strings; an empty optional field becomes NULL.
- Queries are async; always parameterised; never concatenate user data into SQL. Lists of
  valid values (roles, statuses, types) live in `Helpers/WorkConstants.cs` and match the DB
  CHECK constraints.
- Hash passwords with `PasswordHasher` (package `Microsoft.Extensions.Identity.Core`). Never
  write passwords, hashes or cookies to logs or Views.
- Check permissions with `[Authorize(Roles = ...)]` on the controller/action, not merely by
  hiding a button. The actor's `employeeID` and role come from the authenticated claim, never
  from the form.
- Keep the existing naming/style, change only the lines the task needs; clean up only what
  your own change made redundant. Do not refactor related areas on a hunch.

## 4. SQL and business invariants

The four `database/` files run in the order `01` -> `02` -> `03` -> `04`; file 01 **drops and
recreates** the DB. Reading the source does not mean the engine has been verified. To extend
the schema, edit the SQL files, run them again, re-scaffold and update
`ccm-database/references/schema.md`.

- **Never write** `CONTRACT.TotalValue`, `CUSTOMER.Tier`, `FEEDBACK.ResolvedDate` or
  `AUDIT_LOG` from the application; triggers own them.
- Adding/signing/renewing/cancelling a contract goes only through `sp_AddContract`,
  `sp_SignContract`, `sp_RenewContract`, `sp_CancelContract`. Assigning feedback / changing
  its status goes through `sp_AssignFeedback`, `sp_UpdateFeedbackStatus`. Deactivating /
  reactivating a customer goes through `sp_SetCustomerStatus`. Never hard-delete a customer.
- Dashboards, lists and reports read from `vw_*` or `sp_*`; do not rewrite an existing JOIN.
- **Tables with triggers and EF Core:** EF writes `OUTPUT` by default, which causes error 334
  on `SaveChanges` against `CONTRACT`, `CONTRACT_DETAIL`, `CUSTOMER`, `FEEDBACK`. Configure it
  in `OnModelCreatingPartial` (sample in `ccm-mvc-architecture/references/code-mau.md`) and
  actually try it once.
- **Audit log:** every time EF opens a connection it must run
  `sp_set_session_context N'EmployeeID', <employeeID from the claim>` (through a
  `DbConnectionInterceptor`), otherwise `AUDIT_LOG` records a NULL actor.
- `sp_ExpireContracts` does not run by itself (there is no SQL Agent): the application calls it
  at start-up and once a day via a `BackgroundService`.
- The "feedback needs an assignee" rule reaches the app through two error codes: 50004 (via a
  procedure) and 547 (direct table writes). Translate errors using the code table in
  `ccm-database/references/schema.md`; for an unrecognised error, log it on the server, show a
  generic message in the user's language and never expose SQL details.
- Do not add refunds, online payments, multiple branches or features beyond `design.md`
  without approval.
- Test transactions and triggers on **real SQL Server** with a separate test DB. Do not
  substitute EF InMemory or SQLite (they do not run triggers/procedures). Never write tests
  into the live DB.

## 5. Shared UI and required UX

- Razor UI follows `docs/design.md` and the reference style (Tailwind, Font Awesome, the Be
  Vietnam Pro font, colours via CSS variables in `site.css`). No hard-coded colours in
  individual pages.
- **Split every page into sections.** The page `.cshtml` holds the header and the `<partial>`
  calls only; filters, tables, forms, summary cards, tab panels and modals are separate partial
  views. Never build a page as one long file, and never copy a block between pages. `_Layout` is
  only the shell (sidebar, topbar, notification bell, `@RenderBody()`).
- **Language: Vietnamese by default, English as the second language.** Every visible string comes
  from a resource key in `Resources/SharedResource*.resx`, never typed straight into a page; coded
  values from the database are shown through their label helper. Dates `dd/MM/yyyy`; times `HH:mm`;
  money right-aligned (`80.000.000 VNĐ` in Vietnamese, `80,000,000 VND` in English). Details and the
  glossary are in the `ccm-language` skill. Pass properly typed values to the DB, never a
  pre-formatted string.
- `@Model.X` HTML-encodes automatically. **Never use `Html.Raw` with user-entered data.** Every
  POST form carries an anti-forgery token.
- Handle states separately: loading, empty, business error, success. A business error keeps the
  entered data and shows the message next to the field, in the user's language. Do not show a fake
  total of 0 when the DB failed.
- A status uses text or an icon together with colour, not colour alone. Dangerous actions
  (cancelling a contract, deactivating a customer) require confirmation.
- Forms match the DB constraints: Business customers hide gender/date of birth; there is no
  customer-tier field; the satisfaction score appears only for a Survey; at least an email or
  a phone number is required.
- Responsive down to phone size. Details in `ccm-web-ui`.

## 6. Git and hand-over report

Run `git rev-parse --show-toplevel` before any Git operation. Never commit `.env`, passwords,
User Secrets, `bin/`, `obj/` or backups containing real data. Never reset or clean the user's
changes. Commit only when the user asks, one feature per commit, with a clear message.

Hand over briefly: what was done, the main paths, the verification results that were **actually
run**, the BLOCKED/NOT RUN items, and the step awaiting user approval. Never mark a module done
just because code or docs were written.

## Demo accounts

Password `123456`: `john`, `susan` (Manager), `mary`, `kevin` (Sales), `david`, `helen`
(Support). For development only.
