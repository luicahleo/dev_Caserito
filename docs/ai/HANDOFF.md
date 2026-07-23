# Handoff de sesión

## Objetivo

Completar la tarea 7 de Chat 3C según el spec y plan activos, sin avanzar a la
tarea 8, Chat 3D o Fase 4.

## Estado verificado al iniciar

- Rama: `feat/chat-3c-seguridad-moderacion`, con upstream homónimo en `origin`.
- Punta local y remota inicial:
  `f0fa5cec0dc642084576191e0c1c574133cc16d9`.
- El worktree estaba limpio.
- No se hizo merge, rebase ni eliminación de ramas.
- Documentos activos:
  - `docs/superpowers/specs/2026-07-20-chat-3c-seguridad-moderacion-design.md`
  - `docs/superpowers/plans/2026-07-20-chat-3c-seguridad-moderacion.md`

## Avance parcial de la tarea 7

- Se agregó
  `ChatFlujoTests.Reportar_recurso_inexistente_o_ajeno_devuelve_404_indistinguible`.
  Crea conversación propia y ajena, usa el mismo request contra un GUID
  inexistente y la conversación ajena, exige `404`, compara los campos públicos
  genéricos y comprueba que no se reflejan GUIDs ni el detalle recibido.
- El primer rojo de ese test fue un falso positivo del test: buscaba la palabra
  `Conversacion`, que forma parte del código genérico permitido. Tras limitar la
  comprobación a valores sensibles recibidos, quedó verde sin cambio productivo.
- Se agregó `ChatFlujoTests.Reportar_objetivo_duplicado_devuelve_409_generico`;
  quedó verde con el contrato genérico existente y sin eco de argumentos.
- Se agregó `ChatFlujoTests.Reportar_sin_autenticacion_devuelve_401`; quedó
  verde con la autorización del grupo participante.
- Se creó `ModeracionChatTests`.
- Se agregó `Cola_sin_permiso_chat_moderar_devuelve_403`. El rojo causal fue
  `404` porque no existía la ruta. Se creó
  `Host/Endpoints/ModeracionChatEndpoints.cs`, protegido con
  `PoliticasAutorizacion.Permiso(Permisos.ChatModerar)`, y se mapeó en
  `Program.cs`; quedó verde.
- Se agregó
  `Cola_administrativa_devuelve_referencias_sin_contenido_ni_participantes`.
  El rojo causal fue una colección vacía del stub. La ruta ahora envía
  `ListarReportesChatQuery` y devuelve `ReporteChatColaDto`; quedó verde y
  comprueba ausencia de detalle y reportante.
- La tarea 7 sigue incompleta. No usar aún el commit final
  `feat(chat): publica endpoints de seguridad y moderacion`.

## Tests ejecutados

- Rojo de prueba:
  `dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~ChatFlujoTests.Reportar_recurso_inexistente_o_ajeno_devuelve_404_indistinguible`
  (falso positivo por coincidencia con el código genérico).
- Verde del test anterior: mismo comando (1/1).
- Verde:
  `... --filter FullyQualifiedName~ChatFlujoTests.Reportar_objetivo_duplicado_devuelve_409_generico`
  (1/1).
- Verde:
  `... --filter FullyQualifiedName~ChatFlujoTests.Reportar_sin_autenticacion_devuelve_401`
  (1/1).
- Rojo:
  `... --filter FullyQualifiedName~ModeracionChatTests.Cola_sin_permiso_chat_moderar_devuelve_403`
  (esperado `403`, real `404`).
- Verde del rojo anterior: mismo comando (1/1).
- Rojo:
  `... --filter FullyQualifiedName~ModeracionChatTests.Cola_administrativa_devuelve_referencias_sin_contenido_ni_participantes`
  (la colección del stub estaba vacía).
- Verde del rojo anterior: mismo comando (1/1).

## Continuación exacta

Próximo test a escribir, uno solo:

`ModeracionChatTests.Tomar_y_liberar_reporte_actualiza_cola_y_auditoria`

Debe crear un reporte pendiente, autenticar un moderador, tomarlo y comprobar
`204`, estado `EnRevision`, asignación persistida y un único registro auditado
`Tomar`; luego liberarlo, comprobar `204`, retorno a `Pendiente`, asignación
limpia y un único registro adicional `Liberar`. Los cuerpos y DTO públicos no
deben exponer contenido ni participantes.

Después continuar secuencialmente con evidencia auditada, atender/descartar,
cierre/reapertura por moderación, conflictos/transiciones administrativas
genéricas, rate limit de baja frecuencia y OpenAPI completo. No avanzar a tarea
8 antes de cerrar, probar, commitear y publicar la 7.

## Puntas

- Padre exacto del checkpoint:
  `f0fa5cec0dc642084576191e0c1c574133cc16d9`.
- La punta local/remota final será el commit de checkpoint que contiene este
  handoff; verificar el hash exacto con `git rev-parse HEAD` y
  `git rev-parse '@{upstream}'` al retomar.
