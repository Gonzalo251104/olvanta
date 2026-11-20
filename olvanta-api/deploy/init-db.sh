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
ELSE ALTER USER [olvanta_app] WITH LOGIN = [olvanta_app];
GRANT CONNECT TO [olvanta_app];
GO
IF SCHEMA_ID('identity') IS NULL EXEC('CREATE SCHEMA [identity]');
IF SCHEMA_ID('organization') IS NULL EXEC('CREATE SCHEMA [organization]');
IF SCHEMA_ID('catalog') IS NULL EXEC('CREATE SCHEMA [catalog]');
IF SCHEMA_ID('audit') IS NULL EXEC('CREATE SCHEMA [audit]');
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[identity] TO [olvanta_app];
GRANT SELECT, INSERT, UPDATE ON SCHEMA::[organization] TO [olvanta_app];
GRANT SELECT, INSERT, UPDATE ON SCHEMA::[catalog] TO [olvanta_app];
GRANT SELECT, INSERT ON SCHEMA::[audit] TO [olvanta_app];
GO
SQL
