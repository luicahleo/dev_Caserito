# Reconstruye y levanta el entorno de desarrollo (sqlserver+api+web) en segundo plano.
# Uso: ./rebuild.ps1        -> levanta en background
#      ./rebuild.ps1 -Logs  -> levanta y sigue los logs
param([switch]$Logs)
docker compose -f docker-compose.dev.yml up -d --build
if ($Logs) { docker compose -f docker-compose.dev.yml logs -f }
