# Handoff — Fase 6 CaseritoApp

## Estado al cerrar sesión

- Spec aprobado: `docs/superpowers/specs/2026-07-27-fase-6-notificaciones-endurecimiento-piloto-design.md`
- Plan escrito: `docs/superpowers/plans/2026-07-27-fase-6-notificaciones-endurecimiento-piloto.md`
- Worktree en `master`, limpio salvo archivos no commiteados de docs:
  - `docs/superpowers/specs/2026-07-27-fase-6-notificaciones-endurecimiento-piloto-design.md`
  - `docs/superpowers/plans/2026-07-27-fase-6-notificaciones-endurecimiento-piloto.md`
- No se escribió código de Fase 6.

## Prompt para la siguiente sesión

Continuar CaseritoApp Fase 6. El spec está aprobado y el plan de implementación
está en `docs/superpowers/plans/2026-07-27-fase-6-notificaciones-endurecimiento-piloto.md`.

Objetivo: ejecutar el plan tarea por tarea, empezando por la Task 0
(despacho de eventos de integración por MediatR) y siguiendo el orden propuesto.
Usar `superpowers:subagent-driven-development` para delegar tareas independientes
y revisar entre cada una.

Reglas:
- Leer `AGENTS.md` raíz, `CaseritoApp/AGENTS.md` y `web/AGENTS.md`.
- Leer `docs/ai/ECONOMIA_TOKENS.md`.
- Seguir el plan; no rediseñar.
- Ejecutar tests dirigidos por tarea y la suite completa al integrar.
- No hacer git commit/push/merge sin autorización explícita.
- Mantener anti-PII: no loggear emails, cuerpos, IDs sensibles, documentos ni tokens.

Graphify está disponible en `graphify-out/graph.json` si el agente necesita
relaciones entre archivos, pero el plan ya indica rutas exactas; usar graphify
solo para dudas arquitectónicas amplias, no como paso inicial.
