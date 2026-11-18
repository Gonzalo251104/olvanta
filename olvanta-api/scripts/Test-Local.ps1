[CmdletBinding()]
param([string]$BaseUrl = 'http://127.0.0.1:5180')
$ErrorActionPreference = 'Stop'
$deadline = [DateTime]::UtcNow.AddMinutes(2)
do {
    try {
        $homePage = Invoke-WebRequest "$BaseUrl/" -UseBasicParsing -TimeoutSec 5
        $live = Invoke-RestMethod "$BaseUrl/health/live" -TimeoutSec 5
        $ready = Invoke-RestMethod "$BaseUrl/health/ready" -TimeoutSec 5
        $status = Invoke-RestMethod "$BaseUrl/api/v1/system/status" -TimeoutSec 5
        if ($homePage.Content -match '<title>Olvanta' -and $live -eq 'Healthy' -and $ready -eq 'Healthy' -and
            $status.status -eq 'ready' -and $status.database -eq 'connected') {
            Write-Output "Olvanta disponible en $BaseUrl. Web, API y SQL Server verificados."
            return
        }
    } catch { Write-Verbose $_.Exception.Message }
    Start-Sleep -Seconds 2
} while ([DateTime]::UtcNow -lt $deadline)
throw "La verificación de web, API y SQL Server falló en $BaseUrl. Consulte docker compose logs."
