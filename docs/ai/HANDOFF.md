# Handoff de sesión

## Objetivo

Implementar una puerta de calidad determinista que permita integrar cambios sin
revisión humana del código. Las verificaciones dejan de vivir en los prompts y
pasan a scripts que fallan solos, de modo que la regla aplique por igual a
Claude, Codex, Kimi o cualquier agente futuro.

## Rama y estado de Git

- Rama: `feature/puerta-calidad` (creada desde `master`, sin divergencia).
- Árbol limpio. Solo hay commits de documentación; **no se ha escrito código aún**.

## Spec y plan activos

- Spec: `docs/superpowers/specs/2026-08-07-puerta-calidad-design.md`
- Plan: `docs/superpowers/plans/2026-08-07-puerta-calidad.md` — 10 tareas con
  ciclo TDD paso a paso, código completo y comandos de verificación.

El plan es autocontenido: no hace falta el historial de la sesión anterior.

## Decisiones aprobadas

1. Gate **bloqueante en local y en CI**. Si no pasa, no hay merge.
2. Umbrales de **no-regresión contra baseline**, nunca absolutos. La deuda solo
   puede decrecer.
3. Punto de entrada único `verify.ps1` / `verify.sh`, con la lógica en
   `quality/*.mjs` (Node 22, sin dependencias npm nuevas) para no mantener dos
   implementaciones divergentes.
4. Gate TDD por diff: cambio en `src/` exige cambio en `tests/`.
5. Exención `[sin-test] <motivo>` con motivo de **20 caracteres mínimo**, aviso
   destacado en `verify` y **límite de 5 archivos** de `src/` verificado en CI.
6. Tolerancia de cobertura: **0,5 pp**. Absorbe el ruido de instrumentación; el
   hueco que deja lo cierra el gate TDD.
7. Mutation testing **fuera del gate**, como job nocturno acotado a `Domain` y
   `Application` — los tests de integración usan Testcontainers.MsSql y harían
   inviable la ejecución síncrona.
8. Complejidad (CA1502/CA1505/CA1506) como **warning con recuento**, no como
   error, para no romper el build sobre el código existente.
9. El punto de control humano queda **en el spec, antes de codificar**.
10. Detección de duplicación (DRY) y cobertura del frontend quedan **diferidas**.

## Completado

- Exploración del repositorio y diagnóstico del estado actual.
- Spec escrito, autorevisado y commiteado.
- Plan escrito, autorevisado y commiteado.

## Tarea exacta en curso

Ninguna. **Empezar por la Tarea 1** del plan.

## Archivos relevantes

Estado medido del repositorio (verificado, no supuesto):

- `379 unit tests` corren en ~1 s. El gate rápido es viable.
- `NetArchTest.Rules` 1.3.2 ya está en `Directory.Packages.props`, y
  `CaseritoApp.ArchitectureTests` ya referencia los 8 árboles de `src/`. Ese
  proyecto solo contiene hoy `PiiRedactionTests.cs`.
- `coverlet.collector` **no** está declarado: `--collect:"XPlat Code Coverage"`
  no recolecta nada. La Tarea 6 lo añade.
- Bounded contexts: `Identity`, `Catalog`, `Chat`, `Orders`, `Reputation`,
  `Notifications`, más `BuildingBlocks` y `Host`.
- Los **76 handlers** del repo son `public sealed class …Handler`. Ninguno es
  `internal`. Las fitness functions codifican esa convención real.
- `.github/workflows/ci.yml` ya cubre formato, build con warnings-as-errors,
  tests, lint/typecheck web y deriva del contrato OpenAPI.
- Husky ejecuta `dotnet format` sobre archivos staged en pre-commit.

## Verificaciones ejecutadas

- `dotnet test` sobre `CaseritoApp.UnitTests`: **379 pasan, 0 fallan** (~1 s).
- Intento de recolección de cobertura: falla por ausencia de
  `coverlet.collector`. Es el punto de partida de la Tarea 6.

## Fallos o bloqueos

Ninguno pendiente. Tres riesgos conocidos, con instrucciones en el plan:

1. **Tareas 3, 4 y 5 pueden revelar violaciones arquitectónicas preexistentes.**
   El plan ordena **parar y reportar al humano** los tipos infractores. Es
   plausible que aparezca acoplamiento legítimo entre contextos (por ejemplo
   `Notifications` reaccionando a eventos de `Orders`). **No relajar la regla
   sin decisión humana.**
2. **Tarea 7: volumen de violaciones de complejidad desconocido.** Si el
   analizador escupe cientos, hay que consultar al humano si se ataca la deuda
   o se congela la baseline.
3. **Tarea 8: duración de Stryker.** Si excede ~40 min, acotar `mutate` a
   `Catalog` y `Orders` y anotarlo en la baseline.

## Próximo paso

Ejecutar el plan con `superpowers:subagent-driven-development`, una tarea por
subagente, empezando por la Tarea 1.

Cada tarea termina con su propio commit. Al completar las 10 y con
`verify --full` en verde, integrar según el flujo git autorizado: merge a
`master`, push y borrado de la rama.

Reportar al cierre: duración de ambos niveles, cobertura global de la baseline,
total de violaciones de complejidad, mutation score inicial y cualquier fitness
function que haya revelado una violación preexistente.

## Commits

- `84305e8` docs(spec): diseño de puerta de calidad determinista
- `e882c06` docs(spec): refuerza la exencion del gate TDD y justifica la tolerancia
- `aa847a0` docs(plan): plan de implementacion de la puerta de calidad
