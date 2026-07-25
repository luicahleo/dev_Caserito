# Cancelación de acuerdo (4B) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir que comprador o vendedor cancelen un acuerdo `Requested` o `Agreed`, liberando el aviso para una nueva solicitud, sin tocar dinero ni mercancía.

**Architecture:** Se extiende el agregado `Orden` con un estado terminal `Cancelled` y el método `Cancelar`. Se agrega un command CQRS-lite, un endpoint HTTP y UI en Mis Acuerdos. El índice único `(AvisoId, CompradorId)` pasa a filtrado para excluir canceladas; se reutiliza `EstadoOrdenCambiado`/`OrderStatusChanged` y el publicador in-process existentes.

**Tech Stack:** .NET 10, MediatR + Result + FluentValidation, EF Core sobre SQL Server (schema `orders`), Testcontainers.MsSql, React + TypeScript + MUI + TanStack Query, cliente OpenAPI generado.

**Spec:** `docs/superpowers/specs/2026-07-25-orders-4b-cancelacion-design.md`

## Global Constraints

- Ejecutar comandos backend desde `CaseritoApp/`; frontend desde `web/`.
- Clean Architecture: Domain sin dependencias salientes; Application solo de Domain; Infrastructure implementa puertos; Host compone y autoriza. Sin FK ni referencias entre bounded contexts.
- Nombres de estado persistidos y publicados estables en inglés: `Requested`, `Agreed`, `Cancelled`.
- Anti-PII: nunca registrar bodies, claims, participantes, tokens ni datos del acuerdo; errores genéricos; accesos de terceros ocultos con `404`.
- TypeScript estricto sin `any`; tipos API desde `src/api/schema.d.ts` regenerado; convertir `number | string` con `Number()`; textos visibles en español correcto.
- Warnings-as-errors, nullable, `.editorconfig`. Versiones solo en `Directory.Packages.props`.
- Cada tarea: rojo → implementación mínima → verde → autorrevisión → commit. Suite completa solo al cierre.

---

### Task 1: Estado `Cancelled` y `Orden.Cancelar` (dominio)

**Files:**
- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Domain/Ordenes/EstadoOrden.cs`
- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Domain/Ordenes/Orden.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Orders/OrdenTests.cs`

**Interfaces:**
- Consumes: `Orden.Crear`, `Orden.Aceptar`, `EstadoOrden`, `EstadoOrdenCambiado`, `ErroresOrden` (existentes).
- Produces: `EstadoOrden.Cancelled = 3`; `Result Orden.Cancelar(Guid actorId, DateTimeOffset ocurrioEn)`.

- [ ] **Step 1: Escribir los tests que fallan**

Agregar a `OrdenTests.cs`:

```csharp
[Theory]
[InlineData(true)]  // comprador
[InlineData(false)] // vendedor
public void Cancelar_desde_requested_por_participante_cambia_estado_y_emite_evento(bool porComprador)
{
    var compradorId = Guid.NewGuid();
    var vendedorId = Guid.NewGuid();
    var orden = CrearOrdenCon(compradorId, vendedorId);
    orden.LimpiarEventos();
    var actorId = porComprador ? compradorId : vendedorId;
    var ocurrioEn = DateTimeOffset.Now;

    var resultado = orden.Cancelar(actorId, ocurrioEn);

    Assert.True(resultado.EsExito);
    Assert.Equal(EstadoOrden.Cancelled, orden.Estado);
    Assert.Equal(ocurrioEn.ToUniversalTime(), orden.ActualizadaEn);
    var evento = Assert.IsType<EstadoOrdenCambiado>(Assert.Single(orden.EventosDeDominio));
    Assert.Equal(EstadoOrden.Requested, evento.EstadoAnterior);
    Assert.Equal(EstadoOrden.Cancelled, evento.EstadoNuevo);
}

[Theory]
[InlineData(true)]
[InlineData(false)]
public void Cancelar_desde_agreed_por_participante_cambia_estado_y_emite_evento(bool porComprador)
{
    var compradorId = Guid.NewGuid();
    var vendedorId = Guid.NewGuid();
    var orden = CrearOrdenCon(compradorId, vendedorId);
    Assert.True(orden.Aceptar(vendedorId, DateTimeOffset.UtcNow).EsExito);
    orden.LimpiarEventos();
    var actorId = porComprador ? compradorId : vendedorId;

    var resultado = orden.Cancelar(actorId, DateTimeOffset.UtcNow);

    Assert.True(resultado.EsExito);
    Assert.Equal(EstadoOrden.Cancelled, orden.Estado);
    var evento = Assert.IsType<EstadoOrdenCambiado>(Assert.Single(orden.EventosDeDominio));
    Assert.Equal(EstadoOrden.Agreed, evento.EstadoAnterior);
    Assert.Equal(EstadoOrden.Cancelled, evento.EstadoNuevo);
}

[Fact]
public void Cancelar_por_tercero_oculta_la_orden()
{
    var orden = CrearOrden(Guid.NewGuid());

    var resultado = orden.Cancelar(Guid.NewGuid(), DateTimeOffset.UtcNow);

    Assert.False(resultado.EsExito);
    Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
    Assert.Equal(EstadoOrden.Requested, orden.Estado);
    Assert.Empty(orden.EventosDeDominio);
}

[Fact]
public void Cancelar_reintento_es_idempotente()
{
    var compradorId = Guid.NewGuid();
    var vendedorId = Guid.NewGuid();
    var orden = CrearOrdenCon(compradorId, vendedorId);
    Assert.True(orden.Cancelar(compradorId, DateTimeOffset.UtcNow).EsExito);
    orden.LimpiarEventos();

    var resultado = orden.Cancelar(vendedorId, DateTimeOffset.UtcNow.AddMinutes(1));

    Assert.True(resultado.EsExito);
    Assert.Equal(EstadoOrden.Cancelled, orden.Estado);
    Assert.Empty(orden.EventosDeDominio);
}

[Fact]
public void Cancelar_con_actor_vacio_oculta_la_orden()
{
    var orden = CrearOrden(Guid.NewGuid());

    var resultado = orden.Cancelar(Guid.Empty, DateTimeOffset.UtcNow);

    Assert.False(resultado.EsExito);
    Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
}
```

Agregar el helper junto a `CrearOrden` existente:

```csharp
private static Orden CrearOrdenCon(Guid compradorId, Guid vendedorId)
{
    var resultado = Orden.Crear(
        Guid.NewGuid(), compradorId, vendedorId, 10m, "BOB", DateTimeOffset.UtcNow);
    Assert.True(resultado.EsExito);
    return resultado.Valor;
}
```

- [ ] **Step 2: Ejecutar los tests para verificar que fallan**

Run: `dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Orders.OrdenTests`
Expected: FAIL — `EstadoOrden.Cancelled` y `Orden.Cancelar` no existen (error de compilación).

- [ ] **Step 3: Agregar el estado**

En `EstadoOrden.cs`:

```csharp
public enum EstadoOrden
{
    Requested = 1,
    Agreed = 2,
    Cancelled = 3,
}
```

- [ ] **Step 4: Implementar `Cancelar`**

En `Orden.cs`, agregar tras `Aceptar`:

```csharp
public Result Cancelar(Guid actorId, DateTimeOffset ocurrioEn)
{
    if (!EsParticipante(actorId))
    {
        return NoEncontrada();
    }

    if (Estado == EstadoOrden.Cancelled)
    {
        return Result.Exito();
    }

    if (Estado is not (EstadoOrden.Requested or EstadoOrden.Agreed))
    {
        return Result.Fallo(new Error(
            ErroresOrden.TransicionInvalida,
            "La orden no admite esta transición."));
    }

    var fechaUtc = ocurrioEn.ToUniversalTime();
    var estadoAnterior = Estado;
    Estado = EstadoOrden.Cancelled;
    ActualizadaEn = fechaUtc;
    AgregarEvento(new EstadoOrdenCambiado(
        Id,
        estadoAnterior,
        Estado,
        fechaUtc));
    return Result.Exito();
}
```

- [ ] **Step 5: Ejecutar los tests para verificar que pasan**

Run: `dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Orders.OrdenTests`
Expected: PASS (todos los tests de `OrdenTests`).

- [ ] **Step 6: Autorrevisión**

Verificar: `EsParticipante` cubre actor vacío; idempotencia no agrega evento; sin PII; sin lógica fuera del dominio.

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Orders/CaseritoApp.Orders.Domain/Ordenes/EstadoOrden.cs \
        CaseritoApp/src/Orders/CaseritoApp.Orders.Domain/Ordenes/Orden.cs \
        CaseritoApp/tests/CaseritoApp.UnitTests/Orders/OrdenTests.cs
git commit -m "feat(orders): agrega estado y transición de cancelación"
```

---

### Task 2: Command `CancelarOrden` y filtro de listado (application)

**Files:**
- Create: `CaseritoApp/src/Orders/CaseritoApp.Orders.Application/Ordenes/CancelarOrdenCommand.cs`
- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Application/Ordenes/ListarOrdenesQuery.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Orders/CancelarOrdenCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioOrdenes` (`ObtenerAsync`), `Orden.Cancelar`, `ErroresOrden`.
- Produces: `CancelarOrdenCommand(Guid OrdenId, Guid ActorId) : ICommand`; `CancelarOrdenCommandHandler`; `CancelarOrdenCommandValidator`.

- [ ] **Step 1: Escribir el test que falla**

Crear `CancelarOrdenCommandHandlerTests.cs`:

```csharp
using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;

namespace CaseritoApp.UnitTests.Orders;

public sealed class CancelarOrdenCommandHandlerTests
{
    [Fact]
    public async Task Cancelar_por_participante_cambia_estado_y_emite_evento()
    {
        var compradorId = Guid.NewGuid();
        var orden = CrearOrden(compradorId, Guid.NewGuid());
        var handler = new CancelarOrdenCommandHandler(new RepositorioFake(orden));

        var resultado = await handler.Handle(
            new CancelarOrdenCommand(orden.Id, compradorId),
            CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoOrden.Cancelled, orden.Estado);
        Assert.IsType<EstadoOrdenCambiado>(Assert.Single(orden.EventosDeDominio));
    }

    [Fact]
    public async Task Cancelar_oculta_una_orden_ausente()
    {
        var handler = new CancelarOrdenCommandHandler(new RepositorioFake(null));

        var resultado = await handler.Handle(
            new CancelarOrdenCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
    }

    [Fact]
    public async Task Cancelar_oculta_la_orden_a_un_tercero()
    {
        var orden = CrearOrden(Guid.NewGuid(), Guid.NewGuid());
        var handler = new CancelarOrdenCommandHandler(new RepositorioFake(orden));

        var resultado = await handler.Handle(
            new CancelarOrdenCommand(orden.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresOrden.NoEncontrada, resultado.Error.Code);
        Assert.Empty(orden.EventosDeDominio);
    }

    private static Orden CrearOrden(Guid compradorId, Guid vendedorId)
    {
        var resultado = Orden.Crear(
            Guid.NewGuid(), compradorId, vendedorId, 100m, "BOB", DateTimeOffset.UtcNow);
        Assert.True(resultado.EsExito);
        resultado.Valor.LimpiarEventos();
        return resultado.Valor;
    }

    private sealed class RepositorioFake(Orden? orden) : IRepositorioOrdenes
    {
        public Task<bool> ExisteAbiertaAsync(Guid avisoId, Guid compradorId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task<Orden?> ObtenerAsync(Guid ordenId, CancellationToken ct) =>
            Task.FromResult(orden);

        public void Agregar(Orden orden)
        {
        }
    }
}
```

- [ ] **Step 2: Ejecutar el test para verificar que falla**

Run: `dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Orders.CancelarOrdenCommandHandlerTests`
Expected: FAIL — `CancelarOrdenCommand`/`CancelarOrdenCommandHandler` no existen.

- [ ] **Step 3: Implementar el command**

Crear `CancelarOrdenCommand.cs` (espejo de `AceptarOrdenCommand.cs`):

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Orders.Domain.Ordenes;
using FluentValidation;

namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record CancelarOrdenCommand(Guid OrdenId, Guid ActorId) : ICommand;

public sealed class CancelarOrdenCommandHandler(IRepositorioOrdenes repositorio)
    : ICommandHandler<CancelarOrdenCommand>
{
    public async Task<Result> Handle(
        CancelarOrdenCommand request,
        CancellationToken cancellationToken)
    {
        var orden = await repositorio.ObtenerAsync(request.OrdenId, cancellationToken);
        if (orden is null)
        {
            return Result.Fallo(new Error(
                ErroresOrden.NoEncontrada,
                "La orden no está disponible."));
        }

        return orden.Cancelar(request.ActorId, DateTimeOffset.UtcNow);
    }
}

public sealed class CancelarOrdenCommandValidator : AbstractValidator<CancelarOrdenCommand>
{
    public CancelarOrdenCommandValidator()
    {
        RuleFor(command => command.OrdenId).NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
    }
}
```

- [ ] **Step 4: Ampliar los estados permitidos en el listado**

En `ListarOrdenesQuery.cs`, en `ListarOrdenesQueryValidator`, reemplazar `_estadosPermitidos`:

```csharp
private static readonly string[] _estadosPermitidos =
    [nameof(EstadoOrden.Requested), nameof(EstadoOrden.Agreed), nameof(EstadoOrden.Cancelled)];
```

- [ ] **Step 5: Ejecutar los tests para verificar que pasan**

Run: `dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~Orders`
Expected: PASS (dominio, handlers de aceptar y cancelar, consultas).

- [ ] **Step 6: Autorrevisión**

Verificar: sin `verificado` (cancelar no requiere KYC); errores genéricos; el validator del listado ahora admite `Cancelled`.

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Orders/CaseritoApp.Orders.Application/Ordenes/CancelarOrdenCommand.cs \
        CaseritoApp/src/Orders/CaseritoApp.Orders.Application/Ordenes/ListarOrdenesQuery.cs \
        CaseritoApp/tests/CaseritoApp.UnitTests/Orders/CancelarOrdenCommandHandlerTests.cs
git commit -m "feat(orders): agrega caso de uso de cancelación"
```

---

### Task 3: Índice único filtrado y `ExisteAbiertaAsync` (infrastructure)

**Files:**
- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/ConfiguracionOrden.cs`
- Modify: `CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/RepositorioOrdenesEfCore.cs`
- Create: `CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Migrations/<timestamp>_OrdersCancelacionIndiceFiltrado.cs` (generada por EF)
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/OrdersPersistenciaTests.cs`

**Interfaces:**
- Consumes: `OrdersDbContext.Orders`, `EstadoOrden.Cancelled`, `IRepositorioOrdenes`.
- Produces: índice único filtrado `WHERE [Estado] <> 'Cancelled'`; `ExisteAbiertaAsync` que ignora canceladas.

- [ ] **Step 1: Escribir los tests que fallan**

Agregar a `OrdersPersistenciaTests.cs` (usar los helpers/fixtures existentes del archivo para crear el `OrdersDbContext` con Testcontainers; seguir el patrón de los tests de unicidad ya presentes):

```csharp
[Fact]
public async Task Puede_resolicitar_tras_cancelar_la_orden_previa()
{
    var avisoId = Guid.NewGuid();
    var compradorId = Guid.NewGuid();
    var vendedorId = Guid.NewGuid();

    await using (var contexto = CrearContexto())
    {
        var previa = Orden.Crear(
            avisoId, compradorId, vendedorId, 50m, "BOB", DateTimeOffset.UtcNow).Valor;
        previa.Cancelar(compradorId, DateTimeOffset.UtcNow);
        contexto.Orders.Add(previa);
        await contexto.SaveChangesAsync();
    }

    await using (var contexto = CrearContexto())
    {
        var nueva = Orden.Crear(
            avisoId, compradorId, vendedorId, 50m, "BOB", DateTimeOffset.UtcNow).Valor;
        contexto.Orders.Add(nueva);

        var afectados = await contexto.SaveChangesAsync();

        Assert.Equal(1, afectados);
    }
}

[Fact]
public async Task Rechaza_dos_ordenes_no_canceladas_para_el_mismo_aviso_y_comprador()
{
    var avisoId = Guid.NewGuid();
    var compradorId = Guid.NewGuid();
    var vendedorId = Guid.NewGuid();

    await using var contexto = CrearContexto();
    contexto.Orders.Add(Orden.Crear(
        avisoId, compradorId, vendedorId, 50m, "BOB", DateTimeOffset.UtcNow).Valor);
    await contexto.SaveChangesAsync();

    contexto.Orders.Add(Orden.Crear(
        avisoId, compradorId, vendedorId, 50m, "BOB", DateTimeOffset.UtcNow).Valor);

    await Assert.ThrowsAnyAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
}
```

> Nota: `CrearContexto()` es el helper del fixture existente en el archivo. Si el
> nombre difiere, usar el que ya emplean los tests de persistencia de Orders.

- [ ] **Step 2: Ejecutar los tests para verificar que fallan**

Run: `dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~OrdersPersistencia`
Expected: FAIL — la re-solicitud lanza `DbUpdateException` porque el índice aún no está filtrado.

- [ ] **Step 3: Filtrar el índice en la configuración**

En `ConfiguracionOrden.cs`, reemplazar la línea del índice único:

```csharp
entidad.HasIndex(orden => new { orden.AvisoId, orden.CompradorId })
    .IsUnique()
    .HasFilter("[Estado] <> 'Cancelled'");
```

- [ ] **Step 4: Excluir canceladas en `ExisteAbiertaAsync`**

En `RepositorioOrdenesEfCore.cs`:

```csharp
public Task<bool> ExisteAbiertaAsync(Guid avisoId, Guid compradorId, CancellationToken ct) =>
    db.Orders.AnyAsync(
        orden => orden.AvisoId == avisoId
            && orden.CompradorId == compradorId
            && orden.Estado != EstadoOrden.Cancelled,
        ct);
```

- [ ] **Step 5: Generar la migración**

Run:
```powershell
dotnet ef migrations add OrdersCancelacionIndiceFiltrado `
  --project src/Orders/CaseritoApp.Orders.Infrastructure `
  --startup-project src/Host/CaseritoApp.Host `
  --context OrdersDbContext
```
Expected: se crea la migración que hace `DropIndex` + `CreateIndex` con `filter: "[Estado] <> 'Cancelled'"` sobre `(AvisoId, CompradorId)` en el schema `orders`. Revisar que solo toque ese índice.

- [ ] **Step 6: Ejecutar los tests para verificar que pasan**

Run: `dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~OrdersPersistencia`
Expected: PASS — re-solicitud tras cancelar persiste; dos no canceladas fallan.

- [ ] **Step 7: Autorrevisión**

Verificar: la migración no altera datos ni otros índices; schema `orders`; `ExisteAbiertaAsync` coherente con el índice filtrado.

- [ ] **Step 8: Commit**

```bash
git add CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/ConfiguracionOrden.cs \
        CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Ordenes/RepositorioOrdenesEfCore.cs \
        CaseritoApp/src/Orders/CaseritoApp.Orders.Infrastructure/Migrations/
git commit -m "feat(orders): filtra el índice único al cancelar"
```

---

### Task 4: Endpoint HTTP `cancelar` e integración

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/OrdersEndpoints.cs`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/OrdersFlujoTests.cs`

**Interfaces:**
- Consumes: `CancelarOrdenCommand`, `ISender`, helpers `TryUserId`/`DesdeError`/`EsConflictoPersistencia`/`ConflictoPersistencia` (existentes en el endpoint).
- Produces: `POST /api/orders/{id:guid}/cancelar` → `204`.

- [ ] **Step 1: Escribir el test de flujo que falla**

Agregar a `OrdersFlujoTests.cs` (reutilizar los helpers del archivo para autenticar comprador/vendedor y crear/aceptar órdenes):

```csharp
[Fact]
public async Task Comprador_cancela_solicitud_y_puede_resolicitar()
{
    // Arrange: crear aviso contactable + comprador y vendedor verificados
    // usando los helpers existentes del fixture.
    var (clienteComprador, avisoId) = await PrepararAvisoYCompradorAsync();
    var creada = await SolicitarOrdenAsync(clienteComprador, avisoId);

    // Act: cancelar
    var cancelar = await clienteComprador.PostAsync(
        $"/api/orders/{creada.Id}/cancelar", content: null);

    // Assert
    Assert.Equal(HttpStatusCode.NoContent, cancelar.StatusCode);

    var resolicitar = await clienteComprador.PostAsJsonAsync(
        "/api/orders", new { avisoId });
    Assert.Equal(HttpStatusCode.Created, resolicitar.StatusCode);
}

[Fact]
public async Task Cancelar_por_un_tercero_devuelve_404()
{
    var (clienteComprador, avisoId) = await PrepararAvisoYCompradorAsync();
    var creada = await SolicitarOrdenAsync(clienteComprador, avisoId);
    var clienteTercero = await AutenticarClienteVerificadoAsync();

    var respuesta = await clienteTercero.PostAsync(
        $"/api/orders/{creada.Id}/cancelar", content: null);

    Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
}

[Fact]
public async Task Cancelar_dos_veces_es_idempotente()
{
    var (clienteComprador, avisoId) = await PrepararAvisoYCompradorAsync();
    var creada = await SolicitarOrdenAsync(clienteComprador, avisoId);

    var primera = await clienteComprador.PostAsync(
        $"/api/orders/{creada.Id}/cancelar", content: null);
    var segunda = await clienteComprador.PostAsync(
        $"/api/orders/{creada.Id}/cancelar", content: null);

    Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, segunda.StatusCode);
}
```

> Nota: `PrepararAvisoYCompradorAsync`, `SolicitarOrdenAsync` y
> `AutenticarClienteVerificadoAsync` representan los helpers ya usados por los
> tests de `OrdersFlujoTests`. Usar los nombres reales del archivo; no crear
> infraestructura nueva si ya existe.

- [ ] **Step 2: Ejecutar el test para verificar que falla**

Run: `dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~OrdersFlujo`
Expected: FAIL — `404` porque la ruta `/cancelar` no existe.

- [ ] **Step 3: Mapear el endpoint**

En `OrdersEndpoints.cs`, tras el bloque `MapPost("/{id:guid}/aceptar", ...)`:

```csharp
grupo.MapPost("/{id:guid}/cancelar", CancelarAsync)
    .RequireRateLimiting("orders-acciones")
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status429TooManyRequests);
```

Agregar el handler (espejo de `AceptarAsync`):

```csharp
private static async Task<IResult> CancelarAsync(
    Guid id,
    ClaimsPrincipal usuario,
    ISender sender,
    CancellationToken ct)
{
    if (!TryUserId(usuario, out var actorId))
    {
        return Results.Unauthorized();
    }

    try
    {
        var resultado = await sender.Send(new CancelarOrdenCommand(id, actorId), ct);
        return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
    }
    catch (Exception ex) when (EsConflictoPersistencia(ex))
    {
        return ConflictoPersistencia();
    }
    catch (ValidationException ex)
    {
        return ProblemaDeValidacion(ex);
    }
}
```

- [ ] **Step 4: Ejecutar el test para verificar que pasa**

Run: `dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~OrdersFlujo`
Expected: PASS — `204`, `404` para tercero, idempotencia, re-solicitud `201`.

- [ ] **Step 5: Autorrevisión**

Verificar: rate limit `orders-acciones`; `DesdeError` mapea `TransicionInvalida→409` y `NoEncontrada→404` (ya cubierto); Problem Details sin PII.

- [ ] **Step 6: Commit**

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/OrdersEndpoints.cs \
        CaseritoApp/tests/CaseritoApp.IntegrationTests/OrdersFlujoTests.cs
git commit -m "feat(api): expone la cancelación de acuerdos"
```

---

### Task 5: OpenAPI y cliente tipado

**Files:**
- Modify: `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` (regenerado)
- Modify: `web/src/api/schema.d.ts` (regenerado)
- Modify: `web/src/api/orders.ts`
- Test: `web/src/api/orders.test.ts`

**Interfaces:**
- Consumes: endpoint `POST /api/orders/{id}/cancelar` del Task 4; helpers `api`/`desempaquetar`.
- Produces: `cancelarOrden(ordenId: string): Promise<void>`; tipo `EstadoOrden` con `'Cancelled'`.

- [ ] **Step 1: Regenerar el contrato y los tipos**

Run:
```powershell
dotnet build CaseritoApp.sln -p:GenerateOpenApi=true
cd ..\web
npm run generate:api
```
Expected: `CaseritoApp.Host.json` incluye la ruta `/api/orders/{id}/cancelar`; `schema.d.ts` la refleja. Sin `any`.

- [ ] **Step 2: Escribir el test del cliente que falla**

Agregar a `web/src/api/orders.test.ts` (seguir el patrón de mock existente para `aceptarOrden`):

```ts
it('cancela una orden por id', async () => {
  const post = vi.fn().mockResolvedValue({ data: undefined, error: undefined });
  vi.mocked(api).POST = post as never;

  await cancelarOrden('11111111-1111-1111-1111-111111111111');

  expect(post).toHaveBeenCalledWith('/api/orders/{id}/cancelar', {
    params: { path: { id: '11111111-1111-1111-1111-111111111111' } },
  });
});
```

> Nota: usar el mismo mecanismo de mock de `api` que ya usa el test de
> `aceptarOrden` en el archivo.

- [ ] **Step 3: Ejecutar el test para verificar que falla**

Run: `npm run test -- --run src/api/orders.test.ts`
Expected: FAIL — `cancelarOrden` no está exportada.

- [ ] **Step 4: Implementar el cliente**

En `web/src/api/orders.ts`, extender el tipo y agregar la función:

```ts
export type EstadoOrden = 'Requested' | 'Agreed' | 'Cancelled';
```

```ts
export async function cancelarOrden(ordenId: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/orders/{id}/cancelar', {
      params: { path: { id: ordenId } },
    }),
  );
}
```

- [ ] **Step 5: Ejecutar el test para verificar que pasa**

Run: `npm run test -- --run src/api/orders.test.ts`
Expected: PASS.

- [ ] **Step 6: Autorrevisión**

Verificar: sin `any`; `EstadoOrden` extendido usado de forma consistente; contrato regenerado, no editado a mano.

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/artifacts/openapi/CaseritoApp.Host.json \
        web/src/api/schema.d.ts web/src/api/orders.ts web/src/api/orders.test.ts
git commit -m "feat(web): agrega cliente tipado de cancelación"
```

---

### Task 6: UI web de cancelación en Mis Acuerdos

**Files:**
- Modify: `web/src/routes/MisAcuerdosPage.tsx`
- Modify: `web/src/routes/MisAcuerdosPage.test.tsx`
- Modify (si aplica la traducción/estado): `web/src/routes/DetalleAcuerdoPage.tsx` y su test.

**Interfaces:**
- Consumes: `cancelarOrden`, `listarOrdenes`, tipo `EstadoOrden` (Task 5); TanStack Query; MUI.
- Produces: botones de cancelar/rechazar por rol y estado, diálogo de confirmación, traducción “Cancelado”.

> Antes de codificar, leer `MisAcuerdosPage.tsx` y su test para reutilizar el
> patrón de mutación/refetch y el mapa de traducción de estados ya presentes
> (`Requested→Solicitado`, `Agreed→Acordado`).

- [ ] **Step 1: Escribir el test que falla**

Agregar a `MisAcuerdosPage.test.tsx` un caso que renderice una compra `Requested`, pulse **Cancelar solicitud**, confirme en el diálogo y verifique que se llamó `cancelarOrden` y se refrescó la lista. Usar el mock del cliente y `renderConProviders` (o el helper de render existente del archivo). Cubrir además que una orden `Cancelled` muestra el chip “Cancelado” y no muestra botones de acción.

```tsx
it('el comprador cancela una solicitud tras confirmar', async () => {
  vi.mocked(listarOrdenes).mockResolvedValue({
    items: [{
      id: 'o1', avisoId: 'a1', estado: 'Requested',
      montoAcordado: 100, moneda: 'BOB', rol: 'comprador',
      actualizadaEn: '2026-07-25T00:00:00Z',
    }],
    pagina: 1, tamano: 20, total: 1,
  });
  const cancelar = vi.mocked(cancelarOrden).mockResolvedValue();

  renderMisAcuerdos();

  await userEvent.click(await screen.findByRole('button', { name: /cancelar solicitud/i }));
  await userEvent.click(await screen.findByRole('button', { name: /confirmar/i }));

  expect(cancelar).toHaveBeenCalledWith('o1');
});
```

> Nota: `renderMisAcuerdos`/`renderConProviders` y los nombres exactos de mocks
> deben tomarse del archivo de test existente.

- [ ] **Step 2: Ejecutar el test para verificar que falla**

Run: `npm run test -- --run src/routes/MisAcuerdosPage.test.tsx`
Expected: FAIL — no existe el botón ni el flujo de cancelación.

- [ ] **Step 3: Implementar la UI**

En `MisAcuerdosPage.tsx`:
- Extender el mapa de traducción: `Cancelled → 'Cancelado'`.
- Calcular la etiqueta de acción según rol/estado: comprador `Requested`→“Cancelar solicitud”, `Agreed`→“Cancelar acuerdo”; vendedor `Requested`→“Rechazar solicitud”, `Agreed`→“Cancelar acuerdo”; sin botón si `Cancelled`.
- Diálogo MUI de confirmación con botón **Confirmar** que aclara que la cancelación cierra el acuerdo y no implica pago ni penalización.
- `useMutation` que invoca `cancelarOrden` y en `onSuccess` invalida/`refetch` los listados afectados (mismo patrón que `aceptarOrden`).

- [ ] **Step 4: Ejecutar el test para verificar que pasa**

Run: `npm run test -- --run src/routes/MisAcuerdosPage.test.tsx`
Expected: PASS.

- [ ] **Step 5: Autorrevisión**

Verificar: etiquetas por rol/estado correctas; canceladas sin acciones; sin PII en errores; MUI `Grid size={{}}`/`slotProps`; textos en español correcto.

- [ ] **Step 6: Commit**

```bash
git add web/src/routes/MisAcuerdosPage.tsx web/src/routes/MisAcuerdosPage.test.tsx
git commit -m "feat(web): incorpora la cancelación de acuerdos"
```

---

### Task 7: Integración y cierre

**Files:** ninguno nuevo; solo correcciones si surgen.

- [ ] **Step 1: Revisar el diff completo contra el spec**

Verificar: transición y estado `Cancelled`; permisos por participante; índice filtrado; anti-PII; contrato backend/frontend; ausencia de QR/pago/envío/`MarkedAsSold`/`Completed`/motivo.

- [ ] **Step 2: Ejecutar las suites completas**

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
Expected: todas verdes; solo cambios del bloque 4B; artefactos derivados actualizados.

- [ ] **Step 3: Commit de cierre (si aplica)**

Solo si hubo correcciones de integración.

No hacer merge ni push. Entregar rama, commits, conteos, limitaciones y la
siguiente acción que requiera autorización.
