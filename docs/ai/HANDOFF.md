# Handoff — notificaciones y recibos de chat

Fecha: 2026-08-09  
Estado: en ejecución, no publicar todavía como feature completa

## Objetivo vigente

Implementar badges inmediatos, recibos enviado/entregado/leído y Web Push para el
chat comprador-vendedor, con experiencia de marketplace tipo Wallapop.

Spec aprobado:
`docs/superpowers/specs/2026-08-09-chat-notificaciones-recibos-design.md`.

Plan autoritativo:
`docs/superpowers/plans/2026-08-09-chat-notificaciones-recibos.md`.

## Estado Git verificado

Rama `develop`. Commits locales creados en esta sesión:

- `c98ca48 docs(chat): diseña notificaciones y recibos de mensajes`
- `e60fee0 docs(chat): planifica notificaciones y recibos`
- `ec5e564 feat(chat): modela entrega de mensajes`
- `a73a2b9 feat(chat): persiste recibos y conteo no leído`
- `e6759b4 feat(chat): expone entrega y contador no leído`

No se ha hecho push porque la feature todavía no tiene frontend, propagación global
ni Web Push. Comprobar `git status --short --branch` al reanudar.

## Implementado

- `AGENTS.md` declara CaseritoApp como marketplace inspirado en la experiencia de
  Wallapop sin copiar identidad visual.
- `Conversacion` guarda cursores de entrega por comprador/vendedor.
- `MarcarEntrega` es monotónico, idempotente, autorizado y emite `EntregaAvanzada`.
- Marcar lectura implica avanzar entrega.
- Migración Chat `20260809151158_ChatRecibosEntrega` y snapshot.
- `ConversacionResumenDto` proyecta cursores de entrega/lectura de la contraparte.
- `IConsultaConversaciones.ContarNoLeidosAsync` cuenta todas las conversaciones.
- `ContarMensajesNoLeidosQuery`.
- `MarcarEntregaCommand`, validator y handler.
- HTTP:
  - `GET /api/chat/no-leidos` → `{ cantidad }`;
  - `PUT /api/chat/conversaciones/{id}/entrega`.

## Evidencia

- `ConversacionTests`: 15 verdes.
- Unitarias vecinas de Chat: 76 verdes.
- `ConsultasChatHandlerTests`: 8 verdes.
- Persistencia dirigida contra Testcontainers.MsSql: 1 verde.
- Última `./verify.ps1 -Changed`: verde; 391 unitarias y 209 arquitectura,
  formato y build correctos.

La primera ejecución del gate del bloque de dominio falló solo por LF/CRLF y se
corrigió con `dotnet format`. La migración generada necesitó convertir su namespace
a file-scoped por `IDE0161`; el gate posterior quedó verde.

## Siguiente paso exacto

Continuar por la tarea 4 del plan: propagación global SignalR.

1. Escribir pruebas rojas para grupo interno por usuario y eventos mínimos de
   contador/recibos.
2. Implementar `GruposChat.ParaUsuario`, alta en `ChatHub.OnConnectedAsync` y
   publicación post-commit sin texto.
3. Añadir integración HTTP para los dos endpoints nuevos antes de regenerar
   OpenAPI.
4. Seguir tareas 5–10: persistencia/worker Web Push, endpoints de suscripción,
   contrato web, badges responsive, checks y service worker.

## Cuidados

- No registrar texto, endpoints push, claves, tokens ni IDs.
- `NotificarNuevoMensajeHandler` actualmente construye un mensaje con el ID técnico
  de conversación y envía correo; al tocarlo conservar el comportamiento existente
  de correo pero convertir el texto a genérico.
- La entrega solo se confirma al procesarla un cliente/service worker; aceptar un
  push en el proveedor no equivale a entregado.
- El service worker no debe guardar el access token: usar comprobante opaco limitado.
- Regenerar OpenAPI y `web/src/api/schema.d.ts`; nunca editar el generado a mano.
- Ejecutar gates desde la raíz, no desde `CaseritoApp/`.
