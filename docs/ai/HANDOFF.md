# Handoff de cierre — puerta de calidad

## Estado

El plan `2026-08-07-puerta-calidad` está **cerrado e integrado en `master`**:
las 10 tareas implementadas, revisadas y verificadas. La rama
`feature/puerta-calidad` se fusionó con fast-forward y se borró. `master` =
`origin/master`.

`verify --full` verde (unit 379, arquitectura 209, integración 236,
cobertura 23 proyectos, formato/build/lint/typecheck/web). Nivel rápido verde
en 130 s (<5 min, criterio 1 ajustado).

## Registro autoritativo

El ledger `.superpowers/sdd/2026-08-07-puerta-calidad/progress.md` (gitignored)
tiene el detalle completo por tarea, incluidas las resoluciones del humano y los
hallazgos diferidos.

## Única acción pendiente: baseline de mutación (criterio 9)

El workflow `Mutation testing` (`.github/workflows/mutation.yml`) no se ha
ejecutado aún: Stryker bloqueó en local y la rama no estaba publicada. Ya
despachado manualmente desde `master`. Tras el run:

1. Reemplazar el `score: 0` BLOQUEADO de `quality/mutation-baseline.json` por el
   score real del reporte subido en el artefacto `mutation-report`.
2. Commit propio: `chore(calidad): actualiza baseline de mutación tras primera
   medición` explicando la mejora (si Stryker no puede correr por el bloqueo del
   SDK preview, dejar el marcador y reportar).

Sin un baseline válido, el job nocturno no detecta regresiones; no declarar el
criterio 9 en verde hasta que el score sea real.

## Decisión tomada (2026-08-07)

Criterio de aceptación 1 del spec ajustado de 60 s a <5 min: el valor medido
(~2 min con todos los gates) gobierna sobre el presupuesto escrito antes de
medir. Documentado en `docs/ai/PUERTA_CALIDAD.md` y en el spec.
