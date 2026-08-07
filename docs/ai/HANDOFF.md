# Handoff de sesión

## Objetivo

Implementar una puerta de calidad determinista que permita integrar cambios sin
revisión humana del código. Las verificaciones dejan de vivir en los prompts y
pasan a scripts que fallan solos, de modo que la regla aplique por igual a
Claude, Codex, Kimi o cualquier agente futuro.

## Rama y estado de Git

- Rama: `feature/puerta-calidad`. `master` = `origin/master` = `0f8e491`.
- **Verificar el estado real con `git log --oneline -6` y `git status --short`
  antes de cualquier cosa.** La sesión 2 dejó el árbol limpio con la Tarea 3
  commiteada y revisada.

Commits de código esperados (los de documentación son anteriores):

- `f4b7c4e` feat(calidad): punto de entrada unico de la puerta de calidad
- `a1d9792` feat(calidad): gate TDD con exencion auditable
- `d8fb9d8` fix(calidad): gate TDD no ignora errores de git ni alcances de commit
- `4fd38e0` test(arquitectura): reglas de dependencia entre capas
- `692f504` test(arquitectura): aislamiento entre bounded contexts
- `4280f6d` test(arquitectura): convenciones CQRS verificadas
- `9054d71` refactor(arquitectura): unifica el TheoryData de contextos en Ensamblados

## Tarea exacta en curso

**Tareas 1 a 5: completas y revisadas.** Las dos de nivel alto (4 y 5) se
cerraron en la sesión 3 con spec ✅ y calidad Aprobada; la 5 pasó por una ronda
de arreglos (detalle en el ledger). No queda nada pendiente de las cinco
primeras.

**Primer paso de la nueva sesión: despachar la Tarea 6**, con el brief ya
extraído en `task-6-brief.md`. De la 6 a la 10 el nivel es intermedio.

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
subagente**, empezando por la **Tarea 6**. Abrir la sesión en nivel alto para la
orquestación.

Nivel de modelo por tarea, ya decidido: **intermedio** en 6, 7, 8, 9 y 10 (las de
nivel alto, 4 y 5, ya están cerradas). **Ninguna baja a nivel económico:** todas
tocan configuración de build o de verificación, donde
`docs/ai/ECONOMIA_TOKENS.md` §1 lo prohíbe.

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

## Corrección al plan: su premisa era falsa (ya resuelta)

El handoff de la sesión 1 afirmaba que `CaseritoApp.ArchitectureTests` solo
contenía `PiiRedactionTests.cs`. Tiene 15 archivos. El solapamiento ya está
consolidado y **no queda nada por hacer en este frente**:

- `Layering/CapasPorContextoTests.cs`: absorbido y borrado por la Tarea 3.
- `Layering/AislamientoEntreContextosTests.cs`: absorbido y borrado por la
  Tarea 4, que lo reescribió como `ContextosTests.cs` sin perder cobertura y
  ampliándola (Infrastructure como origen; `Application → Application` ajeno
  prohibido).
- El solapamiento de la Tarea 5 se verificó antes de despachar: no existían
  tests de convenciones CQRS. La carpeta `Layering/` ya no existe.

**Sin violaciones arquitectónicas preexistentes.** Ni la Tarea 4 ni la 5
activaron el punto de parada 1: los contextos ya están aislados, y los 84
handlers y 32 validadores de Application ya son `public sealed`.

## Estado medido del repositorio (verificado, no supuesto)

- Tests de arquitectura: **209 pasan** (102 tras la Tarea 3, 191 tras la Tarea 4
  con sus 3 teorías × 30 pares, 209 tras la Tarea 5 con 3 reglas × 6 contextos).
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
- **Trampas del entorno, ya pagadas en las tareas 3 a 5:**
  - Sonar `S1135` interpreta la palabra española **«Todo»** como marcador TODO y,
    con `TreatWarningsAsErrors`, rompe el build. Precisión de la Tarea 5: se
    dispara sobre **comentarios**, no sobre identificadores. Aun así, todos los
    analizadores Sonar son errores aquí (`S1481`, `S1118`, `S3400`…).
  - El hook `pre-commit` exige **CRLF** en los `.cs` nuevos.
  - `[MemberData]` exige miembro `public` aunque la clase sea `internal`
    (`xUnit1016`). Por eso `Ensamblados.ContextosComoDatos()` es `public`.
- NetArchTest lee **IL**, no el texto fuente: un `using` sin usar no genera
  dependencia. Para provocar un fallo de prueba hace falta un tipo real que use
  el ensamblado prohibido.
- Interfaz compartida por los tests de arquitectura, ya estable:
  `Ensamblados.Contextos`, `Ensamblados.De(contexto, capa)` y
  `Ensamblados.ContextosComoDatos()`. Reutilizarla; no duplicar `TheoryData`.
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
