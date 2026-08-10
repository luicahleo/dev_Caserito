param(
    [string]$Ip,
    [switch]$Logs,
    [switch]$RecrearDatos,
    [switch]$ConfirmarBorradoDatos
)

$ErrorActionPreference = 'Stop'
$argumentos = @{
    Perfil = 'pc1'
}
if ($Ip) { $argumentos.Ip = $Ip }
if ($Logs) { $argumentos.Logs = $true }
if ($RecrearDatos) { $argumentos.RecrearDatos = $true }
if ($ConfirmarBorradoDatos) { $argumentos.ConfirmarBorradoDatos = $true }

& (Join-Path $PSScriptRoot 'iniciar-pc2.ps1') @argumentos
