# Lee el evento del hook por stdin y formatea el .cs editado.
$ErrorActionPreference = 'SilentlyContinue'
$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }

try { $evt = $raw | ConvertFrom-Json } catch { exit 0 }

$file = $evt.tool_input.file_path
if (-not $file) { exit 0 }
if ($file -notmatch '\.cs$') { exit 0 }
if (-not (Test-Path $file)) { exit 0 }

$sln = Join-Path $PSScriptRoot '..\..\CaseritoApp\CaseritoApp.sln'
if (-not (Test-Path $sln)) { exit 0 }

& dotnet format $sln --include $file --verbosity quiet | Out-Null
exit 0
