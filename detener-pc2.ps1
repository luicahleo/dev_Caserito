param(
    [switch]$BorrarDatos,
    [switch]$ConfirmarBorradoDatos
)

$ErrorActionPreference = 'Stop'
$rutaIp = Join-Path $PSScriptRoot '.local/pc2/ip.txt'
$ipLan = if (Test-Path $rutaIp) { (Get-Content $rutaIp -Raw).Trim() } else { '127.0.0.1' }
$env:CASERITO_LAN_IP = $ipLan
$env:CASERITO_LAN_HOST = "$ipLan.sslip.io"
$archivosCompose = @('-f', 'docker-compose.dev.yml', '-f', 'docker-compose.pc2.yml', '-f', 'docker-compose.argos.yml')

if ($BorrarDatos -and -not $ConfirmarBorradoDatos) {
    throw 'El borrado elimina la base y volúmenes locales. Repite con -BorrarDatos -ConfirmarBorradoDatos.'
}

$argumentos = @('down')
if ($BorrarDatos) { $argumentos += '--volumes' }
& docker compose @archivosCompose @argumentos
if ($LASTEXITCODE -ne 0) { throw 'No se pudo detener el entorno PC2.' }

if ($BorrarDatos) {
    Write-Host 'Se eliminaron los volúmenes Docker locales de Caserito; no son recuperables desde este script.' -ForegroundColor Yellow
}
