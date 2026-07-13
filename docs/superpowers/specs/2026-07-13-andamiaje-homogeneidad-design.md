# Diseño: Andamiaje de homogeneidad de código — CaseritoApp

- **Fecha**: 2026-07-13
- **Estado**: Aprobado (brainstorming)
- **Alcance**: Solo el andamiaje transversal de homogeneidad. NO decide topología de arquitectura, patrón interno (CQRS/MediatR/Result), stack completo, framework móvil ni modelo de datos — eso es Fase 0 del plan de desarrollo.

## Contexto

CaseritoApp es un marketplace C2C para Bolivia (ver `CaseritoApp/Documentacion/marketplace-bolivia-mvp-brief.md` y `plan-desarrollo-mvp-v1.md`). El equipo prioriza **solidez de diseño sin atajos** (brief §8.2) y se apoya fuertemente en IA para desarrollar. Para que el código sea homogéneo independientemente de quién lo escriba (humano o IA), se necesitan dos niveles complementarios:

- **Nivel A — enforcement mecánico**: reglas que la compilación y el CI aplican solos (`.editorconfig`, analizadores, warnings-as-errors, tests de arquitectura). Es la fuente de verdad.
- **Nivel B — artefactos de Claude Code**: hacen que la IA genere código que ya cumple el Nivel A (CLAUDE.md, skills, subagente, hook de formato).

Este documento define ambos. El repositorio es greenfield: hoy solo existen los dos documentos y `.claude/settings.local.json`; no hay código, git, `CLAUDE.md` ni `.editorconfig`.

## Decisiones tomadas

| Decisión | Valor |
|---|---|
| Nivel de rigor | **Estricto desde el día 1**: warnings-as-errors, nullable enable, analizadores en nivel alto |
| Enfoque de tooling | **A — Stack de calidad estándar**: analizadores .NET + Roslynator + SonarAnalyzer, sin StyleCop |
| Control de versiones / CI | **Git + GitHub Actions** |
| Artefactos de Claude | CLAUDE.md, skills de scaffolding, subagente code-reviewer, hook de auto-formato |
| Proyecto de validación | **Sí** — smoke project mínimo para verificar que el andamiaje compila y el CI pasa en verde |
| Skills de scaffolding | `nuevo-bounded-context` (estructural) ahora; `nuevo-caso-de-uso` como placeholder pendiente de patrón |
| Versión .NET | **.NET 10 (último LTS)** en `global.json`, confirmable en Fase 0 |

## Estructura de directorios

```
dev_Caserito/                         ← raíz git + raíz del proyecto Claude
├─ .claude/
│  ├─ settings.json                   (hook de auto-formato; versionado)
│  ├─ settings.local.json             (ya existe; git-ignored)
│  ├─ skills/nuevo-bounded-context/
│  └─ agents/code-reviewer-caserito.md
├─ .github/workflows/ci.yml
├─ .gitignore
├─ CLAUDE.md
├─ docs/superpowers/specs/            (este documento)
└─ CaseritoApp/                       ← la aplicación
   ├─ Documentacion/                  (ya existe)
   ├─ src/                            (bounded contexts — se pueblan en Fase 0)
   │  └─ CaseritoApp.SmokeLib/        (validación; borrable/reutilizable)
   ├─ tests/
   │  └─ CaseritoApp.ArchitectureTests/
   ├─ .editorconfig
   ├─ Directory.Build.props
   ├─ Directory.Packages.props
   ├─ global.json
   └─ CaseritoApp.sln
```

Racional de las dos raíces: el directorio de trabajo y el proyecto Claude ya están anclados en `dev_Caserito` (ahí vive `.claude/`), por lo que los artefactos de Claude y el CI se ubican ahí para ser descubiertos. La solución .NET vive bajo `CaseritoApp/` conforme lo pidió el equipo.

## Nivel A — Enforcement mecánico

### `.editorconfig` (en `CaseritoApp/`)
Convenciones de estilo C# con severidad `warning`/`error`:
- `file_scoped` namespaces; `using` ordenados con System primero.
- Naming: `PascalCase` (tipos, métodos, propiedades, constantes), `camelCase` (locales/parámetros), `_camelCase` (campos privados), prefijo `I` en interfaces, `T` en genéricos.
- Modificadores de accesibilidad obligatorios; `readonly` preferido; `var` cuando el tipo es evidente; expression-bodied members donde aporta claridad.
- Reglas de análisis (`dotnet_diagnostic.*`) alineadas con warnings-as-errors.

### `Directory.Build.props` (en `CaseritoApp/`)
Propiedades comunes a todos los proyectos:
- `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`.
- `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`.
- `EnableNETAnalyzers=true`, `AnalysisLevel=latest-recommended`.
- Referencias de analizadores (`PrivateAssets=all`): **Roslynator.Analyzers**, **SonarAnalyzer.CSharp**.

### `Directory.Packages.props` (en `CaseritoApp/`)
- `ManagePackageVersionsCentrally=true` + entradas `PackageVersion` (versiones centralizadas para evitar drift entre contextos).

### `global.json` (en `CaseritoApp/`)
- Fija el SDK a la banda **.NET 10** con `rollForward: latestFeature`.

### Tests de arquitectura — `CaseritoApp.ArchitectureTests`
Proyecto de test con **NetArchTest** que verifica reglas válidas para cualquier Clean Architecture (independientes del patrón interno):
- `Domain` no depende de `Application`, `Infrastructure` ni presentación.
- `Application` depende solo de `Domain`.
- Dependencias siempre hacia adentro.
- Convenciones de nombres verificables (p. ej. handlers terminan en `Handler`).

Corren en CI y rompen el build ante una violación de capas. En el smoke inicial validan contra `CaseritoApp.SmokeLib`; en Fase 0 se apuntan a los contextos reales.

### Logging y política anti-PII
- **Serilog** con logging estructurado: `Enrich.FromLogContext`, correlation id por request, sink de consola en dev (Seq/archivo se decide luego).
- **Política "nunca PII en logs"** (activo de mayor riesgo legal, brief §2.3 / §10): lista explícita de campos prohibidos (número de CI, imágenes de documento/selfie, tokens de sesión, cadenas de QR/pago). Se implementa como *destructuring policy* de Serilog que enmascara/omite esos campos, y como ítem obligatorio del checklist del code-reviewer.
- La configuración concreta de logging se aplica cuando existan proyectos ejecutables (Fase 1+); aquí se define la convención y el helper de enmascarado.

### CI — `.github/workflows/ci.yml`
En `push` y `pull_request`:
1. `actions/setup-dotnet` (según `global.json`).
2. `dotnet restore`.
3. `dotnet format --verify-no-changes` (falla si el formato no cumple `.editorconfig`).
4. `dotnet build` con warnings-as-errors.
5. `dotnet test` (incluye los tests de arquitectura).

### Git hooks — husky.net
- Pre-commit: `dotnet format` sobre archivos `.cs` staged, para no depender de que el CI sea el primero en detectar desviaciones de formato.

## Nivel B — Artefactos de Claude Code

### `CLAUDE.md` (raíz `dev_Caserito`)
Conciso; contiene:
- Visión del proyecto y punteros a `Documentacion/`.
- Mapa de arquitectura con placeholders de bounded contexts (a completar en Fase 0).
- Reglas de capas de Clean Architecture y convenciones de nombres.
- Política de logging y anti-PII.
- Cómo hacer build / test / format y la estrategia de ramas.
- Ubicación de specs de diseño (`docs/superpowers/specs/`).

### Skills de scaffolding (`.claude/skills/`)
- **`nuevo-bounded-context`** (se crea ahora, nivel estructural): genera el esqueleto de proyectos por capas (Domain / Application / Infrastructure / presentación) con las referencias correctas, respetando las reglas de arquitectura.
- **`nuevo-caso-de-uso`** (placeholder documentado): generará command/query + handler + validator + test. Su cuerpo depende del patrón interno (CQRS/MediatR/Result), fuera del alcance de este andamiaje; se completa cuando se decida el patrón en Fase 0.

### Subagente — `.claude/agents/code-reviewer-caserito.md`
Revisor afinado a estas convenciones: verifica reglas de capas, naming, política anti-PII en logs, y uso correcto de los patrones acordados. Complementa (no reemplaza) el `/code-review` genérico.

### Hook de auto-formato — `.claude/settings.json`
Hook `PostToolUse` sobre `Edit`/`Write` de archivos `.cs` que ejecuta `dotnet format` sobre el archivo editado, para que el formato nunca se desvíe. Se versiona en `settings.json` (no en `settings.local.json`).

## Proyecto de validación (smoke)

`CaseritoApp.SmokeLib` (biblioteca mínima) + `CaseritoApp.ArchitectureTests` existen para **verificar end-to-end** que:
- La solución compila con warnings-as-errors y todos los analizadores activos.
- `dotnet format --verify-no-changes` pasa.
- Los tests de arquitectura corren en verde.
- El workflow de GitHub Actions completa exitosamente.

Es desechable o reutilizable en Fase 0; no compromete decisiones de arquitectura.

## Fuera de alcance (explícito)

- Topología de arquitectura (monolito modular vs microservicios).
- Patrón interno de los bounded contexts (CQRS, MediatR, Result pattern, validación, mapeo).
- Framework móvil (Flutter / React Native / MAUI) y decisión de stack completa.
- Modelo de datos, contratos de eventos, RBAC, manejo de PII a nivel de features.
- El cuerpo de la skill `nuevo-caso-de-uso`.

Todo esto pertenece a la Fase 0 / fases posteriores del plan de desarrollo.

## Verificación

El andamiaje se considera correcto cuando, desde una máquina limpia:
1. `dotnet build` en `CaseritoApp/CaseritoApp.sln` compila sin warnings ni errores.
2. `dotnet format --verify-no-changes` no reporta cambios.
3. `dotnet test` pasa (incluidos los tests de arquitectura).
4. El push dispara el workflow de GitHub Actions y este completa en verde.
5. El hook de Claude formatea automáticamente un `.cs` tras editarlo.
6. La skill `nuevo-bounded-context` genera un esqueleto que compila y respeta las reglas de arquitectura.
