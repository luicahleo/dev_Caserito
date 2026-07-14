# Contenedorización + persistencia base (Fase 1 — Bloque A) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Llevar dev y test a contenedores y conectar el host a SQL Server: wiring de persistencia (migrate-on-dev), Dockerfiles (dev multi-stage no-root + plantillas prod), `docker-compose.dev.yml` (sqlserver + api + web), `.env.example`, `rebuild.ps1/.sh`, base de tests con Testcontainers.MsSql, y plantillas de prod. Sin features de auth.

**Architecture:** Replica las convenciones de las apps de referencia del usuario (ICARUS/Decoraciones): compose dev que construye desde fuente, SQL Server en contenedor con healthcheck + `depends_on`, cadena de conexión por env con host = nombre de servicio, migración automática solo en Development, tests con Testcontainers bajo entorno `Testing`. Mejoras: usuario no-root, secretos fuera del compose.

**Tech Stack:** Docker + docker compose, SQL Server 2022 (contenedor), .NET 10 (EF Core + SQL Server), Node 22 (Vite), Testcontainers.MsSql, xUnit.

## Global Constraints

- **Docker es prerrequisito** de varios pasos (build de imágenes, `compose up`, tests con Testcontainers). Los pasos marcados **[Docker]** requieren un daemon disponible; si el entorno de ejecución no tiene Docker, produce los artefactos y valida con `docker compose config`/revisión, y deja la verificación de arranque/tests para una máquina con Docker (documentándolo).
- **Sin secretos versionados**: ninguna contraseña en archivos commiteados. La password de SA vive en `.env` (no versionado) y `.env.example` trae placeholders. Para el flujo híbrido (api en host), usar `dotnet user-secrets`.
- **Usuario no-root** en todos los Dockerfiles.
- **Migración automática solo en `Development`** (`if (app.Environment.IsDevelopment())`); nunca en `Testing`/`Production`.
- CPM: versiones de paquetes .NET en `CaseritoApp/Directory.Packages.props` (sin `Version=` en csproj).
- Puerto de la API en contenedor: **8080** (no-root no puede bindear 80).
- Nombres/comentarios en español. El rigor estricto del repo sigue vigente (build 0 warnings, format, lint).
- SQL Server imagen `mcr.microsoft.com/mssql/server:2022-latest`, `MSSQL_PID=Developer`.

## Fuera de alcance (a bloques posteriores)

Auth/Identity y su primera migración real (Bloque B); deploy efectivo a la VPS, NGINX/TLS, secretos de prod, usuario de BD dedicado.

---

### Task 1: Wiring de persistencia (DbContext + migrate-on-dev) — [sin Docker]

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/appsettings.Development.json` (o modificar si existe)
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj` (referencia a Identity.Infrastructure)
- Modify: `CaseritoApp/tests/CaseritoApp.IntegrationTests/HealthEndpointTests.cs` (fijar entorno Testing)

**Interfaces:**
- Produces: el host registra `IdentityDbContext` con `UseSqlServer` **solo si hay cadena de conexión**, y migra al arrancar solo en Development. El `/health` sigue sin depender de la BD.

- [ ] **Step 1: Referenciar el módulo Identity desde el host**

Run (desde `CaseritoApp/`):
```bash
dotnet add src/Host/CaseritoApp.Host reference src/Identity/CaseritoApp.Identity.Infrastructure
```

- [ ] **Step 2: Registrar persistencia en `Program.cs`**

En `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`, tras crear `builder` y antes de `builder.Build()`, añade el registro guardado por cadena de conexión:
```csharp
using CaseritoApp.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;

// ... dentro de la configuración de servicios:
var cadena = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(cadena))
{
    builder.Services.AddDbContext<IdentityDbContext>(opciones =>
        opciones.UseSqlServer(cadena));
}
```
Y tras `var app = builder.Build();`, antes de mapear endpoints:
```csharp
if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(cadena))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}
```
> Sin cadena de conexión (p. ej. tests `/health` sin BD), no se registra el DbContext ni se migra — el host arranca igual. Con cadena (contenedor dev o user-secrets), migra en Development (hoy no-op: aún no hay migraciones).

- [ ] **Step 3: `appsettings.Development.json` SIN password**

Crea/edita `CaseritoApp/src/Host/CaseritoApp.Host/appsettings.Development.json` con la cadena **sin credenciales embebidas** (la password llega por user-secrets o env):
```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } }
}
```
> No pongas `ConnectionStrings` con password aquí. En híbrido, el dev ejecuta:
> `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=CaseritoDb;User Id=sa;Password=<tu-pass>;TrustServerCertificate=True;MultipleActiveResultSets=true"` (inicializa user-secrets en el csproj del host si hace falta). En contenedor, lo inyecta el compose.

- [ ] **Step 4: El test de /health corre bajo `Testing`**

Edita `CaseritoApp/tests/CaseritoApp.IntegrationTests/HealthEndpointTests.cs` para fijar el entorno `Testing` (sin BD, sin migración), sustituyendo el uso directo de la factory por una que fije el entorno:
```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_responde_200()
    {
        var cliente = factory
            .WithWebHostBuilder(b => b.UseEnvironment("Testing"))
            .CreateClient();
        var respuesta = await cliente.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
```
Lo importante: entorno `Testing` (no migra ni conecta a BD); `using Microsoft.Extensions.Hosting;` puede quitarse si no se usa.

- [ ] **Step 5: Verificar (sin Docker)**

Run (desde `CaseritoApp/`):
```bash
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
```
Expected: build 0/0; tests verdes (el `/health` pasa bajo Testing sin BD). `dotnet format CaseritoApp.sln --verify-no-changes` limpio.

- [ ] **Step 6: Commit**

```bash
cd ..
git add CaseritoApp/src/Host CaseritoApp/tests/CaseritoApp.IntegrationTests
git commit -m "feat(host): wiring de persistencia SQL Server + migrate-on-dev (guardado por conexión)"
```

---

### Task 2: Dockerfile del API (dev multi-stage, no-root) — [Docker]

**Files:**
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Dockerfile`
- Create: `CaseritoApp/.dockerignore`

**Interfaces:**
- Produces: imagen dev de la API que compila desde fuente y corre como usuario no-root en el puerto 8080. Contexto de build = `CaseritoApp/`.

- [ ] **Step 1: `.dockerignore` (contexto CaseritoApp)**

`CaseritoApp/.dockerignore`:
```
**/bin/
**/obj/
**/.vs/
**/*.user
Documentacion/
.git/
```

- [ ] **Step 2: Dockerfile**

`CaseritoApp/src/Host/CaseritoApp.Host/Dockerfile`:
```dockerfile
# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore CaseritoApp.sln
RUN dotnet publish src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# Runtime (no-root)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "CaseritoApp.Host.dll"]
```
> Las imágenes .NET 8+ traen un usuario `app` no-root pre-creado; `USER app` lo activa. Puerto 8080 (no-root no bindea 80).

- [ ] **Step 3: Verificar build de la imagen [Docker]**

Run (desde `CaseritoApp/`):
```bash
docker build -f src/Host/CaseritoApp.Host/Dockerfile -t caserito-api:dev .
```
Expected: build exitoso. Comprueba usuario no-root:
```bash
docker run --rm caserito-api:dev whoami
```
Expected: imprime `app` (no `root`).
> Si el entorno no tiene Docker: valida la sintaxis del Dockerfile por revisión y deja este build para una máquina con Docker (documenta en el reporte).

- [ ] **Step 4: Commit**

```bash
cd ..
git add CaseritoApp/.dockerignore CaseritoApp/src/Host/CaseritoApp.Host/Dockerfile
git commit -m "feat(docker): Dockerfile dev del API (multi-stage, no-root)"
```

---

### Task 3: Dockerfile del web + proxy de Vite por env — [Docker]

**Files:**
- Create: `web/Dockerfile`
- Create: `web/.dockerignore`
- Modify: `web/vite.config.ts` (target del proxy por variable de entorno)

**Interfaces:**
- Produces: imagen dev del frontend que corre el dev server de Vite accesible en la red del compose; el proxy apunta al servicio `api` cuando se define `VITE_API_PROXY_TARGET`.

- [ ] **Step 1: `.dockerignore` (contexto web)**

`web/.dockerignore`:
```
node_modules/
dist/
dev-dist/
.git/
```

- [ ] **Step 2: Proxy de Vite por env**

En `web/vite.config.ts`, cambia el target fijo del proxy por una variable de entorno con fallback al puerto de dev en host (5245):
```ts
const apiTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5245';
// ...
  server: {
    host: true,
    proxy: {
      '/health': apiTarget,
      '/api': apiTarget,
    },
  },
```

- [ ] **Step 3: Dockerfile dev del web**

`web/Dockerfile`:
```dockerfile
FROM node:22-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
EXPOSE 5173
ENV CHOKIDAR_USEPOLLING=true
USER node
CMD ["npm", "run", "dev", "--", "--host"]
```
> `CHOKIDAR_USEPOLLING=true` para que el HMR detecte cambios en bind mounts sobre Docker/Windows. `USER node` (usuario no-root de la imagen oficial).

- [ ] **Step 4: Verificar [Docker]**

Run (desde `web/`):
```bash
docker build -t caserito-web:dev .
```
Expected: build exitoso. (El arranque real se prueba vía compose en la Task 4.)
> Sin Docker: valida sintaxis por revisión; deja el build para máquina con Docker.
Verifica además que `npm run build` local sigue verde tras el cambio de `vite.config.ts` (esto NO necesita Docker):
```bash
npm run build && npm run typecheck
```

- [ ] **Step 5: Commit**

```bash
cd ..
git add web/Dockerfile web/.dockerignore web/vite.config.ts
git commit -m "feat(docker): Dockerfile dev del web + proxy de Vite por env"
```

---

### Task 4: docker-compose.dev.yml + .env.example + scripts — [Docker]

**Files:**
- Create: `docker-compose.dev.yml` (raíz)
- Create: `.env.example` (raíz)
- Create: `rebuild.ps1`, `rebuild.sh` (raíz)
- Modify: `.gitignore` (asegurar `.env` ignorado)

**Interfaces:**
- Produces: `docker compose -f docker-compose.dev.yml up` levanta sqlserver+api+web; `rebuild.ps1` es el atajo.

- [ ] **Step 1: `.env.example` y `.gitignore`**

`.env.example`:
```
# Password de SA para el SQL Server de desarrollo (NO usar en prod)
SA_PASSWORD=Caserito_Dev_Pass123!
```
Añade `.env` a `.gitignore` (raíz) si no está.

- [ ] **Step 2: `docker-compose.dev.yml`**

`docker-compose.dev.yml`:
```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: caserito-sqlserver
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_PID: "Developer"
      MSSQL_SA_PASSWORD: "${SA_PASSWORD}"
    ports:
      - "1433:1433"
    volumes:
      - caserito-sqlserver-data:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$SA_PASSWORD\" -C -Q 'SELECT 1' || exit 1"]
      interval: 15s
      timeout: 10s
      retries: 5
      start_period: 30s
    restart: unless-stopped

  api:
    build:
      context: ./CaseritoApp
      dockerfile: src/Host/CaseritoApp.Host/Dockerfile
    container_name: caserito-api
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ASPNETCORE_HTTP_PORTS: "8080"
      ConnectionStrings__DefaultConnection: "Server=sqlserver,1433;Database=CaseritoDb;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;MultipleActiveResultSets=true"
    ports:
      - "8080:8080"
    depends_on:
      sqlserver:
        condition: service_healthy
    restart: unless-stopped

  web:
    build:
      context: ./web
    container_name: caserito-web
    environment:
      VITE_API_PROXY_TARGET: "http://api:8080"
    ports:
      - "5173:5173"
    volumes:
      - ./web:/app
      - /app/node_modules
    depends_on:
      - api
    restart: unless-stopped

volumes:
  caserito-sqlserver-data:
```

- [ ] **Step 3: `rebuild.ps1` y `rebuild.sh`**

`rebuild.ps1`:
```powershell
param([switch]$Logs)
docker compose -f docker-compose.dev.yml up -d --build
if ($Logs) { docker compose -f docker-compose.dev.yml logs -f }
```
`rebuild.sh`:
```sh
#!/bin/sh
docker compose -f docker-compose.dev.yml up -d --build
if [ "$1" = "--logs" ]; then docker compose -f docker-compose.dev.yml logs -f; fi
```

- [ ] **Step 4: Verificar [Docker]**

Run (desde la raíz):
```bash
cp .env.example .env
docker compose -f docker-compose.dev.yml config
docker compose -f docker-compose.dev.yml up -d --build
```
Espera a que `sqlserver` esté healthy y verifica:
```bash
docker compose -f docker-compose.dev.yml ps
curl -fsS http://localhost:8080/health
```
Expected: los 3 servicios arriba; `/health` → 200 (con `{"estado":"ok"}`); la SPA carga en `http://localhost:5173`. Luego `docker compose -f docker-compose.dev.yml down` (sin `-v`, para conservar datos).
> `docker compose config` valida el YAML aunque no haya daemon corriendo el `up`. Si no hay Docker: valida con `config` y deja el `up` para máquina con Docker.

- [ ] **Step 5: Commit**

```bash
git add docker-compose.dev.yml .env.example rebuild.ps1 rebuild.sh .gitignore
git commit -m "feat(docker): compose dev (sqlserver+api+web) + .env.example + rebuild scripts"
```

---

### Task 5: Base de tests con Testcontainers — [Docker]

**Files:**
- Modify: `CaseritoApp/Directory.Packages.props` (Testcontainers.MsSql)
- Modify: `CaseritoApp/tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj`
- Create: `CaseritoApp/tests/CaseritoApp.IntegrationTests/Infrastructure/CaseritoApiFactory.cs`
- Create: `CaseritoApp/tests/CaseritoApp.IntegrationTests/PersistenciaSmokeTests.cs`

**Interfaces:**
- Consumes: `Program`, `IdentityDbContext`.
- Produces: `CaseritoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` que levanta un `MsSqlContainer` y expone la app en entorno `Testing` apuntando a ese contenedor; un test que arranca el app contra SQL real y verifica `/health` + que la BD conecta.

- [ ] **Step 1: Paquete**

Añade a `CaseritoApp/Directory.Packages.props`:
```xml
    <PackageVersion Include="Testcontainers.MsSql" Version="4.4.0" />
```
Y a `CaseritoApp/tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj`:
```xml
    <PackageReference Include="Testcontainers.MsSql" />
```
(sin `Version=`; CPM). Referencia también el proyecto Identity.Infrastructure si el test usa `IdentityDbContext`:
```bash
cd CaseritoApp
dotnet add tests/CaseritoApp.IntegrationTests reference src/Identity/CaseritoApp.Identity.Infrastructure
```

- [ ] **Step 2: Factory con Testcontainers**

`CaseritoApp/tests/CaseritoApp.IntegrationTests/Infrastructure/CaseritoApiFactory.cs`:
```csharp
using CaseritoApp.Identity.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace CaseritoApp.IntegrationTests.Infrastructure;

public sealed class CaseritoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(servicios =>
        {
            servicios.RemoveAll(typeof(DbContextOptions<IdentityDbContext>));
            servicios.AddDbContext<IdentityDbContext>(o => o.UseSqlServer(_sql.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync() => await _sql.DisposeAsync();
}
```
> `RemoveAll` requiere `using Microsoft.Extensions.DependencyInjection.Extensions;`. Como el host solo registra el DbContext si hay cadena de conexión (Task 1) y en Testing no la hay, aquí lo registramos nosotros apuntando al contenedor.

- [ ] **Step 3: Test smoke de persistencia**

`CaseritoApp/tests/CaseritoApp.IntegrationTests/PersistenciaSmokeTests.cs`:
```csharp
using System.Net;
using CaseritoApp.IntegrationTests.Infrastructure;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class PersistenciaSmokeTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task App_arranca_contra_sql_real_y_health_responde_200()
    {
        var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
```

- [ ] **Step 4: Verificar [Docker]**

Run (desde `CaseritoApp/`):
```bash
dotnet test CaseritoApp.sln
```
Expected: verde, incluyendo `PersistenciaSmokeTests` (Testcontainers levanta SQL efímero, migra, y `/health` responde). Requiere Docker.
> Sin Docker: el test de Testcontainers no puede correr; documenta y deja su ejecución para CI/máquina con Docker. El resto de la suite debe seguir verde.

- [ ] **Step 5: Commit**

```bash
cd ..
git add CaseritoApp/Directory.Packages.props CaseritoApp/tests/CaseritoApp.IntegrationTests
git commit -m "test: base de integración con Testcontainers.MsSql + smoke de persistencia"
```

---

### Task 6: Plantillas de producción — [validación con config]

**Files:**
- Create: `docker-compose.yml` (raíz, plantilla prod)
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Dockerfile.prod`
- Create: `web/Dockerfile.prod`

**Interfaces:**
- Produces: plantillas de deploy (imágenes runtime + red externa compartida). Deploy real diferido.

- [ ] **Step 1: `docker-compose.yml` (prod, plantilla)**

`docker-compose.yml`:
```yaml
services:
  api:
    image: caserito-api:latest
    container_name: caserito-api
    env_file: .env
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_HTTP_PORTS: "8080"
      TZ: "America/La_Paz"
    ports:
      - "127.0.0.1:8080:8080"
    networks: [trajano-shared-network]
    restart: always

  web:
    image: caserito-web:latest
    container_name: caserito-web
    environment:
      TZ: "America/La_Paz"
    ports:
      - "127.0.0.1:8090:80"
    networks: [trajano-shared-network]
    restart: always

networks:
  trajano-shared-network:
    external: true
```
> Sin servicio SQL: la BD de prod es externa/compartida (cadena en `.env`). NGINX/TLS en otro stack, en la red externa. Deploy real (CI+VPS) diferido.

- [ ] **Step 2: `Dockerfile.prod` del API (runtime-only)**

`CaseritoApp/src/Host/CaseritoApp.Host/Dockerfile.prod`:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY publish/ ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "CaseritoApp.Host.dll"]
```
> Copia los binarios que el CI publica (`dotnet publish` → `publish/`), como en ICARUS.

- [ ] **Step 3: `Dockerfile.prod` del web (build → nginx)**

`web/Dockerfile.prod`:
```dockerfile
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
```

- [ ] **Step 4: Verificar (config)**

Run (desde la raíz):
```bash
docker compose -f docker-compose.yml config
```
Expected: el YAML valida (parsea). `config` puede advertir de la red externa/imágenes ausentes — es esperado en plantilla (no hacemos `up`).
> Si no hay Docker, valida el YAML por revisión.

- [ ] **Step 5: Commit**

```bash
git add docker-compose.yml CaseritoApp/src/Host/CaseritoApp.Host/Dockerfile.prod web/Dockerfile.prod
git commit -m "feat(docker): plantillas de producción (compose + Dockerfiles runtime)"
```

---

### Task 7: Convenciones de contenedores en CLAUDE.md — [sin Docker]

**Files:**
- Modify: `CLAUDE.md` (raíz)

- [ ] **Step 1: Añadir sección**

Añade a `CLAUDE.md` una sección `## Contenedores y base de datos`:
```markdown
## Contenedores y base de datos

Dev y test corren en contenedores (SQL Server 2022 + api + web).

- Levantar todo (dev): `./rebuild.ps1` (o `docker compose -f docker-compose.dev.yml up -d --build`). Requiere un `.env` (copiar de `.env.example`).
- BD: SQL Server en contenedor; cadena por env `ConnectionStrings__DefaultConnection` (host = `sqlserver` en compose). Migración automática **solo en Development** al arrancar; prod es controlada.
- Flujo híbrido (api en host contra SQL en contenedor): `docker compose -f docker-compose.dev.yml up -d sqlserver` + `dotnet user-secrets` con la cadena a `localhost,1433`. La password de SA vive en `.env`/user-secrets, nunca versionada.
- Tests de integración: **Testcontainers.MsSql** bajo entorno `Testing` (`CaseritoApiFactory`); requieren Docker.
- Prod: `docker-compose.yml` (plantilla) con imágenes runtime y red externa `trajano-shared-network` (NGINX/TLS fuera del repo). Deploy a VPS diferido.
- Dockerfiles corren como usuario **no-root**; API en puerto 8080.
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: convenciones de contenedores y BD en CLAUDE.md"
```

---

## Verificación end-to-end (al terminar)

Con Docker disponible, desde la raíz:
1. `cp .env.example .env` y `./rebuild.ps1` → `docker compose -f docker-compose.dev.yml ps` muestra sqlserver (healthy) + api + web arriba.
2. `curl http://localhost:8080/health` → 200; la SPA en `http://localhost:5173` muestra "Backend: ok".
3. Desde `CaseritoApp/`: `dotnet build` 0/0, `dotnet format --verify-no-changes` limpio, `dotnet test` verde (incluye el smoke de Testcontainers).
4. `docker run --rm caserito-api:dev whoami` → `app` (no-root).
5. `docker compose -f docker-compose.yml config` valida la plantilla de prod.
6. No hay contraseñas en archivos versionados (`.env` git-ignored; solo `.env.example` con placeholder).
7. `CLAUDE.md` documenta el flujo de contenedores.
