# Handoff de sesión

## Objetivo

Completar Fase 3, subbloque 3C: seguridad, reportes, bloqueo, cierre y
moderación de conversaciones, mediante TDD y sin incorporar 3D ni Fase 4.

## Estado verificado

- `master` estaba limpio y sincronizado con `origin/master` en
  `5a0ebea merge: integra avance de chat 3c` al iniciar la sesión.
- Se creó desde ese commit la rama persistente
  `feat/chat-3c-seguridad-moderacion`, se publicó y se configuró su upstream.
- El handoff anterior estaba desactualizado: sus commits ya estaban integrados
  en `5a0ebea` y la rama feature que describía no existía local ni remotamente.
- Tarea activa: 6, `Creación y workflow de reportes en Application`.
- La tarea 6 está incompleta; no fusionar en `master` ni usar el commit final
  previsto hasta cerrar todos sus comportamientos.

## Spec y plan activos

- `docs/superpowers/specs/2026-07-20-chat-3c-seguridad-moderacion-design.md`
- `docs/superpowers/plans/2026-07-20-chat-3c-seguridad-moderacion.md`

## Avance de la tarea 6

- Se creó `ModeracionChatHandlerTests` mediante rojos unitarios individuales.
- `ReportarChatCommand`, su validador y handler ya:
  - exigen que el reportante pertenezca a la conversación;
  - crean reportes de conversación sin referencia de mensaje;
  - consultan el mensaje señalado y validan que pertenezca a la conversación;
  - rechazan un reporte abierto duplicado con error genérico.
- Se añadió `IRepositorioReportesChat` con el alta y la consulta de duplicado.
- `IRepositorioMensajes` incorporó consulta por identificador y se actualizaron
  el adaptador EF y sus dobles existentes.
- No se añadieron logs, payloads, participantes ni contenido a DTO o auditoría.
- Se verificó otra omisión del estado heredado: existen el modelo y la
  configuración EF de moderación, pero todavía no hay adaptadores EF para
  reportes y registros. Deben completarse después de estabilizar los puertos.

## Verificaciones ejecutadas

- Rojo inicial: faltaban el namespace y los puertos de moderación de
  Application.
- Rojo de mensaje: faltaba la consulta por identificador en el puerto de
  mensajes.
- Rojo de duplicado: faltaba la consulta de reporte abierto.
- `dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~ModeracionChatHandlerTests`:
  3/3 verde.
- `dotnet format CaseritoApp.sln --no-restore`: ejecutado.
- `git diff --check`: verde.

## Continuación exacta

Antes del siguiente rojo, añadir cobertura mínima de los comportamientos ya
satisfechos estructuralmente por la firma actual: reporte de participante sin
identificador objetivo enviado por cliente y `404` indistinguible para
conversación/mensaje externos.

Próximo test rojo exacto:

`ModeracionChatHandlerTests.Tomar_reporte_pendiente_asigna_un_solo_moderador_y_audita`

Debe crear un reporte pendiente, ejecutar `TomarReporteChatCommandHandler` con
dobles mínimos de reportes y registros, y afirmar estado `EnRevision`,
moderador asignado y un único registro `Tomar`. El rojo causal esperado es que
todavía no existen `ObtenerAsync` en el puerto de reportes, el puerto de
registros, el comando ni su handler.

Continuar luego, un rojo por vez, con:

1. segundo intento de toma rechazado de forma genérica;
2. liberación solo por moderador asignado;
3. atender/descartar solo por moderador asignado;
4. atender con cierre de moderación en la misma unidad transaccional;
5. cierre/reapertura desde expediente con registro append-only;
6. DTO de cola sin contenido ni participantes;
7. evidencia limitada que solo se consulta después de persistir auditoría;
8. fallo genérico y cero contenido si la auditoría no se persiste;
9. adaptadores EF e inscripción de los nuevos puertos.

Al cerrar la tarea ejecutar toda `ModeracionChatHandlerTests`, verificaciones
vecinas, formato y `git diff --check`; recién entonces usar:

`feat(chat): implementa workflow de reportes`

## Restricciones vigentes

- Nunca registrar texto, payloads, tokens, IDs sensibles, participantes,
  grupos, claves de idempotencia ni argumentos.
- Mantener errores genéricos e indistinguibilidad para participantes.
- No añadir 3D, Fase 4 ni capacidades excluidas por el spec.
- No hacer merge a `master`, rebase ni eliminar la rama mientras Chat 3C siga
  incompleto.
