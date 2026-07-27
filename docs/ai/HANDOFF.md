# Handoff — Fase 6 CaseritoApp

## Estado al cerrar sesión

- Spec aprobado: `docs/superpowers/specs/2026-07-27-fase-6-notificaciones-endurecimiento-piloto-design.md`
- Plan escrito: `docs/superpowers/plans/2026-07-27-fase-6-notificaciones-endurecimiento-piloto.md`
- **Task 0 completada y verificada:** despacho de eventos de integración por MediatR.
  - `CaseritoApp.BuildingBlocks.Contracts.csproj` referencia `MediatR`.
  - `IIntegrationEvent` extiende `INotification`.
  - `PublicadorEventosIntegracionMediatR` creado en `BuildingBlocks.Infrastructure/Messaging/`.
  - `Program.cs` reemplaza `IPublicadorEventosIntegracion` con `PublicadorEventosIntegracionMediatR`.
  - Verificaciones:
    - `dotnet build CaseritoApp.sln` → exit 0, 0 advertencias, 0 errores.
    - `dotnet test tests/CaseritoApp.UnitTests` → 281 correctos, 0 fallos.
- No se realizó commit de la Fase 6; el working tree permanece sin cambios commiteados de esta fase.
- Worktree en `master`, limpio salvo archivos no commiteados de docs:
  - `docs/superpowers/specs/2026-07-27-fase-6-notificaciones-endurecimiento-piloto-design.md`
  - `docs/superpowers/plans/2026-07-27-fase-6-notificaciones-endurecimiento-piloto.md`
  - `docs/ai/HANDOFF.md`

## Prompt para la siguiente sesión

Continuar CaseritoApp Fase 6 — **Task 1: Domain de Notifications (agregado Notificación)**.

Seguir el plan `docs/superpowers/plans/2026-07-27-fase-6-notificaciones-endurecimiento-piloto.md` al pie de la letra, sin rediseñar. Trabajar desde `CaseritoApp/`.

Archivos a crear/modificar:
1. Crear `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Domain/Notificaciones/TipoNotificacion.cs`
2. Crear `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Domain/Notificaciones/ErroresNotificacion.cs`
3. Crear `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Domain/Notificaciones/Notificacion.cs`
4. Crear tests `CaseritoApp/tests/CaseritoApp.UnitTests/Notifications/NotificacionTests.cs`

Flujo:
1. Escribir primero los tests de dominio (TDD).
2. Ejecutar `dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~NotificacionTests"` → debe fallar (tipos no existen).
3. Implementar `TipoNotificacion`, `ErroresNotificacion` y `Notificacion` exactamente como indica el plan.
4. Ejecutar los tests de nuevo → deben pasar.
5. Ejecutar `dotnet build CaseritoApp.sln` y `dotnet test tests/CaseritoApp.UnitTests` para verificar que nada se rompe.

Reglas:
- Leer `AGENTS.md` raíz, `CaseritoApp/AGENTS.md`, `web/AGENTS.md` y `docs/ai/ECONOMIA_TOKENS.md`.
- No rediseñar; seguir el plan literalmente.
- Código en inglés, textos de negocio y comentarios en español.
- No hacer git commit/push sin autorización explícita.
- No implementar Task 2 ni adelantarse; solo Task 1.

Graphify está disponible en `graphify-out/graph.json` si el agente necesita relaciones entre archivos, pero el plan ya indica rutas exactas; usar graphify solo para dudas arquitectónicas amplias, no como paso inicial.
