# Diseño: Contenedorización + persistencia base (Fase 1 — Bloque A)

- **Fecha**: 2026-07-14
- **Estado**: Aprobado (brainstorming)
- **Alcance**: Llevar todo el entorno de **desarrollo y test a contenedores** y conectar el host a **SQL Server**, replicando las convenciones de las apps de referencia del usuario (`ICARUS`, `Decoraciones`). NO implementa features de auth (eso es el Bloque B). Habilita que el Bloque B (ASP.NET Core Identity) tenga BD real y tests con Testcontainers.

## Contexto

CaseritoApp (monolito modular .NET 10 + frontend React en `web/`, ya en `master`). El equipo desarrolla y despliega **todo dentro de contenedores** (dev + test en Docker; producción en una VPS con contenedores). Referencias del propio usuario: `C:\Users\lrcahuana\source\repos\dev\ICARUS` y `C:\Users\lrcahuana\source\repos\Decoraciones`. Hasta ahora el host arrancaba sin BD; este bloque introduce la persistencia y la capa de contenedores.

Convenciones extraídas de las referencias (a replicar):
- `docker-compose.dev.yml` que **construye desde fuente** (Dockerfiles multi-stage, copia de `.csproj` primero para cachear el restore) e incluye **SQL Server** en contenedor con **healthcheck** (`sqlcmd`) y `depends_on: condition: service_healthy`.
- Cadena de conexión inyectada por env `ConnectionStrings__DefaultConnection`, usando el **nombre del servicio** como host.
- **Migración automática solo en `Development`** al arrancar; esquema de producción controlado (manual), nunca auto-migrado en prod.
- **Tests de integración con `Testcontainers.MsSql`** + `WebApplicationFactory<Program>` bajo entorno `Testing`, reemplazando el `DbContextOptions` por la cadena del contenedor efímero.
- `docker-compose.yml` de producción con **imágenes runtime** (binarios publicados por CI), sin servicio de BD, unido a una **red Docker externa compartida** (NGINX/TLS fuera del repo), `TZ=America/La_Paz`, puerto solo a loopback.
- Script **`rebuild.ps1`** (`docker compose -f docker-compose.dev.yml up -d --build`, con flags `-All`/`-Logs`).

**Mejoras deliberadas sobre las referencias** (marcadas por la exploración): **usuario no-root** en los Dockerfiles y **secretos fuera del compose** (en `.env` no versionado + `.env.example`; en dev, user-secrets para .NET). Nunca contraseñas en texto plano versionadas.

## Decisiones tomadas

| Tema | Decisión |
|---|---|
| Entorno dev | `docker-compose.dev.yml` con **sqlserver + api + web**, build desde fuente |
| Motor BD | **SQL Server 2022** (`mcr.microsoft.com/mssql/server:2022-latest`, `MSSQL_PID=Developer`) en contenedor, volumen persistente, healthcheck `sqlcmd` |
| Conexión | `ConnectionStrings__DefaultConnection` por env; host = nombre de servicio (`sqlserver,1433`) |
| Migración | `MigrateAsync()` en el arranque **solo si `Development`** (ni en `Testing` ni `Production`) |
| Tests | **Testcontainers.MsSql** + `WebApplicationFactory<Program>` bajo entorno `Testing` |
| Prod | `docker-compose.yml` plantilla: imágenes runtime, red externa compartida, loopback, TZ; **deploy real diferido** (aún no hay VPS/dominio) |
| Scripts | `rebuild.ps1` + `rebuild.sh` (paridad) |
| Seguridad | Usuario **no-root** en Dockerfiles; secretos en `.env`/user-secrets (no versionados), `.env.example` como plantilla |

## Estructura en el repo

```
dev_Caserito/
├─ docker-compose.dev.yml        (sqlserver + api + web, build desde fuente)
├─ docker-compose.yml            (PLANTILLA prod: imágenes runtime + red externa; deploy diferido)
├─ .env.example                  (placeholders: password SA dev, cadena de conexión, puertos)
├─ .dockerignore                 (por contexto de build)
├─ rebuild.ps1 / rebuild.sh      (up -d --build del compose dev; flags -All/-Logs)
├─ CaseritoApp/
│  └─ src/Host/CaseritoApp.Host/
│     ├─ Dockerfile              (dev multi-stage: sdk build → aspnet runtime, no-root, EXPOSE 8080)
│     └─ Dockerfile.prod         (runtime-only: copia binarios publicados; para el deploy futuro)
└─ web/
   ├─ Dockerfile                 (dev: node, vite dev server con --host, EXPOSE 5173)
   └─ Dockerfile.prod            (multi-stage: node build → nginx sirve dist)
```

## Servicios del `docker-compose.dev.yml`

- **`sqlserver`**: `mcr.microsoft.com/mssql/server:2022-latest`; env `ACCEPT_EULA=Y`, `MSSQL_PID=Developer`, `MSSQL_SA_PASSWORD=${SA_PASSWORD}` (desde `.env`); puerto `1433:1433`; volumen `caserito-sqlserver-data:/var/opt/mssql`; **healthcheck** con `sqlcmd -C -Q "SELECT 1"`; `restart: unless-stopped`.
- **`api`**: build desde `CaseritoApp/src/Host/CaseritoApp.Host/Dockerfile` (contexto = `CaseritoApp/`); env `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_HTTP_PORTS=8080`, `ConnectionStrings__DefaultConnection=Server=sqlserver,1433;Database=CaseritoDb;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;MultipleActiveResultSets=true`; puerto `8080:8080`; **`depends_on: sqlserver: condition: service_healthy`**.
- **`web`**: build desde `web/Dockerfile` (dev); monta `./web` (con node_modules en volumen anónimo para no pisarlo), corre `npm run dev -- --host`, puerto `5173:5173`; el **proxy de Vite** apunta al servicio `api` por su nombre de red (`http://api:8080`) mediante una variable de entorno leída en `vite.config.ts` (fallback a `http://localhost:5245` para ejecución fuera de contenedor). `depends_on: api`.

Red default de compose; volúmenes con nombre explícito (`caserito-sqlserver-data`).

## Persistencia (host .NET)

- El host registra un `DbContext` (el del contexto **Identity**, que en el Bloque B recibirá las tablas de ASP.NET Core Identity) con `UseSqlServer(configuration.GetConnectionString("DefaultConnection"))`.
- `appsettings.Development.json`: cadena a `Server=localhost,1433;…` (permite el flujo híbrido "api en host + SQL en contenedor"); el compose la sobreescribe con host = `sqlserver`.
- Al arrancar, `if (app.Environment.IsDevelopment()) await db.Database.MigrateAsync();` — hoy es no-op (no hay migraciones aún); el Bloque B añade la primera migración. En `Testing`/`Production` no auto-migra.
- El host debe seguir arrancando aunque `/health` no dependa de la BD; pero con la BD wired, la migración dev corre al inicio.

## Tests con Testcontainers

- El proyecto `CaseritoApp.IntegrationTests` gana una **fixture base** (`CaseritoApiFactory : WebApplicationFactory<Program>`) que: levanta un `MsSqlContainer` (`new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build()`) en `InitializeAsync`, y en `ConfigureWebHost` fija el entorno `Testing` y reemplaza la cadena/`DbContextOptions` por `container.GetConnectionString()`.
- Paquete: `Testcontainers.MsSql`.
- Un test de este bloque prueba **end-to-end la base**: el app arranca contra un SQL Server real en contenedor y `GET /health` responde 200 (demuestra que el wiring de persistencia no rompe el arranque). El test `/health` existente sin BD se conserva.
- Requiere Docker para correr los tests; el CI de GitHub Actions (`ubuntu-latest`) ya trae Docker, así que el job de backend corre estos tests.

## Producción (plantilla, deploy diferido)

- `docker-compose.yml` (prod) como **plantilla** mirroring ICARUS: servicios `api` y `web` con `image: caserito-*:latest`, `restart: always`, `env_file: .env`, `TZ=America/La_Paz`, unidos a `trajano-shared-network` (`external: true`), puertos a loopback. **No** levanta SQL Server (BD gestionada/compartida en la VPS).
- Dockerfiles `*.prod` runtime-only (copian binarios/`dist` publicados por CI).
- El pipeline de deploy a la VPS (rsync + `docker compose up`) y los secretos reales **se configuran cuando exista la VPS/dominio** — fuera de alcance de este bloque; aquí solo quedan las plantillas.

## Fuera de alcance (a bloques/fases posteriores)

- Features de auth/Identity, tablas y primera migración real (Bloque B).
- Deploy efectivo a la VPS, NGINX/TLS, secretos de producción.
- Usuario de BD dedicado (no-sa) para prod.
- Reverse proxy local.

## Verificación

1. `.\rebuild.ps1` (o `docker compose -f docker-compose.dev.yml up -d --build`) levanta `sqlserver`, `api` y `web`; `docker compose ps` muestra los tres, con `sqlserver` healthy.
2. `GET http://localhost:8080/health` → 200 (api en contenedor, contra SQL en contenedor).
3. La SPA en `http://localhost:5173` carga y su HomePage muestra "Backend: ok" (proxy de Vite → servicio `api`).
4. `dotnet test CaseritoApp.sln` verde, incluyendo el test de integración con Testcontainers (levanta SQL efímero y arranca el app en `Testing`). Requiere Docker.
5. Los Dockerfiles corren como usuario no-root; no hay contraseñas en archivos versionados (solo en `.env` no versionado / `.env.example` con placeholders).
6. El CI (job backend) sigue verde corriendo los tests con Docker disponible.
