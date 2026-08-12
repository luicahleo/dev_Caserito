# Plan TDD — notificaciones y recibos de chat

Spec: `docs/superpowers/specs/2026-08-09-chat-notificaciones-recibos-design.md`

## Estrategia

Implementar por dependencias: primero cursores autoritativos de Chat, después
contratos HTTP/SignalR, luego persistencia y despacho Web Push y finalmente la UI y
el service worker. Cada tarea empieza con una prueba dirigida roja, produce un
commit pequeño y ejecuta `./verify.ps1 -Changed` antes del commit.

## Tarea 1 — Modelar entrega monotónica en Chat

### Entradas

- `Conversacion` ya conserva cursores de lectura por participante.
- El spec define que leer implica entregar y que ningún cursor retrocede.

### Prueba roja

Ampliar `CaseritoApp/tests/CaseritoApp.UnitTests/Chat/ConversacionTests.cs` con:

- destinatario avanza entrega hasta una secuencia existente;
- repetición y retroceso son idempotentes;
- tercero, secuencia negativa y secuencia futura fallan;
- marcar lectura avanza también entrega;
- remitente no puede marcar como entregados sus propios mensajes más allá de lo
  recibido por la contraparte.

Comando:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~ConversacionTests
```

Resultado rojo esperado: faltan cursores y `MarcarEntrega`.

### Implementación

- Añadir `UltimaSecuenciaEntregadaComprador` y
  `UltimaSecuenciaEntregadaVendedor` a
  `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/Conversacion.cs`.
- Añadir `EntregaAvanzada` a `EventosConversacion.cs`, sin texto ni usuario.
- Implementar `MarcarEntrega(usuarioId, hastaSecuencia, ocurrioEn)` monotónico.
- Hacer que `MarcarLectura` avance entrega antes de registrar el avance de lectura.

### Salidas y verificación

La prueba dirigida queda verde y los tests vecinos de Chat Domain pasan.

Commit previsto: `feat(chat): modela entrega de mensajes`

## Tarea 2 — Persistir cursores y proyectar estado autoritativo

### Prueba roja

Ampliar:

- `CaseritoApp/tests/CaseritoApp.IntegrationTests/ChatPersistenciaTests.cs` para
  roundtrip y concurrencia de entrega;
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/ChatFlujoTests.cs` para cursores
  de contraparte y conteo exacto;
- `CaseritoApp/tests/CaseritoApp.UnitTests/Chat/ConsultasChatHandlerTests.cs` para la
  nueva consulta de no leídos.

Comandos dirigidos con filtros `ChatPersistenciaTests`, `ChatFlujoTests` y
`ConsultasChatHandlerTests`. El fallo esperado es la ausencia de columnas, DTO y
consulta.

### Implementación

- Configurar ambos cursores en
  `CaseritoApp.Chat.Infrastructure/Conversaciones/ConfiguracionChat.cs`.
- Crear migración `ChatRecibosEntrega` y actualizar snapshot mediante `dotnet ef`.
- Ampliar `ConversacionDto` y `ConversacionResumenDto` con
  `UltimaSecuenciaEntregadaContraparte` y `UltimaSecuenciaLeidaContraparte`.
- Proyectar los cursores relativos en `ConsultaConversacionesEfCore`.
- Añadir `ContarNoLeidosAsync(usuarioId)` a `IConsultaConversaciones` y su
  implementación SQL, contando mensajes ajenos posteriores al cursor de lectura en
  todas las conversaciones.
- Añadir `ContarMensajesNoLeidosQuery` y handler en Chat Application.

### Salidas y verificación

Migración reproducible, consultas autorizadas y pruebas dirigidas verdes.

Commit previsto: `feat(chat): persiste recibos y conteo no leído`

## Tarea 3 — Exponer entrega, lectura y contador por HTTP

### Prueba roja

Ampliar:

- `CaseritoApp/tests/CaseritoApp.UnitTests/Chat/MarcarLecturaCommandHandlerTests.cs`;
- crear `MarcarEntregaCommandHandlerTests.cs`;
- ampliar `CaseritoApp/tests/CaseritoApp.IntegrationTests/ChatFlujoTests.cs`;
- ampliar los tests de forma de endpoints en ArchitectureTests.

Casos: participante, tercero/inexistente indistinguibles, validación de secuencia,
idempotencia y `GET /api/chat/no-leidos`.

### Implementación

- Crear `MarcarEntregaCommand`, handler y validator en Chat Application.
- Añadir `MarcarEntregaRequest` y
  `PUT /api/chat/conversaciones/{id}/entrega` en `ChatEndpoints.cs`.
- Añadir `GET /api/chat/no-leidos` con respuesta `{ cantidad }`.
- Reutilizar las respuestas genéricas, reintentos de concurrencia y rate limiting
  existentes.

### Salidas y verificación

Contrato HTTP completo y pruebas dirigidas verdes.

Commit previsto: `feat(chat): expone entrega y contador no leído`

## Tarea 4 — Propagar contadores y recibos por SignalR

### Prueba roja

Ampliar:

- `CaseritoApp/tests/CaseritoApp.ArchitectureTests/Chat/ChatTiempoRealArchitectureTests.cs`;
- tests unitarios de publicadores/handlers de Chat;
- tests de integración SignalR existentes.

Casos: conexión autenticada entra en grupo interno de usuario; mensaje nuevo avisa
al destinatario sin texto; entrega/lectura avisan al remitente con cursores; una
reconexión vuelve al grupo; payloads no contienen texto ni IDs de usuario.

### Implementación

- Añadir `GruposChat.ParaUsuario(Guid)` solo para uso servidor.
- Sobrescribir `ChatHub.OnConnectedAsync` para registrar el grupo autenticado.
- Crear contratos de cliente `ContadorChatActualizado` y
  `EstadoMensajesActualizado` con conversación, entrega y lectura, sin texto.
- Publicar eventos después del commit mediante handlers de integración/dominio y
  abstracciones en Host, manteniendo SignalR fuera de Domain/Application.
- Conservar `MensajeCreado` exclusivamente en el grupo de conversación.

### Salidas y verificación

Eventos mínimos, autorizados, tolerantes a duplicados y tests verdes.

Commit previsto: `feat(chat): actualiza recibos y contador en tiempo real`

## Tarea 5 — Persistir suscripciones e intenciones Web Push

### Prueba roja

Crear pruebas de Notifications para:

- suscripción válida, actualización idempotente y revocación por propietario;
- rechazo de claves o endpoints inválidos sin reflejarlos;
- intención durable única por evento de mensaje;
- lease, reintento y eliminación de suscripción expirada;
- comprobante de entrega válido, vencido, manipulado y repetido;
- ausencia de secretos, endpoints, payloads e IDs en logs.

### Implementación

- Añadir agregados `SuscripcionPush` e `IntencionPush` en Notifications Domain.
- Persistirlos en `NotificationsDbContext` sin FK a Chat y crear migración
  `NotificationsWebPush`.
- Definir puertos `IWebPushSender`, `IRepositorioSuscripcionesPush`,
  `IAlmacenIntencionesPush` y `IProtectorComprobantesEntrega`.
- Implementar comprobantes protegidos con ASP.NET Core Data Protection, propósito
  versionado y caducidad limitada.
- Incorporar una librería .NET Web Push solo si no existe una implementación segura
  en el framework; centralizar su versión en `Directory.Packages.props`.
- Implementar worker con lease, backoff y categorías de error genéricas.
- Cambiar `NotificarNuevoMensajeHandler` para conservar la notificación interna y el
  correo existente, pero usar texto genérico sin ID técnico, y crear la intención
  push después del evento confirmado.

### Salidas y verificación

Web Push durable, secrets por configuración y pruebas unitarias/persistencia verdes.

Commit previsto: `feat(notificaciones): persiste y despacha web push de chat`

## Tarea 6 — Exponer configuración, suscripciones y confirmación push

### Prueba roja

Ampliar tests de `NotificationsEndpoints` e integración con:

- clave pública disponible sin clave privada;
- alta/actualización/baja autenticadas por dispositivo;
- usuario ajeno no puede revocar;
- confirmación con comprobante válido avanza entrega;
- comprobante inválido devuelve respuesta genérica y no filtra causa;
- rate limiting y OpenAPI.

### Implementación

- Añadir DTO estrictos para endpoint, claves `p256dh` y `auth`.
- Incorporar endpoints bajo `/api/notificaciones/push` para configuración,
  suscripción y revocación.
- Añadir endpoint público limitado de confirmación que solo acepta el comprobante
  opaco y envía `MarcarEntregaCommand` mediante un adaptador Host permitido.
- Validar longitudes, HTTPS en producción y propiedad autenticada.
- Configurar VAPID por secretos/variables; producción falla al arrancar si Web Push
  está habilitado sin configuración válida.

### Salidas y verificación

API segura y tests dirigidos verdes.

Commit previsto: `feat(notificaciones): expone suscripciones web push`

## Tarea 7 — Regenerar OpenAPI y adaptar el cliente web

### Prueba roja

Ampliar `web/src/api/chat.test.ts` y crear/expandir tests de
`web/src/api/notificaciones.ts` para cursores, conteo, entrega y suscripciones.

### Implementación

- Regenerar `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` por el mecanismo
  existente.
- Ejecutar `npm run generate:api`; no editar `schema.d.ts` manualmente.
- Adaptar `web/src/api/chat.ts` y `web/src/api/notificaciones.ts`.
- Convertir enteros potencialmente `number | string` únicamente en el límite HTTP.

### Salidas y verificación

Tests API, `npm run typecheck` y comprobación de artefactos verdes.

Commit previsto: `feat(web): adapta contratos de recibos y push`

## Tarea 8 — Crear cliente global y badges responsive

### Prueba roja

Ampliar `web/src/app/AppLayout.test.tsx` y tests de chat con:

- badge de escritorio;
- badge sobre `Mi cuenta` por debajo de `lg`;
- badge en el elemento `Mensajes` del menú;
- invalidación inmediata por `ContadorChatActualizado`;
- refresco al recuperar foco/reconectar y sondeo de respaldo;
- cero no renderiza una burbuja vacía.

### Implementación

- Crear un hook/controlador global en `web/src/chat/` que mantenga una conexión
  SignalR autenticada mientras exista sesión.
- Consultar `GET /api/chat/no-leidos` con TanStack Query.
- Sustituir la suma de 50 conversaciones en `ContadorChat.tsx`.
- Extraer un componente pequeño reutilizable de badge si lo requieren los tres
  consumidores y conectarlo en `AppLayout.tsx`.
- Invalidar/refrescar sin copiar datos remotos a un store global.

### Salidas y verificación

Badge inmediato y accesible desde 320 px, tests dirigidos verdes.

Commit previsto: `feat(web): muestra mensajes no leídos en toda la navegación`

## Tarea 9 — Mostrar checks y confirmar entrega/lectura

### Prueba roja

Ampliar `web/src/routes/ConversacionPage.test.tsx` y tests de tiempo real para:

- check enviado, doble check entregado y doble check leído;
- solo mensajes propios;
- texto accesible, contraste y estado no dependiente solo del color;
- evento de recibo avanza estado sin recarga;
- cursores desordenados no retroceden;
- mensaje recibido con pantalla abierta confirma entrega y lectura;
- fallo de confirmación no oculta mensajes.

### Implementación

- Derivar estado con una función pura en `web/src/chat/estadoMensaje.ts`.
- Crear `EstadoMensaje` usando iconos MUI y tooltip accesible.
- Integrarlo junto a la hora del mensaje en `ConversacionPage.tsx`.
- Consumir cursores de conversación e invalidar al recibir
  `EstadoMensajesActualizado`.
- Confirmar entrega y mantener el marcado de lectura después del render.

### Salidas y verificación

Estados monotónicos tipo marketplace y tests dirigidos verdes.

Commit previsto: `feat(web): muestra entrega y lectura de mensajes`

## Tarea 10 — Activación contextual y service worker Web Push

### Prueba roja

Crear tests para:

- soporte/incompatibilidad y estados de permiso;
- el permiso solo se pide tras pulsar `Activar notificaciones`;
- alta, renovación, revocación y error genérico;
- push genérico sin texto privado;
- confirmación de entrega;
- supresión si la conversación está visible;
- agrupación por conversación y navegación al hacer clic.

### Implementación

- Cambiar VitePWA a service worker inyectado mediante `injectManifest` para mantener
  precache/fallback y añadir manejadores `push`/`notificationclick` testeables.
- Crear `web/src/pwa/push.ts`, `web/src/pwa/service-worker.ts` y adaptadores API.
- Añadir invitación contextual reutilizable en la conversación después de iniciar o
  recibir el primer mensaje.
- Guardar únicamente la decisión UI no sensible; la suscripción autoritativa vive en
  el servidor/navegador.
- No solicitar permiso en montaje y no insistir automáticamente tras denegación.

### Salidas y verificación

PWA compilable, notificación genérica y flujo accesible verde.

Commit previsto: `feat(web): activa notificaciones push de mensajes`

## Tarea 11 — Integración y cierre

### Integración

- Ejecutar migraciones en una base reconstruida con Docker.
- Probar comprador → vendedor y vendedor → comprador con dos clientes.
- Verificar aplicación visible, segundo plano, navegador cerrado, permiso denegado,
  SignalR desconectado y suscripción expirada.
- Revisar que el correo/notificación interna existentes ya no muestren IDs técnicos.
- Actualizar Graphify únicamente si está configurado y su actualización no requiere
  exponer secretos.

### Comandos

Desde `CaseritoApp/`:

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

Desde `web/`:

```powershell
npm run generate:api
npm run typecheck
npm run lint
npm run test -- --run
npm run format:check
npm run build
```

Desde la raíz:

```powershell
git diff --check
./verify.ps1 -Changed
```

### Revisión final

- comparar diff completo con el spec;
- verificar autorización, límites entre contextos, concurrencia y cursores;
- buscar mojibake y plantillas de logs con datos sensibles;
- confirmar que ningún push o error contiene texto, participantes o IDs visibles;
- confirmar migraciones, OpenAPI y cliente generado;
- dejar worktree limpio, publicar `develop` y comprobar CI.

Commit final previsto solo si quedan artefactos de integración:
`chore(chat): integra notificaciones y recibos`
