# Puerta de calidad

Verificaciones deterministas que permiten integrar cambios sin revisión humana
del código. Neutrales al proveedor: aplican a cualquier agente.

## Comandos

    ./verify.ps1          # Windows, nivel rápido, sin Docker
    ./verify.sh           # POSIX, nivel rápido
    ./verify.ps1 -Changed # Windows, nivel rápido según cambios pendientes
    ./verify.sh --changed # POSIX, nivel rápido según cambios pendientes
    ./verify.ps1 -Full    # nivel completo, requiere Docker
    ./verify.sh --full

Nivel por alcance antes de **cada commit y push a `develop`**. El nivel rápido
general queda disponible para revisiones manuales transversales. Nivel completo
antes de **promover a `master`**.

El modo por alcance inspecciona los cambios pendientes respecto de `HEAD`,
incluidos archivos nuevos: `web/` ejecuta los gates web, `CaseritoApp/` los
gates .NET y la infraestructura transversal ejecuta ambos. Los cambios solo
documentales ejecutan los controles comunes. Sin cambios pendientes, el comando
falla explícitamente; `Changed` y `Full` no se pueden combinar.

## Duración medida

Medida el 2026-08-07 al cerrar la implementación de la puerta (rama
`feature/puerta-calidad`, máquina de desarrollo Windows, build incremental):

- **Nivel rápido: ~2 minutos en total** (113–123 s en dos mediciones).
  Desglose por gate, en segundos aproximados: Gate TDD 0,3 · Formato .NET 34 ·
  Build Release 4 (incremental) · Complejidad 0,1 · Unit tests 3 · Tests de
  arquitectura 3 · Lint web 8 · Typecheck web 6 · Tests web 55.
- **Nivel por alcance:** depende del stack afectado y evita ejecutar el otro.
- Nivel completo: bastante más largo, porque añade la suite de integración con
  Testcontainers.MsSql y la recolección de cobertura.

El presupuesto del nivel rápido es **menos de 5 minutos sin Docker**. Si una
medición futura supera ese presupuesto sin que el repositorio haya crecido de
forma correspondiente, hay que investigar qué gate se degradó.

## Qué mide cada gate

En el orden real de ejecución de `quality/verify.mjs` (se detiene en el primer
fallo, para retroalimentación rápida):

| Gate | Nivel | Qué comprueba | Cómo se arregla un fallo |
|---|---|---|---|
| Gate TDD | Ambos | Todo cambio en `CaseritoApp/src/` trae cambios en `CaseritoApp/tests/` | Escribe el test. Si el cambio no admite test, usa la exención. |
| Formato .NET | Ambos | `dotnet format --verify-no-changes` (excluye CA1502/CA1505/CA1506, medidos aparte) | `dotnet format CaseritoApp.sln` |
| Build Release | Ambos | Warnings tratados como errores | Corrige el warning. Nunca lo silencies con `#pragma`. |
| Complejidad | Ambos | El recuento de CA1502/CA1505/CA1506 no sube respecto a `quality/complexity-baseline.json` | Extrae métodos, reduce ramas. |
| Unit tests | Rápido | 379 tests | Arregla el código o el test, según cuál esté mal. |
| Tests de arquitectura | Rápido | Capas, aislamiento de contextos, convenciones CQRS (209 tests) | Mueve el código a la capa correcta. Nunca relajes la regla. |
| Lint web | Ambos | ESLint del frontend | Según el mensaje. |
| Typecheck web | Ambos | TypeScript estricto | Según el mensaje. |
| Tests web | Ambos | 224 tests del frontend | Según el mensaje. |
| Tests con cobertura | Completo | Ejecuta de una sola vez la suite entera de la solución (unit + arquitectura + integración) con cobertura; la integración usa Testcontainers.MsSql | Requiere Docker en marcha. |
| Cobertura | Completo | Ningún proyecto cae más de 0,5 pp respecto a `quality/coverage-baseline.json` (23 proyectos) | Añade tests al código nuevo. |

En el nivel completo los gates «Unit tests» y «Tests de arquitectura» no se
repiten por separado: «Tests con cobertura» ejecuta esas mismas suites dentro
de la pasada única sobre la solución.

El **mutation testing no es un gate de `verify`**: es demasiado lento para un
gate síncrono. Corre en un workflow nocturno de GitHub Actions
(`.github/workflows/mutation.yml`, también lanzable manualmente) y compara el
score contra `quality/mutation-baseline.json`.

## Reglas innegociables

1. **Nunca uses `--no-verify`.** Ni en commit ni en push.
2. **Nunca relajes una baseline, un umbral o una exclusión para que pase el
   gate.** Si el gate falla, el problema está en el código.
3. **Las baselines solo se mueven hacia mejor.** Se actualizan tras un cambio que
   mejore la métrica, en un commit propio que explique la mejora.
4. **Nunca afirmes verde sin haber ejecutado el comando y visto la salida.**

## La exención del gate TDD

Para cambios en `src/` que genuinamente no admiten test — refactor puro,
renombrados, textos de UI — incluye en el mensaje del commit:

    [sin-test] <motivo de al menos 20 caracteres>

Límites: máximo 5 archivos de `src/` por commit eximido (lo vigila CI, no solo
el gate local). `verify` imprime un aviso cuando la detecta, y el uso queda
auditado:

    git log --grep="\[sin-test\]" --oneline

## Actualizar una baseline legítimamente

Solo cuando la métrica **ha mejorado**:

1. Haz el cambio que mejora el código y verifica que la métrica sube.
2. Regenera la baseline con el procedimiento del spec: la de cobertura a partir
   de los reportes de `CaseritoApp/artifacts/coverage/` que genera
   `verify --full`, y la de complejidad a partir de los avisos CA1502/CA1505/
   CA1506 del `artifacts-build.log` que escribe el gate de build.
3. Commit separado: `chore(calidad): actualiza baseline de <métrica> tras <mejora>`.
4. El mensaje debe decir qué mejoró y por qué.

Nunca regeneres una baseline para hacer pasar un gate que falla.
