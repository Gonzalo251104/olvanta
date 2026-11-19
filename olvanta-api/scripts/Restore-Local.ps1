[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $File,
    [Parameter(Mandatory)] [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')] [string] $Project,
    [Parameter(Mandatory)] [ValidateRange(1024, 65535)] [int] $Port,
    [string] $EnvironmentFile = (Join-Path $PSScriptRoot '../deploy/.env')
)
$ErrorActionPreference = 'Stop'
if ($Project -eq 'olvanta') { throw 'Elija un nombre nuevo para el entorno de recuperación.' }
$source = (Get-Item -LiteralPath $File).FullName
$credentials = (Get-Item -LiteralPath $EnvironmentFile).FullName
$existingContainers = docker ps -aq --filter "label=com.docker.compose.project=$Project"
if ($LASTEXITCODE) { throw 'Docker no pudo comprobar el proyecto de destino.' }
$existingVolumes = docker volume ls -q --filter "label=com.docker.compose.project=$Project"
if ($LASTEXITCODE) { throw 'Docker no pudo comprobar los volúmenes de destino.' }
if ($existingContainers -or $existingVolumes) { throw 'El proyecto de destino ya tiene contenedores o volúmenes.' }
$probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
try { $probe.Start() } finally { $probe.Stop() }

$deployment = Join-Path $PSScriptRoot '../deploy'
$compose = @('compose', '-f', (Join-Path $deployment 'compose.yaml'), '--project-name', $Project, '--env-file', $credentials)
$originalPort = $env:OLVANTA_PORT
$env:OLVANTA_PORT = [string]$Port
try {
    docker @compose up -d --wait --wait-timeout 180 sql
    if ($LASTEXITCODE) { throw 'SQL Server de recuperación no arrancó.' }

    $temporary = '/tmp/olvanta-restore.bak'
    docker @compose cp $source "sql:${temporary}"
    if ($LASTEXITCODE) { throw 'Falló la copia del respaldo al SQL Server de recuperación.' }
    docker @compose exec -T -u 0 sql chown mssql $temporary
    if ($LASTEXITCODE) { throw 'No se pudo asignar el archivo al usuario de SQL Server.' }

    @"
IF DB_ID(N'Olvanta') IS NOT NULL THROW 50001, 'La base de destino debe estar vacía.', 1;
RESTORE VERIFYONLY FROM DISK = N'$temporary' WITH CHECKSUM;
RESTORE DATABASE [Olvanta] FROM DISK = N'$temporary' WITH CHECKSUM, RECOVERY;
DBCC CHECKDB (N'Olvanta') WITH PHYSICAL_ONLY, NO_INFOMSGS;
"@ | docker @compose exec -T sql bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b'
    if ($LASTEXITCODE) { throw 'Falló la restauración o la comprobación física de la base.' }
    docker @compose exec -T sql rm -- $temporary
    if ($LASTEXITCODE) { Write-Warning 'Quedó una copia temporal del respaldo en el contenedor de recuperación.' }

    docker @compose up -d --build
    if ($LASTEXITCODE) { throw 'Falló el arranque de la aplicación restaurada.' }
    & (Join-Path $PSScriptRoot 'Test-Local.ps1') -BaseUrl "http://127.0.0.1:$Port"
} finally {
    $env:OLVANTA_PORT = $originalPort
}
