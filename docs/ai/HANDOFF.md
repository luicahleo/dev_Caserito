# Handoff — Fase 6 CaseritoApp

## Estado al cerrar sesión

- **Task 3 completada y verificada:** Infrastructure de Notifications — persistencia y email.
  - Configuración EF Core de `Notificacion` en `Notificaciones/ConfiguracionNotificacion.cs`.
  - `NotificationsDbContext` actualizado con `DbSet<Notificacion>` y schema `notifications`.
  - Repositorio EF Core `NotificacionRepositoryEfCore` implementado.
  - `UnitOfWorkNotifications` creado.
  - `IEmailSender` en Application; adaptadores `LogEmailSender` y `SmtpEmailSender` en Infrastructure.
  - `DependencyInjection.AgregarNotifications(...)` y `DesignTimeNotificationsDbContextFactory` creados.
  - Migración inicial `NotificationsInicial` generada.
  - Test de persistencia `NotificationsPersistenciaTests` creado; `CaseritoApiFactory` registra `NotificationsDbContext` para la suite.
  - Verificaciones:
    - `dotnet build CaseritoApp.sln` → exit 0, 0 advertencias, 0 errores.
    - `dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj` → 286 correctos, 0 fallos.
    - `dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj` → 160 correctos, 0 fallos.
    - `dotnet format CaseritoApp.sln --verify-no-changes` → exit 0.
- No se realizó commit/push; el usuario gestionará los commits manualmente.
- Worktree en `master`, con cambios sin commitear de Task 3.

## Prompt para la siguiente sesión

Continuar CaseritoApp Fase 6 — **Task 4: Handlers de eventos de integración para notificaciones**.

Seguir el plan `docs/superpowers/plans/2026-07-27-fase-6-notificaciones-endurecimiento-piloto.md` al pie de la letra.

Archivos principales:
1. Crear `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/IConsultaParticipantesOrden.cs`
2. Crear `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/IConsultaProductoParaAlerta.cs`
3. Crear `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/IConsultaEmailUsuario.cs`
4. Crear `CaseritoApp/src/Host/CaseritoApp.Host/Notifications/ConsultaEmailUsuarioAdapter.cs`
5. Crear handlers de notificación.

Reglas:
- No rediseñar; seguir el plan literalmente.
- Código en inglés, textos de negocio y comentarios en español.
- No hacer git commit/push sin autorización explícita.
- Verificar build, tests y `dotnet format --verify-no-changes` antes de reportar completo.
