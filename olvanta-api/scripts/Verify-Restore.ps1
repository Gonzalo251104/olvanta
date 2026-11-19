[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Evidence,
    [Parameter(Mandatory)] [uri] $BaseUrl,
    [ValidateRange(1, 20)] [int] $Samples = 1,
    [string] $EnvironmentFile = (Join-Path $PSScriptRoot '../deploy/.env')
)
$ErrorActionPreference = 'Stop'
if ($BaseUrl.Scheme -ne 'http' -or $BaseUrl.Host -ne '127.0.0.1') { throw 'Use la dirección local del entorno restaurado.' }
$expected = Get-Content -LiteralPath $Evidence -Raw | ConvertFrom-Json
$root = $BaseUrl.AbsoluteUri.TrimEnd('/')
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$password = ((Select-String -LiteralPath $EnvironmentFile -Pattern '^OLVANTA_ADMIN_PASSWORD=' | Select-Object -First 1).Line -split '=', 2)[1]
if (!$password) { throw 'Falta la contraseña local de la cuenta administradora del ensayo.' }
$csrf = (Invoke-RestMethod "$root/api/v1/auth/csrf" -WebSession $session).token
$null = Invoke-WebRequest "$root/api/v1/auth/login" -WebSession $session -Method Post -ContentType 'application/json' `
    -Headers @{ 'X-CSRF-TOKEN' = $csrf } -Body (@{ userName = 'admin'; password = $password } | ConvertTo-Json -Compress)
try {
    $version = (Invoke-RestMethod "$root/openapi/v1.json").info.version
    if ($version -ne $expected.version) { throw "La API restaurada reporta $version en lugar de $($expected.version)." }
    $latencies = [Collections.Generic.List[double]]::new()
    foreach ($report in $expected.reports.PSObject.Properties) {
        $path = "$root/api/v1/branches/$($expected.branchId)/reports/$($report.Name)$($expected.dateFilter)"
        $expectedJson = ConvertTo-Json $report.Value -Depth 20 -Compress
        for ($sample = 0; $sample -lt $Samples; $sample++) {
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $actual = Invoke-RestMethod $path -WebSession $session
            $latencies.Add($watch.Elapsed.TotalMilliseconds)
            if ((ConvertTo-Json $actual -Depth 20 -Compress) -cne $expectedJson) {
                throw "El reporte $($report.Name) difiere tras restaurar."
            }
        }
        Write-Output "Reporte recuperado · $($report.Name) · $($actual.rows.Count) filas"
    }
    if ($Samples -gt 1) {
        $latencies.Sort()
        $p95 = $latencies[[int][Math]::Ceiling($latencies.Count * 0.95) - 1]
        Write-Output ('Reportes · {0} consultas secuenciales · p95 {1:N0} ms' -f $latencies.Count, $p95)
    }
    $cdr = Invoke-WebRequest "$root/api/v1/$($expected.cdrPath)" -WebSession $session
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$cdr.Content))
    if ($hash -ne $expected.cdrHash) { throw 'El hash de la constancia restaurada difiere del original.' }
    Write-Output "Olvanta $version restaurado. Se verificaron los reportes y la constancia fiscal simulada."
} finally {
    $csrf = (Invoke-RestMethod "$root/api/v1/auth/csrf" -WebSession $session).token
    $null = Invoke-WebRequest "$root/api/v1/auth/logout" -WebSession $session -Method Post -Headers @{ 'X-CSRF-TOKEN' = $csrf }
}
