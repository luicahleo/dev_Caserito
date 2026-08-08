param(
    [string]$Ip,
    [switch]$Argos,
    [switch]$Logs
)

$ErrorActionPreference = 'Stop'
$raizCaserito = $PSScriptRoot

function Obtener-IpLan([string]$IpSolicitada) {
    if ($IpSolicitada) {
        $direccion = $null
        if (-not [System.Net.IPAddress]::TryParse($IpSolicitada, [ref]$direccion) -or
            $direccion.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork -or
            [System.Net.IPAddress]::IsLoopback($direccion)) {
            throw 'La IP indicada no es una IPv4 LAN válida.'
        }
        return $direccion.IPAddressToString
    }

    $ruta = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction Stop |
        Where-Object { $_.NextHop -ne '0.0.0.0' } |
        Sort-Object RouteMetric, InterfaceMetric |
        Select-Object -First 1
    if (-not $ruta) { throw 'No se encontró una ruta de red activa. Usa -Ip <IPv4-WiFi>.' }
    $direccion = Get-NetIPAddress -InterfaceIndex $ruta.InterfaceIndex -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -notlike '169.254.*' } |
        Select-Object -First 1
    if (-not $direccion) { throw 'No se encontró una IPv4 LAN. Usa -Ip <IPv4-WiFi>.' }
    return $direccion.IPAddress
}

if (-not (Test-Path (Join-Path $raizCaserito '.env'))) {
    throw 'Falta .env. Copia .env.example como .env y completa SA_PASSWORD con un valor local fuerte.'
}

docker info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Desktop no está iniciado o no responde.' }

$ipLan = Obtener-IpLan $Ip
$hostLan = "$ipLan.sslip.io"
$env:CASERITO_LAN_IP = $ipLan
$env:CASERITO_LAN_HOST = $hostLan
New-Item -ItemType Directory -Force (Join-Path $raizCaserito '.local/pc2/caddy-data') | Out-Null
New-Item -ItemType Directory -Force (Join-Path $raizCaserito '.local/pc2/caddy-config') | Out-Null
Set-Content -Path (Join-Path $raizCaserito '.local/pc2/ip.txt') -Value $ipLan -Encoding ascii

$archivosCompose = @('-f', 'docker-compose.dev.yml', '-f', 'docker-compose.pc2.yml')
if ($Argos) {
    $rutaArgos = Join-Path $raizCaserito '..\dev\ARGOS\Dockerfile'
    if (-not (Test-Path $rutaArgos)) {
        throw 'No se encontró ARGOS en ../dev/ARGOS. Corrige su ubicación o inicia sin -Argos.'
    }
    $archivosCompose += @('-f', 'docker-compose.argos.yml')
}

& docker compose @archivosCompose up -d --build --renew-anon-volumes
if ($LASTEXITCODE -ne 0) { throw 'No se pudo levantar el entorno PC2.' }

$certificado = Join-Path $raizCaserito '.local/pc2/caddy-data/caddy/pki/authorities/local/root.crt'
for ($intento = 0; $intento -lt 30 -and -not (Test-Path $certificado); $intento++) {
    Start-Sleep -Seconds 1
}

$urlSalud = "https://$hostLan/health"
$saludable = $false
for ($intento = 0; $intento -lt 30 -and -not $saludable; $intento++) {
    & curl.exe -k -f -sS --max-time 5 --resolve "${hostLan}:443:$ipLan" $urlSalud -o NUL 2>$null
    $saludable = $LASTEXITCODE -eq 0
    if (-not $saludable) { Start-Sleep -Seconds 1 }
}
if (-not $saludable) {
    & docker compose @archivosCompose logs --tail 50 gateway web api
    throw 'El gateway HTTPS no alcanzó un estado saludable.'
}

Write-Host ''
Write-Host "Caserito PC2: https://$hostLan" -ForegroundColor Green
if (Test-Path $certificado) {
    Write-Host "CA pública para instalar en el móvil: $certificado" -ForegroundColor Yellow
} else {
    Write-Warning 'Caddy aún no creó la CA. Revisa: docker compose ... logs gateway'
}
Write-Host 'Si el móvil no conecta, permite los puertos TCP 80 y 443 para redes privadas en Firewall de Windows.'

if ($Logs) { & docker compose @archivosCompose logs -f }
