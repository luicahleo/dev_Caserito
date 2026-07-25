# Cierre positivo del acuerdo (4C) Implementation Plan

> **For agentic workers:** ejecutar una tarea por vez con TDD. No ampliar el
> alcance sin actualizar primero el spec aprobado.

**Goal:** Permitir que el vendedor marque una orden acordada como vendida, retire
el aviso de Catalog, cancele las órdenes competidoras y permita que el comprador
complete bilateralmente el acuerdo.

**Architecture:** Orders incorpora `MarkedAsSold` y `Completed`, fechas de
confirmación e idempotencia. Catalog incorpora `Vendido` y la referencia opaca
`OrdenVentaId`. El Host orquesta commits separados y reintentables mediante
commands/queries públicos de Application; no hay FK, acceso cruzado a schemas,
outbox, bus ni Saga. `OrderStatusChanged` conserva su contrato.

**Tech Stack:** .NET 10, MediatR + Result + FluentValidation, EF Core/SQL Server,
Testcontainers.MsSql, React + TypeScript estricto, MUI, TanStack Query y cliente
OpenAPI generado.

**Spec:** `docs/superpowers/specs/2026-07-25-orders-4c-cierre-positivo-design.md`

## Restricciones globales

- Ejecutar backend desde `CaseritoApp/` y frontend desde `web/`.
- Estados persistidos/publicados: `Requested`, `Agreed`, `Cancelled`,
  `MarkedAsSold`, `Completed`.
- `MarkedAsSold` es confirmación del vendedor; `Completed` exige además la del
  comprador. No implica pago ni entrega.
- Preservar cancelación 4B, re-solicitud sobre avisos disponibles y sus eventos.
- Orders y Catalog solo se conectan mediante Application y composición Host.
- No registrar IDs de participantes, aviso/orden, precio, contenido, pagos,
  direcciones, claims, tokens ni bodies.
- Tercero o actor incorrecto para la acción recibe `404`.
- No modificar detalle/contacto ni SignalR salvo pruebas de regresión.
- Cada tarea: test rojo, implementación mínima, test verde, autorrevisión y
  commit lógico.

---

## Task 1: Estados y cierre bilateral en el dominio Orders

**Files:**

- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Domain/Ordenes/EstadoOrden.cs`
- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Domain/Ordenes/Orden.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Orders/OrdenTests.cs`

**Produces:**

```csharp
EstadoOrden.MarkedAsSold = 4
EstadoOrden.Completed = 5
DateTimeOffset? Orden.MarcadaVendidaEn
DateTimeOffset? Orden.CompradorConfirmoEn
DateTimeOffset? Orden.CompletadaEn
Result Orden.MarcarComoVendida(Guid actorId, DateTimeOffset ocurrioEn)
Result Orden.ConfirmarCierre(Guid actorId, DateTimeOffset ocurrioEn)
```

- [ ] Añadir tests de `MarcarComoVendida`:
  - vendedor desde `Agreed` cambia a `MarkedAsSold`;
  - registra UTC en `MarcadaVendidaEn` y `ActualizadaEn`;
  - emite exactamente un `EstadoOrdenCambiado`;
  - comprador, tercero y actor vacío obtienen `NoEncontrada`;
  - `Requested` y `Cancelled` producen `TransicionInvalida`;
  - reintento del vendedor en `MarkedAsSold` y `Completed` es éxito sin cambiar
    fechas ni emitir evento.
- [ ] Añadir tests de `ConfirmarCierre`:
  - comprador desde `MarkedAsSold` cambia a `Completed`;
  - `CompradorConfirmoEn`, `CompletadaEn` y `ActualizadaEn` comparten UTC;
  - emite exactamente un `EstadoOrdenCambiado`;
  - vendedor, tercero y actor vacío obtienen `NoEncontrada`;
  - estados anteriores producen `TransicionInvalida`;
  - reintento del comprador en `Completed` es éxito sin cambiar fechas ni evento.
- [ ] Añadir regresiones: `Aceptar` y `Cancelar` rechazan los estados nuevos;
  `Cancelled` conserva su reintento idempotente.

**Rojo:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Orders.OrdenTests
```

Esperado: falla por estados, propiedades y métodos inexistentes.

- [ ] Implementar los valores al final del enum, propiedades nullable y métodos.
  Usar un helper privado para registrar la transición sin duplicar lógica; no
  aceptar flags de confirmación desde fuera.

**Verde:** repetir el comando; todos los tests de `OrdenTests` pasan.

**Autorrevisión:** irreversibilidad, actores, UTC, idempotencia, un evento por
transición y ausencia de semántica de pago/entrega.

**Commit:** `feat(orders): modela cierre bilateral del acuerdo`

---

## Task 2: Caso de uso Orders y cancelación de competidoras

**Files:**

- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Application/Ordenes/IRepositorioOrdenes.cs`
- Create: `CaseritoApp/src/Orders/CaseritoApp.Orders.Application/Ordenes/MarcarOrdenVendidaCommand.cs`
- Create: `CaseritoApp/src/Orders/CaseritoApp.Orders.Application/Ordenes/ConfirmarCierreOrdenCommand.cs`
- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Application/Ordenes/ListarOrdenesQuery.cs`
- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Application/Ordenes/DtosOrden.cs`
- Create: `CaseritoApp/tests/CaseritoApp.UnitTests/Orders/MarcarOrdenVendidaCommandHandlerTests.cs`
- Create: `CaseritoApp/tests/CaseritoApp.UnitTests/Orders/ConfirmarCierreOrdenCommandHandlerTests.cs`
- Modify fakes: `CaseritoApp/tests/CaseritoApp.UnitTests/Orders/*HandlerTests.cs`

**Interfaces:**

```csharp
Task<IReadOnlyList<Orden>> ObtenerAbiertasPorAvisoAsync(
    Guid avisoId, Guid excluirOrdenId, CancellationToken ct);

sealed record ResultadoOrdenMarcadaVendida(Guid AvisoId);
sealed record MarcarOrdenVendidaCommand(Guid OrdenId, Guid ActorId)
    : ICommand<ResultadoOrdenMarcadaVendida>;
sealed record ConfirmarCierreOrdenCommand(Guid OrdenId, Guid ActorId) : ICommand;
```

- [ ] Escribir tests del handler de marcar vendido:
  - orden ausente → `NoEncontrada`;
  - vendedor marca la ganadora y obtiene su `AvisoId`;
  - cancela competidoras `Requested` y `Agreed` usando el vendedor común;
  - ignora `Cancelled`, `MarkedAsSold` y `Completed`;
  - una competidora genera su propio evento;
  - reintento sobre ganadora no vuelve a cancelar ni emitir;
  - actor incorrecto no consulta/muta competidoras.
- [ ] Escribir tests del handler de confirmar:
  - delega al agregado;
  - orden ausente y actor incorrecto → `NoEncontrada`;
  - reintento completado es idempotente.
- [ ] Actualizar todos los fakes de `IRepositorioOrdenes` con la nueva consulta.
- [ ] Extender el validador del listado con ambos estados.
- [ ] Extender `OrdenDetalleDto` con:

```csharp
DateTimeOffset? MarcadaVendidaEn,
DateTimeOffset? CompradorConfirmoEn,
DateTimeOffset? CompletadaEn
```

**Rojo:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Orders
```

Esperado: falla por commands y método de repositorio inexistentes.

- [ ] Implementar handlers y validators. El handler debe mutar todos los
  agregados antes de que `UnitOfWorkBehavior` guarde, para obtener una única
  transacción de Orders.

**Verde:** repetir el comando; todos los unit tests de Orders pasan.

**Autorrevisión:** la orden elegida debe estar `Agreed`; no existe reserva al
aceptar; el resultado solo expone `AvisoId`, nunca participantes.

**Commit:** `feat(orders): cierra venta y cancela órdenes competidoras`

---

## Task 3: Persistencia, consultas y carreras de Orders

**Files:**

- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/ConfiguracionOrden.cs`
- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/RepositorioOrdenesEfCore.cs`
- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/ConsultaOrdenesEfCore.cs`
- Create: `CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Migrations/<timestamp>_OrdersCierrePositivo.cs`
- Modify generated: `CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Migrations/OrdersDbContextModelSnapshot.cs`
- Modify: `CaseritoApp/tests/CaseritoApp.IntegrationTests/OrdersPersistenciaTests.cs`

- [ ] Añadir tests de mapping para las tres fechas nullable y estados como string.
- [ ] Añadir test que una operación carga ganadora y competidoras en tracking y
  persiste `MarkedAsSold` más todas las `Cancelled` en un commit.
- [ ] Añadir carrera entre dos órdenes `Agreed` del mismo aviso: solo una puede
  quedar `MarkedAsSold`; la otra termina cancelada o la petición perdedora
  obtiene conflicto.
- [ ] Añadir proyecciones del detalle para las fechas nuevas.

**Rojo:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~OrdersPersistencia
```

Esperado: falla por columnas/mapping/consulta inexistentes.

- [ ] Configurar las tres propiedades nullable.
- [ ] Implementar `ObtenerAbiertasPorAvisoAsync` con tracking, estados
  `Requested | Agreed` y exclusión del `OrdenId` ganador.
- [ ] Mantener intacto el índice filtrado
  `WHERE [Estado] <> 'Cancelled'`.
- [ ] Actualizar proyecciones.
- [ ] Generar migración:

```powershell
dotnet ef migrations add OrdersCierrePositivo `
  --project src/Orders/CaseritoApp.Orders.Infrastructure `
  --startup-project src/Host/CaseritoApp.Host `
  --context OrdersDbContext
```

Esperado: agrega únicamente las tres columnas nullable y actualiza snapshot; no
renumera ni transforma filas existentes.

**Verde:** repetir el test dirigido.

**Autorrevisión:** schema `orders`, sin FK, filas previas compatibles, índice de
4B intacto y carreras cubiertas con `rowversion`.

**Commit:** `feat(orders): persiste estados de cierre positivo`

---

## Task 4: Estado vendido e invariantes en el dominio Catalog

**Files:**

- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/EstadoAviso.cs`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Aviso.cs`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/EventosAviso.cs`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/ErroresAviso.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/AvisoTests.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/AvisoFotosTests.cs`

**Produces:**

```csharp
EstadoAviso.Vendido
Guid? Aviso.OrdenVentaId
Result Aviso.MarcarVendido(
    Guid ordenId, Guid vendedorId, DateTime ocurrioEn)
AvisoMarcadoVendido(Guid AvisoId, Guid OrdenId, DateTime OcurridoEn)
```

- [ ] Tests desde `Activo` y `Pausado`: cambia a `Vendido`, asigna orden,
  normaliza UTC, actualiza fecha y emite un evento.
- [ ] Misma orden: éxito idempotente sin fecha/evento nuevo.
- [ ] Orden diferente sobre vendido: conflicto estable
  `avisos.venta_incompatible`.
- [ ] Dueño incorrecto, IDs vacíos y estados eliminados: error genérico según
  spec; moderación eliminada impide vender.
- [ ] Estado vendido rechaza editar, pausar, reactivar, eliminar, agregar y
  quitar fotos.
- [ ] Estado de moderación oculto se conserva al vender.

**Rojo:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Catalog.Aviso
```

Esperado: falla por estado, propiedad, evento y método inexistentes.

- [ ] Implementar `Vendido` al final, `OrdenVentaId` nullable y el método.
- [ ] Centralizar la comprobación de estado terminal comercial para que todas las
  mutaciones del dueño/fotos rechacen `Vendido` sin alterar reglas de moderación.

**Verde:** repetir tests dirigidos de `Aviso`.

**Autorrevisión:** vendido irreversible; moderación independiente; sin lógica de
Orders ni PII dentro de Catalog.

**Commit:** `feat(catalog): incorpora estado terminal vendido`

---

## Task 5: Casos de uso y persistencia de venta en Catalog

**Files:**

- Create: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ValidarAvisoParaVentaQuery.cs`
- Create: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/MarcarAvisoVendidoCommand.cs`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAviso.cs`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConfiguracionCatalog.cs`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Fotos/ConsultaFotoPublicaEfCore.cs`
- Create: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Migrations/<timestamp>_CatalogAvisoVendido.cs`
- Modify generated: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Migrations/CatalogDbContextModelSnapshot.cs`
- Create: `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/MarcarAvisoVendidoHandlerTests.cs`
- Modify: `CaseritoApp/tests/CaseritoApp.IntegrationTests/AvisosFlujoTests.cs`
- Modify: `CaseritoApp/tests/CaseritoApp.IntegrationTests/DescubrimientoAvisosTests.cs`
- Modify: `CaseritoApp/tests/CaseritoApp.IntegrationTests/FotosAvisoIntegrationTests.cs`

**Interfaces:**

```csharp
sealed record ValidarAvisoParaVentaQuery(Guid AvisoId, Guid VendedorId)
    : IQuery<Result>;
sealed record MarcarAvisoVendidoCommand(
    Guid AvisoId, Guid OrdenId, Guid VendedorId) : ICommand;
```

- [ ] Tests unitarios para preflight y command: ausente/dueño incorrecto se
  ocultan, estados inválidos dan conflicto, misma orden es idempotente.
- [ ] Tests de integración:
  - vendido desaparece de búsqueda, detalle, foto y referencia contactable;
  - aparece en `Mis avisos` como `Vendido`;
  - nueva solicitud no puede obtener referencia contactable;
  - concurrencia misma orden converge; orden distinta da conflicto;
  - mutaciones del dueño sobre vendido devuelven `409`.

**Rojo:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Catalog.MarcarAvisoVendido
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~AvisosFlujo|FullyQualifiedName~DescubrimientoAvisos|FullyQualifiedName~FotosAviso"
```

Esperado: falla por casos de uso, columna y estado inexistentes.

- [ ] Implementar query y command sobre `IRepositorioAvisos`.
- [ ] Mapear `OrdenVentaId` nullable y configurar `Estado` y `OrdenVentaId` como
  tokens de concurrencia para detectar carreras sin añadir FK.
- [ ] Mantener filtros públicos explícitos en `Activo + Visible`; añadir
  regresiones aunque ya excluyan naturalmente `Vendido`.
- [ ] Incluir `Vendido` en proyecciones privadas; no exponer `OrdenVentaId` en
  DTO públicos ni web.
- [ ] Generar migración:

```powershell
dotnet ef migrations add CatalogAvisoVendido `
  --project src/Catalog/CaseritoApp.Catalog.Infrastructure `
  --startup-project src/Host/CaseritoApp.Host `
  --context CatalogDbContext
```

Esperado: agrega `OrdenVentaId` nullable y actualiza el snapshot; ninguna FK.

**Verde:** repetir los comandos dirigidos.

**Autorrevisión:** Catalog decide sus invariantes; las consultas públicas no
filtran por tablas Orders; no se expone la asociación interna.

**Commit:** `feat(catalog): persiste y oculta avisos vendidos`

---

## Task 6: Orquestación Host, endpoints y reparación de fallo parcial

**Files:**

- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Orders/IOrquestadorCierreOrden.cs`
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Orders/OrquestadorCierreOrden.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/OrdersEndpoints.cs`
- Modify: `CaseritoApp/tests/CaseritoApp.IntegrationTests/OrdersFlujoTests.cs`
- Modify: `CaseritoApp/tests/CaseritoApp.IntegrationTests/OrdersAdaptadoresTests.cs`
- Modify: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/Layering/AislamientoEntreContextosTests.cs`

**Contract:**

```http
POST /api/orders/{id:guid}/marcar-vendido
POST /api/orders/{id:guid}/confirmar-completado
```

Ambos sin body, `204`, rate limit `orders-acciones`.

**Orquestación exacta de marcar vendido:**

1. Derivar `ActorId` de `sub`.
2. Enviar el `ObtenerOrdenQuery` existente, exigir resultado con rol
   `vendedor` y tomar su `AvisoId`; cualquier otro actor se traduce a
   `NoEncontrada`, sin mutar.
3. Enviar `ValidarAvisoParaVentaQuery` a Catalog.
4. Enviar `MarcarOrdenVendidaCommand`; al terminar el pipeline, Orders ya está
   confirmado y sus `OrderStatusChanged` ya fueron publicados.
5. Enviar `MarcarAvisoVendidoCommand`.
6. Si el paso 5 no puede asegurarse por fallo técnico, devolver `503` genérico.
7. En reintento, el paso 4 devuelve éxito idempotente y siempre se repite el
   paso 5.

El orquestador usa `ISender`; no recibe `DbContext`, repositorios ni referencia
de Infrastructure de otro contexto. No registrar argumentos.

- [ ] Tests de flujo:
  - vendedor marca `Agreed` y obtiene `204`;
  - comprador/vendedor incorrecto/tercero obtienen `404`;
  - `Requested`/`Cancelled` y aviso incompatible obtienen `409`;
  - marca ganadora y cancela competidoras;
  - evento para ganadora y cada competidora, sin duplicados;
  - Catalog queda `Vendido` por la orden ganadora;
  - dos órdenes concurrentes producen una sola ganadora;
  - repetición devuelve `204`.
- [ ] Inyectar un fallo controlado en la fase Catalog:
  - primer intento confirma Orders y devuelve `503`;
  - segundo intento asegura Catalog y devuelve `204`;
  - no duplica eventos ni fechas.
- [ ] Tests de confirmar:
  - comprador desde `MarkedAsSold` → `204` y `Completed`;
  - vendedor/tercero → `404`;
  - estado inválido → `409`;
  - repetición y concurrencia equivalente → `204`.
- [ ] Verificar explícitamente `400`, `401`, `429` y Problem Details genéricos.
- [ ] Arquitectura: Orders no referencia Catalog, Catalog no referencia Orders,
  y solo Host referencia ambas Application.

**Rojo:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~Orders
dotnet test tests/CaseritoApp.ArchitectureTests --filter FullyQualifiedName~AislamientoEntreContextos
```

Esperado: endpoints/orquestador inexistentes.

- [ ] Implementar orquestador y registrar servicio scoped.
- [ ] Añadir handlers HTTP y traducción:
  - `NoEncontrada` → `404`;
  - transición/venta incompatible/concurrencia no convergente → `409`;
  - fase Catalog pendiente tras commit Orders → `503`;
  - validación → `400`;
  - rate limit → `429`.
- [ ] No capturar `OperationCanceledException`. Cualquier wrapper técnico del
  fallo parcial debe contener solo mensaje genérico y excepción interna, sin
  IDs.

**Verde:** repetir integración Orders y arquitectura.

**Autorrevisión:** el `503` solo aparece cuando repetir puede reparar; una
incompatibilidad estable es `409`; tercero siempre `404`.

**Commit:** `feat(host): orquesta cierre positivo entre orders y catalog`

---

## Task 7: OpenAPI y cliente web tipado

**Files:**

- Modify generated: `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`
- Modify generated: `web/src/api/schema.d.ts`
- Modify: `web/src/api/orders.ts`
- Modify: `web/src/api/orders.test.ts`
- Modify if derived contract changes: `web/src/api/avisos.ts`

- [ ] Generar OpenAPI:

```powershell
dotnet build CaseritoApp.sln -p:GenerateOpenApi=true
```

- [ ] Regenerar tipos desde `web/`:

```powershell
npm run generate:api
```

- [ ] Extender `EstadoOrden` con `MarkedAsSold | Completed`.
- [ ] Añadir:

```ts
marcarOrdenVendida(ordenId: string): Promise<void>
confirmarCierreOrden(ordenId: string): Promise<void>
```

- [ ] Tests de rutas, método POST, ausencia de body y propagación genérica del
  error `503`.

**Rojo/verde:**

```powershell
npm run test -- --run src/api/orders.test.ts
npm run typecheck
```

**Autorrevisión:** cero `any`, tipos desde schema, fechas nullable y sin
`OrdenVentaId` expuesto.

**Commit:** `feat(web): agrega contrato tipado de cierre de acuerdos`

---

## Task 8: Experiencia web de cierre positivo

**Files:**

- Modify: `web/src/routes/MisAcuerdosPage.tsx`
- Modify: `web/src/routes/MisAcuerdosPage.test.tsx`
- Modify: `web/src/routes/DetalleAcuerdoPage.tsx`
- Modify: `web/src/routes/DetalleAcuerdoPage.test.tsx`
- Modify: `web/src/routes/MisAvisosPage.tsx`
- Modify: `web/src/routes/MisAvisosPage.test.tsx`
- Regression test: `web/src/routes/DetalleAvisoPage.test.tsx`

- [ ] Tests en `MisAcuerdosPage`:
  - vendedor `Agreed` ve **Marcar como vendido**;
  - diálogo informa irreversibilidad, cancelación de competidoras y ausencia de
    confirmación de pago/entrega;
  - comprador no ve la acción;
  - `MarkedAsSold`/`Completed` no muestran cancelar;
  - etiquetas “Marcado como vendido” y “Completado”.
- [ ] Tests en detalle:
  - vendedor `MarkedAsSold`: espera confirmación, sin acción;
  - comprador `MarkedAsSold`: **Confirmar cierre** y diálogo correcto;
  - `Completed`: “Completado por ambas partes”;
  - fechas se muestran solo cuando existen;
  - error `503`: mensaje genérico reintentable, sin afirmar rollback.
- [ ] Tests de invalidación:
  - marcar vendido invalida `order`, `orders`, `mis-avisos`,
    `avisos-publicos`, `aviso-publico` y fotos/detalle relacionados según las
    query keys reales;
  - confirmar invalida `order` y `orders`.
- [ ] `MisAvisosPage` traduce `Vendido` y oculta editar/pausar/reactivar/eliminar.
- [ ] Regresión: detalle público vendido no ofrece contacto ni nueva orden.

**Rojo:**

```powershell
npm run test -- --run src/routes/MisAcuerdosPage.test.tsx src/routes/DetalleAcuerdoPage.test.tsx src/routes/MisAvisosPage.test.tsx src/routes/DetalleAvisoPage.test.tsx
```

Esperado: fallan acciones, textos y estados nuevos.

- [ ] Implementar mutations/dialogs y centralizar traducción/acciones para no
  dispersar condiciones por estado.
- [ ] Mantener textos en español UTF-8 y advertencia permanente:
  “Caserito no verifica el pago ni la entrega”.

**Verde:** repetir tests dirigidos, `npm run typecheck` y `npm run lint`.

**Autorrevisión:** ninguna acción incorrecta por rol/estado; no se muestra
información de participantes; el título del aviso puede usar el fallback actual
cuando el detalle público ya no exista.

**Commit:** `feat(web): incorpora cierre bilateral de acuerdos`

---

## Task 9: Regresión transversal e integración final

**Files:**

- Modify only if a regression requires it:
  - `CaseritoApp/tests/CaseritoApp.IntegrationTests/ObtenerAvisoContactableTests.cs`
  - `CaseritoApp/tests/CaseritoApp.IntegrationTests/ChatFlujoTests.cs`
  - `CaseritoApp/tests/CaseritoApp.IntegrationTests/ChatTiempoRealTests.cs`
  - `CaseritoApp/tests/CaseritoApp.ArchitectureTests/PiiRedactionTests.cs`
  - tests vecinos ya enumerados.

- [ ] Añadir/confirmar regresiones:
  - `Cancelled` permite re-solicitar si el aviso continúa disponible;
  - vendido no permite re-solicitar;
  - aceptar/cancelar conservan idempotencia previa;
  - `OrderStatusChanged` mantiene exactamente sus cinco campos;
  - logs y eventos no contienen PII ni datos del acuerdo;
  - conversaciones existentes siguen accesibles tras vender;
  - proxy SignalR y autorización de participantes no cambian.
- [ ] Revisar diff completo contra spec: estados, permisos, fallos parciales,
  bounded contexts, migraciones, UI y fuera de alcance.
- [ ] Ejecutar desde `CaseritoApp/`:

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

- [ ] Ejecutar desde `web/`:

```powershell
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

- [ ] Ejecutar desde la raíz:

```powershell
git diff --check
git status --short --branch
```

**Resultado esperado:** suites verdes, migraciones separadas por contexto,
OpenAPI/tipos actualizados y ningún cambio de pago, envío, reputación,
notificaciones, disputas, contacto o SignalR fuera de las regresiones necesarias.

**Commit:** solo correcciones de integración necesarias, con mensaje específico.

No hacer push ni merge. Entregar commits, cantidades de tests, limitaciones y la
siguiente acción que requiera autorización.
