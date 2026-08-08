param([switch]$Logs)

$ErrorActionPreference = 'Stop'
$rutaIp = Join-Path $PSScriptRoot '.local/pc2/ip.txt'
$ipLan = if (Test-Path $rutaIp) { (Get-Content $rutaIp -Raw).Trim() } else { '127.0.0.1' }
$env:CASERITO_LAN_IP = $ipLan
$env:CASERITO_LAN_HOST = "$ipLan.sslip.io"
$archivosCompose = @('-f', 'docker-compose.dev.yml', '-f', 'docker-compose.pc2.yml')
& docker compose @archivosCompose ps
if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar el entorno PC2.' }
if ($Logs) { & docker compose @archivosCompose logs --tail 100 }
