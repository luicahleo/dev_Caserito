# Handoff de sesión

## Objetivo

Completar Fase 3, sub-bloque 3C: seguridad, reportes, bloqueo, cierre y
moderación de conversaciones, mediante TDD y sin incorporar 3D ni Fase 4.

## Estado verificado

- Rama: `feat/chat-3c-seguridad-moderacion`.
- Último commit: `ae2a566 feat(chat): expone seguridad de conversaciones`.
- La rama local está dos commits por delante de
  `origin/feat/chat-3c-seguridad-moderacion`; no se hizo push, merge ni rebase.
- El único cambio pendiente es este handoff actualizado.

## Spec y plan activos

- `docs/superpowers/specs/2026-07-20-chat-3c-seguridad-moderacion-design.md`
- `docs/superpowers/plans/2026-07-20-chat-3c-seguridad-moderacion.md`

## Completado

- Tareas 1 a 3: `ae51597`, `0872897` y `034d258`.
- Tarea 4: `4548025 feat(chat): persiste seguridad y moderacion`.
- Tarea 5: `ae2a566 feat(chat): expone seguridad de conversaciones`:
  - cierre y reapertura por participantes con bloqueo global;
  - ausencia y no pertenencia conservan el mismo error genérico;
  - DTO y resumen exponen estado, origen de cierre y `PuedeEnviar`, sin
    dirección del bloqueo;
  - listado calcula elegibilidad con estado y bloqueo en cualquier dirección;
  - corregido el inicio de una conversación existente para no omitir el
    bloqueo global.

## Verificaciones ejecutadas

- Matriz unitaria de seguridad, consultas, lectura, inicio y envío: 28/28.
- `ChatPersistenciaTests`: 15/15 contra SQL Server Testcontainers.
- `dotnet format CaseritoApp.sln --no-restore --verify-no-changes`: verde.
- `git diff --check`: verde.
- Revisión anti-PII: no se añadieron logs, payloads ni exposición de la
  dirección del bloqueo.

## Tarea exacta siguiente

Tarea 6 del plan: `Creación y workflow de reportes en Application`.

### Próximo test rojo exacto

Crear `tests/CaseritoApp.UnitTests/Chat/ModeracionChatHandlerTests.cs` con un
único test `Reportar_conversacion_deriva_objetivo_y_agrega_reporte` que:

1. cree una conversación y use al comprador como reportante;
2. construya `ReportarChatCommand` para objetivo `Conversacion`, categoría
   `Acoso` y detalle opcional;
3. ejecute el handler con dobles mínimos de conversación, mensajes y reportes;
4. afirme éxito, un único reporte agregado, misma conversación/reportante,
   objetivo `Conversacion` y `MensajeId` nulo.

Ejecutar:

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~ModeracionChatHandlerTests.Reportar_conversacion_deriva_objetivo_y_agrega_reporte
```

Rojo causal esperado: puertos, comando y handler de moderación todavía no
existen. Implementar solamente esas piezas mínimas hasta verde y continuar un
rojo por vez con mensaje perteneciente, contraparte derivada, duplicado, toma,
liberación, resolución asignada, cierre atómico y evidencia condicionada a
auditoría.

Commit previsto al cerrar la tarea:

```text
feat(chat): implementa workflow de reportes
```

## Restricciones vigentes

- Nunca registrar texto, payloads, tokens, IDs sensibles, participantes,
  grupos, claves de idempotencia ni argumentos.
- Mantener errores genéricos e indistinguibilidad para participantes.
- No añadir 3D, Fase 4 ni capacidades excluidas por el spec.
- No hacer push, merge ni rebase sin autorización explícita.
