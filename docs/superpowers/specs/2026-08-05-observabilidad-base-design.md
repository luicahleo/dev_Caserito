# Observabilidad base front-back — diseño

## Objetivo

Permitir diagnosticar errores técnicos y fallos controlados de pasos críticos de
CaseritoApp mediante un código opaco que conecte el frontend con los logs del
backend, sin registrar PII ni desplegar todavía una plataforma externa de
observabilidad.

## No objetivos

- No instalar Loki, Grafana, Seq, Application Insights ni un collector de
  OpenTelemetry.
- No almacenar eventos en la base de datos de negocio.
- No grabar navegación completa, formularios, respuestas HTTP ni contenido del
  usuario.
- No instrumentar en esta entrega todos los flujos funcionales existentes.
- No exponer un visor de logs ni endpoints administrativos de consulta.
- No cambiar los errores de negocio esperados ni sus textos de UI.

## Decisiones

### Salida y retención

El backend emitirá una línea JSON por evento mediante el proveedor de consola
nativo de .NET. Docker seguirá siendo el propietario de la persistencia y se
configurará con rotación por tamaño (`10m`, cinco archivos) para evitar
crecimiento ilimitado. La consulta inicial será por `docker logs`, filtrando por
`ErrorId` o `TraceId`.

No se conectará la referencia parcial a Serilog existente: para esta primera
entrega el proveedor nativo cubre el contrato necesario y evita dependencias y
configuración adicionales. Una fase posterior podrá cambiar el sink sin cambiar
los eventos.

### Correlación y códigos

- ASP.NET Core conservará o creará el `Activity.TraceId` W3C de cada petición.
- Cada respuesta API incluirá `X-Trace-Id`; el valor no se mostrará directamente
  al usuario.
- Un error inesperado recibirá un `ErrorId` aleatorio con formato
  `ERR-XXXXXXXXXXXX` y entropía suficiente para no ser enumerable.
- El manejador global devolverá `ProblemDetails` genérico con `errorId` y
  `traceId`, y registrará el mismo par.
- Un error originado en el navegador recibirá su `ErrorId` en el cliente antes
  de enviarse, para que pueda mostrarse aunque el envío falle.

Los códigos son referencias operativas, no secretos ni identificadores de
usuario.

### Eventos del backend

Un middleware transversal registrará al terminar cada petición:

- `EventName = http.request.completed`;
- método HTTP;
- plantilla del endpoint, nunca la ruta concreta;
- estado HTTP;
- duración en milisegundos;
- `TraceId`;
- `ErrorId` solo cuando exista.

Los errores inesperados registrarán `EventName = http.request.failed`, tipo de
excepción y los identificadores. No se registrarán el mensaje, `Exception`,
stack trace, body, query string, headers, claims ni dirección IP porque pueden
contener datos sensibles. Las validaciones y conflictos esperados conservarán
su comportamiento y no producirán un `ErrorId`.

El `LoggingBehavior` de MediatR seguirá registrando únicamente el nombre del
tipo de request. Al estar dentro de una petición heredará el `TraceId` del scope
de logging.

### Eventos del frontend

El frontend dispondrá de un servicio único de diagnóstico. Solo aceptará un
contrato cerrado:

- `errorId`;
- `eventName` perteneciente a una lista permitida;
- `category` perteneciente a una lista permitida;
- `source`: `router`, `window`, `promise`, `http` o `flow`;
- `traceId` del backend cuando exista;
- `statusCode` HTTP cuando exista;
- versión pública de la aplicación, si está configurada.

La primera entrega capturará:

- errores alcanzados por el `errorElement` del router;
- `window.error`;
- `unhandledrejection`;
- errores de red y respuestas HTTP 5xx del transporte central;
- fallos de carga de chunks ya detectados por la recuperación existente.

Los 4xx esperados no se reportarán automáticamente. El servicio expondrá una
función para instrumentar posteriormente fallos de pasos críticos con nombres
permitidos, sin aceptar contexto libre.

Los envíos serán `POST /api/diagnosticos/frontend`, sin autenticación para
cubrir login y páginas públicas, con cuerpo pequeño, validación estricta y una
política de rate limit por partición técnica. El endpoint responderá `202` y
solo escribirá propiedades validadas en el log. Un fallo al reportar no generará
otro reporte ni afectará al flujo del usuario.

### Experiencia de usuario

La pantalla global de error mostrará un texto genérico y el código de
diagnóstico. Incluirá una acción “Copiar código de diagnóstico”. No mostrará
`traceId`, mensajes internos, stack traces ni objetos de error.

Los errores HTTP que ya se manejan localmente mantendrán sus mensajes actuales.
El `HttpError` podrá transportar el `errorId` seguro del `ProblemDetails` para
que las pantallas críticas lo muestren en cambios posteriores; esta entrega no
reformula todas las vistas existentes.

## Seguridad y anti-PII

Se aplicará una política de lista permitida: el contrato no tendrá campos de
texto libre. En particular, nunca se registrarán documentos, imágenes, tokens,
cookies, pagos, credenciales, mensajes, texto de reportes, formularios,
respuestas, URLs completas, query strings, IDs de entidades, claims, IP ni user
agent.

El backend no confiará en valores de enumeración enviados por el navegador: los
validará por longitud y pertenencia a listas conocidas. Los identificadores se
validarán por formato. El tamaño máximo del cuerpo será pequeño y el rate limit
rechazará abuso con `429`.

La cabecera `traceparent` entrante se usa para correlación estándar, pero nunca
concede autorización ni altera decisiones de negocio.

## Compatibilidad y despliegue

- El endpoint es aditivo y no rompe clientes existentes.
- El contrato OpenAPI y los tipos TypeScript se regenerarán.
- Los logs JSON se activarán por configuración en todos los entornos; los
  niveles podrán variar mediante `Logging:LogLevel`.
- Docker Compose añadirá únicamente la política de rotación del contenedor ya
  existente.
- No hay migraciones de base de datos.

## Pruebas

### Backend

- Generación de `ErrorId` con formato válido y valores distintos.
- Middleware añade `X-Trace-Id` y registra plantilla, estado y duración sin ruta
  concreta ni query string.
- Excepción inesperada devuelve `ProblemDetails` genérico con `errorId` y
  `traceId`, sin mensaje interno.
- Endpoint frontend acepta eventos permitidos y devuelve `202`.
- Endpoint rechaza categorías, fuentes, nombres o identificadores inválidos.
- Prueba anti-PII verifica que el contrato no admite propiedades de contenido
  libre.

### Frontend

- El generador produce un código válido.
- El serializador solo emite el contrato permitido.
- El transporte reporta red/5xx una vez y no reporta 4xx.
- Los listeners globales reportan sin propagar detalles internos.
- La pantalla global muestra y copia el código, sin mostrar el error.
- Un fallo del endpoint de diagnóstico no produce recursión ni rompe la UI.

## Criterios de aceptación

1. Un error inesperado de backend devuelve y registra el mismo `errorId`, junto
   al `traceId` de la petición.
2. Un error inesperado del frontend muestra un `errorId` y genera un evento JSON
   consultable con ese código.
3. Un HTTP 5xx queda correlacionado mediante el `traceId` retornado por backend.
4. Los logs no contienen cuerpos, rutas concretas, mensajes de excepción ni
   campos prohibidos por la política anti-PII.
5. Los reportes inválidos o excesivos se rechazan sin afectar los endpoints de
   negocio.
6. Backend y frontend superan sus verificaciones completas aplicables.

## Trabajo diferido

- Loki/Grafana, paneles, alertas y políticas de retención temporal.
- OpenTelemetry para spans, métricas y servicios externos.
- Instrumentación semántica gradual de flujos críticos adicionales.
- Visor administrativo con permisos y auditoría, si llega a ser necesario.
