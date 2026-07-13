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
