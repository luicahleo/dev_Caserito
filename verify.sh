#!/bin/sh
# Puerta de calidad de CaseritoApp.
# Uso: ./verify.sh          -> nivel rápido, sin Docker
#      ./verify.sh --full   -> nivel completo, requiere Docker
node quality/verify.mjs "$@"
