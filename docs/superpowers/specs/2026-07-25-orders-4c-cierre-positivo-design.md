# Diseño: Fase 4C — cierre positivo del acuerdo

- **Fecha**: 2026-07-25
- **Estado**: Aprobado
- **Alcance**: tercer bloque de Fase 4 sobre `Orders`, limitado al cierre
  positivo de un acuerdo y al retiro del aviso vendido.
- **Base**: continúa los diseños
  `2026-07-24-orders-4a-acuerdo-basico-design.md` y
  `2026-07-25-orders-4b-cancelacion-design.md`.

## Objetivo

Permitir que el vendedor cierre comercialmente un acuerdo aceptado marcando el
artículo como vendido y que el comprador confirme después que la compraventa
terminó. El cierre bilateral lleva la orden a `Completed`, mientras Catalog
retira el aviso de sus superficies públicas.

Este bloque entrega `Agreed → MarkedAsSold → Completed`. Marcar vendido constituye
la confirmación del vendedor; completar requiere además la confirmación explícita
del comprador.

La aplicación no verifica pago, entrega ni conformidad material. Si el comprador
no confirma, la orden permanece en `MarkedAsSold` indefinidamente y no habilita
reputación.

## No objetivos

- QR, referencias, comprobantes o cualquier procesamiento o registro de pago.
- Custodia, escrow o confirmación de pago.
- Tarifas, direcciones, courier o resumen de envío.
- Reputación, reseñas o habilitar anticipadamente la Fase 5.
- Notificaciones o recordatorios a la contraparte.
- Disputas, reclamaciones o resolución del abandono de una confirmación.
- Crear bounded contexts `Payments`, `Shipping` o `Disputes`.
- Reabrir una orden o un aviso después de marcarlo vendido.
- Completar automáticamente por silencio o por vencimiento.
- Outbox, bus, Saga o infraestructura distribuida.

La implementación de QR sin custodia continúa bloqueada por la validación legal
indicada en el plan MVP y no forma parte de este bloque.

## Límites arquitectónicos

`Orders` y `Catalog` permanecen como bounded contexts independientes. Conservan
identificadores opacos, no crean FK entre schemas y no consultan directamente
tablas del otro contexto.

La coordinación del cierre ocurre síncronamente en el Host mediante interfaces
públicas de Application:

1. El Host comprueba que Catalog puede procesar el aviso.
2. Orders marca la orden elegida como vendida y cancela las demás órdenes
   abiertas del aviso en una transacción de su propio schema.
3. Después del commit de Orders, el Host ordena a Catalog asegurar que el aviso
   esté vendido y asociado a la orden ganadora.
4. Si Catalog falla, la misma petición puede repetirse para reparar el estado sin
   duplicar transiciones ni eventos.

No existe una transacción distribuida entre ambos schemas. La operación se diseña
como idempotente y reparable dentro del monolito actual, sin introducir outbox,
bus ni Saga.

## Modelo de dominio de Orders

### Estados nuevos

`EstadoOrden` incorpora valores al final, sin renumerar los existentes:

```text
Requested    = 1
Agreed       = 2
Cancelled    = 3
MarkedAsSold = 4
Completed    = 5
```

La máquina de estados queda:

```text
Requested --cancela participante---------------------> Cancelled
Agreed    --cancela participante---------------------> Cancelled
Agreed    --marca vendido el vendedor----------------> MarkedAsSold
MarkedAsSold --confirma cierre el comprador----------> Completed
```

`MarkedAsSold` y `Completed` son terminales respecto de cancelación y reapertura.
No existe transición desde ellos a `Cancelled`, `Agreed` ni `Requested`.

### Datos nuevos

El agregado `Orden` incorpora:

- `MarcadaVendidaEn`, nullable.
- `CompradorConfirmoEn`, nullable.
- `CompletadaEn`, nullable.

`MarcadaVendidaEn` registra tanto la transición a `MarkedAsSold` como la
confirmación del vendedor. `CompradorConfirmoEn` y `CompletadaEn` se establecen
en la misma transición a `Completed`.

No se guardan referencias de pago, comprobantes, direcciones, entrega ni texto
libre.

### Marcar como vendido

Nuevo comportamiento
`Orden.MarcarComoVendida(Guid actorId, DateTimeOffset ocurrioEn)`:

- Actor vacío o distinto del vendedor: `NoEncontrada`.
- Solo admite una orden `Agreed`.
- Si la orden ya está en `MarkedAsSold` o `Completed`, la repetición del vendedor
  es éxito idempotente sin modificar fechas ni emitir eventos.
- Desde `Requested` o `Cancelled`, responde `TransicionInvalida`.
- En una transición real:
  - cambia a `MarkedAsSold`;
  - normaliza y registra `MarcadaVendidaEn`;
  - actualiza `ActualizadaEn`;
  - registra un único `EstadoOrdenCambiado`.

Marcar vendido no significa pago confirmado, entrega realizada ni protección de
compra.

### Confirmar cierre

Nuevo comportamiento
`Orden.ConfirmarCierre(Guid actorId, DateTimeOffset ocurrioEn)`:

- Actor vacío o distinto del comprador: `NoEncontrada`.
- Solo admite una orden `MarkedAsSold`.
- Si ya está `Completed`, la repetición del comprador es éxito idempotente sin
  modificar fechas ni emitir eventos.
- Desde `Requested`, `Agreed` o `Cancelled`, responde `TransicionInvalida`.
- En una transición real:
  - registra `CompradorConfirmoEn`;
  - cambia a `Completed`;
  - registra `CompletadaEn` con la misma fecha;
  - actualiza `ActualizadaEn`;
  - registra un único `EstadoOrdenCambiado`.

### Órdenes competidoras

Varias órdenes de compradores distintos pueden permanecer en `Requested` o
`Agreed` sobre el mismo aviso. Esto no constituye una reserva.

Cuando el vendedor marca una orden como vendida:

- la orden elegida pasa de `Agreed` a `MarkedAsSold`;
- todas las demás órdenes `Requested` o `Agreed` del mismo `AvisoId` pasan a
  `Cancelled`;
- cada cancelación actualiza su fecha y registra su propio
  `EstadoOrdenCambiado`;
- el conjunto se persiste en una única transacción de Orders.

Una orden ya `Cancelled`, `MarkedAsSold` o `Completed` no vuelve a modificarse.
El estado `Cancelled` y su comportamiento idempotente permanecen compatibles con
4B.

### Invariantes

- Solo el vendedor puede ejecutar `Agreed → MarkedAsSold`.
- Marcar vendido cuenta como confirmación del vendedor.
- Solo el comprador puede ejecutar `MarkedAsSold → Completed`.
- `Completed` siempre implica ambas confirmaciones.
- El silencio del comprador nunca completa una orden.
- Solo una orden puede ser la ganadora de un aviso.
- Marcar vendido es irreversible.
- Las repeticiones no modifican fechas ni generan eventos adicionales.
- Fechas normalizadas a UTC.
- La concurrencia continúa protegida por `Version` (`rowversion`).

## Modelo de dominio de Catalog

### Estado vendido

`EstadoAviso` incorpora `Vendido` al final. Los valores existentes conservan su
numeración.

```text
Activo | Pausado --marca vendido el sistema--> Vendido
```

`Vendido` es un estado terminal: no admite editar, pausar, reactivar, eliminar
por el dueño ni volver a mostrarse como disponible.

Catalog incorpora `OrdenVentaId`, nullable y sin FK. Identifica de forma opaca la
orden que cerró el aviso.

Nuevo comportamiento
`Aviso.MarcarVendido(Guid ordenId, Guid vendedorId, DateTime ocurrioEn)`:

- valida identificadores no vacíos;
- verifica que el actor sea el dueño del aviso;
- admite `Activo` o `Pausado`;
- cambia a `Vendido`, asigna `OrdenVentaId`, actualiza la fecha y registra
  `AvisoMarcadoVendido`;
- si ya está vendido por la misma orden, devuelve éxito idempotente;
- si está vendido por otra orden, devuelve conflicto;
- si está eliminado por el dueño o por moderación, no admite la transición.

La moderación continúa siendo una dimensión independiente. Un aviso oculto puede
marcarse vendido, pero permanece oculto. Un aviso eliminado por moderación no se
puede vender.

### Superficies públicas y privadas

Un aviso `Vendido`:

- no aparece en búsqueda ni descubrimiento;
- no tiene detalle ni fotos públicas;
- no es contactable;
- no admite nuevas órdenes;
- permanece en `Mis avisos` del vendedor con estado “Vendido”.

Las conversaciones ya existentes continúan accesibles a sus participantes. No se
modifican el detalle/contacto ya corregido, la autorización de Chat, SignalR ni
sus proxies.

## Casos de uso y permisos

### Marcar orden como vendida

Actor: vendedor autenticado de una orden `Agreed`.

1. El Host deriva el actor del claim `sub`.
2. Obtiene de Orders la referencia opaca necesaria sin exponer participantes.
3. Comprueba mediante el puerto de Catalog que el aviso admite el cierre.
4. Orders marca la orden ganadora y cancela las competidoras.
5. El commit de Orders publica los cambios de estado.
6. El Host pide a Catalog asegurar el estado vendido con el mismo `OrderId`.
7. Devuelve éxito cuando ambos contextos alcanzan el resultado esperado.

No se introduce un permiso RBAC nuevo.

### Confirmar cierre

Actor: comprador autenticado de una orden `MarkedAsSold`.

1. El Host deriva el actor del claim `sub`.
2. Orders comprueba pertenencia y rol.
3. El agregado transiciona a `Completed`.
4. Se persiste con concurrencia optimista.
5. Se publica `OrderStatusChanged` una sola vez.

Catalog no cambia al confirmar el comprador: el aviso ya es terminalmente
`Vendido`.

### Consultar

Solo comprador y vendedor pueden consultar la orden. Los listados siguen
filtrados por el usuario autenticado. Un tercero no puede descubrir la existencia
ni el estado del acuerdo.

## Orquestación, fallos parciales y concurrencia

### Idempotencia reparable

La operación de marcar vendido se identifica por `OrderId`:

- Orders trata `MarkedAsSold` y `Completed` de esa orden como resultado ya
  alcanzado para el vendedor.
- Catalog trata `Vendido` con el mismo `OrdenVentaId` como resultado ya
  alcanzado.
- En un reintento, el Host vuelve a ejecutar la fase de Catalog aunque Orders no
  genere una transición nueva.

Así, si Orders confirmó su commit y Catalog falló, repetir el mismo endpoint
repara el aviso sin duplicar eventos.

### Carreras

- Dos peticiones concurrentes sobre la misma orden producen una transición y un
  único conjunto de eventos.
- Dos órdenes diferentes no pueden ganar el mismo aviso.
- La transacción de Orders carga la orden elegida y las competidoras; los
  `rowversion` detectan cambios concurrentes.
- Si otra petición ya logró el mismo resultado, la relectura posterior al
  conflicto permite responder éxito idempotente.
- Si el resultado existente pertenece a otra orden o es incompatible, se
  responde conflicto.
- Catalog protege con concurrencia optimista el cambio del aviso y verifica
  `OrdenVentaId`.

## Persistencia y migración

### Orders

La tabla `orders.Orders` agrega columnas nullable:

| Columna | Regla |
|---|---|
| `MarcadaVendidaEn` | UTC; requerida solo para `MarkedAsSold` y `Completed` |
| `CompradorConfirmoEn` | UTC; requerida solo para `Completed` |
| `CompletadaEn` | UTC; requerida solo para `Completed` |

El estado continúa persistido como texto estable. El índice único filtrado de 4B
sigue excluyendo únicamente `Cancelled`; no se renombra ni elimina.

Las filas existentes `Requested`, `Agreed` y `Cancelled` conservan estado y
fechas nuevas en `NULL`. No hay backfill ni migración destructiva.

### Catalog

La tabla de avisos agrega:

| Columna | Regla |
|---|---|
| `OrdenVentaId` | nullable, referencia opaca, sin FK |

`Vendido` se agrega al conversor estable del estado. Las filas existentes no
cambian y conservan `OrdenVentaId = NULL`.

## Contratos HTTP

### Marcar vendido

```http
POST /api/orders/{orderId}/marcar-vendido
```

Sin body. Respuesta `204 No Content`.

### Confirmar cierre

```http
POST /api/orders/{orderId}/confirmar-completado
```

Sin body. Respuesta `204 No Content`.

Ambos reutilizan el rate limit `orders-acciones`.

### Errores

- `400`: identificador o entrada inválida.
- `401`: no autenticado.
- `404`: orden no disponible, tercero o actor incorrecto para la acción.
- `409`: transición inválida, aviso incompatible o carrera con otra orden.
- `429`: límite de acciones excedido.
- `503`: Orders quedó confirmado, pero Catalog no pudo asegurar todavía el
  estado vendido; la operación puede repetirse.

Los Problem Details usan códigos estables y mensajes genéricos. El `503` no
revela qué contexto falló, identificadores, participantes ni estado interno.

## Eventos

### Orders

Se reutiliza el evento de dominio:

```text
EstadoOrdenCambiado(OrderId, EstadoAnterior, EstadoNuevo, OcurridoEn)
```

El `UnitOfWorkOrders` continúa publicando el contrato existente, sin modificar su
forma:

```text
OrderStatusChanged(
    EventId,
    OcurridoEn,
    OrderId,
    OldStatus,
    NewStatus)
```

Se publica una vez por cada transición efectiva:

- `Agreed → MarkedAsSold`;
- cada competidora `Requested | Agreed → Cancelled`;
- `MarkedAsSold → Completed`.

Los reintentos idempotentes no publican eventos adicionales.

### Catalog

Nuevo evento de dominio:

```text
AvisoMarcadoVendido(AvisoId, OrdenId, OcurridoEn)
```

No se crea un evento de integración nuevo para coordinar Orders y Catalog. El
Host realiza la coordinación explícita y `OrderStatusChanged` permanece como
hecho de integración compatible para consumidores actuales o futuros.

## Consultas y OpenAPI

El filtro `estado` de Orders admite:

- `Requested`
- `Agreed`
- `Cancelled`
- `MarkedAsSold`
- `Completed`

El detalle de orden incorpora:

- `marcadaVendidaEn`;
- `compradorConfirmoEn`;
- `completadaEn`.

Los campos son nullable según el estado. Los DTO no incorporan identificadores de
participantes, pago, entrega ni contenido del acuerdo.

El resumen conserva datos mínimos y el rol relativo `comprador | vendedor`. Los
artefactos OpenAPI y `web/src/api/schema.d.ts` se regenerarán durante la
implementación.

## UI web

### Vendedor

Una venta `Agreed` muestra **Marcar como vendido**. La acción abre un diálogo
irreversible que informa:

- el aviso dejará de estar disponible;
- las demás solicitudes y acuerdos abiertos del aviso se cancelarán;
- la acción no confirma pago ni entrega;
- no podrá deshacerse.

En `MarkedAsSold` muestra:

> Marcaste el artículo como vendido. Esperando confirmación del comprador.

No ofrece acciones adicionales.

### Comprador

Una compra `MarkedAsSold` muestra **Confirmar cierre**. El diálogo indica que debe
confirmar únicamente si la compraventa terminó y que Caserito no verifica pago ni
entrega.

Si todavía no confirma, el estado permanece visible sin vencimiento.

### Estados terminales

- `MarkedAsSold` → “Marcado como vendido”.
- `Completed` → “Completado”.
- `Completed` muestra “Completado por ambas partes”.
- Ninguno muestra acciones de cancelar, reabrir o editar.

Tras marcar vendido se invalidan acuerdos, avisos propios, búsqueda y detalle del
aviso. Tras confirmar se invalidan las consultas de acuerdos afectadas.

La UI usa TanStack Query, React Router y los tipos OpenAPI generados. No altera
los arreglos existentes de detalle/contacto ni el proxy SignalR.

## Seguridad, autorización y anti-PII

- El actor siempre deriva del claim `sub`.
- El body no acepta comprador, vendedor, estado, aviso ni confirmaciones.
- Un tercero o actor incorrecto recibe `404`.
- No se registran bodies, claims, participantes, aviso, orden, precio, contenido
  del acuerdo, confirmaciones, tokens ni datos KYC.
- Los logs contienen únicamente operación, resultado genérico, correlación
  técnica y tipo de evento.
- No se registran referencias de pago, comprobantes, direcciones ni entrega.
- El contrato `OrderStatusChanged` no contiene PII.
- No se vuelve a consultar KYC al cerrar: la orden conserva la validez del
  acuerdo creado entre usuarios verificados y no se añade dependencia con
  Identity.
- Los conflictos de concurrencia y fallos parciales se traducen a errores
  genéricos.

## Compatibilidad con 4A y 4B

- `Requested`, `Agreed` y `Cancelled` conservan valores y comportamiento.
- Aceptar continúa siendo idempotente únicamente en `Agreed`; no acepta estados
  posteriores.
- Cancelar continúa permitido solo desde `Requested` y `Agreed`.
- Cancelar una orden ya `Cancelled` continúa siendo éxito idempotente.
- Una orden cancelada permite re-solicitud mientras el aviso siga contactable.
- Un aviso vendido deja de ser contactable, por lo que no admite re-solicitudes.
- El índice filtrado de 4B continúa permitiendo historial cancelado sin abrir una
  segunda orden activa para el mismo comprador y aviso.
- No se modifica la instantánea económica inmutable de 4A.

## Estrategia de pruebas

### Dominio Orders

- `Agreed → MarkedAsSold` únicamente por vendedor.
- Reintento del vendedor en `MarkedAsSold` y `Completed`: éxito sin evento.
- Estados de origen inválidos e irreversibilidad.
- `MarkedAsSold → Completed` únicamente por comprador.
- Reintento del comprador en `Completed`: éxito sin evento.
- Fechas UTC y coherencia de las tres fechas nuevas.
- `Completed` implica ambas confirmaciones.
- Terceros y actores vacíos obtienen `NoEncontrada`.

### Application Orders

- Marca la ganadora y cancela todas las competidoras abiertas.
- Ignora competidoras terminales.
- Persiste el conjunto en una sola transacción.
- Publica un evento por transición real.
- Reintento no vuelve a cancelar ni publicar.
- Conflictos concurrentes se releen para distinguir éxito idempotente de carrera
  incompatible.

### Dominio y Application Catalog

- `Activo` y `Pausado` pasan a `Vendido`.
- Aviso oculto por moderación conserva su restricción.
- Eliminado por dueño o moderación no admite venta.
- Misma orden es idempotente; orden diferente produce conflicto.
- Vendido no admite editar, pausar, reactivar ni eliminar.
- Actor distinto del dueño no puede marcar vendido.

### Infrastructure

- Mapping y migración de estados nuevos.
- Columnas nullable y compatibilidad de filas existentes.
- `OrdenVentaId` sin FK cruzada.
- `rowversion` en carreras de Orders y Catalog.
- Índice filtrado de 4B permanece correcto.
- Consultas públicas excluyen `Vendido`.
- Consultas privadas muestran el aviso vendido al dueño.

### Integración y HTTP

- Flujo completo `Agreed → MarkedAsSold → Completed`.
- `204` para acciones y reintentos idempotentes.
- Cancelación de órdenes competidoras.
- Solo una ganadora bajo concurrencia.
- Fallo de Catalog después del commit de Orders devuelve `503`.
- Reintento repara Catalog sin duplicar `OrderStatusChanged`.
- `400`, `401`, `404`, `409`, `429` y `503`.
- Terceros no descubren órdenes.
- Aviso vendido desaparece de superficies públicas y no admite nuevas órdenes.
- Conversaciones existentes y acceso SignalR permanecen operativos.
- Eventos y logs no contienen PII.

### Frontend

- Botón y diálogo irreversible del vendedor.
- Advertencia sobre cancelación de órdenes competidoras.
- Botón y diálogo de confirmación del comprador.
- Traducciones de `MarkedAsSold` y `Completed`.
- Mensajes de espera según rol.
- Ocultación de acciones no autorizadas o terminales.
- Tratamiento genérico y reintentable del `503`.
- Invalidación de acuerdos, avisos y superficies públicas.
- Regresión de detalle/contacto y proxy SignalR.

### Cierre

- Build, tests y formato backend.
- OpenAPI y cliente regenerados.
- Typecheck, lint, tests y build frontend.
- `git diff --check`.

## Criterios de aceptación

- Solo el vendedor de una orden `Agreed` puede marcar el artículo como vendido.
- Marcar vendido constituye la confirmación del vendedor, sin afirmar pago ni
  entrega.
- El comprador puede confirmar después y solo entonces la orden pasa a
  `Completed`.
- Sin confirmación del comprador, la orden permanece en `MarkedAsSold`.
- El aviso vendido deja de ser público y no admite nuevos contactos ni órdenes.
- Las demás órdenes abiertas del aviso se cancelan atómicamente.
- Los reintentos reparan fallos parciales sin duplicar transiciones ni eventos.
- `Cancelled`, su idempotencia y la re-solicitud sobre avisos disponibles
  permanecen compatibles.
- Orders y Catalog conservan sus límites, sin FK ni acceso directo entre schemas.
- No se incorpora pago, envío, reputación, notificaciones, disputas ni custodia.
- Se preservan los arreglos existentes de detalle/contacto y proxy SignalR.
