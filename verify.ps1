# Puerta de calidad de CaseritoApp.
# Uso: ./verify.ps1         -> nivel rápido, sin Docker
#      ./verify.ps1 -Full   -> nivel completo, requiere Docker
param([switch]$Full)
if ($Full) { node quality/verify.mjs --full } else { node quality/verify.mjs }
exit $LASTEXITCODE
