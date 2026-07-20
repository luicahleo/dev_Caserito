# Chat 3C — seguridad, reportes, bloqueo y moderación

Fecha: 2026-07-20

## Objetivo

Completar la superficie mínima de seguridad del chat del MVP: permitir que sus
participantes bloqueen a la contraparte, cierren conversaciones y reporten una
conversación, un mensaje o al otro participante; y ofrecer a moderación una cola
auditada para revisar evidencia y cerrar o reabrir conversaciones.

El diseño extiende Chat 3A/3B sin cambiar su asociación unívoca entre aviso,
comprador y vendedor, su historial inmutable ni su orden global de mensajes.

## No objetivos y límite con 3D

3C no implementa notificaciones, push, correo, badges ni preferencias. Tampoco
incluye presencia, indicador de escritura, adjuntos, edición o eliminación de
mensajes. Las notificaciones de nuevo mensaje y sus preferencias pertenecen a
3D y consumirán los eventos ya confirmados por 3B; deberán respetar los estados
de bloqueo y cierre definidos aquí.

Quedan fuera órdenes, pagos, QR y envíos de Fase 4. Moderación de chat no aplica
sanciones globales a cuentas ni modifica publicaciones: esas capacidades
requieren un diseño posterior o los flujos de sus bounded contexts.

## Modelo de dominio

### Conversación

`Conversacion` incorpora:

- `Estado`: `Activa`, `Cerrada` o `CerradaPorModeracion`.
- fecha y actor de la última transición, para concurrencia y trazabilidad.
- cierre operativo compartido: comprador o vendedor puede cerrar una
  conversación activa y cualquiera puede reabrir una cerrada por participante.
- cierre de moderación: solo un actor con `chat.moderar` puede aplicarlo o
  revertirlo.

Cerrar o reabrir en el estado solicitado es idempotente. Un participante no
puede cerrar ni reabrir mientras exista un bloqueo activo entre la pareja. Una
conversación cerrada conserva mensajes, lectura, paginación y recuperación, pero
rechaza nuevos mensajes. Reabrir no crea una conversación nueva.

### Bloqueo

`BloqueoUsuario` pertenece a Chat y representa una relación dirigida entre
`BloqueadorId` y `BloqueadoId`, con fecha de creación. Solo el bloqueador puede
crear o retirar su bloqueo. La pareja ordenada es única, por lo que bloquear y
desbloquear son idempotentes.

El bloqueo tiene alcance global dentro de Chat: si existe en cualquier dirección
entre dos usuarios, ninguno puede iniciar otra conversación con el otro ni enviar
en las conversaciones existentes. Ambos conservan acceso HTTP al historial y
pueden avanzar su marcador de lectura. Moderación no crea ni retira bloqueos en
nombre de usuarios.

Desbloquear no reabre conversaciones ni reanuda suscripciones SignalR. Solo
elimina la restricción de bloqueo; el estado de cada conversación sigue vigente.

### Reporte

`ReporteChat` tiene exactamente un objetivo:

- `Conversacion`;
- `Mensaje`;
- `Participante` (siempre la contraparte de la conversación).

Guarda conversación, reportante, tipo y referencia del objetivo, categoría,
detalle opcional, estado y fechas de workflow. Las categorías iniciales son
`Acoso`, `Estafa`, `ContenidoProhibido`, `Spam`, `Suplantacion`,
`SalidaDePlataforma` y `Otro`. El detalle normalizado es opcional y tiene un
máximo de 1000 caracteres.

Solo un participante puede reportar su conversación, un mensaje perteneciente a
ella o a su contraparte. No puede autorreportarse ni señalar otro usuario o un
mensaje de otra conversación. Se permite un único reporte no resuelto por
reportante y objetivo; un duplicado devuelve conflicto genérico. Un reporte no
cierra ni bloquea automáticamente.

Estados:

- `Pendiente`: creado y visible en la cola.
- `EnRevision`: tomado por un moderador; registra quién y cuándo.
- `Atendido`: resolución final, con o sin cierre de moderación.
- `Descartado`: resolución final sin sanción.

Solo el moderador asignado puede resolver o liberar un reporte `EnRevision`.
Tomar un reporte pendiente es atómico. Liberarlo vuelve a `Pendiente`. Los
estados finales no se reabren en 3C.

### Auditoría de moderación

`RegistroModeracionChat` es append-only y guarda acción, fecha, moderador,
reporte y conversación afectados. Registra `Tomar`, `Liberar`,
`ConsultarEvidencia`, `Atender`, `Descartar`, `CerrarConversacion` y
`ReabrirConversacion`. No guarda contenido, participantes, payloads ni argumentos.

## Permisos

Los endpoints de participantes requieren identidad autenticada y vuelven a
validar pertenencia en Application. Solo un participante:

- bloquea o desbloquea a su contraparte;
- cierra o reabre una conversación que no esté cerrada por moderación;
- crea reportes sobre objetivos válidos de su conversación;
- lee historial y marca lectura aunque exista bloqueo o cierre.

Moderación usa el permiso existente `chat.moderar`, separado de
`publicaciones.moderar`. Este permiso permite gestionar la cola, consultar
evidencia auditada, cerrar/reabrir por moderación y resolver reportes. No permite
buscar libremente conversaciones, leer contenido fuera de un reporte ni
administrar bloqueos personales.

Para participantes, recurso inexistente, conversación ajena, mensaje ajeno y
objetivo no autorizado producen el mismo `404` con mensaje genérico. No se usa
`403` para revelar existencia. Los endpoints administrativos sí devuelven `403`
por ausencia del permiso, antes de consultar recursos; dentro de la zona
autorizada, identificadores inexistentes devuelven `404` genérico.

## Flujos y efectos

### Bloquear y desbloquear

Al bloquear desde una conversación se deriva la contraparte en servidor; el
cliente nunca envía el identificador del usuario objetivo. La transacción crea el
bloqueo si falta. Tras confirmar, todas las conexiones de ambos participantes se
retiran de los grupos de sus conversaciones compartidas. El desbloqueo elimina
solo el bloqueo propio y no añade conexiones a grupos automáticamente.

Iniciar conversación y enviar mensaje consultan bloqueos dentro de la misma
operación de negocio. Los índices únicos y tokens de concurrencia impiden carreras
que dejen dos decisiones incompatibles confirmadas; un conflicto recuperable
produce el error genérico ya usado por Chat.

### Cerrar y reabrir

El cierre confirmado retira las conexiones de ambos participantes del grupo de
esa conversación. Reabrir no vuelve a suscribirlas. Lectura, listado y
recuperación siguen disponibles y los DTO exponen estado, origen del cierre y si
el usuario actual puede enviar, sin exponer quién bloqueó a quién.

El intento de enviar en una conversación cerrada o bloqueada devuelve `409` con
un único código público `chat_conversacion_no_disponible_para_envio`; no distingue
la causa. La falta de pertenencia continúa siendo `404`.

### Reportar y moderar

Crear un reporte valida el objetivo sin devolver sus datos y responde `201` con
el identificador del reporte. La cola administrativa contiene solo referencias,
categoría, estado y fechas; excluye textos, detalles y participantes.

Consultar evidencia es una operación separada. Primero persiste el registro de
auditoría y después devuelve:

- el detalle del reporte, si existe;
- para conversación o participante, metadatos mínimos y los últimos 10 mensajes;
- para mensaje, el mensaje señalado y hasta 5 anteriores y 5 posteriores.

No devuelve identidades de participantes: usa roles `Comprador` y `Vendedor` y
marca autor del reporte/objetivo con roles relativos. La evidencia no admite
búsqueda arbitraria ni paginación más allá de esa ventana. Si no puede persistir
la auditoría, no devuelve contenido.

Atender puede opcionalmente cerrar por moderación en la misma transacción.
Descartar no cambia la conversación. Cerrar o reabrir desde el expediente siempre
genera un registro append-only.

## SignalR y consistencia de acceso

`SuscribirConversacion` solo acepta conversaciones activas, sin bloqueo entre sus
participantes y accesibles al usuario. Ante cualquier fallo conserva el
`HubException` genérico actual.

El Host mantiene un registro efímero y acotado de conexiones por usuario y
conversación, sin escribirlo en logs ni métricas. Bloquear o cerrar usa ese
registro para retirar inmediatamente todas las conexiones implicadas del grupo.
Desconectar limpia el registro. Los nombres de grupo y los connection IDs nunca
se registran.

El retiro de grupos es una defensa rápida, no la única autorización. Antes de
publicar cada entrega de outbox, el despachador vuelve a consultar si la
conversación sigue habilitada para tiempo real. Si está cerrada o bloqueada,
marca esa entrega como procesada sin emitir el payload; el mensaje permanece
recuperable por HTTP. Así una conexión obsoleta o una caída entre commit y retiro
no filtra contenido. Reabrir o desbloquear exige una suscripción nueva.

## Persistencia

El schema `chat` incorpora:

- columnas de estado/cierre y token de concurrencia en `Conversaciones`;
- `BloqueosUsuario`, con índice único `(BloqueadorId, BloqueadoId)` e índices de
  consulta en ambas direcciones;
- `Reportes`, con objetivo discriminado, estado concurrente, asignación y un
  índice único filtrado para reportes `Pendiente`/`EnRevision` del mismo
  reportante y objetivo;
- `RegistrosModeracion`, append-only e indexado por reporte y fecha.

Las referencias son internas a Chat y no crean FK entre bounded contexts. La
migración asigna `Activa` a conversaciones existentes. Los mensajes existentes
no cambian.

## Contratos HTTP

Participantes, bajo `/api/chat`:

- `PUT /conversaciones/{id}/bloqueo` → `204` idempotente.
- `DELETE /conversaciones/{id}/bloqueo` → `204` idempotente.
- `PUT /conversaciones/{id}/cierre` → `204` idempotente.
- `DELETE /conversaciones/{id}/cierre` → `204` idempotente.
- `POST /conversaciones/{id}/reportes` con `tipoObjetivo`, `mensajeId?`,
  `categoria` y `detalle?` → `201`.

`tipoObjetivo=Participante` deriva la contraparte; no acepta usuario objetivo.
`tipoObjetivo=Mensaje` exige `mensajeId`; los demás tipos lo prohíben.

Moderación, bajo `/api/admin/moderacion/chat` y permiso `chat.moderar`:

- `GET /reportes?estado=&cursor=&limite=`;
- `POST /reportes/{id}/tomar` y `/liberar`;
- `GET /reportes/{id}/evidencia`;
- `POST /reportes/{id}/atender` con `cerrarConversacion: bool`;
- `POST /reportes/{id}/descartar`;
- `PUT /reportes/{id}/cierre-conversacion`;
- `DELETE /reportes/{id}/cierre-conversacion`.

Validación devuelve `400`; duplicado o transición incompatible, `409`; recurso
indistinguible, `404`; falta de autenticación, `401`; falta del permiso
administrativo, `403`; rate limit, `429`. Los cuerpos públicos usan textos
genéricos y nunca reflejan argumentos recibidos.

## Cliente web mínimo

Se regenera OpenAPI y se extiende `src/api/chat.ts` con los contratos de estado,
`puedeEnviar`, cierre, bloqueo y reporte. Como 3A/3B no incorporaron una pantalla
de conversaciones, 3C no crea esa UI completa: deja adaptadores tipados y pruebas
para que la futura vista deshabilite el compositor cuando `puedeEnviar` sea falso.
No se añaden badges ni notificaciones.

La administración incorpora una ruta protegida por `chat.moderar`, reutilizando
el patrón visual de moderación de avisos pero con cola y diálogo de evidencia
propios. El contenido solo se solicita cuando el moderador abre explícitamente el
expediente; cerrar el diálogo elimina ese dato del estado de la vista. Los textos
son españoles y no muestran identificadores técnicos.

## Observabilidad y anti-PII

Es obligatorio no registrar texto de mensajes o reportes, payloads, tokens,
identificadores sensibles, participantes, grupos, claves de idempotencia,
connection IDs ni argumentos de comandos/hub/endpoints. Los errores son
genéricos. El pipeline de logging conserva solo nombre de operación, resultado y
duración.

Las métricas, si se añaden, solo usan etiquetas de baja cardinalidad: operación,
resultado, categoría tipada y estado; nunca IDs, texto o nombres de grupo. La
auditoría persistida es el único rastro detallado de moderación y tampoco contiene
contenido. Se agregan pruebas arquitectónicas que inspeccionan plantillas de log,
DTO de cola y dependencias para impedir regresiones.

## Pruebas

### Unitarias

- transiciones e idempotencia de conversación;
- creación/retiro y dirección de bloqueos;
- restricciones de envío/inicio con bloqueo o cierre;
- validación de objetivos, categorías, duplicados y estados de reporte;
- toma concurrente, liberación y resolución por el moderador asignado;
- cierre/reapertura de moderación y registros append-only;
- autorización indistinguible de consultas y comandos.

### Integración

- migración, índices únicos, filtros y concurrencia SQL Server;
- contratos y códigos HTTP de participante y moderación;
- historial legible y envío prohibido tras cierre/bloqueo;
- bloqueo transversal a varias conversaciones y nuevo inicio;
- evidencia limitada, autorizada y condicionada a auditoría persistida;
- retirada de grupos en todas las conexiones y nueva suscripción requerida;
- carrera envío/bloqueo o envío/cierre sin entrega SignalR posterior;
- OpenAPI y permiso `chat.moderar`.

### Arquitectura

- Domain/Application conservan la dirección de dependencias;
- políticas Identity permanecen en Host;
- no aparecen referencias cruzadas entre contextos ni FK externas;
- logs, excepciones, eventos y DTO de cola no contienen campos prohibidos;
- el publicador no puede omitir la comprobación de elegibilidad en tiempo real.

## Criterios de aceptación

1. Cualquier participante puede bloquear globalmente a su contraparte y solo él
   puede retirar ese bloqueo.
2. Un bloqueo en cualquier dirección impide inicio, envío y suscripción en vivo,
   pero no lectura histórica.
3. Los participantes cierran/reabren operativamente; solo moderación revierte un
   cierre de moderación.
4. Se reporta conversación, mensaje o contraparte con categorías y estados
   definidos, sin autorreportes ni objetivos externos.
5. Moderación trabaja con `chat.moderar`, accede a evidencia mínima solo desde un
   reporte y toda consulta de contenido queda auditada antes de responder.
6. Cierre o bloqueo retira conexiones de grupos y ninguna entrega posterior a la
   revocación emite contenido, incluso ante carreras o estado SignalR obsoleto.
7. Recursos inexistentes y no autorizados son indistinguibles para participantes.
8. Backend y web no registran ni exponen PII o contenido fuera de los contratos
   expresamente autorizados.
9. No se incorpora trabajo de 3D, Fase 4 ni capacidades de mensajería diferidas.
