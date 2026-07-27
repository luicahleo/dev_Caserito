# Fase 6 — Notificaciones, endurecimiento y piloto

Fecha: 2026-07-27  
Estado: propuesto / aprobación pendiente

## Objetivo

Cerrar el loop de retención con notificaciones útiles, endurecer la plataforma
para usuarios reales y lanzar un piloto controlado en un nicho y ciudad
acotados.

## Precedencia y alcance vigente

Este spec continúa las fases aprobadas 0–5. No modifica las máquinas de estado
ni las reglas de negocio de Orders, Reputation, Chat, Catalog o Identity. Solo
consume sus eventos de integración existentes y añade los puertos que el Host
implementa.

Quedan expresamente fuera:

- notificaciones push nativas (iOS / Android) o Web Push;
- tiempo real por SignalR/WebSocket para el badge de notificaciones;
- motor de búsqueda dedicado o indexación de texto completo;
- sistema de plantillas de email avanzado;
- custodia de pagos, envíos integrados, disputas automáticas;
- cambios en el modelo de datos de Orders, Reputation, Chat, Catalog o Identity;
- KYC automático, OCR o liveness.

## Decisiones de producto asumidas (pendientes de confirmación)

Dado que las decisiones de lanzamiento aún no están cerradas, este spec propone
valores por defecto. Deben confirmarse antes de implementar los bloques de
piloto y puntos de encuentro.

| Decisión | Propuesta | Razón |
|---|---|---|
| **Nicho inicial** | Tecnología y electrónica de segunda mano (celulares, laptops, accesorios) | Alta densidad en ciudades, necesidad de confianza, diferenciador claro frente a grupos de Facebook |
| **Ciudad piloto** | Cochabamba, Bolivia | Tamaño manejable, comunidad tecnológica activa, ya mencionada en el plan MVP |
| **Estrategia cold-start** | Sembrar oferta con vendedores de confianza (tiendas de reparación, comunidades universitarias) y generar demanda mediante difusión en esos mismos círculos; sin comisiones durante el piloto | Busca densidad transaccional antes de expandir |
| **Canal de notificaciones MVP** | In-app persistentes + email para eventos importantes | Menor complejidad y costo que push; suficiente para cerrar el loop de retención |

Si alguna de estas decisiones cambia, los bloques afectados son:

- Nicho y ciudad: contenido de puntos de encuentro seguros y copy de onboarding.
- Estrategia: métricas de piloto y plan de adquisición.
- Canal: arquitectura de notificaciones (push requeriría infraestructura adicional).

## Alternativas consideradas

### Notificaciones

1. **In-app + email (recomendado).** Persiste una notificación por destinatario
   y envía email para eventos relevantes. Pros: simple, barato, no depende de
   app móvil. Cons: menor engagement que push. Es la opción adecuada para el
   MVP.
2. **Push-first con Firebase Cloud Messaging.** Pros: alto alcance y
   engagement. Cons: requiere PWA/service worker o app móvil, gestión de
   permisos, pruebas cross-browser y, en iOS, APNs. Se deja para post-MVP.
3. **In-app + email + SMS para eventos críticos.** Pros: cubre usuarios con
   poco uso de email. Cons: costo por mensaje, manejo de números de teléfono
   (PII), integración con proveedor local. No justifica el coste en el piloto.

### Búsquedas guardadas

1. **Coincidencia por palabra clave + ciudad/categoría (recomendado).**
   Suficiente para probar el valor de las alertas sin construir un motor de
   búsqueda.
2. **Coincidencia con todos los filtros (precio, estado, ubicación).** Más
   precisa pero requiere mantener sincronizados los filtros de Catalog y
   Notifications. Se puede escalar después de validar interés.

### Alcance del piloto

1. **Diseñar toda la Fase 6 e implementarla por bloques (recomendado).** El
   spec cubre notificaciones, búsquedas guardadas, puntos de encuentro,
   endurecimiento y piloto. Cada bloque puede entrar en producción de forma
   independiente.
2. **Separar notificaciones y piloto en dos fases.** Aumenta la duración del
   MVP sin beneficio claro; el piloto necesita notificaciones para retener.

## Arquitectura

### Bounded context Notifications

Se completa el andamiaje existente:

```text
Notifications.Domain
Notifications.Application
Notifications.Infrastructure
```

`Notifications.Domain` contiene los agregados `Notificacion` y
`BusquedaGuardada`. `Notifications.Application` contiene los handlers de
eventos de integración, casos de uso, puertos y DTO. `Notifications.Infrastructure`
implementa persistencia, envío de email y matching de alertas.

Notifications no depende de Chat, Orders, Catalog ni Identity. Los
identificadores externos son GUID opacos; no existen FK cruzadas.

### Consumo de eventos de integración

Se consumen los eventos existentes en `BuildingBlocks.Contracts`:

- `ChatMessageSent` → notificación al destinatario.
- `OrderStatusChanged` → notificación a comprador y vendedor (mediante puerto).
- `ProductPublished` → evaluación de búsquedas guardadas.

No se modifica la definición de los eventos existentes. Para `OrderStatusChanged`,
que no incluye participantes, el Host implementa el puerto
`IConsultaParticipantesOrden`.

### Puertos hacia otros contextos

El Host implementa:

```text
IConsultaParticipantesOrden.ObtenerAsync(orderId)
    -> ParticipantesOrden(orderId, compradorId, vendedorId)

IConsultaProductoParaAlerta.ObtenerAsync(productId)
    -> ProductoParaAlerta(productId, titulo, categoria, ciudad, precio, estadoProducto)
```

Ambos puertos devuelven `null` si el recurso no existe o no es elegible; los
handlers traducen eso a "no se notifica" sin exponer detalles.

### Envío de email

Application define el puerto:

```text
IEmailSender.EnviarAsync(destinatario, asunto, cuerpoTexto, cuerpoHtml?)
```

Infrastructure provee una implementación configurable:

- En desarrollo y pruebas: `LogEmailSender` que no envía nada y solo registra
  el envío de forma genérica.
- En piloto/producción: `SmtpEmailSender` o adaptador para servicio
  transaccional (SendGrid, AWS SES, etc.), configurado por secretos.

Nunca se registran direcciones de email, asuntos ni cuerpos de mensajes en
logs de aplicación.

## Modelo de dominio

### Agregado `Notificacion`

Datos:

```text
Id
DestinatarioId
Tipo                 // nuevo_mensaje | cambio_estado_orden | alerta_busqueda
Titulo
Mensaje
EntidadRelacionadaId // opcional: ConversacionId, OrderId o ProductId
Leida                // bool
CreadaEn             // UTC
Version              // rowversion
```

Invariantes:

- `DestinatarioId` requerido y distinto de vacío.
- `Tipo` pertenece al conjunto conocido.
- `Titulo` y `Mensaje` normalizados, longitudes máximas 150 y 500.
- `CreadaEn` en UTC.
- Una notificación leída no vuelve a no leída.

El contenido es genérico: no incluye nombres de usuarios, textos de chat ni
montos. El cliente resuelve el texto final y el enlace a partir del `Tipo` y la
`EntidadRelacionadaId`:

- `nuevo_mensaje` → conversación;
- `cambio_estado_orden` → orden;
- `alerta_busqueda` → aviso.

### Agregado `BusquedaGuardada`

Datos:

```text
Id
UsuarioId
PalabraClave       // opcional
Categoria          // opcional
Ciudad             // opcional
PrecioMinimo       // opcional
PrecioMaximo       // opcional
EstadoProducto     // opcional: Nuevo | Usado | Cualquiera
CreadaEn
Version
```

Invariantes:

- `UsuarioId` requerido.
- Al menos uno de `PalabraClave`, `Categoria`, `Ciudad` debe estar presente.
- Rango de precios coherente si ambos están presentes.

### Entidad `PuntoEncuentroSeguro`

Datos:

```text
Id
Ciudad
Nombre
Direccion
Descripcion
Latitud            // opcional
Longitud           // opcional
Horario            // opcional
Activo
```

No es un agregado de dominio complejo; se expone como configuración de
producto por ciudad de piloto.

## Casos de uso

### Recibir un nuevo mensaje de chat

1. Handler consume `ChatMessageSent`.
2. Crea `Notificacion` para `DestinatarioId` con tipo `nuevo_mensaje`.
3. Envía email al destinatario (en MVP el canal email está siempre activo; no
   hay preferencias por tipo).
4. Marca como entidad relacionada la `ConversacionId`.

### Cambio de estado de una orden

1. Handler consume `OrderStatusChanged`.
2. Consulta `IConsultaParticipantesOrden`.
3. Crea una `Notificacion` para cada participante con tipo
   `cambio_estado_orden`.
4. Envía email a ambos participantes (siempre activo en MVP).
5. Marca como entidad relacionada la `OrderId`.

### Publicación que coincide con una búsqueda guardada

1. Handler consume `ProductPublished`.
2. Consulta `IConsultaProductoParaAlerta`.
3. Evalúa todas las búsquedas guardadas activas que coincidan con ciudad,
   categoría, palabra clave, rango de precios y estado.
4. Crea una `Notificacion` de tipo `alerta_busqueda` para cada usuario
   coincidente.
5. Envía email al usuario (siempre activo en MVP).

### Bandeja de notificaciones

- Usuario autenticado lista sus notificaciones ordenadas por `CreadaEn`
  descendente.
- Filtro opcional `soloNoLeidas`.
- Paginación: página inicial 1, tamaño por defecto 20, máximo 50.
- Endpoint de conteo de no leídas para el badge.

### Marcar como leída

- `PATCH /notificaciones/{id}/leida`: una sola notificación.
- `PATCH /notificaciones/marcar-todas-leidas`: todas las no leídas del actor.

### Guardar y eliminar búsquedas

- Crear, listar y eliminar búsquedas guardadas del usuario autenticado.
- Límite máximo de 20 búsquedas por usuario en el piloto.

## Persistencia

Schema: `notifications`.

Tabla `Notifications`:

| Columna | Regla |
|---|---|
| `Id` | PK GUID |
| `RecipientId` | GUID requerido, sin FK |
| `Type` | texto estable, longitud máxima 30 |
| `Title` | texto requerido, longitud máxima 150 |
| `Body` | texto requerido, longitud máxima 500 |
| `RelatedEntityId` | GUID opcional |
| `IsRead` | booleano requerido |
| `CreatedAt` | UTC requerido |
| `Version` | `rowversion` |

Índices:

- `(RecipientId, CreatedAt, Id)` para bandeja;
- `(RecipientId, IsRead, CreatedAt)` para conteo de no leídas.

Tabla `SavedSearches`:

| Columna | Regla |
|---|---|
| `Id` | PK GUID |
| `UserId` | GUID requerido, sin FK |
| `Keyword` | texto opcional, longitud máxima 100 |
| `Category` | texto opcional, longitud máxima 100 |
| `City` | texto opcional, longitud máxima 100 |
| `MinPrice` | decimal opcional |
| `MaxPrice` | decimal opcional |
| `EstadoProducto` | texto opcional, longitud máxima 20 |
| `CreatedAt` | UTC requerido |
| `Version` | `rowversion` |

Índice: `(UserId)`.

Tabla `SafeMeetingPoints`:

| Columna | Regla |
|---|---|
| `Id` | PK GUID |
| `City` | texto requerido, longitud máxima 100 |
| `Name` | texto requerido, longitud máxima 150 |
| `Address` | texto requerido, longitud máxima 250 |
| `Description` | texto opcional, longitud máxima 500 |
| `Latitude` | decimal opcional |
| `Longitude` | decimal opcional |
| `Hours` | texto opcional, longitud máxima 100 |
| `IsActive` | booleano requerido |

Índice: `(City, IsActive)`.

## Contrato HTTP

Grupo autenticado `/api/notificaciones`:

```text
GET  /notificaciones?pagina=1&tamano=20&soloNoLeidas=false
GET  /notificaciones/no-leidas
PATCH /notificaciones/{id}/leida
PATCH /notificaciones/marcar-todas-leidas
```

Grupo autenticado `/api/busquedas-guardadas`:

```text
GET    /busquedas-guardadas
POST   /busquedas-guardadas
DELETE /busquedas-guardadas/{id}
```

Grupo público `/api/publico/puntos-encuentro`:

```text
GET /api/publico/puntos-encuentro?ciudad=Cochabamba
```

Solicitud de creación de búsqueda guardada:

```json
{
  "palabraClave": "iPhone",
  "categoria": "Tecnología",
  "ciudad": "Cochabamba",
  "precioMinimo": null,
  "precioMaximo": 5000,
  "estado": "Usado"
}
```

Respuestas:

- `200`: consultas y actualizaciones parciales;
- `201`: creación de búsqueda guardada;
- `204`: eliminación;
- `400`: validación sintáctica o de dominio;
- `401`: operaciones autenticadas sin sesión;
- `404`: notificación o búsqueda ajena/inexistente;
- `429`: límite excedido.

## Rate limiting

- Creación de búsquedas guardadas: 10 por hora por usuario.
- Envío de notificaciones: controlado por publicación de eventos; no expone
  endpoint de creación directa.
- Consulta de bandeja: 120 por minuto por usuario.
- Conteo de no leídas: 60 por minuto por usuario.

## UI web

### Badge y bandeja

- Icono de campana en el header con conteo de no leídas.
- Dropdown con las últimas notificaciones y enlace a la bandeja completa.
- Bandeja con marcado individual y "marcar todas como leídas".
- Cada notificación enlaza a la entidad relacionada (chat, orden o aviso).
- Polling del conteo cada 60 segundos (sin WebSocket en MVP).

### Búsquedas guardadas

- Pantalla para crear alertas desde una búsqueda actual.
- Listado con opción de eliminar.
- Sin edición en MVP: eliminar y volver a crear.

### Puntos de encuentro seguros

- Sección en el detalle de aviso y en el flujo de orden con puntos activos de
  la ciudad del piloto.
- Muestra nombre, dirección, horario y descripción.

## Endurecimiento

### Seguridad

- Revisión de checklist antes del piloto:
  - autentización y autorización en todos los endpoints;
  - validación de entrada en Host y Application;
  - rate limiting en endpoints sensibles;
  - CORS y cabeceras de seguridad;
  - manejo de secretos (sin valores hardcodeados);
  - revisión de dependencias con `dotnet list package --vulnerable`;
  - confirmación de que no se loggea PII (documentos, emails, cuerpos de chat,
    reseñas, IDs sensibles).
- Pruebas de flujos críticos: registro → KYC → publicación → chat → orden →
  calificación.
- Pruebas de autorización: usuarios no acceden a recursos ajenos.

### Observabilidad

- Endpoint `/health` con chequeos de base de datos.
- Logs estructurados sin PII; niveles adecuados.
- Manejo centralizado de excepciones no controladas.

### Respaldo

- Estrategia de backup de base de datos SQL Server (full + logs).
- Retención y restauración documentada.
- Backup de blobs de imágenes mediante copia de ciclo de vida del proveedor de
  almacenamiento.

## Piloto

### Definición

| Aspecto | Valor propuesto |
|---|---|
| Nicho | Tecnología y electrónica de segunda mano |
| Ciudad | Cochabamba |
| Duración | 4 semanas |
| Meta de usuarios | 20 usuarios activos, al menos 10 publicaciones y 5 acuerdos completados |
| Tarifa | Sin comisiones ni costos durante el piloto |

### Estrategia cold-start

1. **Sembrar oferta**: invitar a 5–10 vendedores de confianza (tiendas de
   reparación, comunidades universitarias, grupos de compra-venta).
2. **Generar demanda**: difusión en grupos de Facebook/WhatsApp de
   universidades y coworkings de Cochabamba, enfocada en la promesa de
   identidad verificada y reputación atada a transacciones.
3. **Habilitar puntos de encuentro seguros**: publicar lugares públicos y
   concurridos de Cochabamba para transacciones presenciales.
4. **Recoger feedback**: formulario corto post-transacción y canal de soporte
   (WhatsApp o email).

### Métricas

- Registros y verificaciones completadas.
- Publicaciones activas.
- Conversaciones iniciadas.
- Órdenes creadas y completadas.
- Reseñas publicadas.
- NPS de usuarios activos.
- Incidentes de seguridad o soporte reportados.

### Criterios de go/no-go

- Al menos 5 órdenes completadas con reseñas.
- NPS mayor a 30.
- Cero incidentes críticos de seguridad sin mitigar.
- Backup y restauración probados al menos una vez.

## Seguridad y anti-PII

- El `DestinatarioId` de cada notificación se deriva de eventos de integración o
  puertos; el cliente no puede especificar destinatario.
- Los títulos y mensajes son genéricos y no contienen nombres, precios,
  direcciones ni contenido de chat.
- No se registran emails, asuntos ni cuerpos de emails en logs de aplicación.
- Las búsquedas guardadas son privadas del usuario; no se exponen a terceros.
- Los puntos de encuentro son datos públicos; no incluyen información de
  usuarios.

## Concurrencia

- Una misma publicación puede coincidir con muchas búsquedas guardadas: el
  handler itera y crea notificaciones idempotentes por `(UsuarioId, ProductId,
  Tipo=alerta_busqueda)`.
- Dos lecturas simultáneas de la bandeja no generan conflictos; `Version` solo
  aplica a marcar como leída.

## Estrategia de pruebas

### Dominio

- creación de notificación válida;
- tipos desconocidos rechazados;
- longitudes de título/mensaje;
- marcado como leída;
- búsqueda guardada con al menos un criterio;
- rango de precios inválido;
- punto de encuentro con ciudad requerida.

### Application

- handler de chat crea notificación para destinatario;
- handler de orden consulta participantes y crea dos notificaciones;
- handler de producto publicado genera alertas solo para coincidencias;
- no se notifica si el puerto devuelve null;
- bandeja paginada y conteo de no leídas;
- marcar como leída propia; ajena devuelve 404;
- límite de 20 búsquedas guardadas.

### Persistencia

- schema y configuración;
- índices de bandeja y conteo;
- concurrencia al marcar como leída.

### Host e integración

- endpoints autenticados y públicos;
- consumo de eventos de integración en flujos reales;
- envío de email simulado en pruebas;
- rate limiting;
- OpenAPI refleja el contrato;
- `/health` responde correctamente.

### Frontend

- badge con conteo de no leídas;
- dropdown y bandeja;
- marcado como leída;
- creación y eliminación de búsquedas guardadas;
- visualización de puntos de encuentro;
- errores genéricos y conversiones de tipos OpenAPI.

### Regresión

- flujos de Orders 4A–4C y Reputación 5;
- chat y bloqueo;
- perfil público y autenticación;
- anti-PII.

## Criterios de aceptación

- Un usuario recibe una notificación in-app cuando le envían un mensaje de chat.
- Comprador y vendedor reciben notificaciones ante cambios de estado de su
  orden.
- Un usuario puede guardar búsquedas y recibir alertas in-app cuando se publica
  un producto coincidente.
- Existe una bandeja de notificaciones con marcado de lectura y conteo de no
  leídas.
- Se envían emails para eventos importantes sin exponer PII en logs.
- Se muestran puntos de encuentro seguros en la ciudad de piloto.
- Se completan revisiones de seguridad, observabilidad y backups antes del
  lanzamiento.
- Se define nicho, ciudad, estrategia cold-start y métricas de piloto.
- No se introducen dependencias directas ni FK cruzadas entre Notifications y
  otros bounded contexts.
- No se incorpora push, custodia de pagos, envíos integrados ni disputas
  automáticas.
