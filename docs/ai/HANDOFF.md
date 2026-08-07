# Handoff de sesión

## Objetivo

Implementar una puerta de calidad determinista que permita integrar cambios sin
revisión humana del código. Las verificaciones dejan de vivir en los prompts y
pasan a scripts que fallan solos, de modo que la regla aplique por igual a
Claude, Codex, Kimi o cualquier agente futuro.

## Rama y estado de Git

- Rama: `feature/puerta-calidad`. `master` = `origin/master` = `0f8e491`.
- **Verificar el estado real con `git log --oneline -6` y `git status --short`
  antes de cualquier cosa.** La sesión anterior se cortó con la Tarea 3
  commiteada o a punto de commitear.

Commits de código esperados (los de documentación son anteriores):

- `f4b7c4e` feat(calidad): punto de entrada unico de la puerta de calidad
- `a1d9792` feat(calidad): gate TDD con exencion auditable
- `d8fb9d8` fix(calidad): gate TDD no ignora errores de git ni alcances de commit
- `4fd38e0` test(arquitectura): reglas de dependencia entre capas

## Tarea exacta en curso

**La Tarea 3 está commiteada pero SIN revisar.** La sesión se cortó por economía
de contexto justo antes de su revisión de tarea. Primer paso de la nueva sesión:

1. Generar el paquete de revisión con `BASE=d8fb9d8`, `HEAD=4fd38e0` y despachar
   la revisión de tarea (nivel intermedio basta: el diff es pequeño y el brief
   traía el código). El revisor debe mirar tres cosas concretas: que borrar
   `Layering/CapasPorContextoTests.cs` no perdiera ninguna comprobación, que la
   exclusión de MediatR esté documentada y acotada solo a esa regla, y que la
   evidencia del Paso 3 (provocar el fallo con EF y revertirlo) sea real y el
   árbol quedara limpio.
2. Cerrar los hallazgos que salgan.
3. Recién entonces despachar la Tarea 4.

## Spec, plan y ledger activos

- Spec: `docs/superpowers/specs/2026-08-07-puerta-calidad-design.md`
- Plan: `docs/superpowers/plans/2026-08-07-puerta-calidad.md` — 10 tareas con
  ciclo TDD, código completo y comandos de verificación.
- **Ledger: `.superpowers/sdd/2026-08-07-puerta-calidad/progress.md`.** Es el
  registro autoritativo de lo hecho, las resoluciones del humano y los hallazgos
  diferidos. Léelo antes que este handoff si hay discrepancia.
- Briefs por tarea ya extraídos: `task-<N>-brief.md` en ese mismo directorio.
  El script `task-brief` de la skill **no sirve** con este plan: busca encabezados
  «Task N» y los nuestros son «Tarea N». Los briefs ya están extraídos; el de la
  Tarea 10 se extrajo por rango de líneas porque contiene encabezados `##`.

## Cómo continuar

Ejecutar con `superpowers:subagent-driven-development`, **una tarea por
subagente**, empezando por la **Tarea 4**. Abrir la sesión en nivel alto para la
orquestación.

Nivel de modelo por tarea, ya decidido: **intermedio** en 6, 7, 8, 9 y 10;
**alto** en 4 y 5 (no por escribir los tests, sino por juzgar si una dependencia
entre contextos es acoplamiento legítimo, error real o excepción a documentar).
**Ninguna baja a nivel económico:** todas tocan configuración de build o de
verificación, donde `docs/ai/ECONOMIA_TOKENS.md` §1 lo prohíbe.

Tras cada tarea: `./verify.sh`. No mergear hasta que las 10 estén completas y
`./verify.sh --full` esté en verde. Cada tarea termina con su propio commit.

## Puntos de parada: preguntar al humano, no decidir solo

1. Una fitness function revela una violación arquitectónica preexistente.
2. El conteo de violaciones de complejidad supera las 100.
3. Stryker excede los 40 minutos.

## Decisiones ya tomadas por el humano en esta sesión

1. **Presupuesto de tiempo.** El nivel rápido tarda **188 s** medidos (dotnet
   format 53 s, build 24 s, vitest 74 s, resto ~37 s), no los <60 s que exige el
   criterio de aceptación 1 del spec. Gobierna el valor medido: **ajustar el
   criterio del spec a un presupuesto realista y documentarlo en
   `PUERTA_CALIDAD.md`**. No paralelizar el orquestador ni recortar el nivel
   rápido. **Se ejecuta en la Tarea 10**, con las duraciones finales medidas.
2. **MediatR en las capas Domain.** `BuildingBlocks.Domain/IDomainEvent.cs:8`
   declara `IDomainEvent : INotification`, así que los 14 eventos de dominio
   arrastran MediatR a Identity, Catalog, Chat y Orders. Decisión: **excepción
   documentada**. La regla prohíbe EF y AspNetCore en Domain, no MediatR, con un
   comentario que explique que el marcador vive en BuildingBlocks por diseño.
   **No se refactoriza `IDomainEvent`.**
3. **Tests duplicados.** Decisión: **consolidar en el esquema nuevo** —
   reescribir las reglas existentes con `Ensamblados` y casos `Theory` por
   contexto, borrar los duplicados y añadir encima solo lo nuevo.

## Corrección importante al plan: su premisa era falsa

El handoff anterior afirmaba que `CaseritoApp.ArchitectureTests` solo contenía
`PiiRedactionTests.cs`. **Tiene 15 archivos de test** (verificado). Consecuencias:

- `Layering/CapasPorContextoTests.cs` duplicaba parte de la Tarea 3: **ya fue
  absorbido y borrado** por la Tarea 3.
- `Layering/AislamientoEntreContextosTests.cs:13` **ya implementa la Tarea 4
  completa**, y de forma más amplia: recorre las tres capas de cada contexto
  contra cualquier otro `CaseritoApp.{otro}`. Por tanto **la Tarea 4 no es
  «crear» sino «consolidar y ampliar»**: reescribe esa regla con `Ensamblados` y
  `TheoryData` de pares de contextos (mejor diagnóstico: dice qué par falla),
  borra el archivo viejo y no pierdas ninguna comprobación que tuviera. Antes de
  borrar, confirma que la versión nueva cubre todo lo que cubría la vieja.
- Revisa el mismo solapamiento antes de la Tarea 5 (`ConvencionesTests.cs`):
  puede haber ya tests de convenciones entre esos 15 archivos.

## Estado medido del repositorio (verificado, no supuesto)

- Tests de arquitectura: **102 pasan** tras la consolidación de la Tarea 3
  (eran 85 antes; la Tarea 3 sustituye 1 `Fact` por 18 casos de `Theory`).
- Unit tests: 379 pasan en ~1 s. Tests web: 224 pasan.
- Tests de los scripts de calidad: 13 pasan (`node --test quality/`).
- Bounded contexts: `Identity`, `Catalog`, `Chat`, `Orders`, `Reputation`,
  `Notifications`, más `BuildingBlocks` y `Host`.
- `Reputation` y `Notifications` **no tienen eventos de dominio**: sus verdes en
  las reglas de Domain son accidentales. Está anotado en `CapasTests.cs`.
- Los 76 handlers son `public sealed class …Handler`. Ninguno es `internal`.
- `coverlet.collector` **no** está declarado: `--collect:"XPlat Code Coverage"`
  no recolecta nada todavía. Lo añade la Tarea 6.
- `NetArchTest.Rules` 1.3.2 ya está en `Directory.Packages.props`.
- Node disponible: **v24** (el plan dice 22; sirve igual).
- `.github/workflows/ci.yml` ya cubre formato, build con warnings-as-errors,
  tests, lint/typecheck web y deriva del contrato OpenAPI.

## Hallazgos menores diferidos (para la revisión final, no ahora)

- `quality/lib/ejecutar.mjs:11` — `DeprecationWarning: DEP0190` de Node por usar
  `shell: true` con `args` en array. Viene literal del plan.
- `quality/lib/ejecutar.mjs` — `ejecutar` descarta `resultado.error`, así que un
  `ENOENT` se ve como código 1 con salida vacía.
- Sin tests para `ejecutar.mjs`; sin test del borde exacto 19/20/21 caracteres
  del motivo de la exención `[sin-test]`.

## Avisos operativos

- La **Tarea 8, Paso 6** manda lanzar el workflow de mutación desde la pestaña
  Actions. La rama no está en GitHub y el plan no la empuja hasta el cierre: o
  empujas `feature/puerta-calidad` para poder hacer `workflow_dispatch`, o dejas
  ese paso explícitamente pendiente y lo dices en el informe. No lo declares
  verde sin haberlo ejecutado.
- La **Tarea 8, Paso 4** referencia `quality/check-mutation.mjs` antes de que el
  Paso 5 lo cree, y ese archivo no figura en la tabla «Crear» del plan. Es un
  desorden del plan, no un problema real: créalo.
- No usar `--no-verify` en ningún commit ni push.
- Cada tarea termina con su propio commit. No dejar la rama a medias.

## Cierre

Con las 10 tareas completas y `verify --full` en verde, hacer la revisión amplia
de toda la rama (nivel alto) y después integrar según el flujo git autorizado:
merge a `master`, push y borrado de la rama.

Reportar al humano en el cierre:

- Duración del nivel rápido y del completo, y el criterio ajustado.
- Cobertura global de la baseline.
- Número total de violaciones de complejidad.
- Mutation score inicial.
- Cualquier fitness function que haya revelado una violación preexistente.
