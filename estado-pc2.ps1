param([switch]$Logs)

$ErrorActionPreference = 'Stop'
$archivosCompose = @('-f', 'docker-compose.dev.yml', '-f', 'docker-compose.pc2.yml')
& docker compose @archivosCompose ps
if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar el entorno PC2.' }
if ($Logs) { & docker compose @archivosCompose logs --tail 100 }
