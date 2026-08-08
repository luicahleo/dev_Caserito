#!/bin/sh
# Puerta de calidad de CaseritoApp.
# Uso: ./verify.sh          -> nivel rápido, sin Docker
#      ./verify.sh --changed -> nivel rápido según cambios pendientes
#      ./verify.sh --full   -> nivel completo, requiere Docker
node quality/verify.mjs "$@"
