[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $File,
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')] [string] $Project = 'olvanta',
    [string] $EnvironmentFile = (Join-Path $PSScriptRoot '../deploy/.env')
)
$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($File)
if (Test-Path -LiteralPath $destination) { throw 'El archivo de destino ya existe.' }
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
$temporary = '/var/opt/mssql/data/olvanta-export-' + [Guid]::NewGuid().ToString('N') + '.bak'
$deployment = Join-Path $PSScriptRoot '../deploy'
$compose = @('compose', '-f', (Join-Path $deployment 'compose.yaml'), '--project-name', $Project, '--env-file', (Get-Item -LiteralPath $EnvironmentFile -Force).FullName)
@"
BACKUP DATABASE [Olvanta] TO DISK = N'$temporary' WITH COPY_ONLY, CHECKSUM, COMPRESSION;
RESTORE VERIFYONLY FROM DISK = N'$temporary' WITH CHECKSUM;
"@ | docker @compose exec -T sql bash -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b'
if ($LASTEXITCODE) { throw 'SQL Server no completó y verificó el respaldo.' }
docker @compose cp "sql:${temporary}" $destination
if ($LASTEXITCODE -or !(Test-Path -LiteralPath $destination -PathType Leaf)) { throw 'Falló la copia del respaldo al equipo.' }
docker @compose exec -T sql rm -- $temporary
if ($LASTEXITCODE) { Write-Warning 'El respaldo se exportó y quedó una copia temporal en el volumen de origen.' }
Get-FileHash -LiteralPath $destination -Algorithm SHA256
