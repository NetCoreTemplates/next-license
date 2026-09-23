#!/usr/bin/env bash
# Executed inside the SQL Server accessory. No credentials are printed or passed on argv.
set -euo pipefail
: "${MSSQL_SA_PASSWORD:?Missing SQL Server admin password}"
: "${DB_PASSWORD:?Missing application password}"
export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"
sqlcmd=/opt/mssql-tools18/bin/sqlcmd
ready=false
for attempt in $(seq 1 60); do
    if "$sqlcmd" -S localhost -U sa -C -b -Q 'SELECT 1' >/dev/null 2>&1; then ready=true; break; fi
    sleep 5
done
[[ "$ready" == true ]] || { echo 'SQL Server did not become ready within 300 seconds' >&2; exit 1; }
# Escape T-SQL literals; -x disables sqlcmd variable substitution in password text.
app_password_sql=${DB_PASSWORD//\'/\'\'}
"$sqlcmd" -S localhost -U sa -C -b -x <<SQL
IF DB_ID('next_license') IS NULL CREATE DATABASE [next_license];
GO
IF SUSER_ID('next_license') IS NULL
    CREATE LOGIN [next_license] WITH PASSWORD = '$app_password_sql', CHECK_POLICY = ON;
GO
USE [next_license];
GO
IF USER_ID('next_license') IS NULL CREATE USER [next_license] FOR LOGIN [next_license];
GO
ALTER ROLE db_owner ADD MEMBER [next_license];
GO
SQL
