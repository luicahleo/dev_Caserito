# Spec — Despliegue de CaseritoApp en la VPS (Trajano) + CI/CD con GitHub Actions

**Fecha:** 2026-07-30
**Estado:** propuesta (pendiente revisión del usuario)
**Canal de coordinación:** `preguntasrespuestasCaseritoApp_AgenteLocal_AgenteVPS/`
(`10_handoff_despliegue_caseritoapp.md` del agenteVPS → `11_respuestas_agente_local.md` del agente local)

## 1. Contexto

CaseritoApp (API .NET 10 + SPA React + SignalR + KYC/ARGOS) debe desplegarse en la VPS Trajano
siguiendo el patrón probado de decoraciones/icarus: un contenedor en `trajano-shared-network`,
nginx del host terminando TLS, SQL Server compartido, deploy por GitHub Actions (publish → rsync →
build en VPS, sin registry).

El agenteVPS ya reservó: subdominio `caserito.app` (DNS activo), puerto host
**8084** (bind `127.0.0.1`), carpeta `/var/apps/caseritoapp/`, BD **`CaseritoAppDB`** + login
**`caseritoapp_app`**, clave SSH `github-actions-caseritoapp` (autorizada).

## 2. Objetivos y no-objetivos

**Objetivos (lado repo):**

1. Que la API arranque en `Production` (hoy es imposible por el fail-fast de PII, §4.1).
2. Un solo contenedor `caseritoapp` que sirva API + SPA + SignalR en el puerto 8084.
3. Workflow `.github/workflows/deploy.yml`: push a `master` + `workflow_dispatch`.
4. Compose y Dockerfile de producción alineados con la plantilla del agenteVPS.
5. `.env.example` de producción documentando todas las variables requeridas.

**No-objetivos:**

- Envelope/KMS para PII (sigue diferido por decisión del proyecto).
- Backups, vhost nginx, certbot, creación de BD/login (lado agenteVPS, ya coordinado).
- Registry de imágenes, monitorización centralizada, segundo entorno (staging).
- Cambios funcionales de la app (features).

## 3. Decisiones de arquitectura

### 3.1 Un solo contenedor: la SPA se sirve desde el Host .NET

El handoff del agenteVPS asume el patrón decoraciones (un contenedor). CaseritoApp tiene API + SPA
React + SignalR. Se descartan dos contenedores (api + web-nginx) por salirse de las convenciones
del VPS (segundo puerto, nginx custom). El Host servirá los estáticos de `web/dist` desde
`wwwroot` con fallback a `index.html`. SignalR (`/hubs`) y REST (`/api`) conviven same-origin sin
CORS (el frontend ya usa rutas relativas; no hay CORS configurado en el backend).

### 3.2 Cifrado de PII: `IEncryptor` sobre ASP.NET Core Data Protection

El fail-fast de `Identity.Infrastructure/DependencyInjection.cs` (throw fuera de
Development/Testing con `PassthroughEncryptor`) exige un encryptor real. Se implementa sobre
`IDataProtection` (ya usado en el repo para tokens de email) en vez de AES-GCM escrito a mano:
criptografía mantenida por Microsoft, rotación de claves incorporada, una sola pila cripto.
Requiere **persistir el key ring** en disco (`PersistKeysToFileSystem`), volumen
`/data/dataprotection-keys`. Esto corrige además un bug latente: los tokens de confirmación de
email se invalidaban al recrear el contenedor (key ring efímero).

### 3.3 Deploy web: build en el runner, imagen runtime-only en la VPS

El runner de GitHub compila `npm run build` y copia `dist/` al `wwwroot` del `dotnet publish`;
rsync del conjunto a `/var/apps/caseritoapp/web/`; la VPS construye la imagen runtime-only
(aspnet:10.0 + binarios). No se compila nada en la VPS (patrón icarus/decoraciones).

### 3.4 Logs a consola, no a `/app/logs`

La app loguea a stdout; `docker compose logs` lo cubre. No se añade sink de fichero ni volumen de
logs (coordinado con agenteVPS; si su convención lo exige, se añade después).

### 3.5 Seed de admin en Production

Hoy no existe forma de crear el primer admin fuera de Development (`BootstrapUsuariosPrueba` se
omite fuera de Development). Se añade seed opt-in idempotente (estilo decoraciones): si
`SeedSettings__AdminEmail` + `SeedSettings__AdminPassword` están presentes y el usuario no existe,
se crea con rol `AdminPlataforma`. Fallo de contraseña (política: ≥8, mayús, minús, dígito,
símbolo) → `LogError` ruidoso, sin tumbar la app (decisión análoga a decoraciones post-despliegue).

## 4. Componentes (cambios en el repo)

### 4.1 `Identity.Infrastructure` — `DataProtectionEncryptor`

- Nueva clase `DataProtectionEncryptor : IEncryptor` que envuelve
  `IDataProtectionProvider.CreateProtector("CaseritoApp.KycPii")`.
- Registro condicional en `DependencyInjection.cs`: fuera de Development/Testing se registra el
  encryptor real (se elimina el `throw`); en Development/Testing se mantiene `PassthroughEncryptor`
  para no romper tests ni dev local.
- Configuración de Data Protection en el Host: `PersistKeysToFileSystem` con ruta de
  `DataProtection__RutaClaves` (default `/data/dataprotection-keys`).
- Tests: cifrar/descifrar round-trip, tamper detection (payload alterado → fallo), propósito
  aislado (protector con otro purpose no descifra).

### 4.2 Host — SPA estática + ForwardedHeaders + URL pública

- `Program.cs`: `UseStaticFiles()` + `MapFallbackToFile("index.html")` con cuidado de no capturar
  `/api`, `/hubs`, `/health` (el fallback de ASP.NET ya respeta endpoints mapeados; verificar
  orden). Solo sirve wwwroot si existe contenido (tests no lo generan).
- `UseForwardedHeaders` (`X-Forwarded-For`/`Proto`) antes de auth: nginx termina TLS; sin esto
  enlaces y cookies salen con esquema/host internos.
- **Bug a corregir:** `EnviarConfirmacionEmailHandler.cs:18` tiene hardcodeada
  `https://caserito.trajano.online/confirmar-email?...` (dominio equivocado y fijo). Se mueve a
  configuración `App__UrlPublica` (nueva opción `OpcionesApp`, sección `App`), con la ruta
  `/confirmar-email` construida sobre esa base. Tests del handler actualizados.
- Cookies de refresh: verificar `Secure=Always` + `SameSite` correcto en Production tras activar
  ForwardedHeaders (el request interno llega por HTTP desde nginx).

### 4.3 Host — seed de admin en Production

- `OpcionesSeedAdmin` (sección `SeedSettings`): `AdminEmail`, `AdminPassword`, `AdminNombre`,
  `AdminCiudad`.
- `SeedAdminPlataforma` ejecutado tras `SembrarRolesAsync()` en el bloque de migraciones opt-in:
  idempotente (busca por email), crea usuario + rol `AdminPlataforma`, `EmailConfirmed = true`.
  Fallos de `CreateAsync`/`AddToRoleAsync` → `LogError` con códigos de Identity (sin PII: solo
  códigos), sin excepción. Sin credenciales configuradas → `LogWarning` y skip.
- Tests: crea admin cuando no existe; no-op cuando existe; skip sin config; log de error ante
  contraseña inválida.

### 4.4 Archivos de despliegue

- **`Dockerfile.web`** (raíz del repo): runtime-only, `FROM aspnet:10.0`,
  `COPY web/ .`, `ENTRYPOINT ["dotnet", "CaseritoApp.Host.dll"]`. El workflow lo rsync-ea a
  `/var/apps/caseritoapp/` junto al compose (la plantilla del agenteVPS los espera ahí).
- **`docker-compose.yml`** de producción (ajustar el existente en raíz): servicio único
  `caseritoapp`, `127.0.0.1:8084:8080`, `env_file: .env`, red externa `trajano-shared-network`,
  `restart: unless-stopped`, `mem_limit: 512m`, `cpus: 1.0`, volúmenes:
  - `/var/apps/caseritoapp/fotos-avisos:/data/fotos-avisos`
  - `/var/apps/caseritoapp/kyc-blobs:/data/kyc-blobs`
  - `/var/apps/caseritoapp/dataprotection-keys:/data/dataprotection-keys`
  - healthcheck con curl a `http://localhost:8080/health`.
  - Se elimina el servicio `web` y el volumen `logs` del compose actual. El compose de desarrollo
    (`docker-compose.dev.yml`) no se toca.
- **`.env.production.example`** (raíz): plantilla de producción con todas las variables (ver §5),
  sin secretos. El `.env.example` actual (dev) se conserva tal cual.
- **`.github/workflows/deploy.yml`** (endurecido el 2026-08-03; el diseño inicial
  de rsync al directorio vivo queda sustituido por releases aislados):
  - Trigger: `push` a **`master`** + `workflow_dispatch` restringido por condición
    a `master`; environment protegido `production`, permisos de solo lectura y
    concurrency sin cancelación.
  - Job `deploy`: checkout → setup-dotnet (global.json) → `dotnet publish` Release del Host →
    setup-node 22 + `npm ci` + `npm run build` (web) → copiar `web/dist` a `publish/wwwroot` →
    ssh-agent (`VPS_SSH_KEY`) + `known_hosts` prevalidado → rsync del payload a
    `/var/apps/caseritoapp/releases/<GITHUB_SHA>` → imagen
    `caseritoapp:<GITHUB_SHA>` → recreación sin build → estado Docker healthy +
    health de loopback + health HTTPS público.
  - Ante fallo posterior a la recreación restaura la imagen anterior y vuelve a
    comprobar salud. `latest` se actualiza únicamente tras el éxito.
  - Secrets: `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY` y
    `VPS_SSH_KNOWN_HOSTS` (preverificado; nunca obtenido con keyscan durante el job).
  - El `ci.yml` existente no se toca.

### 4.5 Nginx del host (lado agenteVPS, documentado)

Server block con `proxy_pass http://127.0.0.1:8084`, `client_max_body_size 12M` y **soporte
WebSocket** (`proxy_http_version 1.1` + headers `Upgrade`/`Connection`) para `/hubs`. Certbot para
`caserito.app`. No requiere cambios en el repo; ya comunicado en `11_respuestas`.

## 5. Variables de entorno de producción (`.env` del host, lo crea el agenteVPS)

| Variable | Origen | Notas |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | agenteVPS | `Server=trajano-sqlserver,1433;Database=CaseritoAppDB;User Id=caseritoapp_app;Password=…;TrustServerCertificate=True;MultipleActiveResultSets=true` |
| `Jwt__Key` | agenteVPS (aleatoria) | ≥32 bytes, fail-fast si falta |
| `Migraciones__EjecutarAlArranque` | fija `true` | migra 5 DbContexts + siembra roles |
| `SeedSettings__AdminEmail/Password/Nombre/Ciudad` | humano (canal seguro) | §3.5 |
| `Argos__Url` | fija `http://argos:5000` | contenedor argos ya en la red |
| `Argos__ApiKey` | agenteVPS (si aplica) | pendiente confirmar si argos exige `X-Service-Key` |
| `Correo__Host/Puerto/HabilitarSsl/Remitente/NombreRemitente` | `mail`/`587`/pendiente STARTTLS | relay interno sin auth |
| `AlmacenFotos__RutaBase` | `/data/fotos-avisos` | |
| `Kyc__RutaBase` | `/data/kyc-blobs` | PII cifrada |
| `DataProtection__RutaClaves` | `/data/dataprotection-keys` | key ring persistido |
| `App__UrlPublica` | `https://caserito.app` | enlaces de correo |

## 6. Flujo de despliegue (end-to-end)

1. Push a `master` → CI (`ci.yml`) ya verde.
2. `deploy.yml`: publish API + build web + merge en `publish/wwwroot` → release por SHA → VPS.
3. VPS: build de imagen por SHA + `up -d --no-build` → contenedor arranca → migraciones EF (5 contextos) → seed roles →
   seed admin → app sirve SPA + API + SignalR.
4. El workflow exige estado healthy y comprueba loopback y `https://caserito.app/health`.
5. agenteVPS: vhost + certbot + registry + backups (BD + `fotos-avisos` + `kyc-blobs` +
   `dataprotection-keys`).

## 7. Manejo de errores y riesgos

- **Sin `Jwt__Key` o cadena de conexión:** fail-fast en arranque (ValidateOnStart) — el contenedor
  no levanta y el smoke check del workflow falla. Correcto.
- **Contraseña de admin no conforme:** `LogError`, la app sigue sirviendo; se corrige el `.env` y
  se reinicia (seed idempotente reintenta).
- **Key ring de Data Protection perdido:** PII KYC indescifrable → volumen con backup (§4.4).
- **Recreate del contenedor:** volúmenes bind del host preservan fotos, blobs KYC y key ring; el
  rsync solo toca `web/`.
- **WebSocket:** si el vhost nginx no lleva headers Upgrade, el chat no conecta — verificación
  manual post-despliegue (abrir chat).
- **Rollback:** la imagen anterior se captura antes de recrear. Un fallo de salud
  la restaura automáticamente; cada entrega conserva su tag por SHA y `latest`
  solo cambia después de validar la nueva imagen.

## 8. Testing

- Tests unitarios nuevos: `DataProtectionEncryptor` (round-trip, tamper, purpose), seed admin
  (crea/no-op/skip/log), handler de confirmación con `App__UrlPublica`.
- Tests de integración existentes deben seguir verdes (Passthrough se mantiene en
  Development/Testing; orden de endpoints sin cambios salvo el fallback SPA, que no afecta al
  contrato OpenAPI porque no genera documento).
- Verificación local pre-push: `dotnet test` (Host + Identity), `npm run build` (web), build de la
  imagen runtime-only con un publish local + `docker run` smoke (opcional, si Docker local
  disponible).
- Verificación post-despliegue (agenteVPS + humano): `/health`, registro + confirmación de email
  (correo real vía `mail`), login admin, chat WebSocket, subida de foto de aviso, flujo KYC contra
  argos.

## 9. Dependencias externas pendientes (no bloquean el código, sí el primer deploy)

- Respuesta del agenteVPS: STARTTLS en `mail:587`, API key de ARGOS.
- Humano: secrets GitHub (`VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`), credenciales admin y `Jwt__Key`
  por canal seguro.
- agenteVPS: crear BD + login, `.env` en host, carpetas de volúmenes, vhost + certbot.
