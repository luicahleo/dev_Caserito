# Diseño: Fase 4B — cancelación de acuerdo

- **Fecha**: 2026-07-25
- **Estado**: Aprobado
- **Alcance**: segundo bloque de Fase 4 sobre `Orders`, limitado a cancelar un
  acuerdo no terminal.
- **Base**: continúa el diseño `2026-07-24-orders-4a-acuerdo-basico-design.md`.

## Objetivo

Permitir que un participante cierre un acuerdo por el lado negativo: el comprador
retira su solicitud o el vendedor la rechaza, y cualquiera de los dos puede
cancelar un acuerdo ya aceptado que no prospera. La cancelación libera el aviso
para que el mismo comprador pueda volver a solicitarlo.

Este bloque agrega la transición hacia un estado terminal `Cancelled`. No mueve
dinero, no coordina transporte, no avanza la venta ni notifica a la contraparte.

## No objetivos

- Estados `MarkedAsSold` y `Completed`, ni cualquier avance de la transacción.
- QR, referencias, comprobantes o cualquier procesamiento de pago.
- Tarifas, direcciones, courier o resumen de envío.
- Motivo de cancelación o cualquier texto libre del usuario.
- Notificar a la contraparte, reputación, disputas.
- Reactivar, reservar o modificar automáticamente el aviso.
- Crear bounded contexts `Payments`, `Shipping` o `Disputes`.
- Outbox, bus, Saga o infraestructura distribuida.
- Distinguir en persistencia `Rechazado` frente a `Retirado` como estados
  separados: un único estado terminal `Cancelled`.

## Límites arquitectónicos

Se conservan los límites de 4A. `Orders` sigue siendo un bounded context
independiente con identificadores opacos y sin FK entre schemas. Este bloque no
introduce nuevos puertos ni adaptadores entre contextos: opera sobre el agregado,
el repositorio y el publicador de integración ya existentes.

## Modelo de dominio

### Estado nuevo

```text
Requested --cancela participante--> Cancelled
Agreed    --cancela participante--> Cancelled
```

`EstadoOrden` incorpora `Cancelled = 3`. Los valores existentes (`Requested = 1`,
`Agreed = 2`) no cambian. Los nombres persistidos y publicados permanecen
estables en inglés para respetar el contrato de Fase 0.

### Comportamiento del agregado

Nuevo método `Orden.Cancelar(Guid actorId, DateTimeOffset ocurrioEn)`:

- Si el actor no es participante (`actorId != CompradorId && actorId != VendedorId`)
  o es vacío, responde `NoEncontrada` (mismo patrón que `Aceptar`).
- Si el estado ya es `Cancelled`, responde éxito idempotente sin evento.
- Si el estado es `Requested` o `Agreed`, transiciona a `Cancelled`, actualiza
  `ActualizadaEn` y registra `EstadoOrdenCambiado(Id, estadoAnterior, Cancelled, fechaUtc)`.
- Cualquier otro estado responde `TransicionInvalida` (defensa a futuro; hoy no
  hay más estados terminales).

### Invariantes

- Solo comprador o vendedor pueden cancelar.
- Solo una orden no terminal (`Requested` o `Agreed`) puede pasar a `Cancelled`.
- Una acción repetida no genera una transición ni un evento adicional.
- Tras cancelar, deja de existir una orden abierta para ese `(AvisoId, CompradorId)`,
  de modo que el mismo comprador puede volver a solicitar sobre el aviso.
- Fechas normalizadas a UTC.
- La concurrencia se controla mediante `Version` (`rowversion`) como token optimista.

## Casos de uso y permisos

### Cancelar acuerdo

Actor: comprador o vendedor de la orden, autenticado.

1. Se carga la orden por Id.
2. Para usuarios que no son participantes se responde como recurso no encontrado.
3. El agregado ejecuta `Cancelar` según las reglas anteriores.
4. Se persiste con concurrencia optimista.
5. Se publica `OrderStatusChanged` tras guardar solo si hubo transición real.

No se introduce un permiso RBAC nuevo. Es una operación propia del rol Cliente
protegida por identidad y pertenencia al agregado.

### Efecto sobre solicitar (4A)

`IRepositorioOrdenes.ExisteAbiertaAsync` deja de contar órdenes `Cancelled`;
"abierta" pasa a significar "no cancelada". Así, el rechazo previo del vendedor o
el retiro del comprador ya no bloquean una nueva solicitud del mismo comprador
sobre el mismo aviso. El resto del flujo de solicitar y aceptar de 4A no cambia.

### Consultar (4A)

Sin cambios de lógica. `Cancelled` se admite como valor de filtro `estado` en el
listado y aparece en el detalle como cualquier otro estado.

## Persistencia

Tabla `orders.Orders` (sin columnas nuevas). El único cambio es el índice único
que impide solicitudes duplicadas:

| Antes | Después |
|---|---|
| Índice único `(AvisoId, CompradorId)` sin filtro | Índice único `(AvisoId, CompradorId)` **filtrado** `WHERE [Estado] <> 'Cancelled'` |

Una migración nueva en el schema `orders` recrea el índice como filtrado. La
migración no toca datos: al no existir aún órdenes `Cancelled`, el conjunto
indexado es idéntico. Los índices de consulta `(CompradorId, ActualizadaEn)` y
`(VendedorId, ActualizadaEn)` no cambian.

## Contratos HTTP

### Cancelar

```http
POST /api/orders/{orderId}/cancelar
```

Respuesta `204 No Content`. Rate limit `orders-acciones` (reutilizado de aceptar).

### Errores

- `401`: no autenticado.
- `404`: orden inexistente o acceso de un tercero.
- `409`: transición inválida o conflicto de concurrencia.
- `429`: límite de acciones excedido.

Los Problem Details usan códigos estables y mensajes genéricos, sin identificadores
de participantes ni detalles internos, igual que en 4A.

### Filtro de listado

`GET /api/orders?estado=Cancelled` se vuelve válido. El validador de
`ListarOrdenesQuery` amplía los estados permitidos a
`Requested | Agreed | Cancelled`.

## Eventos

Se reutiliza el evento de dominio existente:

```text
EstadoOrdenCambiado(OrderId, EstadoAnterior, Cancelled, OcurridoEn)
```

Al persistir la cancelación, `UnitOfWorkOrders` publica el contrato ya existente
sin cambios:

```text
OrderStatusChanged(
    EventId,
    OcurridoEn,
    OrderId,
    "Requested" | "Agreed",   // estado anterior real
    "Cancelled")
```

La publicación es in-process posterior a la persistencia, con el mecanismo mínimo
ya presente. Los logs del publicador contienen solo tipo de evento y metadatos
técnicos permitidos; nunca PII. Outbox sigue diferido.

## UI web

Sobre la ruta **Mis acuerdos** existente:

- Una compra `Requested` muestra **Cancelar solicitud**; una compra `Agreed`
  muestra **Cancelar acuerdo**.
- Una venta `Requested` muestra **Rechazar solicitud**; una venta `Agreed`
  muestra **Cancelar acuerdo**.
- La acción abre un diálogo de confirmación antes de enviarse; el texto aclara
  que la cancelación cierra el acuerdo y no implica pago ni penalización.
- El estado visible `Cancelled` se traduce a “Cancelado”.
- Las órdenes canceladas siguen visibles en el historial, con su chip de estado y
  sin botones de acción.
- Tras cancelar, se refrescan los listados afectados.

La UI usa los tipos OpenAPI generados, TanStack Query y React Router. El cliente
`orders.ts` agrega `cancelarOrden(id)` y extiende el tipo `EstadoOrden` con
`'Cancelled'`.

## Seguridad y anti-PII

- El actor deriva del claim `sub`; ningún participante llega como autoridad desde
  el cliente.
- No se registran bodies, claims, participantes, tokens ni datos del acuerdo.
- Los accesos de terceros se ocultan con `404`.
- Rate limit por usuario para la acción de cancelar (`orders-acciones`).
- No se captura ni almacena motivo de cancelación, evitando texto libre con PII.
- Los conflictos de concurrencia se traducen a `409` genérico.

## Pruebas

### Dominio

- Cancelar desde `Requested` por comprador y por vendedor: transición y evento.
- Cancelar desde `Agreed` por comprador y por vendedor: transición y evento.
- Cancelar por un tercero: `NoEncontrada`, sin transición ni evento.
- Cancelar una orden ya `Cancelled`: éxito idempotente sin evento.
- Actor vacío: `NoEncontrada`.

### Application

- Handler carga la orden; orden ausente → `NoEncontrada`.
- Publicación de integración únicamente tras una transición persistida.
- `ExisteAbiertaAsync` excluye canceladas: solicitar tras cancelar es posible.

### Infrastructure

- Índice único filtrado: dos órdenes no canceladas para el mismo
  `(AvisoId, CompradorId)` fallan; una nueva tras cancelar la previa persiste.
- Mapping de `Cancelled` como string.
- Conflicto de concurrencia en cancelación.

### Integración HTTP

- `204` al cancelar como comprador y como vendedor, desde `Requested` y `Agreed`.
- `404` para un tercero.
- `409` por concurrencia.
- Idempotencia: doble cancelación devuelve `204`.
- Re-solicitud tras cancelación exitosa devuelve `201`.
- Filtro `estado=Cancelled` en el listado.
- El evento de integración no contiene PII.

### Frontend

- Botones de cancelar/rechazar según rol y estado.
- Diálogo de confirmación y llamada al cliente.
- Traducción “Cancelado” y ocultación de acciones en canceladas.
- Estados de carga y error genérico.

### Cierre

- Build, tests y formato backend.
- OpenAPI y cliente regenerados.
- Typecheck, lint, tests y build frontend.
- `git diff --check`.

## Criterios de aceptación

- Comprador y vendedor pueden cancelar una orden `Requested` o `Agreed`.
- Un tercero no puede cancelar ni descubrir la orden.
- Tras cancelar, el mismo comprador puede volver a solicitar sobre el aviso.
- Cada cancelación válida registra y publica su evento una sola vez.
- Una cancelación repetida es idempotente y no publica un evento adicional.
- No se incorpora pago, envío, reputación, notificación ni custodia.
- La solución mantiene límites de contexto, anti-PII y verificaciones verdes.
