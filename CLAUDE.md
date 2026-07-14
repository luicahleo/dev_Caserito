# CaseritoApp

Marketplace C2C para Bolivia (web + Android). Ver alcance en
`CaseritoApp/Documentacion/marketplace-bolivia-mvp-brief.md` y el plan por fases
en `CaseritoApp/Documentacion/plan-desarrollo-mvp-v1.md`.

## Estructura

- `CaseritoApp/` — solución .NET. Config transversal, `global.json`, `.sln`.
  - `src/` — proyectos de producción (bounded contexts; se definen en Fase 0).
  - `tests/` — proyectos de test, incluidos los de arquitectura.
- `.github/` — CI. `.claude/` — skills, agentes, hooks. `docs/superpowers/` — specs y planes.

## Arquitectura

Clean Architecture. Bounded contexts (a poblar en Fase 0): Identity, Catalog,
Chat, Orders, Reputation, Notifications. Reglas de capa (verificadas por
`CaseritoApp.ArchitectureTests`):

- `Domain` no depende de nada hacia afuera.
- `Application` depende solo de `Domain`.
- Las dependencias apuntan siempre hacia adentro.

El patrón interno de cada contexto (CQRS/MediatR/Result) se decide en Fase 0.

## Convenciones de código

Fuente de verdad: `CaseritoApp/.editorconfig` y `CaseritoApp/Directory.Build.props`.
Rigor estricto: nullable enable, warnings-as-errors, analizadores .NET +
Roslynator + Sonar. Nada compila si viola las reglas.

- Namespaces file-scoped; `using` fuera del namespace, System primero.
- `PascalCase` tipos/miembros; `camelCase` locales; `_camelCase` campos privados; `I` en interfaces.
- Versiones de paquetes solo en `CaseritoApp/Directory.Packages.props` (CPM). Nunca `Version=` en un `.csproj`.

## Política anti-PII en logs (NO negociable)

Jamás loguear: número de CI, imágenes de documento o selfie, tokens de sesión,
cadenas de QR/pago. Usar el helper de enmascarado (`PiiRedaction`) para cualquier
campo sensible. Es el activo de mayor riesgo legal del MVP.

## Comandos

Desde `CaseritoApp/`:

- Build: `dotnet build CaseritoApp.sln`
- Test: `dotnet test CaseritoApp.sln`
- Formatear: `dotnet format CaseritoApp.sln`
- Verificar formato (como el CI): `dotnet format CaseritoApp.sln --verify-no-changes`

## Ramas

`main` estable; trabajo en ramas `feat/*`, `fix/*`, `chore/*` con PR hacia `dev`.

## Especificaciones

Diseños y planes en `docs/superpowers/specs/` y `docs/superpowers/plans/`.

## Frontend web (web/)

SPA React + TypeScript (Vite) como PWA instalable; se envuelve con Capacitor para
tiendas. Consume la Web API de `CaseritoApp/` (host .NET). Vive en `web/`.

- UI: **MUI** (Material UI) + Emotion; tema en `src/theme/`.
- Navegación: **React Router** (`src/app/router.tsx`).
- Estado de servidor/API: **TanStack Query**; la capa de API vive en `src/api/`
  (hoy cliente a mano de `/health`; en Fase 1 se genera un cliente tipado del OpenAPI).
- Calidad: TypeScript estricto, ESLint + Prettier, Vitest + React Testing Library.
- Textos de UI y comentarios en **español**.
- Nunca exponer PII en logs del cliente (mismo criterio que el backend).

Comandos (desde `web/`): `npm run dev` | `build` | `lint` | `typecheck` | `test`.
Dev: Vite proxya `/health` y `/api` al host .NET (ver `vite.config.ts`).

## Contenedores y base de datos

Dev y test corren en contenedores (SQL Server 2022 + api + web).

- Levantar todo (dev): `./rebuild.ps1` (o `docker compose -f docker-compose.dev.yml up -d --build`). Requiere un `.env` (copiar de `.env.example`).
- BD: SQL Server en contenedor; cadena por env `ConnectionStrings__DefaultConnection` (host = `sqlserver` en compose). Migración automática **solo en Development** al arrancar; prod es controlada.
- Flujo híbrido (api en host contra SQL en contenedor): `docker compose -f docker-compose.dev.yml up -d sqlserver` + `dotnet user-secrets` con la cadena a `localhost,1433`. La password de SA vive en `.env`/user-secrets, nunca versionada.
- Tests de integración: **Testcontainers.MsSql** bajo entorno `Testing` (`CaseritoApiFactory`); requieren Docker.
- Prod: `docker-compose.yml` (plantilla) con imágenes runtime y red externa `trajano-shared-network` (NGINX/TLS fuera del repo). Deploy a VPS diferido.
- Dockerfiles corren como usuario **no-root**; API en puerto 8080.

## Auth (Identity + JWT)

Autenticación y autorización mediante **ASP.NET Core Identity** + **JWT** + **Refresh Tokens** en cookie httpOnly.

### Identidad (ApplicationUser)

- `ApplicationUser` en `Identity.Domain` (hereda `IdentityUser<Guid>`). Claims: `sub` (UserId), `email`, `name`.
- BD: tablas de Identity bajo schema `identity` (migración `InicialIdentity`). Tabla `RefreshToken` (HashedToken, Expiry, RevokedAt) vinculada a usuario.

### Flujo de Auth

1. **Register** (`POST /api/auth/register`): email/password → hash bcrypt en Identity, usuario creado, retorna vacío (201).
2. **Login** (`POST /api/auth/login`): email/password → valida, genera `accessToken` (JWT corto, 15 min), siembra cookie de `refresh` (httpOnly, SameSite=Strict, Secure en prod/staging), retorna `{ accessToken }` (200).
3. **Refresh** (`POST /api/auth/refresh`): valida cookie + token en BD, rota refresh (nuevo hash), retorna nuevo `accessToken` + reemplaza cookie (200). Detecta reuso: token no es salt de otro en BD → 401.
4. **Logout** (`POST /api/auth/logout`): revoca refresh en BD (RevokedAt = now), limpia cookie (200).

### JWT y Claims

- `accessToken`: HS256, claims: `sub` (UserId), `email`, `name`, `iat`, `exp` (15 min).
- Clave en `Jwt:Key` (user-secrets en dev/test, env en prod). Nunca versionada.
- Fail-fast si `Jwt:Key` falta fuera de Development/Testing.

### Refresh Token Rotation

- Guardado hasheado en `RefreshToken.HashedToken` (mismo algo que password, ej. bcrypt).
- Al refresh: si token existe, no está revocado, y no es salt de otro → nuevo hash, invalida anterior (OptionalExpiry o marca para pruning).
- Cookie: `HttpOnly=true`, `SameSite=Strict` (CSRF), `Secure` en prod/staging, `Max-Age=7d` (7 días).

### Endpoints y Autorización

- `/api/auth/register`: POST, anónimo, `(email: string, password: string) → 201 | 400 (valdación)`.
- `/api/auth/login`: POST, anónimo, `(email, password) → { accessToken: string }` + cookie, `200 | 401 (credenciales)`.
- `/api/auth/refresh`: POST, anónimo (cookie-driven), `() → { accessToken }` + cookie, `200 | 401 (token inválido/reuso)`.
- `/api/auth/logout`: POST, anónimo (cookie-driven), `() → 200`.
- `/api/perfil`: GET/PUT, `[Authorize]` (requires bearer accessToken).
  - GET: `() → { id, email, name, ...perfil }` (CQRS query).
  - PUT: `(name, ...perfil) → 204` (CQRS command). Validaciones en `Domain`.

### Migración EF Core

Comando para crear nueva migración (ej. `InicialIdentity`):

```bash
dotnet ef migrations add InicialIdentity \
  --project src/Identity/CaseritoApp.Identity.Infrastructure \
  --startup-project src/Host/CaseritoApp.Host \
  --output-dir Migrations
```

Aplicada automáticamente en Development al arrancar (via `CaseritoApiFactory` en tests con Testcontainers).
