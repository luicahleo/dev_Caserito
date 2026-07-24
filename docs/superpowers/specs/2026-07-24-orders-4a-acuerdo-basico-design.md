# Diseño: Fase 4A — acuerdo básico de compra

- **Fecha**: 2026-07-24
- **Estado**: Aprobado
- **Alcance**: primer bloque de Fase 4 sobre `Orders`, limitado a solicitar y aceptar un acuerdo.

## Objetivo

Permitir que dos clientes verificados conviertan el interés sobre un aviso
contactable en un acuerdo formal y persistente. El comprador solicita la compra,
el vendedor la acepta y ambos pueden consultar el acuerdo desde sus respectivas
vistas.

Este bloque entrega `Requested → Agreed`. No mueve dinero, no coordina transporte
y no implica reserva, entrega ni protección de compra.

## No objetivos

- QR, referencias, comprobantes o cualquier procesamiento de pago.
- Tarifas, direcciones, courier o resumen de envío.
- Estados `MarkedAsSold` y `Completed`.
- Pausar, reservar o modificar automáticamente el aviso.
- Reputación, notificaciones, disputas, Fases 5 y 6.
- Crear bounded contexts `Payments`, `Shipping` o `Disputes`.
- Outbox, bus, Saga o infraestructura distribuida.
- Repetir las pruebas manuales diferidas de Fases 1–3.

La implementación de QR continúa bloqueada hasta la validación legal indicada en
el plan MVP. Este bloque no depende de ella.

## Límites arquitectónicos

`Orders` es un bounded context independiente con capas Domain, Application e
Infrastructure. Conserva únicamente identificadores opacos de recursos de otros
contextos y no crea FK entre schemas.

Para crear una orden, Orders consume un puerto propio de lectura de un aviso
contactable. El Host implementa el adaptador sobre la interfaz pública de
Application de Catalog, siguiendo el precedente `Chat → Host adapter → Catalog`.
Orders no referencia ensamblados de Catalog ni Identity.

El Host deriva el comprador y su estado de verificación de los claims. El
vendedor, su estado vigente y la instantánea comercial se obtienen mediante
adaptadores; ningún dato de autoridad se acepta desde el body.

## Modelo de dominio

### Agregado `Orden`

- `Id`
- `AvisoId`
- `CompradorId`
- `VendedorId`
- `Estado`
- `MontoAcordado`
- `Moneda`
- `CreadaEn`
- `ActualizadaEn`
- `Version`

`MontoAcordado` y `Moneda` son una instantánea inmutable del aviso en el momento
de la solicitud. Los cambios posteriores del aviso no modifican la orden.

### Estados

```text
Requested --acepta vendedor--> Agreed
```

Los nombres persistidos y publicados permanecen estables en inglés para respetar
el contrato preparado en Fase 0:

- `Requested`
- `Agreed`

### Invariantes

- Todos los identificadores deben ser no vacíos.
- Comprador y vendedor deben ser distintos.
- Ambos participantes deben estar verificados al solicitar.
- El aviso debe existir, estar activo, visible y ser contactable.
- El monto debe ser positivo y la moneda debe ser una admitida por Catalog.
- Solo el vendedor puede aceptar.
- Solo una orden `Requested` puede pasar a `Agreed`.
- Una acción repetida no genera una transición ni un evento adicional.
- No puede existir más de una orden abierta para el mismo comprador y aviso.
- Fechas normalizadas a UTC.
- La concurrencia se controla mediante `Version` como token optimista.

La creación registra `OrdenSolicitada`. La aceptación registra
`EstadoOrdenCambiado` con estado anterior, nuevo y fecha.

## Casos de uso y permisos

### Solicitar compra

Actor: cliente autenticado y verificado que no es dueño del aviso.

1. El Host obtiene `CompradorId` y `verificado` de claims.
2. Orders consulta la referencia contactable y la instantánea comercial.
3. Orders comprueba que el vendedor continúa verificado.
4. Se rechaza una orden abierta previa del mismo comprador y aviso.
5. Se crea y persiste la orden en `Requested`.

### Aceptar acuerdo

Actor: vendedor de la orden, autenticado.

1. Se carga la orden.
2. Para usuarios que no son el vendedor se responde como recurso no encontrado.
3. El agregado cambia `Requested → Agreed`.
4. Se persiste con concurrencia optimista.
5. Se publica `OrderStatusChanged` tras guardar correctamente.

### Consultar

Solo comprador y vendedor pueden ver el detalle. Los listados se filtran siempre
por el usuario autenticado; no se admite un usuario arbitrario en la petición.
Un tercero recibe un error genérico equivalente a recurso inexistente.

No se introduce un permiso RBAC nuevo. Son operaciones propias del rol Cliente
protegidas por identidad, verificación y pertenencia al agregado.

## Persistencia

Tabla `orders.Orders`:

| Columna | Regla |
|---|---|
| `Id` | PK |
| `AvisoId` | referencia opaca, sin FK |
| `CompradorId` | referencia opaca, sin FK |
| `VendedorId` | referencia opaca, sin FK |
| `Estado` | texto estable y acotado |
| `MontoAcordado` | `decimal(18,2)`, positivo |
| `Moneda` | texto estable y acotado |
| `CreadaEn` | UTC |
| `ActualizadaEn` | UTC |
| `Version` | `rowversion` |

Un índice único sobre `(AvisoId, CompradorId)` impide solicitudes duplicadas.
En este bloque no hay estados terminales, por lo que no necesita filtro; cuando
se introduzcan cancelación o cierre, la migración futura podrá volverlo filtrado.

Las consultas de compras y ventas se ordenan por actividad descendente y usan
paginación acotada.

## Contratos HTTP

### Crear

```http
POST /api/orders
Content-Type: application/json

{ "avisoId": "uuid" }
```

Respuesta `201 Created` con `Location: /api/orders/{id}` y resumen de la orden.

### Aceptar

```http
POST /api/orders/{orderId}/aceptar
```

Respuesta `204 No Content`.

### Listar

```http
GET /api/orders?rol=comprador|vendedor&estado=Requested|Agreed&pagina=1&tamano=20
```

Respuesta paginada con datos mínimos del acuerdo.

### Detalle

```http
GET /api/orders/{orderId}
```

Respuesta con aviso, instantánea económica, estado, rol relativo y fechas.

### Errores

- `400`: entrada, filtro o paginación inválida.
- `401`: no autenticado.
- `403`: comprador no verificado al solicitar.
- `404`: aviso u orden no disponible; incluye acceso de terceros.
- `409`: solicitud duplicada, transición inválida o concurrencia.
- `429`: límite de acciones excedido.

Los Problem Details usan códigos estables y mensajes genéricos. No incluyen
identificadores de participantes ni detalles internos.

## Eventos

Eventos de dominio:

```text
OrdenSolicitada(OrderId, AvisoId, OcurridoEn)
EstadoOrdenCambiado(OrderId, EstadoAnterior, EstadoNuevo, OcurridoEn)
```

Al persistir la aceptación se publica el contrato ya existente:

```text
OrderStatusChanged(
    EventId,
    OcurridoEn,
    OrderId,
    "Requested",
    "Agreed")
```

No se publica `OrderStatusChanged` al crear porque el contrato exige un estado
anterior real. No se inventa `"None"` ni un estado artificial.

El bloque usa publicación in-process posterior a persistencia con el mecanismo
mínimo existente. Los logs del publicador contienen solo tipo de evento y
metadatos técnicos permitidos; nunca información de pago o PII. Outbox queda
diferido.

## UI web

- En el detalle de un aviso ajeno aparece **Proponer compra** para un usuario
  autenticado y verificado.
- Antes de enviar se informa: “Esta acción crea un acuerdo. No realiza ningún
  pago ni reserva el artículo.”
- Nueva ruta **Mis acuerdos**, con pestañas **Compras** y **Ventas**.
- Una venta `Requested` muestra **Aceptar acuerdo**.
- El detalle muestra título del aviso, precio acordado, rol relativo, estado y
  fechas.
- Los estados visibles se traducen: `Requested` → “Solicitado” y `Agreed` →
  “Acordado”.
- No se muestran datos bancarios, direcciones ni promesas de entrega.

La UI usa los tipos OpenAPI generados, TanStack Query y React Router.

## Seguridad y anti-PII

- Comprador, vendedor, precio, moneda y estado nunca llegan como autoridad desde
  el cliente.
- No se registran bodies, claims, participantes, tokens ni datos del acuerdo.
- Los logs contienen operación, resultado genérico y correlación técnica.
- Los accesos ajenos se ocultan con `404`.
- Rate limit por usuario para creación y aceptación.
- El contrato no incluye correo, nombre legal, información KYC ni datos
  bancarios.
- Los errores de unicidad y concurrencia se traducen a conflictos genéricos.

## Pruebas

### Dominio

- Creación válida e inválida.
- Participantes distintos, monto, moneda y UTC.
- Aceptación solo por vendedor y desde `Requested`.
- Idempotencia de reintento sin evento duplicado.
- Eventos de dominio correctos.

### Application

- Aviso no disponible y comprador/vendedor no verificados.
- Instantánea obtenida del puerto, no de la petición.
- Duplicidad y acceso por participante.
- Publicación de integración únicamente tras aceptación persistida.

### Infrastructure

- Mapping, schema, `rowversion` e índice único.
- Repositorio y proyecciones paginadas.
- Conflictos de unicidad y concurrencia.

### Integración HTTP

- `201`, `204`, consultas y filtros.
- `401`, `403`, `404`, `409` y `429`.
- Aislamiento entre usuarios.
- El evento de integración no contiene PII.

### Frontend

- CTA y aviso sin custodia.
- Creación, listados comprador/vendedor y aceptación.
- Estados de carga, vacío y error.
- Ocultación de acciones no autorizadas.

### Cierre

- Build, tests y formato backend.
- OpenAPI y cliente regenerados.
- Typecheck, lint, tests y build frontend.
- `git diff --check`.

## Criterios de aceptación

- Dos clientes verificados pueden crear y aceptar una orden sobre un aviso
  contactable.
- Ambos ven el mismo acuerdo desde su rol; terceros no pueden descubrirlo.
- Precio y moneda quedan congelados al solicitar.
- Cada transición válida registra su evento una sola vez.
- No se incorpora pago, envío, reputación ni custodia.
- La solución mantiene límites de contexto, anti-PII y verificaciones verdes.
