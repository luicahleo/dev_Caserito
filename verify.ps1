# Puerta de calidad de CaseritoApp.
# Uso: ./verify.ps1         -> nivel rápido, sin Docker
#      ./verify.ps1 -Changed -> nivel rápido según cambios pendientes
#      ./verify.ps1 -Full   -> nivel completo, requiere Docker
param([switch]$Full, [switch]$Changed)
if ($Full -and $Changed) {
  Write-Error 'Los parámetros -Full y -Changed no se pueden combinar.'
  exit 1
}
if ($Full) { node quality/verify.mjs --full }
elseif ($Changed) { node quality/verify.mjs --changed }
else { node quality/verify.mjs }
exit $LASTEXITCODE
