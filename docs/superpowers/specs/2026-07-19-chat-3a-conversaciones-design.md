# Chat 3A — conversaciones y mensajes

Fecha: 2026-07-19  
Estado: aprobado

## Objetivo

Implementar el núcleo backend del bounded context Chat para que un comprador y el
vendedor de un aviso público puedan mantener una conversación persistida dentro de
la aplicación. El bloque incluye dominio, persistencia, API HTTP, autorización,
idempotencia, paginación y estado de lectura.

Chat conserva únicamente identificadores opacos de avisos y usuarios. No depende de
Catalog ni de Identity y no crea claves foráneas hacia sus schemas.

## No objetivos

Quedan fuera de 3A:

- SignalR, presencia, escritura y entrega en tiempo real;
- bloqueo, reportes, cierre operativo y moderación;
- envío efectivo de notificaciones;
- adjuntos, edición, eliminación y búsqueda de mensajes;
- previews con texto en el listado de conversaciones;
- recibos detallados de lectura por mensaje;
- retención, exportación o anonimización automatizadas;
- outbox, bus distribuido y rate limiting distribuido.

## Decisiones funcionales

### Inicio y participantes

- Cualquier usuario autenticado puede iniciar una conversación como comprador, sin
  exigir KYC, siempre que no sea el vendedor del aviso.
- El vendedor no puede iniciar una conversación; puede responder una vez creada.
- Comprador y vendedor nunca pueden coincidir.
- Existe una única conversación permanente por `(CompradorId, AvisoId)`.
- Reintentar el inicio devuelve la conversación existente.
- Los participantes se obtienen de fuentes confiables: comprador desde el claim
  autenticado y vendedor desde Catalog. El cliente no envía IDs de participantes.

### Relación con el aviso

Solo un aviso activo y visible permite iniciar una conversación. Avisos inexistentes,
pausados, ocultos, eliminados o eliminados por moderación se presentan de manera
indistinguible como no contactables.

El estado del aviso se valida únicamente al iniciar. Una conversación creada sigue
operativa aunque el aviso se pause, oculte o elimine después. Solo reglas propias de
Chat podrán impedir mensajes en bloques posteriores.

### Texto e inmutabilidad

- El texto se recorta en los extremos y debe contener entre 1 y 2.000 caracteres
  Unicode.
- Se rechaza texto vacío o compuesto solo por espacios.
- Se conservan los saltos de línea y espacios internos.
- Se almacena y devuelve como texto plano; la UI no debe interpretarlo como HTML.
- URLs y teléfonos no se filtran en 3A.
- Un mensaje persistido no se edita ni se elimina.

## Arquitectura

### Límites de contextos

`Chat.Domain` contiene el agregado y sus invariantes. `Chat.Application` contiene
commands, queries, DTOs y puertos. `Chat.Infrastructure` implementa persistencia EF
Core. Host compone autenticación, endpoints y el adaptador entre Chat y la API de
aplicación de Catalog.

`Chat.Application` declara un puerto de consulta que devuelve un snapshot mínimo del
aviso: `AvisoId` y `VendedorId`, o ausencia si el aviso no es contactable. El adaptador
de Host consume una consulta de aplicación de Catalog. Puede agregarse a Catalog un
contrato de lectura mínimo específico para este propósito, sin exponer el agregado ni
crear una referencia desde Chat.

No se permiten consultas de Chat a tablas de Catalog, referencias de proyectos entre
bounded contexts ni FK cruzadas.

### Modelo de dominio

`Conversacion` es raíz de agregado y contiene:

- `Id`;
- `AvisoId`;
- `CompradorId`;
- `VendedorId`;
- `CreadaEn`;
- `UltimaActividadEn`;
- `UltimaSecuenciaLeidaComprador`;
- `UltimaSecuenciaLeidaVendedor`;
- token de concurrencia.

Sus operaciones son:

- crear validando IDs no vacíos y participantes distintos;
- verificar participación;
- preparar un mensaje para uno de sus participantes;
- registrar la secuencia persistida y avanzar la última actividad;
- avanzar de forma monotónica la lectura del participante.

La conversación no carga todo su historial como colección. `Mensaje` pertenece a la
conversación, pero se persiste y consulta separadamente para que el agregado no crezca
sin límite. Contiene:

- `Id`;
- `ConversacionId`;
- `RemitenteId`;
- `ClaveIdempotencia`;
- `Secuencia` `bigint` generada por SQL Server;
- `Texto`;
- `EnviadoEn`.

La secuencia determina un orden total y estable. Puede tener saltos y no constituye un
identificador público con semántica de negocio.

## Persistencia

El schema `chat` tendrá tablas `Conversaciones` y `Mensajes`.

Índices y restricciones:

- PK opaca en ambas tablas;
- índice único en `Conversaciones(CompradorId, AvisoId)`;
- índices para listar por comprador/vendedor y última actividad;
- índice en `Mensajes(ConversacionId, Secuencia)`;
- índice único en
  `Mensajes(ConversacionId, RemitenteId, ClaveIdempotencia)`;
- FK interna de mensaje a conversación con borrado restrictivo;
- longitudes y columnas Unicode explícitas para texto;
- token de concurrencia en conversación.

No habrá FK a `catalog.Avisos` ni a tablas de Identity. La migración pertenece a
`ChatDbContext`, que se registrará, migrará y probará junto con los demás contextos.

Todos los timestamps son `DateTimeOffset` UTC generados en servidor mediante
`TimeProvider`. No se aceptan fechas del cliente.

### Concurrencia e idempotencia

La creación concurrente se resuelve mediante el índice único: la solicitud perdedora
recupera la conversación existente y responde como reutilización idempotente.

Cada envío exige una `ClaveIdempotencia` GUID generada por el cliente:

- la primera solicitud crea el mensaje;
- repetir la clave con el mismo texto devuelve el mensaje original;
- reutilizarla con texto distinto devuelve conflicto;
- la restricción única impide duplicados bajo concurrencia.

Las actualizaciones conmutativas de última actividad y lectura pueden reintentarse de
forma acotada. Un conflicto que no pueda resolverse con seguridad se expone como 409.

## API HTTP

Todos los endpoints viven bajo `/api/chat`, requieren autenticación y extraen el ID
del usuario desde `sub`.

### Iniciar conversación

`POST /api/chat/conversaciones`

```json
{
  "avisoId": "guid"
}
```

- `201 Created` al crear;
- `200 OK` si ya existía para el mismo comprador y aviso;
- `404 Not Found` si el aviso no es contactable;
- `409 Conflict` si comprador y vendedor coinciden.

La respuesta contiene el DTO de conversación y una ubicación estable.

### Enviar mensaje

`POST /api/chat/conversaciones/{conversacionId}/mensajes`

```json
{
  "claveIdempotencia": "guid",
  "texto": "¿Sigue disponible?"
}
```

- `201 Created` para un mensaje nuevo;
- `200 OK` para un reintento idéntico;
- `404 Not Found` si la conversación no existe o el usuario no participa;
- `409 Conflict` si la clave se reutiliza con otro texto o hay un conflicto no
  recuperable.

### Listar conversaciones propias

`GET /api/chat/conversaciones?cursor={opaco}&limite=20`

- orden descendente por última actividad con desempate estable por ID;
- límite predeterminado 20 y máximo 50;
- cursor opaco;
- devuelve IDs, rol del usuario, timestamps, última secuencia y cantidad de no
  leídos;
- no incluye texto ni preview del último mensaje.

### Obtener mensajes

`GET /api/chat/conversaciones/{conversacionId}/mensajes?cursor={opaco}&limite=50`

- sin cursor devuelve los mensajes más recientes;
- límite predeterminado 50 y máximo 100;
- el cursor representa internamente la frontera de secuencia para cargar anteriores;
- los elementos se devuelven en orden cronológico para presentarlos;
- un tercero recibe 404, igual que para una conversación inexistente.

### Marcar lectura

`PUT /api/chat/conversaciones/{conversacionId}/lectura`

```json
{
  "hastaSecuencia": 123
}
```

- el cursor solo avanza y la repetición es idempotente;
- no puede superar la última secuencia de la conversación;
- los no leídos son mensajes posteriores enviados por el otro participante;
- responde `204 No Content` cuando se acepta.

## Autorización y errores

- 401 para autenticación ausente o inválida;
- 400 para IDs vacíos, texto inválido, cursor mal formado o límite fuera de rango;
- 404 para aviso no contactable, conversación inexistente o usuario no participante;
- 409 para autochat, clave idempotente incompatible o concurrencia no recuperable;
- 429 para rate limit.

No se usa 403 para recursos de Chat identificados por ID, evitando confirmar su
existencia a terceros. Los errores usan `ProblemDetails`, códigos estables y mensajes
genéricos sin datos sensibles.

## Lectura y no leídos

No se guarda un booleano mutable por mensaje. Cada participante tiene en la
conversación un cursor monotónico de última secuencia leída. El conteo considera solo
mensajes posteriores enviados por la contraparte. Los recibos detallados de lectura
quedan fuera de 3A.

## Rate limiting

Se aplican políticas particionadas por usuario autenticado:

- iniciar conversación: 10 solicitudes por hora;
- enviar mensajes: 20 solicitudes por minuto, admitiendo una ráfaga pequeña;
- consultas y lectura: una política general más amplia para evitar abuso accidental.

En 3A se usa el rate limiter local de ASP.NET Core. La coordinación distribuida queda
diferida hasta que exista despliegue con múltiples instancias.

## Retención

Conversaciones y mensajes se conservan indefinidamente durante el MVP. No existe
borrado físico ni endpoint de borrado. Cualquier política de retención, exportación o
anonimización requiere una decisión legal y de producto posterior.

## Eventos

El dominio expresa:

- `ConversacionIniciada`;
- `MensajeEnviado`;
- `LecturaAvanzada`.

Se agrega el contrato mínimo `ChatMessageSent` en
`BuildingBlocks.Contracts.Chat`, con `EventId`, `OcurridoEn`, conversación, mensaje,
remitente y destinatario. Nunca incluye texto, preview, título del aviso ni perfil.

3A no promete publicación externa: el sistema actual carece de outbox y despacho
post-commit confiable. La entrega y el consumo se habilitarán con Notifications en el
bloque correspondiente. Los logs de integración solo pueden contener tipo y EventId.

## Seguridad y anti-PII

- Nunca registrar texto de mensajes, IDs de participantes, claves de idempotencia,
  tokens ni cuerpos HTTP.
- Nunca incluir esos valores en errores, excepciones, métricas o eventos de
  integración destinados a logging.
- El pipeline puede registrar únicamente el nombre del tipo de request.
- Las pruebas de arquitectura amplían la redacción para campos como `texto`,
  `mensaje`, `participante`, `remitente`, `destinatario` y
  `claveIdempotencia`.
- Los DTO solo exponen contenido a participantes autorizados.

## Trabajo diferido

- 3B: SignalR, entrega en tiempo real, reconexión y actualización en vivo.
- 3C: bloqueo, reportes, moderación, cierre operativo y controles de abuso.
- 3D: consumo de `ChatMessageSent`, notificaciones y garantías de entrega; outbox si
  se adopta.
- Fuera de Fase 3: adjuntos, búsqueda, edición, eliminación, presencia, indicador de
  escritura y retención automática.

## Estrategia de pruebas

- Unitarias de dominio para creación, participantes, texto, inmutabilidad y lectura.
- Unitarias de handlers y validadores para avisos no contactables, idempotencia,
  paginación y errores.
- Arquitectura para aislamiento entre contextos, schema y anti-PII.
- Integración con SQL Server para índices únicos, secuencia, concurrencia y cursores.
- Integración HTTP para autenticación, no enumeración, flujos de ambos participantes,
  reintentos y rate limiting.
- Verificación final de build, suite completa, formato y artefacto OpenAPI.

## Criterios de aceptación

- Un comprador autenticado inicia una sola conversación para un aviso público ajeno.
- Reintentos y carreras no crean conversaciones ni mensajes duplicados.
- Solo comprador y vendedor pueden listar, leer, enviar y marcar lectura.
- Los mensajes son inmutables, conservan orden estable y se paginan sin desplazamiento.
- Los cambios posteriores del aviso no interrumpen la conversación.
- Chat no depende de Catalog ni Identity y no contiene FK cruzadas.
- La API usa errores consistentes sin filtrar existencia ni datos sensibles.
- Ningún log o evento expone texto o identificadores sensibles.
- Migración, pruebas unitarias, de arquitectura e integración cubren los flujos
  críticos y la suite completa queda verde.
