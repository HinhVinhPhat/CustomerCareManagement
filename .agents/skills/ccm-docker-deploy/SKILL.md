---
name: ccm-docker-deploy
description: Packaging and running the customer care management system with Docker Compose - the db service (SQL Server 2022), db-init (loads the 4 SQL files once, without wiping data on restart), web (ASP.NET Core MVC); the .env variables, connection string, healthcheck, sqlcmd with the -I and -f 65001 flags, keeping login sessions, and common problems on Windows and Apple-silicon Macs. Use this skill when writing or editing docker-compose.yml, the Dockerfile, database/init.sh or .env.example, when the db container keeps stopping, when text in the DB is garbled, when db-init reports an error, or when the user mentions Docker, compose, container, deployment or running on another machine.
---

# Docker for the customer care system

Read `ccm-database` for the four SQL files and their run order; the app structure is in
`ccm-mvc-architecture`. Section 11 of `docs/design.md` describes a separate `api` + `web` model,
which is outdated: the application is now **one** MVC project, so there is only one `web` service.

## Service layout

| Service | Job | Notes |
|---|---|---|
| `db` | SQL Server 2022, data in the `sqldata` volume | Has a healthcheck. Port 1433 is dev only |
| `db-init` | Runs once: loads `database/01` to `04` | Waits for `db` to be healthy, does not restart |
| `web` | ASP.NET Core MVC | `depends_on: db-init` with `condition: service_completed_successfully` |

The templates already exist in the project skeleton: `docker-compose.yml` and `.env.example` at the
project root, and `database/init.sh`. Only `db` and `db-init` are enabled for now; the `web` service
is commented out until `src/CcmWeb` exists with a Dockerfile. These files have not been run on a real
machine yet: try them and adjust as needed.

## The biggest trap: file 01 drops the database

`database/01_CreateTables.sql` starts by running `DROP DATABASE CustomerCareDB` if the DB already
exists. If `db-init` ran this file on every `docker compose up`, **all the data would be lost on
every restart**. `init.sh` must check first:

- DB does not exist: run 01, 02, 03.
- DB exists: **skip** 01 to 03.
- File 04 (creating the `app_ccm` login) uses `IF NOT EXISTS`, so it is safe to re-run and always runs.

To refresh the sample data (its dates are relative to the moment file 03 runs), use
`docker compose down -v` and `up` again.

## sqlcmd: three mandatory flags

- `-I`: enables `QUOTED_IDENTIFIER`. File 01 creates a filtered index (`WHERE`), which fails without it.
- `-f 65001`: reads the SQL file as UTF-8; without it, non-ASCII text turns into garbage.
- `-C`: trusts the container's self-signed certificate (the `mssql-tools18` tool requires encryption
  by default). SQL Server 2022 images use `/opt/mssql-tools18/bin/sqlcmd`; older images use
  `/opt/mssql-tools/bin/sqlcmd` and do not need `-C`. Check the real path inside the image you use.

File 04 needs a variable: `-v APP_PASSWORD="$APP_PASSWORD"`. That password must not contain a single
quote (`'`), because it is embedded in SQL text, and must be strong (at least 8 characters with upper
case, lower case, digits and symbols) because the login uses `CHECK_POLICY = ON`. Since it also
appears in the `web` connection string, **also avoid the semicolon (`;`) and the `$` character**.

## The web service (once the source exists)

A multi-stage Dockerfile in `src/CcmWeb`: `FROM mcr.microsoft.com/dotnet/sdk:<version> AS build` to
run `dotnet publish -c Release`, then `FROM mcr.microsoft.com/dotnet/aspnet:<version>` to run the
result. Use exactly the .NET version the project targets (check the `.csproj` and `global.json`),
never change it on your own. A service template for compose:

```yaml
  web:
    build: ./src/CcmWeb
    depends_on:
      db-init:
        condition: service_completed_successfully
    environment:
      ConnectionStrings__DefaultConnection: "Server=db,1433;Database=CustomerCareDB;User Id=app_ccm;Password=${APP_PASSWORD};TrustServerCertificate=True"
      ASPNETCORE_URLS: http://+:8080
    ports:
      - "8080:8080"
```

- The `__` in an environment variable name maps to a `:` in .NET configuration, so it matches
  `GetConnectionString("DefaultConnection")`.
- The DB host is `db` (the service name), not `localhost`. From your machine (SSMS, Azure Data
  Studio) use `localhost,1433` instead.
- The app connects as `app_ccm`, **never `sa`**. `sa` is only used inside `db-init`.
- The `aspnet` image from .NET 8 onwards listens on port 8080 by default (not 80).
- **Keep login sessions:** the cookie encryption key (Data Protection) is lost when the container
  restarts, signing everyone out. Persist the keys to a volume with
  `AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("/keys"))` and mount a volume at `/keys`.
- When `web` runs in a container, the `SessionContextInterceptor` and the background work (see
  `ccm-mvc-architecture`) work as usual, since they only need a DB connection.

## Passwords and configuration

- `MSSQL_SA_PASSWORD` must be strong, otherwise the `db` container stops immediately on start-up.
- The secrets (`SA_PASSWORD`, `APP_PASSWORD`) live in `.env`, which is **not** committed (it is in
  `.gitignore`); only `.env.example` with fake values is committed.

## Common problems

- **Windows**: `init.sh` must use LF line endings. With CRLF, bash inside the container reports odd
  errors like `\r: command not found`. `.gitattributes` already has `*.sh text eol=lf`; if the file
  was converted, save it as LF again.
- **Apple-silicon Mac (M1/M2/M3)**: the SQL Server image runs under x86 emulation (enable "Use
  Rosetta" in Docker Desktop), starts slowly, and needs at least 4 GB of RAM given to Docker.
- **`db-init` reports "Login failed"**: `db` is not ready yet. Check the healthcheck and that
  `depends_on` uses `condition: service_healthy`.
- **Garbled text in the DB**: `-f 65001` is missing, or the `.sql` file is not saved as UTF-8.
- **The web app cannot log into the DB**: file 04 has not run, or the `APP_PASSWORD` in `.env`
  differs from the one used when the login was created (re-running 04 does not change an existing
  login's password; use `down -v` to change it).
- **An overdue contract still shows as "Active"**: the container has no SQL Agent, so the app must
  call `sp_ExpireContracts` periodically (a `BackgroundService`).

## Going to production

Remove the `1433` port mapping on `db`, do not commit `.env`, put an HTTPS reverse proxy (Nginx or
Caddy) in front of `web`, enable the `Secure` cookie, change every password, and never use the demo
`123456` account.

## Checklist

- [ ] `docker compose up -d --build` runs cleanly on a new machine with no manual steps.
- [ ] `docker compose logs db-init` reports that the data was loaded (or "skipped" when the DB exists).
- [ ] `docker compose down` then `up` again: the data is **intact**. `down -v` then `up`: the DB is recreated from scratch.
- [ ] The application does not use `sa`. `.env` is in `.gitignore`.
- [ ] Log in as `john`/`123456`, act on a contract, then check `AUDIT_LOG` has a non-NULL `EmployeeID`.
