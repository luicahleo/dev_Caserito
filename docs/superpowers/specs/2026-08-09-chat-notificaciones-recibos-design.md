# Chat — notificaciones inmediatas, Web Push y recibos

Fecha: 2026-08-09  
Estado: aprobado

## Objetivo

Hacer que compradores y vendedores perciban inmediatamente los mensajes nuevos,
dentro y fuera de CaseritoApp, y que el remitente conozca si cada mensaje fue
enviado, entregado o leído. La experiencia sigue el patrón de confianza e
inmediatez de un marketplace como Wallapop, sin copiar su identidad visual.

HTTP continúa siendo la fuente de verdad. SignalR reduce la latencia cuando la
aplicación está activa y Web Push avisa cuando está en segundo plano o cerrada.

## No objetivos

- correo, SMS o notificaciones de marketing;
- indicador de escritura o presencia en línea;
- adjuntos, edición o eliminación de mensajes;
- mostrar nombres, perfiles o identificadores técnicos de la contraparte;
- garantizar entrega física del sistema operativo cuando el proveedor push no la
  confirma;
- sustituir los flujos actuales de seguridad, bloqueo y moderación.

## Diagnóstico y punto de partida

El diagnóstico `diagnostico-SES-E2DBA018C341.json` muestra respuestas correctas al
listar conversaciones y mensajes y al marcar lectura. No evidencia un fallo HTTP.
La carencia está en la propagación y presentación del estado:

- el contador global consulta como máximo 50 conversaciones cada 30 segundos;
- el acceso directo con badge solo aparece a partir del breakpoint `lg`;
- la opción móvil `Mensajes`, dentro de `Mi cuenta`, no muestra contador;
- SignalR solo suscribe la conversación abierta y no avisa globalmente;
- Chat persiste cursores de lectura, pero no los proyecta al remitente;
- `MensajeDto` no incluye un estado de entrega o lectura.

## Estados y semántica

Cada mensaje propio muestra un estado monotónico:

- **Enviado**: el mensaje quedó confirmado y persistido por Chat. Se representa
  con un check gris.
- **Entregado**: al menos un cliente de la contraparte confirmó que recibió el
  mensaje, bien con la aplicación activa o desde el service worker. Se representa
  con dos checks grises.
- **Leído**: la contraparte avanzó su cursor de lectura hasta la secuencia del
  mensaje. Se representa con dos checks azules.

`Leído` implica `Entregado`. Un estado nunca retrocede. Los iconos incluyen un
nombre accesible y una ayuda visible al tocar, enfocar o mantener el puntero.

La aceptación del mensaje por el proveedor Web Push no se considera entrega por sí
sola. El service worker debe confirmar que procesó el push. Si el navegador o el
sistema operativo no ejecutan esa confirmación, el mensaje puede permanecer como
`Enviado` aunque se haya mostrado; esta limitación se comunica mediante la semántica
técnica, no con un error alarmante al usuario.

## Persistencia y dominio de Chat

`Conversacion` conserva, además de los cursores de lectura actuales, un cursor de
última secuencia entregada por participante. La transición de entrega:

- solo puede realizarla el participante destinatario;
- no puede superar `UltimaSecuencia`;
- es monotónica e idempotente;
- genera un evento de dominio sin texto ni identificadores de usuario.

La lectura existente mantiene sus garantías y también implica avanzar el cursor de
entrega hasta la misma secuencia. Se añade una migración del contexto Chat con
valores iniciales cero; no hay FK entre bounded contexts.

Las consultas de conversación e historial proyectan exclusivamente el cursor de la
contraparte necesario para calcular el estado de los mensajes propios. Nunca
exponen cursores de terceros ajenos ni cambian la autorización existente.

## Contratos HTTP

Se incorpora una operación autenticada e idempotente para confirmar entrega hasta
una secuencia concreta:

```http
PUT /api/chat/conversaciones/{id}/entrega
{ "hastaSecuencia": 123 }
```

Conserva la misma política de ocultación que lectura: conversación inexistente y
tercero producen una respuesta genérica equivalente. Secuencias negativas o
superiores a la existente producen `400` genérico.

Los DTO de conversación incluyen `ultimaSecuenciaEntregadaContraparte` y
`ultimaSecuenciaLeidaContraparte`. El frontend deriva el estado comparando esos
cursores con la secuencia de cada mensaje propio; no se persiste un estado duplicado
por mensaje.

Para Web Push se añaden endpoints autenticados para:

- obtener la clave pública VAPID;
- registrar o actualizar una suscripción del dispositivo;
- revocar la suscripción del dispositivo.

La confirmación desde el service worker utiliza un comprobante opaco, de un solo
propósito y vida limitada, vinculado a conversación, destinatario y secuencia. No
usa ni persiste el token de acceso del usuario. El endpoint no revela información
ante comprobantes inválidos y aplica límites de frecuencia.

OpenAPI y `web/src/api/schema.d.ts` se regeneran a partir del contrato.

## Tiempo real global

Al autenticarse, cada conexión entra en un grupo interno por usuario administrado
exclusivamente por el servidor. El cliente no proporciona nombres de grupo ni IDs de
usuario. Se mantienen las suscripciones por conversación para recibir texto solo
cuando la pantalla está abierta.

El canal global emite eventos mínimos sin texto:

- `ContadorChatActualizado`, que ordena invalidar el conteo autoritativo;
- `EstadoMensajesActualizado`, con conversación y cursores autorizados para que el
  remitente refresque o avance los checks.

El mensaje completo sigue limitado al grupo de la conversación abierta. Tras una
reconexión, el cliente recupera contadores y cursores por HTTP. Los eventos pueden
duplicarse o desordenarse; el cliente invalida por claves de TanStack Query y los
cursores monotónicos preservan el resultado.

## Contador y badges

El conteo global deja de aproximarse sumando la primera página. Chat expone un
conteo autoritativo de mensajes no leídos para el participante autenticado.

La navegación muestra:

- badge rojo sobre el acceso `Mensajes` en escritorio;
- badge rojo sobre `Mi cuenta` en móvil y tableta cuando existen no leídos;
- el mismo conteo junto a `Mensajes` dentro del menú de cuenta;
- etiqueta accesible con la cantidad, limitada visualmente a `99+`.

SignalR provoca una actualización inmediata. HTTP se consulta al montar, al volver
el documento a primer plano, tras reconectar y con sondeo espaciado como respaldo.
Marcar lectura actualiza la caché de forma optimista y luego reconcilia con el
servidor.

## Web Push y permiso

La aplicación presenta una invitación propia después de iniciar una conversación o
recibir el primer mensaje. Solo el botón `Activar notificaciones` invoca el permiso
del navegador. No se solicita al cargar la aplicación.

Estados contemplados:

- navegador incompatible: no se ofrece la activación;
- permiso pendiente: invitación descartable y reabrible desde preferencias;
- permiso concedido: se registra la suscripción y se informa de forma breve;
- permiso denegado: se explica cómo cambiarlo sin insistencia automática;
- suscripción expirada: se renueva o elimina de manera segura.

El push contiene texto genérico (`Tienes un nuevo mensaje`) y datos mínimos para
abrir la ruta autorizada. Nunca contiene el texto del chat, nombres, correo, IDs
visibles ni información del aviso. El service worker:

1. recibe y valida la forma del push;
2. confirma entrega usando el comprobante opaco;
3. evita mostrar la notificación si una ventana visible ya tiene abierta esa
   conversación;
4. muestra una notificación agrupada por conversación en otro caso;
5. al pulsarla enfoca una ventana existente o abre la conversación.

El servidor elimina suscripciones cuando el proveedor responde que ya no existen.
Los fallos de push no revierten el envío durable del mensaje.

## Notifications y límites entre contextos

Chat publica un contrato de integración mínimo después del commit, sin texto, para
solicitar el aviso al destinatario. Notifications consume el contrato, administra
suscripciones Web Push y despacha el aviso. No referencia Domain o Infrastructure de
Chat y no crea FK cruzadas.

La intención de push es durable y reintentable. La entrega es al menos una vez; el
service worker y los comprobantes toleran duplicados. Los reintentos usan backoff y
no registran endpoints, claves, tokens, IDs ni payloads.

## UI de conversación

Los checks aparecen únicamente bajo mensajes propios, junto a la hora, con contraste
suficiente y sin depender solo del color. Los mensajes ajenos no muestran recibos.
Al recibir mensajes con la conversación visible, el cliente confirma entrega; tras
renderizar la secuencia más alta visible, conserva el marcado de lectura actual.

Las actualizaciones de entrega y lectura refrescan los checks sin recargar la página.
En desconexión se mantiene el último estado confirmado y no se promete tiempo real.

## Errores, compatibilidad y privacidad

- Todos los errores visibles son breves, genéricos y permiten reintento cuando
  corresponde.
- Sin Web Push, el chat y los badges dentro de la aplicación siguen funcionando.
- Sin SignalR, HTTP y el sondeo recuperan el estado.
- Tokens VAPID privados y secretos se configuran fuera del repositorio.
- Endpoints de suscripción, comprobantes, contenido push y texto del chat no se
  registran.
- No se usan como etiquetas métricas usuarios, conversaciones, mensajes, endpoints
  push ni claves.
- La telemetría se limita a conteos, latencias y categorías técnicas de baja
  cardinalidad.

## Pruebas

### Backend

- dominio: entrega monotónica, idempotencia, autorización, límites e implicación de
  entrega al leer;
- persistencia: migración, cursores y conteo autoritativo;
- aplicación/HTTP: confirmación, ocultación a terceros y DTO autorizados;
- integración: mensaje → intención push, reintentos, suscripción expirada y
  comprobante válido/inválido/repetido;
- SignalR: grupo global autenticado, eventos mínimos y recuperación;
- arquitectura/PII: contratos entre contextos, secretos y plantillas de log.

### Frontend

- badge responsive en escritorio, cuenta móvil y opción de menú;
- actualización por evento, foco, reconexión y respaldo HTTP;
- estados `Enviado`, `Entregado` y `Leído`, monotónicos y accesibles;
- invitación contextual y estados de permiso;
- registro, renovación y revocación de suscripciones;
- service worker: deduplicación, supresión con conversación visible, confirmación y
  navegación al pulsar;
- errores genéricos, navegador incompatible y responsive desde 320 px.

## Criterios de aceptación

1. Un mensaje nuevo actualiza el badge del destinatario sin esperar al sondeo cuando
   su aplicación está conectada.
2. El conteo aparece tanto en el acceso de escritorio como en `Mi cuenta` y en la
   opción móvil `Mensajes`.
3. Un usuario que aceptó Web Push recibe un aviso genérico con la aplicación en
   segundo plano o cerrada y puede abrir la conversación desde él.
4. No se duplica la notificación del sistema cuando la conversación está visible.
5. Cada mensaje propio muestra `Enviado`, `Entregado` o `Leído` con checks y texto
   accesible; los estados nunca retroceden.
6. Entrega y lectura se propagan en tiempo real y se recuperan por HTTP tras una
   desconexión.
7. El permiso del navegador solo se solicita después de una acción explícita en la
   invitación contextual.
8. La falta de soporte, denegación o fallo de Web Push no bloquea el chat.
9. Ningún log, error, métrica o push contiene texto del chat, participantes, tokens,
   endpoints de suscripción ni identificadores sensibles.
10. Migraciones, OpenAPI, cliente generado, pruebas dirigidas y puertas backend/web
    quedan verificadas antes de publicar `develop`.
