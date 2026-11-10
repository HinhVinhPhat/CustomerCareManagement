#!/bin/bash
# Initialise the database once. NOT yet tested on a real machine: try it and adjust as needed.
# Runs inside the db-init container (see docker-compose.yml). This file MUST use LF line endings.
set -euo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd     # older images: /opt/mssql-tools/bin/sqlcmd (and drop the -C flag)
HOST="${DB_HOST:-db}"

run() { "$SQLCMD" -S "$HOST" -U sa -P "$SA_PASSWORD" -C -I -f 65001 -b "$@"; }

echo "Waiting for SQL Server to be ready..."
for i in $(seq 1 60); do
  if "$SQLCMD" -S "$HOST" -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then break; fi
  sleep 2
done

# File 01 contains DROP DATABASE, so only run 01-03 when the DB does NOT exist yet
# (otherwise every `up` would wipe the data).
EXISTS=$("$SQLCMD" -S "$HOST" -U sa -P "$SA_PASSWORD" -C -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'CustomerCareDB') IS NULL THEN 0 ELSE 1 END" | tr -d "[:space:]")

if [ "$EXISTS" = "0" ]; then
  for f in 01_CreateTables 02_Function_View_Proc_Trigger 03_SampleData; do
    echo "Running $f.sql"
    run -i "/database/$f.sql"
  done
  echo "OK: sample data loaded"
else
  echo "Skipping 01-03: the database already exists (use docker compose down -v to recreate from scratch)"

  # The healthcheck answers as soon as master is up, but SQL Server then autostarts the user
  # databases one by one. File 04 begins with "USE CustomerCareDB", which fails with error 904
  # while that database is still starting, so wait until it can really be opened.
  READY=0
  for i in $(seq 1 60); do
    if "$SQLCMD" -S "$HOST" -U sa -P "$SA_PASSWORD" -C -I -h -1 -W \
      -Q "SET NOCOUNT ON; USE CustomerCareDB; SELECT 1" >/dev/null 2>&1; then
      READY=1
      break
    fi
    sleep 2
  done
  echo "CustomerCareDB is ready (${READY})"
fi

# File 04 is safe to run again (it uses IF NOT EXISTS). It is retried because SQL Server autostarts
# the user databases in the background: a restart can still answer error 904 (database is being
# started) for a few seconds after master already accepts connections.
echo "Running 04_CreateAppUser.sql"
READY=0
for i in $(seq 1 30); do
  if run -v APP_PASSWORD="$APP_PASSWORD" -i /database/04_CreateAppUser.sql; then
    READY=1
    break
  fi
  echo "The database is still starting, retrying in 2s..."
  sleep 2
done

if [ "$READY" != "1" ]; then
  echo "ERROR: could not create the app_ccm login"
  exit 1
fi

echo "OK: app_ccm user is ready"
