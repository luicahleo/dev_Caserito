# Diseño — Verificación al contactar y conversación retenida (backend)

Fecha: 2026-09-21
Bloque: 2 de 4 de la serie «verificación al contactar».

## 1. Contexto y objetivo

La serie mueve la exigencia de verificación de identidad desde el registro hasta
el momento del contacto: el vendedor sigue necesitando KYC para publicar, el
comprador puede explorar sin verificarse, y solo se le pide identidad cuando
quiere escribirle a un vendedor. El bloque 1
(`2026-09-19-kyc-resolucion-automatica-design.md`) hizo que esa verificación se
resuelva sola en segundos cuando la evidencia es clara.

Hoy el chat no exige nada: cualquier cuenta con correo confirmado puede escribir
a cualquier vendedor. Eso deja la moderación sin dientes, porque una cuenta
quemada se recrea con otro correo en segundos.

**Objetivo**: que el mensaje de un comprador solo llegue al vendedor si el
comprador tiene identidad verificada, sin bloquearle el teclado mientras se
resuelve su verificación y sin perder lo que escribió.

### Serie completa (contexto, no alcance)

1. ✅ Resolución automática de KYC.
2. **Este bloque**: gate al contactar, conversación retenida y liberación.
3. Aviso al administrador de solicitudes en espera.
4. Distintivo de verificado en listado, detalle y chat.

## 2. No objetivos

- **La UI web no entra aquí.** Tiene su propio spec, siguiendo el precedente del
  proyecto (B1/B2 de auth, 2A/2E de catálogo).
- No se toca el gate de publicar aviso (`CrearAvisoCommand`) ni el de
  `SolicitarOrdenCommand`.
- No se añade verificación por teléfono ni ningún otro factor.
- No se purgan las conversaciones retenidas de usuarios que nunca se verifican.
- No se implementa outbox ni se hace transaccional el guardado multi-contexto.
- No se modifica la mecánica de cursores de entrega y lectura.

## 3. Decisiones

1. **El gate no rechaza: retiene.** Un comprador sin identidad habilitada puede
   iniciar conversación y escribir; su conversación nace en un estado nuevo y no
   llega al vendedor hasta que se verifique. Se descartó distinguir «tiene
   solicitud en curso» de «no tiene ninguna»: obligaba a ampliar el puerto hacia
   Identity y empeoraba la experiencia sin aportar seguridad, porque en ambos
   casos el mensaje queda igualmente retenido.
2. **La autoridad es el estado real, no el claim.** El gate consulta
   `IConsultaVerificacionKyc.EstaHabilitadoParaMarketplaceAsync` a través de un
   puerto, no `ClaimsApp.IdentidadHabilitada` del token. Es lo que permite que
   la aprobación instantánea del bloque 1 surta efecto sin esperar a que el JWT
   rote: `EmisorSesion.cs:32` solo refresca el claim al emitir un token nuevo, y
   frenar por eso al comprador recién aprobado anularía el propósito de la serie.
3. **Liberación por evento con red de seguridad perezosa.** Un handler de Chat
   escucha `UserVerified` y libera; además, el acceso del comprador a su
   conversación comprueba el estado real y libera si el evento se perdió. El
   despacho es in-process por MediatR (`Program.cs:73-74`) y sin outbox: sin la
   red, un fallo dejaría la conversación retenida para siempre.
4. **Un rechazo de KYC no altera nada.** La conversación sigue retenida con su
   mensaje. El dominio de KYC permite reintentar, y si el segundo intento
   aprueba, se libera sola sin que el comprador reescriba.
5. **El valor nuevo del enum va al final.** `Estado` no está configurado en
   `ConfiguracionChat` y EF lo mapea por convención como `int`. Insertar el valor
   en medio cambiaría el significado de las filas existentes.

## 4. Comportamiento

### 4.1 Iniciar conversación

`IniciarConversacionCommand` consulta si el comprador está habilitado:

| Comprador | Conversación resultante |
|---|---|
| Habilitado | `Activa`, como hoy |
| No habilitado | `RetenidaPorVerificacion` |

El resto de reglas actuales no cambian: conversación existente se reutiliza,
bloqueo entre usuarios sigue impidiendo el contacto, aviso inexistente sigue
devolviendo `AvisoNoContactable`.

### 4.2 Enviar mensaje

La regla de `Conversacion.cs:156` (`Estado != Activa` ⇒ `NoDisponibleParaEnvio`)
pasa a admitir también `RetenidaPorVerificacion`, **con una restricción**: en una
conversación retenida solo puede escribir el comprador. El vendedor no la ve, de
modo que un envío suyo solo puede venir de un identificador filtrado o de un
error, y se rechaza con `NoDisponibleParaEnvio`.

Los mensajes se guardan con normalidad, con su secuencia y sus cursores. No hay
estado por mensaje: la retención vive en la conversación.

### 4.3 Invisibilidad para el vendedor

Mientras una conversación esté retenida, el vendedor no puede enterarse de su
existencia por **ninguna** vía:

- no aparece en su listado de conversaciones;
- no suma al contador de mensajes no leídos;
- no genera evento de SignalR hacia él;
- no genera notificación push.

Un contador que se mueve ya es información filtrada; por eso la regla se enuncia
sobre el conocimiento, no sobre la pantalla.

El comprador, en cambio, ve su conversación con normalidad y puede escribir.

**Punto único de corte para tiempo real y push.** El endpoint de envío publica
`ChatMessageSent` cuando el mensaje se crea, y de ese evento cuelgan tanto
SignalR como la notificación push. Basta **no publicarlo** mientras la
conversación esté retenida para cortar ambas vías a la vez, en lugar de filtrar
en cada suscriptor. Para saberlo sin una consulta extra, el resultado de
`EnviarMensajeCommand` incorpora si la conversación está retenida.

### 4.4 Liberación

Al aprobarse el KYC del comprador:

1. Identity publica `UserVerified` (ya lo hace, tanto en la aprobación manual
   como en la automática del bloque 1).
2. Un handler en `Chat.Application` toma todas las conversaciones
   `RetenidaPorVerificacion` de ese comprador y las pasa a `Activa`.
3. El vendedor pasa a verlas, con todos los mensajes acumulados.

**Una sola notificación por conversación liberada**, no una por mensaje: si el
comprador escribió cinco mensajes esperando, cinco push seguidos serían una
ráfaga injustificada. El mecanismo es publicar un único `ChatMessageSent`
referido al **último** mensaje de la conversación liberada, reutilizando el
camino de notificación ya probado en lugar de inventar uno.

Red de seguridad: la liberación perezosa se aplica en
`PuedeAccederConversacionQuery`, que es por donde pasa el acceso del comprador a
la conversación. Si el estado real ya es habilitado, se libera ahí mismo.

`LiberarPorVerificacion` es idempotente sobre una conversación ya `Activa` y
falla sobre una cerrada: una conversación cerrada por moderación no debe
reabrirse por un efecto secundario del KYC.

## 5. Componentes

### Chat.Domain

- `EstadoConversacion`: añadir `RetenidaPorVerificacion` **al final**.
- `Conversacion.Crear`: acepta si la conversación nace retenida.
- `Conversacion.LiberarPorVerificacion(DateTimeOffset)`: transición a `Activa`,
  idempotente sobre `Activa`, error sobre `Cerrada` y `CerradaPorModeracion`.
- Evento de dominio de liberación, sin texto ni participantes.
- Regla de envío ampliada según §4.2.

### Chat.Application

- Puerto `IConsultaVerificacionComprador` con
  `Task<bool> EstaHabilitadoAsync(Guid usuarioId, CancellationToken ct)`. Chat no
  puede referenciar Identity.
- `IniciarConversacionCommand`: consulta el puerto y decide el estado inicial.
- `EnviarMensajeCommand`: sin cambios de firma; la regla vive en el dominio.
- Handler `INotificationHandler<UserVerified>` que libera las conversaciones
  retenidas del comprador.
- Liberación perezosa en el acceso del comprador a la conversación.
- Consultas que deben excluir lo retenido para el vendedor: listado, contador de
  no leídos y `PuedeRecibirTiempoRealQuery`.

### Chat.Infrastructure

- Consulta de conversaciones retenidas por comprador, para el handler.
- Sin migración: el enum es `int` por convención y el valor se añade al final.

### Host

- Adaptador en `Host/Chat/`, gemelo de
  `Host/Orders/ConsultaVerificacionParticipanteAdapter.cs`, que traduce el puerto
  de Chat a `IConsultaVerificacionKyc.EstaHabilitadoParaMarketplaceAsync`.
- `ChatEndpoints.EnviarAsync`: no publicar `ChatMessageSent` si el resultado
  indica conversación retenida. `PublicadorSignalRMensajes` no se toca: al no
  existir el evento, no hay nada que emitir.
- Extraer el helper `TieneIdentidadHabilitada`, hoy duplicado en
  `AvisosEndpoints.cs:263` y `OrdersEndpoints.cs:286`, a un único sitio
  compartido del Host. Es una mejora dirigida: este bloque añadiría la tercera
  copia si no se hace.

## 6. Contrato HTTP

Sin endpoints nuevos ni cambios de firma. `ConversacionDto` ya expone `Estado` y
`puedeEnviar`: el estado nuevo se serializa como un valor más y `puedeEnviar`
permanece en `true` para el comprador de una conversación retenida. La UI del
bloque siguiente se apoya en esos dos campos.

El contrato OpenAPI y el cliente TypeScript se regeneran porque cambia el
conjunto de valores posibles de `Estado`.

## 7. Seguridad y PII

- El handler de liberación registra únicamente ids técnicos y el número de
  conversaciones liberadas. Nunca texto de mensajes, ni el aviso, ni el vendedor.
- Se mantiene la política vigente del contexto Chat: ningún log, error ni push
  incluye texto de mensajes ni identidades.
- La invisibilidad de §4.3 es un requisito de seguridad, no de presentación: el
  vendedor no debe poder inferir la existencia de la conversación retenida por
  ningún canal observable.
- El gate consulta el estado real en cada intento de contacto; una consulta de
  solo lectura por intento, acotada por el rate limit `chat-iniciar` y
  `chat-enviar` ya existentes.

## 8. Diferidos y riesgos

- **UI web**: spec propio, bloque siguiente.
- **Purga**: las conversaciones retenidas de usuarios que nunca se verifican se
  acumulan sin límite. Son invisibles para el vendedor y están acotadas por rate
  limit, pero conviene un proceso de retención en su momento.
- **Guardado multi-contexto no transaccional**: si el handler de liberación falla
  tras persistirse la aprobación de KYC, la conversación queda retenida hasta que
  el comprador vuelva. Para eso existe la red de seguridad perezosa; la solución
  de fondo es un outbox, fuera de alcance.
- **Conversaciones anteriores al bloque**: todas las existentes están `Activa` y
  no se ven afectadas. No hay migración de datos.

## 9. Pruebas

### Dominio

- una conversación puede nacer retenida;
- `LiberarPorVerificacion` la pasa a `Activa` y emite su evento;
- liberar dos veces es idempotente;
- liberar una conversación cerrada o cerrada por moderación falla;
- el comprador puede enviar mensajes estando retenida;
- el vendedor **no** puede enviar mensajes estando retenida.

### Application

- comprador no habilitado ⇒ conversación retenida; habilitado ⇒ activa;
- el handler de `UserVerified` libera todas las retenidas de ese comprador y
  ninguna de otro;
- el acceso del comprador libera cuando el estado real ya es habilitado;
- un `UserVerified` de un usuario sin conversaciones retenidas no falla.

### Consultas (una por vía de filtración)

- el listado del vendedor no incluye conversaciones retenidas;
- el contador de no leídos del vendedor no las cuenta;
- `PuedeRecibirTiempoRealQuery` las excluye para el vendedor;
- el listado del comprador **sí** las incluye.

### Integración

- flujo completo: comprador sin verificar inicia conversación y escribe; el
  vendedor no ve nada en listado ni contador; se aprueba el KYC; el vendedor ve
  la conversación con los mensajes dentro.

## 10. Criterios de aceptación

1. Un comprador sin identidad habilitada que contacta obtiene una conversación
   retenida y puede escribir en ella.
2. El vendedor no percibe esa conversación por ninguna vía: listado, contador,
   tiempo real ni push.
3. Al aprobarse el KYC del comprador, la conversación pasa a `Activa` y el
   vendedor la ve con todos los mensajes acumulados.
4. Si el evento de liberación se pierde, el acceso del comprador la libera.
5. Un rechazo de KYC deja la conversación retenida intacta.
6. El vendedor no puede escribir en una conversación retenida.
7. La liberación produce una sola notificación por conversación, no una por
   mensaje.
8. Ningún log incluye texto de mensajes.
9. El contrato OpenAPI y el cliente TypeScript quedan regenerados sin deriva.
10. `./verify.ps1 -Changed` en verde.
