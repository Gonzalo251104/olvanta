[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$deployment = Join-Path $PSScriptRoot '../deploy'
$envPath = Join-Path $deployment '.env'
function New-LocalCredential {
    $bytes = New-Object byte[] 24
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    'Ov1' + [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
}
$stream = [IO.File]::Open($envPath, 'OpenOrCreate', 'ReadWrite', 'None')
try {
    $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true, 1024, $true)
    try { $existing = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $keys = if ($existing.Length -eq 0) {
        'OLVANTA_SA_PASSWORD', 'OLVANTA_APP_PASSWORD', 'OLVANTA_ADMIN_PASSWORD'
    } else { 'OLVANTA_ADMIN_PASSWORD' }
    foreach ($key in $keys) {
        if ($existing -notmatch "(?m)^${key}=") {
            $data = [Text.Encoding]::ASCII.GetBytes("`n$key=$(New-LocalCredential)`n")
            $stream.Write($data, 0, $data.Length)
        }
    }
} finally { $stream.Dispose() }
Push-Location $deployment
try {
    docker compose up --build -d
    if ($LASTEXITCODE) { throw 'Falló el arranque con Docker Compose.' }
    $port = if ($env:OLVANTA_PORT) { $env:OLVANTA_PORT } else { '5180' }
    & (Join-Path $PSScriptRoot 'Test-Local.ps1') -BaseUrl "http://127.0.0.1:$port"
} finally { Pop-Location }
