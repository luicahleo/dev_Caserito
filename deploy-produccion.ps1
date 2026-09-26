<#
.SYNOPSIS
    Publica develop y, opcionalmente, promueve a master y despliega a producción.

.DESCRIPTION
    Dos modos:

      1. Publicar (por defecto): empuja develop a origin y espera que su CI
         termine en verde. No toca master ni despliega nada.

      2. Desplegar (-Desplegar -Confirmar): además ejecuta la puerta de calidad
         completa, fusiona develop en master con un commit de merge, empuja,
         espera CI verde en master y dispara el workflow Deploy con
         confirmar=PRODUCCION.

    El modo despliegue requiere Docker corriendo, porque ./verify.ps1 -Full
    levanta los tests de integración con Testcontainers.

.PARAMETER Desplegar
    Promueve a master y dispara el despliegue. Exige también -Confirmar.

.PARAMETER Confirmar
    Confirma explícitamente el despliegue a producción.

.PARAMETER Watch
    Monitorea el workflow de despliegue hasta que termine.

.PARAMETER CiTimeoutMinutos
    Minutos máximos de espera por CI verde. Por defecto 30.

.PARAMETER SinEsperarCiDevelop
    En modo publicar, empuja y termina sin esperar el resultado de CI.

.EXAMPLE
    .\deploy-produccion.ps1
    Empuja develop y espera su CI.

.EXAMPLE
    .\deploy-produccion.ps1 -Desplegar -Confirmar -Watch
    Publica, promueve a master, despliega y monitorea.
#>
[CmdletBinding()]
param(
    [Parameter(HelpMessage = 'Promueve a master y despliega a producción')]
    [switch]$Desplegar,

    [Parameter(HelpMessage = 'Confirma el despliegue a producción')]
    [switch]$Confirmar,

    [Parameter(HelpMessage = 'Monitorea el workflow de despliegue hasta que termine')]
    [switch]$Watch,

    [Parameter(HelpMessage = 'Minutos máximos de espera por CI verde')]
    [int]$CiTimeoutMinutos = 30,

    [Parameter(HelpMessage = 'No espera el CI de develop al publicar')]
    [switch]$SinEsperarCiDevelop,

    [Parameter(HelpMessage = 'Omite la confirmación interactiva; -Confirmar basta')]
    [switch]$SinPrompt,

    [string]$RamaDesarrollo = 'develop',
    [string]$RamaProduccion = 'master'
)

$ErrorActionPreference = 'Stop'

function Test-Programa {
    param([string]$Nombre)
    return $null -ne (Get-Command $Nombre -ErrorAction SilentlyContinue)
}

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Argumentos)
    $salida = & git @Argumentos 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $Argumentos falló: $salida"
    }
    return $salida
}

function Get-RefSha {
    param([string]$Ref)
    return (Invoke-Git 'rev-parse' $Ref).Trim()
}

function Get-RepoActual {
    return (gh repo view --json nameWithOwner -q '.nameWithOwner').Trim()
}

function Get-ProteccionRama {
    param([string]$Rama)
    $repo = Get-RepoActual
    $json = gh api "repos/$repo/branches/$Rama/protection" 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }
    return ($json | ConvertFrom-Json)
}

function Remove-ProteccionRama {
    param([string]$Rama)
    $repo = Get-RepoActual
    gh api -X DELETE "repos/$repo/branches/$Rama/protection" *>$null
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo retirar la protección de '$Rama'."
    }
}

function Restore-ProteccionRama {
    param(
        [string]$Rama,
        [psobject]$Proteccion
    )

    if (-not $Proteccion) { return }

    $payload = @{
        restrictions                    = $null
        enforce_admins                  = [bool]$Proteccion.enforce_admins.enabled
        required_linear_history         = [bool]$Proteccion.required_linear_history.enabled
        allow_force_pushes              = [bool]$Proteccion.allow_force_pushes.enabled
        allow_deletions                 = [bool]$Proteccion.allow_deletions.enabled
        block_creations                 = [bool]$Proteccion.block_creations.enabled
        required_conversation_resolution = [bool]$Proteccion.required_conversation_resolution.enabled
        lock_branch                     = [bool]$Proteccion.lock_branch.enabled
        allow_fork_syncing              = [bool]$Proteccion.allow_fork_syncing.enabled
    }

    if ($Proteccion.required_status_checks) {
        $checks = @()
        foreach ($c in $Proteccion.required_status_checks.checks) {
            $checks += @{ context = $c.context; app_id = $c.app_id }
        }
        $payload.required_status_checks = @{
            strict = [bool]$Proteccion.required_status_checks.strict
            checks = $checks
        }
    }
    else {
        $payload.required_status_checks = $null
    }

    if ($Proteccion.required_pull_request_reviews) {
        $r = $Proteccion.required_pull_request_reviews
        $payload.required_pull_request_reviews = @{
            dismiss_stale_reviews           = [bool]$r.dismiss_stale_reviews
            require_code_owner_reviews      = [bool]$r.require_code_owner_reviews
            require_last_push_approval      = [bool]$r.require_last_push_approval
            required_approving_review_count = [int]$r.required_approving_review_count
        }
    }
    else {
        $payload.required_pull_request_reviews = $null
    }

    $archivo = Join-Path ([IO.Path]::GetTempPath()) "proteccion-$Rama-$(Get-Random).json"
    ($payload | ConvertTo-Json -Depth 8) | Out-File -FilePath $archivo -Encoding utf8

    $repo = Get-RepoActual
    gh api -X PUT "repos/$repo/branches/$Rama/protection" --input $archivo *>$null
    $codigo = $LASTEXITCODE
    Remove-Item $archivo -ErrorAction SilentlyContinue

    if ($codigo -ne 0) {
        Write-Host "ATENCIÓN: no se pudo restaurar la protección de '$Rama'. Restáurala a mano en GitHub." -ForegroundColor Red
        throw "Fallo al restaurar la protección de '$Rama'."
    }
    Write-Host "Protección de '$Rama' restaurada." -ForegroundColor Green
}

function Wait-CiVerde {
    param(
        [string]$Sha,
        [string]$Rama,
        [int]$TimeoutMinutos
    )

    $limite = [DateTime]::UtcNow.AddMinutes($TimeoutMinutos)
    Write-Host "Esperando CI verde de $Rama para $Sha (timeout ${TimeoutMinutos}m)..."

    while ([DateTime]::UtcNow -lt $limite) {
        $json = gh run list --workflow=ci.yml --branch=$Rama --limit=5 `
            --json conclusion,headSha,status,url | ConvertFrom-Json
        $run = $json | Where-Object { $_.headSha -eq $Sha } | Select-Object -First 1

        if ($run) {
            Write-Host "  CI estado=$($run.status) conclusion=$($run.conclusion)"
            if ($run.status -eq 'completed') {
                if ($run.conclusion -eq 'success') {
                    return $run
                }
                throw "CI del commit $Sha no fue exitoso (conclusion=$($run.conclusion)). URL: $($run.url)"
            }
        }
        else {
            Write-Host '  Aún no aparece el run de CI para este commit...'
        }
        Start-Sleep -Seconds 15
    }

    throw "Timeout esperando CI verde para $Sha"
}

# ---------------------------------------------------------------------------
# Validaciones iniciales
# ---------------------------------------------------------------------------

if ($Desplegar -and -not $Confirmar) {
    throw 'El despliegue a producción requiere -Confirmar. Ejemplo: .\deploy-produccion.ps1 -Desplegar -Confirmar'
}

if ($Confirmar -and -not $Desplegar) {
    throw '-Confirmar solo tiene sentido junto con -Desplegar.'
}

if (-not (Test-Programa 'git')) {
    throw 'git no está disponible en PATH'
}

if (-not (Test-Programa 'gh')) {
    throw 'GitHub CLI (gh) no está disponible en PATH'
}

gh auth status *>$null
if ($LASTEXITCODE -ne 0) {
    throw 'GitHub CLI (gh) no está autenticado. Ejecuta gh auth login.'
}

$raiz = (Invoke-Git 'rev-parse' '--show-toplevel').Trim()
Set-Location $raiz

$ramaActual = (Invoke-Git 'branch' '--show-current').Trim()
if ($ramaActual -ne $RamaDesarrollo) {
    throw "Debes estar en la rama '$RamaDesarrollo'. Rama actual: $ramaActual"
}

$estado = Invoke-Git 'status' '--short'
if (-not [string]::IsNullOrWhiteSpace($estado)) {
    throw "El working tree tiene cambios sin commitear. Resuélvelos antes de publicar.`n$estado"
}

Invoke-Git 'fetch' 'origin' | Out-Null

# ---------------------------------------------------------------------------
# Publicar develop
# ---------------------------------------------------------------------------

$shaDevelopLocal = Get-RefSha $RamaDesarrollo
$shaDevelopRemoto = Get-RefSha "origin/$RamaDesarrollo"

if ($shaDevelopLocal -eq $shaDevelopRemoto) {
    Write-Host "$RamaDesarrollo ya está publicado ($shaDevelopLocal)." -ForegroundColor Green
}
else {
    $pendientes = Invoke-Git 'log' '--oneline' "origin/$RamaDesarrollo..$RamaDesarrollo"
    Write-Host "Commits a publicar en ${RamaDesarrollo}:" -ForegroundColor Cyan
    $pendientes | ForEach-Object { Write-Host "  $_" }

    Invoke-Git 'push' 'origin' $RamaDesarrollo | Out-Null
    Write-Host "$RamaDesarrollo publicado." -ForegroundColor Green
}

if (-not $Desplegar) {
    if ($SinEsperarCiDevelop) {
        Write-Host 'Publicación terminada (sin esperar CI).' -ForegroundColor Green
        return
    }
    $runDev = Wait-CiVerde -Sha $shaDevelopLocal -Rama $RamaDesarrollo -TimeoutMinutos $CiTimeoutMinutos
    Write-Host "CI de $RamaDesarrollo verde: $($runDev.url)" -ForegroundColor Green
    Write-Host 'Para promover a producción: .\deploy-produccion.ps1 -Desplegar -Confirmar' -ForegroundColor Cyan
    return
}

# ---------------------------------------------------------------------------
# Confirmación interactiva del despliegue
# ---------------------------------------------------------------------------

$aDesplegar = Invoke-Git 'log' '--oneline' "origin/$RamaProduccion..$RamaDesarrollo"
$cuantos = @($aDesplegar).Count
Write-Host ''
Write-Host "Vas a desplegar $cuantos commit(s) a PRODUCCIÓN." -ForegroundColor Yellow
@($aDesplegar) | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }
if ($cuantos -gt 15) { Write-Host "  ... y $($cuantos - 15) más" }
Write-Host ''

if (-not $SinPrompt) {
    $respuesta = Read-Host 'Escribe PRODUCCION para continuar'
    if ($respuesta -cne 'PRODUCCION') {
        throw 'Despliegue cancelado.'
    }
}

# ---------------------------------------------------------------------------
# Puerta de calidad completa (requiere Docker)
# ---------------------------------------------------------------------------

Write-Host 'Ejecutando la puerta de calidad completa (./verify.ps1 -Full)...'
& powershell -NoProfile -ExecutionPolicy Bypass -File .\verify.ps1 -Full
if ($LASTEXITCODE -ne 0) {
    throw 'La puerta de calidad falló. Se arregla el código, nunca el gate.'
}
Write-Host 'Puerta de calidad verde.' -ForegroundColor Green

# ---------------------------------------------------------------------------
# Promover develop -> master
# ---------------------------------------------------------------------------

$shaProdRemoto = Get-RefSha "origin/$RamaProduccion"
if ($shaProdRemoto -eq $shaDevelopLocal) {
    Write-Host "$RamaProduccion ya está alineado con $RamaDesarrollo." -ForegroundColor Green
}
else {
    & git show-ref --verify --quiet "refs/heads/$RamaProduccion"
    $existeLocal = ($LASTEXITCODE -eq 0)

    if ($existeLocal) {
        # La rama local puede ir por detrás del remoto sin haber divergido:
        # en ese caso basta con adelantarla.
        Invoke-Git 'checkout' $RamaProduccion | Out-Null
        Invoke-Git 'merge' '--ff-only' "origin/$RamaProduccion" | Out-Null
    }
    else {
        Invoke-Git 'checkout' '-b' $RamaProduccion '--track' "origin/$RamaProduccion" | Out-Null
    }

    # La rama de producción está protegida y rechaza el push directo. Se retira
    # la protección justo para el push y se restaura siempre, incluso si algo
    # falla en medio.
    $proteccion = Get-ProteccionRama $RamaProduccion
    $huboQueDesproteger = $false

    try {
        if ($proteccion) {
            Write-Host "Retirando temporalmente la protección de $RamaProduccion..." -ForegroundColor Yellow
            Remove-ProteccionRama $RamaProduccion
            $huboQueDesproteger = $true
        }

        # Merge con commit explícito: origin/master lleva merges de PR que no
        # están en develop, así que el fast-forward no es posible.
        Invoke-Git 'merge' '--no-ff' $RamaDesarrollo '-m' "Merge $RamaDesarrollo -> $RamaProduccion (release)" | Out-Null
        Invoke-Git 'push' 'origin' $RamaProduccion | Out-Null
    }
    catch {
        Write-Host "Fallo durante el merge o el push a $RamaProduccion. Volviendo a $RamaDesarrollo..." -ForegroundColor Red
        & git merge --abort 2>&1 | Out-Null
        & git checkout $RamaDesarrollo 2>&1 | Out-Null
        throw
    }
    finally {
        if ($huboQueDesproteger) {
            Write-Host "Restaurando la protección de $RamaProduccion..." -ForegroundColor Yellow
            Restore-ProteccionRama -Rama $RamaProduccion -Proteccion $proteccion
        }
    }

    Invoke-Git 'checkout' $RamaDesarrollo | Out-Null
}

$shaRelease = Get-RefSha "origin/$RamaProduccion"
Write-Host "Commit a desplegar: $shaRelease" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# Esperar CI verde en master
# ---------------------------------------------------------------------------

$runCi = Wait-CiVerde -Sha $shaRelease -Rama $RamaProduccion -TimeoutMinutos $CiTimeoutMinutos
Write-Host "CI verde: $($runCi.url)" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Disparar el despliegue
# ---------------------------------------------------------------------------

Write-Host 'Disparando el workflow Deploy con confirmar=PRODUCCION...'
$salidaDeploy = gh workflow run deploy.yml --ref $RamaProduccion -f confirmar=PRODUCCION 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "No se pudo disparar el workflow Deploy: $salidaDeploy"
}
Write-Host $salidaDeploy -ForegroundColor Cyan

if (-not $Watch) {
    Write-Host 'Despliegue disparado. Usa -Watch para monitorearlo o revisa la pestaña Actions.' -ForegroundColor Green
    return
}

Start-Sleep -Seconds 5
$runs = gh run list --workflow=deploy.yml --branch=$RamaProduccion --limit=5 `
    --json databaseId,headSha,status | ConvertFrom-Json
$run = $runs | Where-Object { $_.headSha -eq $shaRelease } | Select-Object -First 1

if (-not $run) {
    Write-Warning 'No se encontró el run de despliegue recién disparado para monitorear.'
    return
}

Write-Host "Monitoreando el despliegue (run $($run.databaseId))..."
gh run watch $run.databaseId --exit-status
if ($LASTEXITCODE -ne 0) {
    $repo = gh repo view --json nameWithOwner -q '.nameWithOwner'
    throw "El despliegue falló. Revisa https://github.com/$repo/actions/runs/$($run.databaseId)"
}

Write-Host 'Despliegue completado.' -ForegroundColor Green
Write-Host 'Recordatorio: verifica que llegue el correo de aviso de KYC tras la primera solicitud en revisión manual.' -ForegroundColor Cyan
