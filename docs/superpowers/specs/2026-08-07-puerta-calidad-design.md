# Puerta de calidad determinista — diseño

Fecha: 2026-08-07
Estado: aprobado

## Objetivo

Permitir que el responsable humano deje de revisar código línea por línea, moviendo
las verificaciones de calidad desde los prompts (donde dependen de que la IA les
haga caso) hacia scripts deterministas que fallan solos.

La propiedad que buscamos: **si la puerta de calidad está verde y ninguna baseline
bajó, el cambio se mergea sin lectura humana del código**.

Las reglas son neutrales al proveedor: aplican igual a Claude, Codex, Kimi, Gemini
o cualquier agente futuro, porque viven en `AGENTS.md` y en scripts ejecutables,
no en la configuración de un harness concreto.

## No objetivos

- No se elimina el control humano sobre *qué* se construye. El punto de control se
  traslada al spec, antes de codificar.
- No se persigue una cifra absoluta de cobertura ni un refactor masivo del código
  existente. Los umbrales son de no-regresión.
- No se añade detección de duplicación (DRY) en esta iteración: mala relación
  señal/ruido frente a DTOs, mappers y migraciones EF. Queda diferido.
- No se bloquea el commit con mutation testing: es demasiado lento para un gate
  síncrono. Corre de forma asíncrona.

## Contexto medido

Estado del repositorio al momento del diseño:

- 379 unit tests en ~1 s. Un gate rápido es viable.
- `NetArchTest.Rules` 1.3.2 ya está en `Directory.Packages.props` y el proyecto
  `CaseritoApp.ArchitectureTests` ya referencia los 8 árboles de `src/`
  (`BuildingBlocks`, `Catalog`, `Chat`, `Host`, `Identity`, `Notifications`,
  `Orders`, `Reputation`). Solo contiene `PiiRedactionTests.cs`.
- `coverlet.collector` **no** está declarado; `--collect:"XPlat Code Coverage"`
  no recolecta nada hoy.
- CI ya cubre formato, build con warnings-as-errors, tests, lint/typecheck web y
  deriva del contrato OpenAPI.
- Husky ejecuta `dotnet format` sobre archivos staged en pre-commit.
- Los tests de integración usan Testcontainers.MsSql y requieren Docker.

## Decisiones

### D1 — Punto de entrada único, lógica en un solo lenguaje

`verify.ps1` y `verify.sh` en la raíz, siguiendo el patrón existente de
`rebuild.ps1` / `rebuild.sh`. Son wrappers delgados; toda la lógica vive en
`quality/*.mjs` ejecutado con Node 22, que ya es dependencia obligatoria del
repositorio por `web/`.

Motivo: mantener dos implementaciones de la misma lógica en PowerShell y Bash
garantiza que se desincronicen.

### D2 — Dos niveles de verificación

| Nivel | Comando | Docker | Contenido |
|---|---|---|---|
| Rápido | `verify` | No | `dotnet format --verify-no-changes`, build Release warnings-as-errors, unit tests, architecture tests, gate TDD, `lint` + `typecheck` + `test` en `web/` |
| Completo | `verify --full` | Sí | Todo lo anterior + tests de integración + cobertura contra baseline + verificación de deriva del contrato OpenAPI |

El nivel rápido se ejecuta antes de cada commit. El completo, antes de mergear.

### D3 — Umbrales de no-regresión, nunca absolutos

Cada métrica tiene una baseline versionada en `quality/`. El gate falla solo si la
métrica **empeora** respecto a la baseline, nunca por estar por debajo de un ideal.

Motivo: permite activar la puerta hoy mismo sin un refactor previo, y garantiza
que la deuda solo pueda decrecer.

### D4 — Complejidad como warning con recuento, no como error

Activar `CA1502`, `CA1505` y `CA1506` como *warning* (no error, pese a
warnings-as-errors, mediante `WarningsNotAsErrors`) y contar las violaciones.
El gate falla si el recuento sube.

Motivo: activarlos como error rompería el build de golpe sobre el código actual.

### D5 — Mutation testing asíncrono y acotado

Stryker.NET limitado a los proyectos `*.Domain` y `*.Application`, que se prueban
con unit tests puros sin Docker. Corre en un workflow nocturno, no en el gate.

Motivo: Stryker reejecuta la suite una vez por mutante; con Testcontainers.MsSql
de por medio el coste pasa de minutos a horas.

## Componentes

### C1 — `verify.ps1` / `verify.sh`

Entrada: flag opcional `--full` (`-Full` en PowerShell).
Salida: código 0 si todo pasa; distinto de 0 con un resumen de qué gate falló.
Dependencias: Node 22, .NET SDK, y Docker solo en modo `--full`.

Delegan en `quality/verify.mjs`, que orquesta los pasos en orden de coste
creciente y **se detiene en el primer fallo** para dar retroalimentación rápida.

### C2 — Fitness functions de arquitectura

Archivos nuevos en `CaseritoApp/tests/CaseritoApp.ArchitectureTests/`:

- **`CapasTests.cs`** — `*.Domain` no depende de `*.Application`, `*.Infrastructure`,
  `*.Host`, EntityFrameworkCore ni MediatR. `*.Application` no depende de
  `*.Infrastructure`, `*.Host` ni EntityFrameworkCore.
- **`ContextosTests.cs`** — ningún bounded context referencia el `Domain` o el
  `Infrastructure` de otro contexto. La comunicación entre contextos ocurre
  únicamente a través de `BuildingBlocks.Contracts`.
- **`ConvencionesTests.cs`** — los handlers CQRS terminan en `Handler`, son
  `internal sealed`, y devuelven `Result`. Todo command o query tiene un validador
  FluentValidation asociado.

Se ejecutan como tests xUnit normales, dentro del nivel rápido.

### C3 — Cobertura

- Añadir `coverlet.collector` a `Directory.Packages.props` y referenciarlo desde
  los tres proyectos de test.
- `quality/coverage-baseline.json` — cobertura de línea y rama por proyecto,
  generada una vez durante la implementación.
- `quality/check-coverage.mjs` — parsea los `coverage.cobertura.xml` y falla si
  algún proyecto baja más de **0,5 puntos porcentuales** respecto a su baseline.

La tolerancia existe porque la cobertura no es estable: el denominador cambia por
código generado por el compilador (`record`s, métodos `async`, lambdas), ramas
defensivas inalcanzables y diferencias entre versiones de coverlet. Un gate con
tolerancia cero fallaría ante descensos de centésimas que no señalan ningún
problema real, y **un gate con falsos positivos entrena al agente a regenerar la
baseline en lugar de investigar** — exactamente lo que la puerta pretende evitar.

El hueco que deja la tolerancia (unas pocas líneas sin cubrir) lo cierra el gate
TDD de C5, que exige que todo cambio en `src/` traiga cambios en `tests/`. Las dos
redes son complementarias: ninguna basta por separado.

### C4 — Complejidad

- `Directory.Build.props`: habilitar los analizadores de mantenibilidad y
  excluirlos de warnings-as-errors.
- `quality/complexity-baseline.json` — recuento de violaciones por regla y por
  proyecto.
- `quality/check-complexity.mjs` — parsea la salida del build y falla si el
  recuento sube.

### C5 — Gate TDD

`quality/check-tdd.mjs` compara el diff contra `origin/master`.

Falla si hay archivos `.cs` modificados bajo `CaseritoApp/src/` sin ningún cambio
bajo `CaseritoApp/tests/`.

Exclusiones (no cuentan como código que exige test): `Migrations/`, `Program.cs`,
`*.Designer.cs`, `*.csproj`.

**Exención.** Existe porque hay cambios legítimos en `src/` que no pueden llevar
test: refactor puro (renombrar, extraer método), corrección de textos de UI,
atributos de logging. El refactor puro es indetectable por ruta, porque toca los
mismos archivos que un cambio de comportamiento. Sin válvula, el agente
bloqueado escribiría un test vacío que no asevera nada — peor que no tener gate —
o recurriría a `--no-verify`, que **no deja rastro alguno**. La exención convierte
un incumplimiento invisible en uno auditable.

Se activa incluyendo `[sin-test]` en el mensaje del commit, seguido de un motivo.
Tres refuerzos la hacen cara de abusar:

1. El motivo debe tener **al menos 20 caracteres** de texto real tras la etiqueta.
   Un "menor" no cuela.
2. `verify` **imprime un aviso destacado al final** cuando detecta una exención,
   de modo que aparezca en la salida que el agente reporta al cerrar.
3. CI **falla** si un commit con `[sin-test]` modifica **más de 5 archivos** bajo
   `CaseritoApp/src/`. Un refactor trivial no toca quince archivos.

El uso de la exención es auditable en cualquier momento con
`git log --grep="\[sin-test\]" --oneline`. Un puñado de exenciones con motivos
razonables indica que el sistema funciona; una racha de motivos vagos indica que
hay que endurecer las reglas.

### C6 — Mutation testing

- `stryker-config.json` en `CaseritoApp/`, con `mutate` restringido a
  los directorios `src/<Contexto>/CaseritoApp.<Contexto>.Domain/` y
  `src/<Contexto>/CaseritoApp.<Contexto>.Application/` de cada contexto, y
  `testProjects` apuntando solo a `CaseritoApp.UnitTests`.
- `.github/workflows/mutation.yml` con `schedule` nocturno y `workflow_dispatch`.
- `quality/mutation-baseline.json` — score de mutación. El job falla si baja.

### C7 — Reglas para agentes

- **`AGENTS.md`** — nueva sección corta **"Puerta de calidad"**:
  - Ejecutar `verify` antes de cada commit y `verify --full` antes de mergear.
  - Prohibido `git commit --no-verify` y `git push --no-verify`.
  - Prohibido relajar una baseline, un umbral o una exclusión para hacer pasar el
    gate. Las baselines solo se actualizan **hacia mejor**, tras arreglar el código.
  - Si el gate falla, se arregla el código, no el gate.
  - Nunca afirmar verde sin haber ejecutado el comando y visto la salida.
- **`docs/ai/WORKFLOW.md`** — la fase de cierre exige `verify --full` en verde con
  la salida como evidencia.
- **`docs/ai/PUERTA_CALIDAD.md`** (nuevo) — qué mide cada gate, cómo leer un fallo,
  y el procedimiento legítimo para actualizar una baseline.

`GEMINI.md`, `.codex/` y `.agents/` ya remiten a `AGENTS.md`, de modo que la regla
se propaga a Kimi, Codex y Gemini sin duplicarla.

## Flujo de trabajo resultante

1. El humano aprueba el spec. **Único punto de control humano.**
2. El agente implementa siguiendo TDD.
3. Antes de cada commit: `verify`. Si falla, arregla el código.
4. Antes de mergear: `verify --full`.
5. CI repite ambos niveles de forma independiente.
6. Si todo está verde y ninguna baseline bajó, se mergea sin lectura humana.
7. El job nocturno de mutación vigila la calidad de las aserciones y deja
   constancia si el score cae.

## Manejo de errores

Cada script imprime, al fallar: qué gate falló, el valor esperado, el valor
obtenido, y el archivo o proyecto responsable. Nada de volcados extensos: un
resumen por causa, archivo y línea, según la regla de contexto ya vigente.

Los scripts respetan la política anti-PII: nunca imprimen contenido de archivos de
datos, tokens ni valores de configuración.

## Pruebas

- Cada script `quality/*.mjs` se prueba con un caso que pasa y uno que falla,
  usando fixtures en `quality/__tests__/`. Los tests usan el runner integrado
  `node:test` y se ejecutan con `node --test quality/`, sin dependencias nuevas.
- Las fitness functions son en sí mismas tests; se verifica que fallan al
  introducir deliberadamente una dependencia prohibida (y se revierte).
- Se verifica que `verify` completo pasa en el repositorio limpio antes de cerrar.

## Criterios de aceptación

1. `verify` termina en verde sobre `master` limpio, en menos de 60 s sin Docker.
2. `verify --full` termina en verde sobre `master` limpio con Docker disponible.
3. Introducir una dependencia de `Domain` hacia `Infrastructure` hace fallar
   `verify`.
4. Modificar un archivo de `src/` sin tocar `tests/` hace fallar `verify`.
5. Bajar la cobertura de un proyecto más de 0,5 pp hace fallar `verify --full`.
6. Un commit con `[sin-test]` y un motivo de menos de 20 caracteres hace fallar
   `verify`; con un motivo válido pasa e imprime el aviso de exención.
7. Un commit con `[sin-test]` que toca más de 5 archivos de `src/` hace fallar CI.
8. `AGENTS.md` contiene la sección "Puerta de calidad" y no supera su tamaño
   razonable actual.
9. El workflow de mutación se ejecuta manualmente al menos una vez y deja una
   baseline registrada.

## Riesgos

- **Volumen de violaciones de complejidad.** Se desconoce cuántas escupirán
  `CA1502`/`CA1505`/`CA1506` sobre el código actual. Si son muchas, la baseline
  será un número grande y poco útil hasta que se ataque la deuda. Se medirá
  durante la implementación y se reportará. No cambia el diseño.
- **Falsos positivos del gate TDD.** Cambios legítimos sin test (configuración,
  renombrados) requerirán la exención. Si la exención se usa con demasiada
  frecuencia, habrá que afinar las exclusiones.
- **Coste de Stryker.** Aunque acotado a Domain y Application, el job nocturno
  puede resultar largo. Si excede lo razonable, se acotará por contexto.

## Trabajo diferido

- Detección de duplicación (`jscpd`).
- Cobertura del frontend con umbral (`vitest --coverage`).
- Mutation testing del frontend (StrykerJS).
- Endurecer las baselines hacia umbrales absolutos una vez la deuda baje.
