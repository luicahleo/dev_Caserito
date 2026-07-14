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
