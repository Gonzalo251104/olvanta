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
if (!(Test-Path -LiteralPath $envPath)) {
    $content = "OLVANTA_SA_PASSWORD=$(New-LocalCredential)`nOLVANTA_APP_PASSWORD=$(New-LocalCredential)`n"
    $stream = [IO.File]::Open($envPath, 'CreateNew', 'Write', 'None')
    try {
        $data = [Text.Encoding]::ASCII.GetBytes($content)
        $stream.Write($data, 0, $data.Length)
    } finally { $stream.Dispose() }
}
Push-Location $deployment
try {
    docker compose up --build -d
    if ($LASTEXITCODE) { throw 'Falló el arranque con Docker Compose.' }
    $port = if ($env:OLVANTA_PORT) { $env:OLVANTA_PORT } else { '5180' }
    & (Join-Path $PSScriptRoot 'Test-Local.ps1') -BaseUrl "http://127.0.0.1:$port"
} finally { Pop-Location }
