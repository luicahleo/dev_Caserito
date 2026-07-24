# Plan: Fase 4A — acuerdo básico de compra

**Spec:** `docs/superpowers/specs/2026-07-24-orders-4a-acuerdo-basico-design.md`

Ejecutar desde `CaseritoApp/` salvo las tareas web. Cada tarea sigue
rojo → implementación mínima → verde → autorrevisión → commit.

## 1. Agregado y máquina de estados

**Entradas:** `AggregateRoot`, `Result`, spec aprobado.

**Crear:**

- `src/Orders/CaseritoApp.Orders.Domain/Ordenes/Orden.cs`
- `src/Orders/CaseritoApp.Orders.Domain/Ordenes/EstadoOrden.cs`
- `src/Orders/CaseritoApp.Orders.Domain/Ordenes/EventosOrden.cs`
- `src/Orders/CaseritoApp.Orders.Domain/Ordenes/ErroresOrden.cs`
- `tests/CaseritoApp.UnitTests/Orders/OrdenTests.cs`

**Comportamiento:**

- `Orden.Crear` valida IDs, participantes, monto, moneda y UTC; produce
  `Requested` y `OrdenSolicitada`.
- `Aceptar` solo permite al vendedor y desde `Requested`.
- Un reintento del vendedor sobre `Agreed` es éxito idempotente sin evento.
- Un tercero recibe error no encontrada.
- La transición registra `EstadoOrdenCambiado`.

**Rojo esperado:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Orders.OrdenTests
```

Falla por tipos inexistentes.

**Verde esperado:** todos los tests dirigidos pasan.

**Commit:** `feat(orders): modela solicitud y aceptación de orden`

## 2. Casos de uso y puertos

**Entradas:** agregado de tarea 1 y contratos CQRS.

**Crear:**

- `src/Orders/CaseritoApp.Orders.Application/Ordenes/IRepositorioOrdenes.cs`
- `src/Orders/CaseritoApp.Orders.Application/Ordenes/IConsultaOrdenes.cs`
- `src/Orders/CaseritoApp.Orders.Application/Ordenes/IConsultaAvisoParaOrden.cs`
- `src/Orders/CaseritoApp.Orders.Application/Ordenes/IConsultaVerificacionParticipante.cs`
- `src/Orders/CaseritoApp.Orders.Application/Ordenes/DtosOrden.cs`
- `src/Orders/CaseritoApp.Orders.Application/Ordenes/SolicitarOrdenCommand.cs`
- `src/Orders/CaseritoApp.Orders.Application/Ordenes/AceptarOrdenCommand.cs`
- `src/Orders/CaseritoApp.Orders.Application/Ordenes/ListarOrdenesQuery.cs`
- `src/Orders/CaseritoApp.Orders.Application/Ordenes/ObtenerOrdenQuery.cs`
- tests unitarios de handlers en `tests/CaseritoApp.UnitTests/Orders/`.

**Interfaces producidas:**

- Referencia contactable con aviso, vendedor, monto y moneda.
- Consulta de verificación por ID opaco.
- Repositorio de escritura y consulta paginada restringida por participante.

**Comportamiento:**

- Solicitar deriva la instantánea de los puertos, comprueba ambos verificados y
  rechaza duplicados.
- Aceptar oculta orden ausente/ajena y publica `OrderStatusChanged` solo tras
  una transición real.
- Queries nunca aceptan un usuario distinto al actor.
- Validators acotan IDs, rol, estado, página y tamaño.

**Rojo esperado:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Orders
```

Falla por commands/handlers inexistentes.

**Verde esperado:** dominio y handlers pasan.

**Commit:** `feat(orders): agrega casos de uso del acuerdo`

## 3. Persistencia SQL Server

**Entradas:** agregado y puertos.

**Crear/modificar:**

- `src/Orders/CaseritoApp.Orders.Infrastructure/OrdersDbContext.cs`
- `src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/ConfiguracionOrden.cs`
- `src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/RepositorioOrdenesEfCore.cs`
- `src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/ConsultaOrdenesEfCore.cs`
- `src/Orders/CaseritoApp.Orders.Infrastructure/UnitOfWorkOrders.cs`
- `src/Orders/CaseritoApp.Orders.Infrastructure/DependencyInjection.cs`
- migración inicial y snapshot de Orders.
- tests de persistencia dirigidos.

**Comportamiento:**

- Schema `orders`, `decimal(18,2)`, strings acotados, `rowversion`.
- Índice único `(AvisoId, CompradorId)`.
- Registro condicional con la conexión configurada.
- Excepciones de unicidad y concurrencia se traducen en Application/Host.

**Rojo esperado:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~OrdersPersistencia
```

Falla por tabla, mapping o servicios inexistentes.

**Verde esperado:** persistencia, unicidad y concurrencia pasan con Testcontainers.

**Commit:** `feat(orders): persiste acuerdos en schema propio`

## 4. Adaptadores entre contextos y eventos

**Entradas:** puertos de Orders, consultas públicas de Catalog e Identity.

**Modificar/crear:**

- ampliar `ReferenciaAvisoContactableDto` y su proyección pública con monto,
  moneda y vendedor.
- adaptador Host Catalog → Orders.
- adaptador Host Identity → Orders.
- publicador de integración reutilizable o implementación Orders sin PII.
- tests unitarios de adaptadores y arquitectura.

**Comportamiento:**

- Catalog solo entrega avisos activos, visibles y contactables.
- Identity responde únicamente un booleano de verificación.
- Ninguna referencia directa entre bounded contexts.
- `OrderStatusChanged` conserva exactamente el contrato de Fase 0.

**Rojo esperado:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Orders
dotnet test tests/CaseritoApp.ArchitectureTests
```

**Verde esperado:** adaptadores y aislamiento pasan.

**Commit:** `feat(host): conecta orders mediante adaptadores`

## 5. Contrato HTTP e integración

**Entradas:** casos de uso e infraestructura.

**Crear/modificar:**

- `src/Host/CaseritoApp.Host/Endpoints/OrdersEndpoints.cs`
- `src/Host/CaseritoApp.Host/Program.cs`
- rate limits `orders-crear`, `orders-acciones`, `orders-consultas`.
- `tests/CaseritoApp.IntegrationTests/OrdersFlujoTests.cs`
- infraestructura de factory solo si es necesario.

**Contratos:**

- `POST /api/orders`
- `POST /api/orders/{id}/aceptar`
- `GET /api/orders`
- `GET /api/orders/{id}`

**Comportamiento:**

- Actor derivado del claim `sub`; verificación derivada del claim y confirmada
  por el puerto cuando corresponda.
- `201` con Location, `204`, `400`, `401`, `403`, `404`, `409`, `429`.
- Problem Details genéricos y sin PII.
- Migración de Orders al arranque junto a los otros contextos.

**Rojo esperado:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~OrdersFlujo
```

Falla con `404` o servicios inexistentes.

**Verde esperado:** flujo completo y aislamiento pasan.

**Commit:** `feat(api): expone acuerdos de compra`

## 6. OpenAPI y cliente tipado

**Entradas:** endpoints verdes.

**Modificar derivados:**

- `artifacts/openapi/CaseritoApp.Host.json`
- `web/src/api/schema.d.ts`
- `web/src/api/orders.ts`
- tests del cliente API.

**Comandos:**

```powershell
dotnet build CaseritoApp.sln -p:GenerateOpenApi=true
cd ..\web
npm run generate:api
npm run test -- --run src/api/orders.test.ts
```

**Resultado esperado:** contrato y cliente tipado sin `any`.

**Commit:** `feat(web): agrega cliente tipado de acuerdos`

## 7. UI web del acuerdo

**Entradas:** cliente tipado y patrones de rutas existentes.

**Crear/modificar:**

- `web/src/routes/MisAcuerdosPage.tsx`
- `web/src/routes/MisAcuerdosPage.test.tsx`
- `web/src/routes/DetalleAcuerdoPage.tsx`
- `web/src/routes/DetalleAcuerdoPage.test.tsx`
- `web/src/routes/DetalleAvisoPage.tsx` y test.
- `web/src/app/router.tsx`, navegación y tests.

**Comportamiento:**

- CTA **Proponer compra** con advertencia de que no paga ni reserva.
- Compras/ventas con estados “Solicitado” y “Acordado”.
- Vendedor acepta una solicitud.
- Carga, vacío, error genérico y acciones según rol/estado.

**Rojo esperado:**

```powershell
npm run test -- --run src/routes/DetalleAvisoPage.test.tsx src/routes/MisAcuerdosPage.test.tsx src/routes/DetalleAcuerdoPage.test.tsx
```

**Verde esperado:** flujo UI dirigido pasa.

**Commit:** `feat(web): incorpora flujo de acuerdos`

## 8. Integración y cierre

**Revisar:** diff completo contra spec, límites, PII, concurrencia, contrato y
ausencia de QR/envío/reputación.

**Ejecutar:**

```powershell
cd CaseritoApp
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
cd ..\web
npm run typecheck
npm run lint
npm run test -- --run
npm run build
cd ..
git diff --check
git status --short --branch
```

**Resultado esperado:** todas las verificaciones verdes, artefactos derivados
actualizados y solo cambios del bloque 4A.

**Commit:** correcciones de integración únicamente si son necesarias.

No hacer merge ni push. Entregar rama, commits, conteos, limitaciones y la
siguiente acción que requiera autorización.
