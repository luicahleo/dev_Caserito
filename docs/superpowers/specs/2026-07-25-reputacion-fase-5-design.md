# Fase 5 — Reputación no manipulable

Fecha: 2026-07-25  
Estado: aprobado

## Objetivo

Permitir que comprador y vendedor se califiquen después del cierre bilateral de
una orden y ofrecer una señal pública de confianza difícil de manipular.

Cada reseña queda anclada a una orden `Completed`, solo puede ser emitida una vez
por cada participante y permanece oculta hasta que ambas partes hayan reseñado.
La reputación se implementa como bounded context independiente.

## Precedencia y alcance vigente

Este spec continúa los bloques aprobados de Orders 4A, 4B y 4C. Para esta fase,
el cierre bilateral existente `MarkedAsSold → Completed` es la única condición
transaccional que habilita reputación.

`CaseritoApp/Documentacion/plan-desarrollo-mvp-v1.md` y
`CaseritoApp/Documentacion/marketplace-bolivia-mvp-brief.md` conservan referencias
históricas a QR y coordinación de envíos dentro de Fase 4. Esas referencias son
obsoletas respecto del alcance aprobado posteriormente en los specs de Orders y
no autorizan trabajo en esta fase.

Quedan expresamente fuera:

- pagos, QR, referencias o comprobantes;
- tarifas, direcciones, couriers o coordinación de envíos;
- notificaciones;
- disputas, reclamaciones o efectos de una disputa sobre reputación;
- edición, eliminación, respuesta, reporte o moderación específica de reseñas;
- crear o completar los bounded contexts `Payments`, `Shipping`,
  `Notifications` o `Disputes`;
- cambiar la máquina de estados de Orders;
- enriquecer `OrderStatusChanged` con participantes;
- revelar reseñas por vencimiento o por intervención administrativa.

## Decisiones de producto

### Elegibilidad

Una reseña solo puede crearse cuando:

- la orden existe y está en `Completed`;
- el actor autenticado es comprador o vendedor de esa orden;
- el destinatario es exactamente la contraparte;
- el actor todavía no publicó una reseña para esa orden.

El cliente solo envía puntuación y comentario. `OrderId` proviene de la ruta y
el actor del claim `sub`; autor, destinatario y rol se resuelven en el servidor.

Una orden `MarkedAsSold` no es elegible aunque el vendedor ya haya confirmado.
No se vuelve a consultar KYC: la validez de los participantes fue comprobada al
crear la orden.

### Contenido

Cada reseña contiene:

- puntuación entera de 1 a 5;
- comentario obligatorio, recortado, entre 10 y 500 caracteres;
- fecha de creación normalizada a UTC;
- rol del autor en la orden: `comprador` o `vendedor`.

Las reseñas son inmutables. No se editan ni eliminan en esta fase.

### Doble ciego

La primera reseña permanece privada. No se devuelve su puntuación o comentario
ni se incorpora al promedio público.

Cuando existe una reseña de cada participante para la misma orden, ambas quedan
reveladas en las consultas. La revelación se deriva de la existencia del par; no
requiere copiar ni coordinar un estado en otra tabla.

Si la contraparte nunca reseña, la primera permanece oculta indefinidamente. No
hay plazo, publicación unilateral ni notificación.

El autor puede consultar que ya envió su reseña, pero mientras siga ciega solo
recibe estado y fecha; no recibe nuevamente puntuación ni comentario.

### Reputación agregada

El resumen público de un usuario incluye:

- promedio de puntuaciones recibidas, redondeado a una cifra decimal;
- cantidad de reseñas recibidas y reveladas.

Solo cuentan reseñas cuyo par de la misma orden ya existe. Si no hay reseñas
reveladas, el promedio es `null` y la cantidad es cero.

No se separan promedios de comprador y vendedor en esta fase. Cada reseña pública
indica el rol del autor, pero no su nombre ni identificador.

## Arquitectura

### Bounded context Reputation

Se completan los proyectos existentes:

```text
Reputation.Domain
Reputation.Application
Reputation.Infrastructure
```

`Reputation.Domain` contiene el agregado `Resena`. `Reputation.Application`
contiene casos de uso, puertos y DTO. `Reputation.Infrastructure` implementa
persistencia y consultas sobre `ReputationDbContext`.

No se añaden referencias de proyecto de Reputation hacia Orders, Identity o
Catalog. Los identificadores externos son GUID opacos y no existen FK cruzadas.

### Integración con Orders

Application define un puerto equivalente a:

```text
IConsultaOrdenCalificable.ObtenerAsync(orderId, actorId)
    -> OrdenCalificable(orderId, autorId, destinatarioId, rolAutor)
```

El Host implementa el puerto mediante una consulta mínima de Orders. La consulta
solo devuelve resultado cuando la orden está `Completed` y el actor es
participante. Ausencia, estado no elegible y tercero se traducen al mismo error
genérico.

Reputation no consume `OrderStatusChanged`: el contrato existente omite
participantes deliberadamente y no debe ampliarse. La comprobación síncrona al
crear la reseña evita replicar datos incompletos y conserva los límites de los
contextos.

### Perfil público

Identity ofrece una consulta pública mínima por usuario:

```text
PerfilPublico(Id, Nombre, Ciudad, Verificado)
```

No incluye email, roles, permisos, información KYC ni estado interno. El Host
compone este perfil con el resumen de Reputation sin crear dependencias entre
ambos contextos.

Catalog añade únicamente `VendedorId` al detalle público de un aviso. Es un
identificador opaco usado por la web para enlazar `/usuarios/{id}`; Catalog no
consulta ni replica nombre o reputación.

## Modelo de dominio

### Agregado `Resena`

Datos:

```text
Id
OrderId
AutorId
DestinatarioId
RolAutor
Puntuacion
Comentario
CreadaEn
Version
```

Invariantes:

- ningún GUID requerido puede estar vacío;
- autor y destinatario deben ser distintos;
- `RolAutor` solo admite `comprador` o `vendedor`;
- puntuación entera entre 1 y 5;
- comentario normalizado entre 10 y 500 caracteres;
- fecha UTC;
- la reseña no cambia después de crearse.

La existencia previa para `(OrderId, AutorId)` se comprueba en Application y se
garantiza además con un índice único en persistencia.

### Errores públicos

Códigos estables:

- `reputacion_orden_no_disponible`: orden inexistente, no completada o actor no
  participante;
- `reputacion_resena_duplicada`: el actor ya reseñó esa orden;
- `reputacion_resena_invalida`: puntuación o comentario inválidos;
- `reputacion_usuario_no_disponible`: perfil público inexistente.

Los mensajes son genéricos y no revelan participantes, estado de la orden,
existencia de una reseña ajena ni contenido.

## Casos de uso

### Crear reseña

1. El Host deriva el actor de `sub`.
2. Se aplica rate limiting por actor.
3. Reputation consulta la elegibilidad mediante su puerto.
4. Comprueba si ya existe `(OrderId, AutorId)`.
5. El dominio valida y crea la reseña.
6. Se persiste dentro de la unidad de trabajo de Reputation.
7. Devuelve `201` para la creación efectiva.

La carrera entre dos solicitudes del mismo actor se resuelve por el índice único
y ambas rutas se traducen al mismo `409`.

No se admite una clave de idempotencia separada: la identidad natural de la
operación es `(OrderId, AutorId)`. Repetir después del éxito responde `409` y no
expone la reseña.

### Consultar estado de una orden

Solo un participante autenticado puede consultar:

- si la orden está habilitada para calificar;
- si el actor ya calificó;
- si la contraparte ya calificó;
- si el par está revelado;
- fecha del envío propio, si existe;
- identificador público opaco de la contraparte.

No devuelve el contenido de ninguna reseña. Un tercero obtiene `404`.

### Consultar reputación pública

El perfil público es anónimo y devuelve:

- datos públicos mínimos de Identity;
- resumen agregado de Reputation;
- primera página de reseñas reveladas o un endpoint paginado enlazado.

Las reseñas se ordenan por fecha descendente y luego por ID para estabilidad.
Cada elemento expone puntuación, comentario, fecha y rol del autor. No expone
`OrderId`, `AutorId` ni `DestinatarioId`.

Paginación:

- página inicial `1`;
- tamaño predeterminado `10`;
- máximo `50`.

## Persistencia

Schema: `reputation`.

Tabla `Reviews`:

| Columna | Regla |
|---|---|
| `Id` | PK GUID |
| `OrderId` | GUID requerido, sin FK |
| `AuthorId` | GUID requerido, sin FK |
| `RecipientId` | GUID requerido, sin FK |
| `AuthorRole` | texto estable, longitud máxima 10 |
| `Rating` | entero requerido |
| `Comment` | texto requerido, longitud máxima 500 |
| `CreatedAt` | UTC requerido |
| `Version` | `rowversion` |

Índices:

- único `(OrderId, AuthorId)`;
- `(OrderId, RecipientId)` para resolver el par;
- `(RecipientId, CreatedAt, Id)` para perfil y agregado.

La migración inicial de Reputation crea únicamente este schema y tabla. No
modifica tablas de Orders, Identity o Catalog.

## Contrato HTTP

Grupo autenticado `/api/reputacion`:

```text
GET  /ordenes/{orderId}
POST /ordenes/{orderId}/resenas
```

Grupo público:

```text
GET /api/publico/usuarios/{userId}
GET /api/publico/usuarios/{userId}/resenas?pagina=1&tamano=10
```

Solicitud de creación:

```json
{
  "puntuacion": 5,
  "comentario": "Cumplió con todo lo acordado."
}
```

Respuestas:

- `201`: reseña creada;
- `200`: consultas;
- `400`: validación sintáctica o de dominio;
- `401`: creación o estado sin autenticación;
- `404`: orden, participación o perfil no disponible;
- `409`: reseña duplicada o conflicto equivalente;
- `429`: límite excedido.

Los DTO y artefactos OpenAPI usan nombres españoles consistentes con la API
actual. La web consume exclusivamente los tipos generados.

## Rate limiting

Políticas:

- creación: ventana fija de 10 intentos por usuario y hora, sin cola;
- estado autenticado: 120 consultas por usuario y minuto;
- perfiles y reseñas públicas: 120 consultas por partición y minuto.

El límite reduce automatización accidental; la unicidad y la elegibilidad son las
garantías contra manipulación.

## UI web

### Detalle de acuerdo

Para una orden `Completed`:

- consulta el estado de Reputation;
- si el actor no calificó, muestra puntuación 1–5, comentario y confirmación de
  envío irreversible;
- si ya calificó y falta la contraparte, muestra “Tu reseña está oculta hasta
  que la otra parte califique”;
- si ambas existen, muestra que las reseñas ya son públicas;
- ofrece enlace al perfil público de la contraparte.

No muestra el formulario antes de `Completed`. Los errores son genéricos y no
revelan el contenido de la reseña contraria.

### Perfil público

Ruta:

```text
/usuarios/:id
```

Muestra:

- nombre, ciudad y badge de verificación;
- promedio con una cifra decimal y cantidad de reseñas;
- listado paginado de reseñas reveladas;
- estado vacío cuando todavía no hay reputación pública.

El detalle público de aviso enlaza el perfil mediante `VendedorId`. No se muestra
email ni información privada.

## Seguridad y anti-PII

- El actor siempre se deriva del token.
- El body nunca acepta autor, destinatario, rol o estado.
- GUID de usuarios y órdenes se tratan como referencias opacas.
- Nunca se registran puntuaciones, comentarios, bodies, IDs de participantes,
  órdenes, avisos, claims, tokens, emails ni datos KYC.
- Los logs contienen solo nombre de operación y resultado genérico.
- Los errores de autorización usan `404` para impedir enumerar participación.
- React renderiza comentarios como texto; no se usa HTML sin procesar.
- El perfil público excluye email, roles, permisos y material KYC.
- Las consultas públicas solo devuelven reseñas reveladas.

Los comentarios son contenido generado por usuarios y pueden contener datos
personales introducidos por ellos. Esta fase minimiza su exposición mediante el
doble ciego y la exclusión absoluta de logs, pero no incorpora moderación
específica. Esa capacidad requiere un bloque posterior aprobado.

## Concurrencia

Casos cubiertos:

- dos envíos simultáneos del mismo autor producen una sola fila;
- envíos simultáneos de ambas partes producen dos filas válidas;
- ninguna consulta observa una sola reseña como pública;
- después de persistir ambas, ambas aparecen en el agregado;
- una orden no completada durante la consulta de elegibilidad no crea reseña.

Las consultas de revelación se basan en `EXISTS` sobre el par y no requieren una
actualización coordinada.

## Estrategia de pruebas

### Dominio

- creación válida y normalización;
- puntuaciones fuera de 1–5;
- comentario vacío, corto o mayor de 500;
- GUID vacíos, participantes iguales y rol inválido;
- fecha normalizada a UTC.

### Application

- orden completada y participante crea reseña;
- orden no disponible, estado incorrecto o tercero reciben error genérico;
- autor y destinatario se derivan del puerto;
- duplicado detectado;
- estado propio no filtra contenido;
- agregado y listado incluyen solo pares completos;
- promedio, conteo, orden y paginación.

### Persistencia

- schema y configuración;
- índice único bajo concurrencia;
- consultas de par y destinatario;
- migración y aislamiento sin FK cruzadas.

### Host e integración

- flujo bilateral de Orders seguido por ambas reseñas;
- primera reseña permanece privada;
- segunda revela ambas y actualiza promedio;
- `400`, `401`, `404`, `409` y `429`;
- terceros no descubren participación;
- perfil público excluye email y datos KYC;
- OpenAPI refleja el contrato;
- logs no contienen comentario ni identificadores sensibles.

### Frontend

- formulario solo para `Completed`;
- validación 1–5 y 10–500;
- envío irreversible y estados de espera/revelación;
- enlace a contraparte;
- perfil público, agregado, paginación y estado vacío;
- detalle público de aviso enlaza al vendedor;
- errores genéricos y conversiones de tipos OpenAPI.

### Regresión

- flujos de Orders 4A–4C;
- perfil privado y autenticación;
- descubrimiento y detalle de avisos;
- aislamiento arquitectónico entre contextos;
- anti-PII.

## Criterios de aceptación

- Solo los dos participantes de una orden `Completed` pueden reseñarse.
- Cada participante puede crear una sola reseña por orden.
- La puntuación es 1–5 y el comentario obligatorio tiene 10–500 caracteres.
- Una reseña unilateral nunca es pública ni afecta el promedio.
- Al existir ambas reseñas, las dos son públicas y cuentan en el agregado.
- El perfil público muestra datos mínimos, promedio y reseñas reveladas.
- El detalle de acuerdo permite calificar y refleja el estado doble ciego.
- El detalle público de aviso enlaza al perfil del vendedor.
- No hay dependencias directas ni FK entre Reputation y otros contextos.
- No se exponen ni registran PII o contenido de reseñas.
- No se incorpora pago, QR, envío, notificaciones ni disputas.
