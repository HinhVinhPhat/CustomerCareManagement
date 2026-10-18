# Customer Care Management System

A Database / SQL Server course project: an ASP.NET Core MVC web app (EF Core
database-first) on SQL Server.

## Layout

| Folder / file | Contents |
|---|---|
| `AGENTS.md` | Guide for AI assistants (rules, skills, workflow) |
| `.agents/skills/` | Topic skills: database, MVC architecture, auth, UI, Docker, workflow |
| `database/` | 4 SQL files (`01` to `04`) and `init.sh` for Docker |
| `docs/design.md` | Page, permission and UI specification |
| `docs/mockups/static-mockups/` | Static UI build kept as a visual reference |
| `CcmWeb.sln` | Solution (classic format) |
| `src/CcmWeb/` | Application source (ASP.NET Core MVC, `net10.0`, EF Core database-first) |
| `tests/CcmWeb.Tests/` | xUnit tests; the database tests rebuild `CustomerCareDB_Test` from `database/*.sql` |
| `tools/` | Helper scripts |
| `docker-compose.yml`, `.env.example` | Run SQL Server with Docker |

## Build the database

Run `database/01` -> `02` -> `03` -> `04` in order using SSMS or `sqlcmd` (needs
`-I -f 65001`, add `-C` in dev). File 01 drops and recreates the DB.

Or use Docker: `cp .env.example .env`, edit the passwords, then `docker compose up -d`.

File `04` needs the application password as a script variable:

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -C -I -f 65001 -b -v APP_PASSWORD="App_Str0ng!Pass2026" -i database\04_CreateAppUser.sql
```

## Run the application

The connection string lives in User Secrets, never in `appsettings.json`:

```powershell
cd src/CcmWeb
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=CustomerCareDB;User Id=app_ccm;Password=App_Str0ng!Pass2026;TrustServerCertificate=True"
dotnet run
```

Then open `http://localhost:5xxx/login`. Demo accounts (password `123456`): `john`, `susan`
(Manager), `mary`, `kevin` (Sales), `david`, `helen` (Support). The first sign-in of an account
replaces the demo SHA2 hash with a `PasswordHasher` hash.

If the setting is missing - a fresh clone, another machine - the application falls back to the
connection string written in `ApplicationDbContext.FallbackConnectionString`
(`Server=localhost\SQLEXPRESS;Database=CustomerCareDB;Trusted_Connection=True`), which uses Windows
Authentication and therefore needs no password in the repository.

## Vietnamese UI, English under the hood

The interface is Vietnamese by default and can be switched to English with the `VI` / `EN` buttons in
the topbar; the light/dark theme is chosen next to them. Both choices are remembered for the signed-in
account (a row in the `SETTINGS` table) and follow a user to another browser.

A Manager can change the **system defaults** on `/settings`, which is also the page that shows what
each employee actually gets. Every string a user reads comes from `src/CcmWeb/Resources/SharedResource.resx`
(Vietnamese, the neutral file) and `SharedResource.en.resx`; the database keeps the English codes and
Vietnamese business data. Details: the `ccm-language` skill.

## Run the tests

```powershell
dotnet test tests\CcmWeb.Tests\CcmWeb.Tests.csproj
```

The tests need the local SQL Server (`.\\SQLEXPRESS`; override with the `CCM_TEST_SERVER`
environment variable). They rebuild `CustomerCareDB_Test` from `database/01`-`03` and never write to
the application database.

Demo accounts (password `123456`): `john`, `susan` (Manager), `mary`, `kevin` (Sales),
`david`, `helen` (Support).

## Working with an AI assistant

Everything is plain markdown, so it works across tools:

| Tool | File it reads |
|---|---|
| Claude Code | `CLAUDE.md` (imports `AGENTS.md`); to auto-activate skills, copy `.agents/skills/*` into `.claude/skills/` |
| OpenAI Codex, OpenCode and many other agents | `AGENTS.md`, the `.agents/skills/` folder |
| Cursor | `.cursor/rules/ccm.mdc` and `AGENTS.md` |
| GitHub Copilot | `.github/copilot-instructions.md` |
| Gemini CLI | `GEMINI.md` |

How each tool reads files can change between versions; if it does not read them by itself,
tell it: "Read AGENTS.md and follow it".

**Chat-only AI** (ChatGPT, Gemini, Claude on the web): paste `AGENTS.md` together with **only
the skill for the task at hand** (never all of them), plus the relevant part of `docs/design.md`
or the file you are editing. Sample opener: "I am working on a customer-care course project.
Here is AGENTS.md and the guide for the next part. Follow them, and if information is missing
ask me instead of guessing. The task is: ..."

## First thing to hand to the AI

"Read AGENTS.md and the ccm-workflow skill. Start module M01 Foundation: write
requirements.md and plan.md for me to approve, do not write code yet."
