#!/bin/sh
# Reconstruye y levanta el entorno de desarrollo (sqlserver+api+web) en segundo plano.
# Uso: ./rebuild.sh          -> levanta en background
#      ./rebuild.sh --logs   -> levanta y sigue los logs
docker compose -f docker-compose.dev.yml up -d --build --renew-anon-volumes
if [ "$1" = "--logs" ]; then docker compose -f docker-compose.dev.yml logs -f; fi
