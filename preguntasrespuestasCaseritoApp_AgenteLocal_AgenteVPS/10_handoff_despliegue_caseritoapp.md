# Handoff — Despliegue de `caseritoapp` en la arquitectura Trajano

**Fecha:** 2026-07-30
**De:** agente del VPS → **Para:** agente local (repo caseritoapp)
**Tipo:** aplicación standalone pública (mismo patrón que `decoraciones`)
**Stack asumido:** .NET 10 + SQL Server (si difiere, avisar antes de desplegar)

---

## 1. Recursos ya asignados (registrados en la guía y reservados)

| Recurso | Valor |
|---|---|
| Subdominio | **`caserito.app`** — DNS ya creado en IONOS, resuelve a `194.164.171.217` ✅ |
| Puerto del host | **`8084`** → contenedor `8080`, bind **`127.0.0.1`** (solo alcanzable por nginx) |
| Contenedor / imagen / carpeta | **`caseritoapp`** / `/var/apps/caseritoapp/` |
| Base de datos | **`CaseritoAppDB`** (convención PascalCase + `DB`) en `trajano-sqlserver` |
| Login SQL dedicado | **`caseritoapp_app`** (`db_owner` solo sobre `CaseritoAppDB`) — lo creo yo |
| Clave SSH de deploy | `github-actions-caseritoapp` (ed25519) — **ya generada y autorizada** en el VPS |
| Red Docker | `trajano-shared-network` (external, ya existe) |

## 2. Lo que el repo debe traer

### 2.1 `Dockerfile.web` (estándar del VPS)

Solo copia binarios publicados por CI y arranca la DLL. Igual que decoraciones/trajano-registry:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY web/ .
ENTRYPOINT ["dotnet", "<CaseritoApp>.Web.dll"]   # ajustar al nombre real de la DLL
```

### 2.2 `docker-compose.yml` (plantilla — ajustar nombres de DLL/health)

```yaml
services:
  caseritoapp:
    build:
      context: .
      dockerfile: Dockerfile.web
    image: caseritoapp:latest
    container_name: caseritoapp
    restart: unless-stopped
    env_file: .env
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_HTTP_PORTS=8080
      - TZ=America/La_Paz
    ports:
      - "127.0.0.1:8084:8080"
    volumes:
      - /var/apps/caseritoapp/uploads:/app/wwwroot/uploads   # solo si sube archivos
      - /var/apps/caseritoapp/logs:/app/logs
    networks:
      - trajano-shared-network
    mem_limit: 512m
    cpus: 1.0

networks:
  trajano-shared-network:
    external: true
```

### 2.3 Workflow GitHub Actions

Copiar el patrón de `decoraciones`/`trajano-registry`: push a `master` + `workflow_dispatch` →
`dotnet publish` → rsync a `/var/apps/caseritoapp/web/` (y de `Dockerfile.web` +
`docker-compose.yml` a `/var/apps/caseritoapp/`) → SSH: `docker build -f Dockerfile.web` +
`docker compose up -d`.

**Secrets del repo** (el humano los crea; mismos nombres de siempre):
- `VPS_HOST` = `194.164.171.217`
- `VPS_USER` = `root`
- `VPS_SSH_KEY` = contenido de `~/.ssh/github_actions_caseritoapp` (privada; el humano la obtiene
  del VPS con `cat ~/.ssh/github_actions_caseritoapp`)

### 2.4 Requisitos de la aplicación (convenciones del VPS)

- **Migraciones EF Core al arranque** (el login tendrá `db_owner`, así que pasan solas).
- **Seed del admin idempotente en Production**, leyendo credenciales de `SeedSettings__AdminEmail` /
  `SeedSettings__AdminPassword` / `SeedSettings__AdminFullName`. Política de contraseña: ≥8 chars,
  mayúscula, minúscula, dígito **y símbolo**. Si el seed falla, debe **loguear el error** (no tragarlo
  — lección de decoraciones).
- **HTTPS apagado en la app + ForwardedHeaders activado** (nginx termina el TLS).
- **Logs Serilog a `/app/logs`** con rolling diario (`retainedFileCountLimit: 30`).
- **Sin secretos en el repo**: la cadena de conexión y credenciales llegan por `.env` del host
  (lo creo yo). En el código, leer `ConnectionStrings__DefaultConnection` del entorno.
- Si envía correo: SMTP interno **`mail:587`** (sin auth, solo red Docker). Las credenciales del relay
  ya están en el contenedor `mail`; la app solo apunta a ese host.
- **Health endpoint** recomendado (`/api/health` o `/health`) para que trajano-registry lo sondee.

## 3. Lo que hace el agente del VPS (yo) cuando el repo esté listo

1. Crear `/var/apps/caseritoapp/{web,uploads,logs,backups}`.
2. Crear BD `CaseritoAppDB` + login `caseritoapp_app` (`db_owner`).
3. Crear `.env` (chmod 600) con: `ConnectionStrings__DefaultConnection`
   (`Server=trajano-sqlserver,1433;Database=CaseritoAppDB;User Id=caseritoapp_app;...`),
   `SeedSettings__*` (credenciales admin que me pase el humano).
4. Crear el vhost nginx `caserito.app` (`proxy_pass http://127.0.0.1:8084`,
   `client_max_body_size 12M` o lo que indique la app) + `certbot --nginx` (Let's Encrypt).
5. Registrar en **trajano-registry**: aplicación, puerto 8084, subdominio, BD, conexiones
   (caseritoapp → sqlserver, y → mail si aplica).
6. Script de backup por cron de `CaseritoAppDB` (+ uploads si aplica).
7. Actualizar la guía de despliegue (tabla de puertos: siguiente libre pasa a 8085).

## 4. Orden de ejecución

1. **Agente local:** prepara el repo (§2) y avisa. 
2. **Humano:** crea los 3 secrets en GitHub y me pasa (por canal seguro) las credenciales del admin
   de producción.
3. **Agente VPS (yo):** ejecuto §3 pasos 1–4.
4. **Humano:** dispara el workflow (primer deploy).
5. **Agente VPS (yo):** verifico (logs, migraciones, seed admin, `curl https://caserito.app`)
   y cierro registro/backups (§3 pasos 5–7).

## 5. Pendientes de confirmar (humano / agente local)

- [ ] ¿La app sube archivos? (define el volumen `uploads/` y `client_max_body_size` de nginx)
- [ ] ¿Envía correo? (conexión a `mail:587`)
- [ ] Nombre real de la `.csproj`/DLL para el Dockerfile y el workflow
- [ ] Credenciales del admin de producción (canal seguro, no en el repo)
- [ ] ¿Necesita alguna variable de entorno adicional? (listarlas y las meto en el `.env`)
