---
name: ccm-docker-deploy
description: Packaging and running the customer care management system with Docker Compose - the db service (SQL Server 2022), db-init (loads the 5 SQL files once, without wiping data on restart), web (ASP.NET Core MVC); the .env variables, connection string, healthcheck, sqlcmd with the -I and -f 65001 flags, keeping login sessions, and common problems on Windows and Apple-silicon Macs. Use this skill when writing or editing docker-compose.yml, the Dockerfile, database/init.sh or .env.example, when the db container keeps stopping, when text in the DB is garbled, when db-init reports an error, or when the user mentions Docker, compose, container, deployment or running on another machine.
---

# Docker for the customer care system

Read `ccm-database` for the five SQL files and their run order; the app structure is in
`ccm-mvc-architecture`. Section 11 of `docs/design.md` describes a separate `api` + `web` model,
which is outdated: the application is now **one** MVC project, so there is only one `web` service.

## Service layout

| Service | Job | Notes |
|---|---|---|
| `db` | SQL Server 2022, data in the `sqldata` volume | Has a healthcheck. Port 1433 is dev only |
| `db-init` | Runs once: loads `database/01` to `04`, then always merges the address catalogue `05` | Waits for `db` to be healthy, does not restart |
| `web` | ASP.NET Core MVC | `depends_on: db-init` with `condition: service_completed_successfully` |

All three services are enabled and were run for real on Windows 11 + Docker Desktop (Linux
containers) in module M07: `docker-compose.yml` and `.env.example` at the project root,
`database/init.sh`, and `src/CcmWeb/Dockerfile` with `src/CcmWeb/.dockerignore`. The image is
`customercaremanagement-web:latest`, about 456 MB, and the application is published from the `net10.0`
SDK image onto the `aspnet:10.0` runtime image.

## The biggest trap: file 01 drops the database

`database/01_CreateTables.sql` starts by running `DROP DATABASE CustomerCareDB` if the DB already
exists. If `db-init` ran this file on every `docker compose up`, **all the data would be lost on
every restart**. `init.sh` must check first:

- DB does not exist: run 01, 02, 03.
- DB exists: **skip** 01 to 03.
- File 04 (creating the `app_ccm` login) uses `IF NOT EXISTS`, so it is safe to re-run and always runs.
- File 05 (province/ward catalogue) creates missing schema and MERGEs rows, so it always runs after 04.

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

## The web service

A multi-stage Dockerfile in `src/CcmWeb`: `FROM mcr.microsoft.com/dotnet/sdk:<version> AS build` to
run `dotnet publish -c Release`, then `FROM mcr.microsoft.com/dotnet/aspnet:<version>` to run the
result. Use exactly the .NET version the project targets (check the `.csproj` and `global.json`),
never change it on your own. The real service in `docker-compose.yml`:

```yaml
  web:
    build: ./src/CcmWeb
    depends_on:
      db-init:
        condition: service_completed_successfully
    environment:
      ConnectionStrings__DefaultConnection: "Server=db,1433;Database=CustomerCareDB;User Id=app_ccm;Password=${APP_PASSWORD};TrustServerCertificate=True"
      ASPNETCORE_URLS: "http://+:8080"
      Security__RedirectToHttps: "false"
      Security__DataProtectionKeys: "/keys"
    ports:
      - "8080:8080"
    volumes:
      - keys:/keys
    healthcheck:
      test: ["CMD", "bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080"]
    restart: unless-stopped
```

- The `__` in an environment variable name maps to a `:` in .NET configuration, so it matches
  `GetConnectionString("DefaultConnection")`.
- The DB host is `db` (the service name), not `localhost`. From your machine (SSMS, Azure Data
  Studio) use `localhost,1433` instead.
- The app connects as `app_ccm`, **never `sa`**. `sa` is only used inside `db-init`.
- The `aspnet` image from .NET 8 onwards listens on port 8080 by default (not 80).
- **Keep login sessions:** the cookie encryption key (Data Protection) is lost when the container
  restarts, signing everyone out. `Program.cs` reads `Security:DataProtectionKeys` and, when it is
  set, calls `AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("/keys"))`; compose sets
  it to `/keys` and mounts the `keys` volume. Without the setting (plain `dotnet run`) the framework
  default is kept. `down -v` deletes the volume, so everybody signs in again.
- **No certificate inside the container:** `Security:RedirectToHttps` defaults to `true` (the
  development behaviour); compose sets it to `false`, which skips `UseHttpsRedirection` **and** HSTS.
- **`/health`** is a public endpoint (200 when `SELECT 1` answers, 503 otherwise) and is the probe to
  call from a reverse proxy. The compose healthcheck only opens the port, because the `aspnet` image
  ships neither `curl` nor `wget`; installing one just for a probe is not worth the layer.
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
- **`db-init` reports error 904 "Database 5 cannot be autostarted during server shutdown or
  startup"**: SQL Server accepts connections (the healthcheck passes) while it is still autostarting
  the user databases, and file 04 starts with `USE CustomerCareDB`. `init.sh` waits until that
  database can really be opened **and** retries file 04 for up to a minute; if the message still
  appears, the app password or the container is at fault, not the data.
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

Checked on a real machine in M07 (Windows 11, Docker Desktop, Linux containers):

- [x] `docker compose up -d --build` runs cleanly on a new machine with no manual steps.
- [x] `docker compose logs db-init` reports that the data was loaded (or "skipped" when the DB exists).
- [x] `docker compose down` then `up` again: the data and the sign-in session are **intact**. `down -v` then `up`: the DB is recreated from scratch and the old cookie is refused.
- [x] The application does not use `sa`. `.env` is in `.gitignore`.
- [x] Log in as `john`/`123456`, write through the app, then check `AUDIT_LOG` has a non-NULL `EmployeeID` (it recorded `EmployeeID = 1`).
- [x] `/health` answers 200, and 503 once `db` is stopped; it recovers to 200 when `db` comes back.
- [x] Vietnamese text survives the round trip into the container database and back to the page.
