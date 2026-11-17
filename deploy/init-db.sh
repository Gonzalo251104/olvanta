#!/bin/bash
set -euo pipefail
# Start-Local generates alphanumeric credentials. Validate before expanding into SQL.
[[ "$APP_PASSWORD" =~ ^[a-zA-Z0-9]{24,96}$ ]] || { echo "Invalid application credential format" >&2; exit 1; }
/opt/mssql-tools18/bin/sqlcmd -S sql -U sa -C -b <<SQL
IF DB_ID(N'Olvanta') IS NULL CREATE DATABASE [Olvanta];
GO
IF SUSER_ID(N'olvanta_app') IS NULL
    CREATE LOGIN [olvanta_app] WITH PASSWORD = '$APP_PASSWORD';
GO
USE [Olvanta];
GO
IF USER_ID(N'olvanta_app') IS NULL CREATE USER [olvanta_app] FOR LOGIN [olvanta_app];
GRANT CONNECT TO [olvanta_app];
GO
SQL
