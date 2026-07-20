# Chat 3B — tiempo real para conversaciones y mensajes

Fecha: 2026-07-20  
Estado: aprobado

## Objetivo

Entregar en tiempo real los mensajes persistidos por Chat 3A a las conexiones
autenticadas de sus participantes. HTTP continúa siendo la única vía de escritura y
la fuente de verdad; SignalR reduce la latencia y la API permite recuperar cualquier
mensaje perdido durante una desconexión.

## No objetivos

Quedan fuera de 3B:

- bloqueo, reportes, cierre y moderación de conversaciones de 3C;
- notificaciones, badges globales, push, correo y preferencias de 3D;
- presencia, indicadores de escritura y recibos detallados de lectura;
- adjuntos, edición o eliminación de mensajes;
- Redis, Azure SignalR u otro backplane distribuido;
- rate limiting distribuido y despliegue activo-activo con entrega inmediata;
- pantallas completas de chat.

## Transporte y protocolo

ASP.NET Core SignalR se expone en `/hubs/chat`. Usa WebSockets cuando están
disponibles y deja negociación y fallback a SignalR. El protocolo inicial es JSON.

El hub no acepta texto ni persiste mensajes. El envío continúa exclusivamente por
`POST /api/chat/conversaciones/{id}/mensajes`, conservando la autorización,
idempotencia, validación y rate limiting de 3A.

El cliente dispone únicamente de estas invocaciones:

- `SuscribirConversacion(conversacionId)`;
- `DesuscribirConversacion(conversacionId)`.

El servidor emite `MensajeCreado`. Los nombres constituyen la versión inicial del
protocolo y cualquier cambio incompatible exigirá versionado explícito.

## Autenticación y ciclo de vida

El hub requiere el mismo JWT Bearer que la API. Para WebSockets y SSE en navegador,
`access_token` puede recibirse en la query únicamente cuando la ruta comienza por
`/hubs/chat`; ningún otro endpoint puede usarlo. El token nunca se registra.

SignalR valida el JWT al abrir la conexión y cierra la conexión al expirar el token
mediante `CloseOnAuthenticationExpiration`. El cliente debe reconectar con un token
vigente; el hub no renueva tokens. Una identidad sin un claim `sub` válido se rechaza.

Las pertenencias a grupos no sobreviven a una reconexión. El cliente debe volver a
suscribirse y recuperar mensajes por HTTP. El cierre explícito o por expiración libera
las pertenencias locales de la conexión.

## Autorización y grupos

Cada conversación usa un nombre interno determinista con el formato
`chat:conversacion:{guid-N}`. El cliente nunca proporciona un nombre de grupo.

`SuscribirConversacion` consulta Chat con el `sub` autenticado y sólo añade la conexión
si el usuario es comprador o vendedor. Conversación inexistente y tercero producen el
mismo error genérico. `DesuscribirConversacion` sólo calcula el grupo a partir del ID y
retira la conexión sin revelar si la conversación existe.

La pertenencia se valida al suscribirse, después de cada reconexión y al reconstruir
una entrega desde persistencia. En 3B los participantes son inmutables, por lo que no
se consulta la pertenencia por cada emisión al grupo. El diseño deja un servicio de
grupos en Host que 3C podrá usar para impedir suscripciones o retirar conexiones cuando
incorpore revocación funcional.

No hay asignación automática a todas las conversaciones. Cada conexión se suscribe
sólo a las conversaciones activas para esa pestaña o dispositivo.

## Contrato de entrega

`MensajeCreado` entrega exactamente:

```json
{
  "id": "guid",
  "conversacionId": "guid",
  "remitenteId": "guid",
  "secuencia": 123,
  "texto": "Texto plano persistido",
  "enviadoEn": "timestamp UTC"
}
```

El contrato reutiliza la forma de `MensajeDto`. No contiene destinatario, roles,
clave de idempotencia, aviso, perfiles, estado de lectura ni datos de notificación.
El texto es imprescindible para mostrar el mensaje y sólo se envía a grupos cuya
suscripción fue autorizada.

## Publicación después del commit

La entrega nunca se inicia desde el endpoint ni desde el handler antes del commit. Se
agrega una outbox mínima al schema `chat`; una entrada se crea en la misma transacción
que un mensaje nuevo y un servicio en segundo plano publica sólo datos confirmados.

La outbox guarda:

- `Id`;
- `ConversacionId`;
- `MensajeId`;
- `Secuencia`;
- `CreadaEn`;
- `Intentos`;
- `ProximoIntentoEn`;
- `ProcesadaEn`;
- `LeaseHasta`.

No duplica texto, participantes, tokens ni claves de idempotencia. El worker carga el
mensaje persistido por su ID, proyecta el contrato autorizado y publica al grupo. Sólo
la primera ejecución de un envío crea la entrada; un reintento idempotente no crea ni
publica otra intención.

La creación de la outbox será una responsabilidad de persistencia de Chat asociada a
la inserción de un `Mensaje`, para conservar una única transacción sin introducir
SignalR en Domain o Application. La migración pertenece a `ChatDbContext`.

## Fallos, reintentos y leases

Si el commit finaliza pero SignalR falla, la respuesta HTTP conserva el éxito porque
el mensaje ya es durable. La entrada queda pendiente y se reintenta con backoff
acotado. Una lease con vencimiento permite recuperar trabajo tras una caída y evita
que dos workers procesen simultáneamente la misma entrada en condiciones normales.

La semántica es al menos una vez. Un fallo después de emitir y antes de marcar la
entrada como procesada puede repetir el evento. No se promete exactamente una vez y
no se crea una dead-letter con payload sensible. Tras errores repetidos la entrada
permanece recuperable y sólo se emiten métricas técnicas de baja cardinalidad.

La reclamación debe ser atómica en SQL Server y apta para más de un worker. Esto
prepara el almacenamiento para varias instancias, pero no sustituye un backplane de
SignalR.

## Orden, duplicados y recuperación

La secuencia persistida de 3A es la autoridad para el orden. Los eventos SignalR
pueden duplicarse o llegar fuera de orden. El cliente:

- deduplica por `id`;
- ordena por `secuencia`;
- conserva la última secuencia aplicada;
- ante un hueco o reconexión consulta la API;
- combina respuesta HTTP y evento vivo sin insertar dos veces el mismo mensaje.

Para recuperar hacia delante se amplía el endpoint existente:

```http
GET /api/chat/conversaciones/{id}/mensajes?despuesDeSecuencia=123&limite=100
```

`cursor` histórico y `despuesDeSecuencia` son mutuamente excluyentes. La consulta
devuelve mensajes con secuencia mayor en orden ascendente, con límite predeterminado
50 y máximo 100. Un valor negativo o una combinación inválida produce 400 genérico.
Conversación inexistente y tercero producen 404. Sin ninguno de los parámetros se
mantiene el comportamiento de 3A para obtener los mensajes recientes.

El cursor opaco de 3A continúa reservado para cargar mensajes anteriores. La secuencia
no adquiere semántica de identificador de negocio.

## Reconexión, pestañas y dispositivos

Después de reconectar, cada cliente:

1. obtiene o suministra un JWT vigente;
2. vuelve a invocar `SuscribirConversacion`;
3. solicita mensajes posteriores a su última secuencia aplicada;
4. deduplica eventos concurrentes con la recuperación HTTP.

Cada pestaña y dispositivo mantiene una conexión independiente. Todas las conexiones
autorizadas y suscritas reciben el evento. No hay elección de pestaña principal,
estado compartido en servidor ni presencia. Marcar lectura permanece como operación
HTTP explícita.

## Límites y abuso

Cada conexión admite como máximo 20 conversaciones suscritas. El valor es
configurable y se valida antes de consultar o añadir otro grupo. Las invocaciones de
suscripción y desuscripción tienen un límite local por conexión. El servidor limita
el tamaño de mensajes SignalR, buffers, paralelismo y tiempo de handshake con valores
conservadores.

IDs inválidos, exceso de grupos, frecuencia abusiva y recursos no autorizados generan
errores genéricos. Ante abuso reiterado el servidor puede cerrar la conexión sin
incluir argumentos del cliente en logs o excepciones. El rate limiting HTTP de 3A no
cambia.

## Varias instancias

El MVP de 3B garantiza tiempo real completo con una sola instancia. Quedan preparados:

- publisher detrás de una abstracción de Host;
- outbox persistida con lease;
- grupos deterministas;
- eventos idempotentes;
- recuperación HTTP como garantía final.

Sin backplane, una instancia sólo puede alcanzar sus conexiones locales. La
recuperación HTTP evita pérdida definitiva, pero no garantiza baja latencia entre
instancias. Un despliegue multiinstancia deberá incorporar Redis, Azure SignalR u otro
backplane antes de declarar entrega inmediata completa.

## Errores y cierre

- handshake anónimo o inválido: 401;
- identidad sin `sub`: conexión rechazada;
- conversación inexistente o tercero: error de hub genérico;
- token expirado: conexión cerrada;
- exceso de límites: invocación rechazada y posible cierre;
- fallo interno: error genérico sin eco de argumentos.

Los detalles de excepción no se envían al cliente. Los IDs sensibles no aparecen en
mensajes de error.

## Observabilidad y anti-PII

Se permiten conteos de conexiones, duración, cantidad de suscripciones, outbox
pendiente/procesada/reintentada, latencia commit-publicación, versión de protocolo y
categoría técnica de fallo.

Está prohibido registrar o usar como etiquetas:

- texto o payloads;
- tokens, query strings o headers;
- IDs de conversación, mensaje o usuario;
- participantes o nombres concretos de grupos;
- claves de idempotencia;
- argumentos de invocaciones;
- excepciones que incorporen valores recibidos del cliente.

Las métricas usan etiquetas de baja cardinalidad. Los logs contienen únicamente
nombre de operación y código técnico genérico. Las guardas de arquitectura anti-PII
se amplían para cubrir hub, outbox y worker.

## Integración frontend mínima

3B incorpora infraestructura reutilizable, no pantallas:

- cliente oficial `@microsoft/signalr`;
- factoría de conexión con token obtenido bajo demanda;
- reconexión automática con backoff y jitter;
- suscripción y desuscripción explícitas;
- callback tipado para `MensajeCreado`;
- recuperación HTTP por secuencia;
- utilidad testeable de deduplicación y orden.

El token no se persiste adicionalmente. No se agregan rutas, pantallas, presencia,
indicadores de escritura ni UX de notificaciones.

## Pruebas

### Unitarias

- contrato exacto de `MensajeCreado`;
- generación de nombres internos de grupos;
- autorización y límite de suscripciones;
- creación única de intención para mensajes nuevos;
- reintentos, backoff y leases;
- deduplicación, orden y detección de huecos;
- ausencia de campos sensibles en outbox y observabilidad.

### Integración backend

- conexión anónima rechazada y JWT válido aceptado;
- token por query aceptado sólo bajo `/hubs/chat`;
- tercero no puede suscribirse y ambos participantes sí;
- rollback no deja outbox ni publica;
- commit exitoso crea una entrega;
- reintento HTTP idempotente no duplica outbox;
- publicación repetida conserva mensaje y secuencia;
- reconexión exige resuscripción;
- recuperación hacia delante mantiene autorización y orden;
- expiración del token cierra la conexión;
- límites devuelven errores genéricos.

### Arquitectura

- Host contiene SignalR y autorización;
- Chat Application y Domain no dependen de SignalR;
- no aparecen dependencias entre bounded contexts;
- outbox y contratos no duplican contenido sensible;
- logging conserva las guardas anti-PII.

### Frontend

- el token se obtiene bajo demanda;
- reconexión vuelve a suscribir;
- un hueco activa recuperación desde la secuencia conocida;
- IDs repetidos se ignoran;
- eventos fuera de orden se aplican por secuencia;
- varias conexiones no requieren estado compartido del servidor.

## Contratos derivados

La ampliación HTTP de recuperación se documenta en OpenAPI y regenera
`artifacts/openapi/CaseritoApp.Host.json` y los tipos web correspondientes. SignalR no
forma parte de OpenAPI; su contrato tipado se mantiene explícitamente en backend y
frontend y se protege con tests de forma.

## Criterios de aceptación

- sólo participantes autenticados pueden suscribirse;
- un mensaje se publica únicamente después de quedar confirmado;
- una caída entre commit y publicación no pierde la intención durable;
- duplicados y desorden no alteran el historial;
- una reconexión recupera mensajes mediante secuencia persistida;
- varias pestañas o dispositivos funcionan independientemente;
- ningún log, error o métrica contiene payloads o identificadores sensibles;
- HTTP continúa siendo la autoridad;
- la solución queda preparada, pero no simula soporte multiinstancia sin backplane;
- pruebas dirigidas, suite completa, build, formato, frontend y artefactos derivados
  quedan verificados al cierre.
