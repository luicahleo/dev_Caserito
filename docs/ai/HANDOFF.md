# Handoff de sesión

## Objetivo

Completar Chat 3C según el spec y plan activos, con TDD estricto y sin ampliar
el alcance a 3D o Fase 4.

## Estado verificado

- Rama: `feat/chat-3c-seguridad-moderacion`.
- Tarea 6 cerrada y publicada en
  `91e5d3d feat(chat): implementa workflow de reportes`.
- La rama local y `origin/feat/chat-3c-seguridad-moderacion` coincidían en
  `91e5d3d` después del push.
- No se hizo merge, rebase ni eliminación de ramas.

## Spec y plan activos

- `docs/superpowers/specs/2026-07-20-chat-3c-seguridad-moderacion-design.md`
- `docs/superpowers/plans/2026-07-20-chat-3c-seguridad-moderacion.md`

## Completado

- Tareas 1 a 5 integradas previamente en `master` mediante `5a0ebea`.
- Tarea 6 completa:
  - reportes de conversación, mensaje perteneciente y contraparte derivada;
  - objetivos externos indistinguibles y duplicados abiertos;
  - toma exclusiva, liberación y resolución por moderador asignado;
  - atender con cierre de moderación en la misma unidad transaccional;
  - cierre/reapertura desde expediente con auditoría append-only;
  - cola sin texto, detalle ni participantes;
  - evidencia por roles relativos, ventana acotada y condicionada a auditoría
    persistida antes de consultar contenido;
  - adaptadores EF e inscripción de todos los puertos de moderación.
- Se corrigió un riesgo de autorización: atender valida la asignación antes de
  mutar la conversación.

## Verificaciones ejecutadas

- `ModeracionChatHandlerTests`, `ReporteChatTests` y
  `RegistroModeracionChatTests`: 19/19.
- `ChatPersistenciaTests` contra SQL Server Testcontainers: 15/15.
- `dotnet build CaseritoApp.sln --no-restore`: verde, cero advertencias.
- `dotnet format CaseritoApp.sln --no-restore --verify-no-changes`: verde.
- `git diff --check`: verde.
- Revisión anti-PII: no se añadieron logs; la cola excluye contenido y
  participantes; los registros de auditoría no guardan contenido.

## Continuación exacta

Tarea 7: `Contratos HTTP y autorización Host`.

Leer `CaseritoApp/AGENTS.md` y buscar con `rg` antes de abrir archivos. Ya se
verificó que:

- `ChatEndpoints.cs` solo expone iniciar, listar, enviar, recuperar y lectura;
- `Program.cs` ya registra rate limits de chat, pero falta una policy de baja
  frecuencia para reportes/acciones;
- `Permisos.ChatModerar` y su asignación al rol Moderador ya existen;
- todavía no existe `ModeracionChatEndpoints.cs`.

Próximo test rojo exacto:

`ChatFlujoTests.Reportar_conversacion_participante_devuelve_201`

Debe autenticar un participante de una conversación existente, enviar
`POST /api/chat/conversaciones/{id}/reportes` con objetivo `Conversacion`,
categoría `Acoso` y detalle opcional, y afirmar `201` con solo el identificador
del reporte. Rojo causal esperado: la ruta todavía devuelve `404`.

Continuar un contrato rojo por vez con bloqueo/cierre de participante y luego
crear `ModeracionChatTests` para `401`, `403` sin `chat.moderar`, cola, toma,
liberación, evidencia, atender/descartar y cierre/reapertura. Mantener `404`
indistinguible, `409` genérico, requests sin usuario objetivo y rate limit de
baja frecuencia.

Commit previsto al cerrar la tarea 7:

`feat(chat): publica endpoints de seguridad y moderacion`

Después ejecutar las verificaciones del plan, publicar el commit y continuar
con la tarea 8. Chat 3C sigue incompleto: no fusionar en `master` ni eliminar la
rama.
