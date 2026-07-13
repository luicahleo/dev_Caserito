# Fase 0 — Fundaciones técnicas (bloque fundacional) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Levantar el esqueleto del monolito modular: BuildingBlocks (Result, behaviors MediatR, contratos, abstracciones de PII), los 6 bounded contexts vacíos y delimitados, sus DbContext con schema propio, y el host Web API con `/health` — todo compilando con rigor estricto y tests verdes.

**Architecture:** Monolito modular .NET 10 / Clean Architecture. CQRS-lite con MediatR (Result<T> para errores esperados, FluentValidation, pipeline behaviors). EF Core sobre SQL Server, una BD con schema por contexto (sin FK entre schemas). Eventos de dominio in-process en el mismo commit. Los contextos se referencian por Id, nunca por assembly ajeno.

**Tech Stack:** .NET 10, C# latest, MediatR, FluentValidation, EF Core 10 + SQL Server, ASP.NET Core (host), xUnit + NetArchTest + Microsoft.AspNetCore.Mvc.Testing.

## Global Constraints

- **Rigor estricto** (heredado del andamiaje): `Nullable=enable`, `TreatWarningsAsErrors=true`, analizadores; nada compila si viola reglas.
- **CPM**: toda versión de paquete en `CaseritoApp/Directory.Packages.props`; ningún `.csproj` con `Version=`.
- **Nombres y comentarios en español**; identificadores de framework en su forma original.
- **Clean Architecture**: `Application → Domain`; `Infrastructure → Application`; dependencias hacia adentro. Ningún contexto referencia el ensamblado de otro contexto (se comunican por Id / contratos).
- **Constantes en PascalCase**; naming del `.editorconfig` vigente.
- **La solución debe compilar y `dotnet test` pasar en CADA commit.**
- **MediatR** pasó a modelo comercial en versiones recientes: fija una versión cuya licencia sea aceptable para uso comercial (o la última MIT, v12.x) y anótalo. Las versiones NuGet indicadas son piso; si `restore` falla, sube a la última estable compatible con .NET 10.
- **El host no debe conectarse a SQL Server en el arranque** (registra DbContexts pero no migra ni abre conexión), para que `/health` y los tests de integración no requieran una BD.
- Rutas .NET relativas a `CaseritoApp/`.

## Fuera de alcance de este plan (documentado en el spec; implementación en Fase 1+)

- **Implementación de RBAC** (tablas de permisos/roles, policies): es una decisión de diseño del spec, pero su implementación necesita entidades del contexto Identity → Fase 1. Aquí solo se dejan los contextos y BuildingBlocks listos.
- **Cableado concreto de PII/KYC** (entidades, blob storage real, KMS/envelope real, auditoría persistida): aquí solo quedan las abstracciones (`IEncryptor`, `IPiiAccessAuditor`) y `PassthroughEncryptor` de dev. La implementación real llega con Identity/Fase 1 y la decisión de hosting.
- **Despacho real de domain events desde agregados**: el `UnitOfWorkBehavior` guarda cambios; conectar la recolección de eventos de los agregados al `IDomainEventDispatcher` se hace cuando existan agregados (Fase 1). La abstracción ya queda definida.
- Modelo de datos detallado por contexto, framework móvil, outbox, OCR.

---

### Task 1: Paquetes en CPM

**Files:**
- Modify: `CaseritoApp/Directory.Packages.props`

**Interfaces:**
- Produces: versiones centralizadas para MediatR, FluentValidation, EF Core (+ SqlServer, Design) y Mvc.Testing, disponibles a las tareas siguientes vía `PackageReference` sin `Version=`.

- [ ] **Step 1: Añadir los `PackageVersion`**

En el `<ItemGroup>` de paquetes de `CaseritoApp/Directory.Packages.props`, añade (revisa/eleva a la última estable compatible con .NET 10 si el restore falla; para MediatR fija una versión de licencia aceptable):
```xml
    <PackageVersion Include="MediatR" Version="12.4.1" />
    <PackageVersion Include="FluentValidation" Version="11.10.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.10.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
```

- [ ] **Step 2: Verificar restore**

Run (desde `CaseritoApp/`):
```bash
dotnet restore CaseritoApp.sln
```
Expected: restore exitoso (exit 0). Si una versión no existe, súbela a la última estable y re-corre.

- [ ] **Step 3: Commit**

```bash
git add CaseritoApp/Directory.Packages.props
git commit -m "chore: versiones CPM para MediatR, FluentValidation, EF Core y testing"
```

---

### Task 2: BuildingBlocks.Domain (Result, eventos, bases de entidad)

**Files:**
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain/CaseritoApp.BuildingBlocks.Domain.csproj`
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain/Error.cs`
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain/Result.cs`
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain/IDomainEvent.cs`
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain/Entity.cs`
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain/AggregateRoot.cs`
- Test: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/BuildingBlocks/ResultTests.cs`
- Test: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/BuildingBlocks/AggregateRootTests.cs`

**Interfaces:**
- Produces:
  - `Error(string Code, string Message)` (record); `Error.None`.
  - `Result` con `bool EsExito`, `Error Error`, factories `Result.Exito()`, `Result.Fallo(Error)`.
  - `Result<T>` con `T? Valor`, `Result<T>.Exito(T)`, `Result<T>.Fallo(Error)`, conversión implícita desde `T`.
  - `IDomainEvent : MediatR.INotification`.
  - `Entity` (Id `Guid`); `AggregateRoot` con `IReadOnlyCollection<IDomainEvent> EventosDeDominio`, `AgregarEvento(IDomainEvent)`, `LimpiarEventos()`.

- [ ] **Step 1: Crear el proyecto y referenciarlo**

Run (desde `CaseritoApp/`):
```bash
cd CaseritoApp
dotnet new classlib -n CaseritoApp.BuildingBlocks.Domain -o src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain
dotnet sln add src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain
rm src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain/Class1.cs
dotnet add tests/CaseritoApp.ArchitectureTests reference src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain
```
`IDomainEvent` usa `MediatR.INotification`, así que el proyecto Domain referencia MediatR. Edita `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain/CaseritoApp.BuildingBlocks.Domain.csproj` para añadir (sin `Version=`):
```xml
  <ItemGroup>
    <PackageReference Include="MediatR" />
  </ItemGroup>
```

- [ ] **Step 2: Escribir los tests (RED)**

`CaseritoApp/tests/CaseritoApp.ArchitectureTests/BuildingBlocks/ResultTests.cs`:
```csharp
using CaseritoApp.BuildingBlocks.Domain;
using Xunit;

namespace CaseritoApp.ArchitectureTests.BuildingBlocks;

public sealed class ResultTests
{
    [Fact]
    public void Exito_marca_es_exito_y_error_none()
    {
        var resultado = Result.Exito();
        Assert.True(resultado.EsExito);
        Assert.Equal(Error.None, resultado.Error);
    }

    [Fact]
    public void Fallo_marca_no_exito_y_conserva_error()
    {
        var error = new Error("codigo", "mensaje");
        var resultado = Result.Fallo(error);
        Assert.False(resultado.EsExito);
        Assert.Equal(error, resultado.Error);
    }

    [Fact]
    public void ResultT_exito_expone_valor()
    {
        Result<int> resultado = 42;
        Assert.True(resultado.EsExito);
        Assert.Equal(42, resultado.Valor);
    }

    [Fact]
    public void ResultT_fallo_no_es_exito()
    {
        var resultado = Result<int>.Fallo(new Error("x", "y"));
        Assert.False(resultado.EsExito);
    }
}
```

`CaseritoApp/tests/CaseritoApp.ArchitectureTests/BuildingBlocks/AggregateRootTests.cs`:
```csharp
using CaseritoApp.BuildingBlocks.Domain;
using Xunit;

namespace CaseritoApp.ArchitectureTests.BuildingBlocks;

public sealed class AggregateRootTests
{
    private sealed record EventoPrueba : IDomainEvent;

    private sealed class AgregadoPrueba : AggregateRoot
    {
        public void Hacer() => AgregarEvento(new EventoPrueba());
    }

    [Fact]
    public void Agrega_y_limpia_eventos_de_dominio()
    {
        var agregado = new AgregadoPrueba();
        agregado.Hacer();
        Assert.Single(agregado.EventosDeDominio);

        agregado.LimpiarEventos();
        Assert.Empty(agregado.EventosDeDominio);
    }
}
```

- [ ] **Step 3: Verificar RED**

Run: `dotnet test CaseritoApp.sln`
Expected: FAIL (no compila: tipos no existen).

- [ ] **Step 4: Implementar**

`Error.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Domain;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
```

`Result.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Domain;

public class Result
{
    protected Result(bool esExito, Error error)
    {
        EsExito = esExito;
        Error = error;
    }

    public bool EsExito { get; }
    public Error Error { get; }

    public static Result Exito() => new(true, Error.None);
    public static Result Fallo(Error error) => new(false, error);

    public static Result<T> Exito<T>(T valor) => Result<T>.Exito(valor);
    public static Result<T> Fallo<T>(Error error) => Result<T>.Fallo(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _valor;

    private Result(bool esExito, T? valor, Error error) : base(esExito, error) => _valor = valor;

    public T Valor => EsExito
        ? _valor!
        : throw new InvalidOperationException("No se puede acceder al valor de un resultado fallido.");

    public static new Result<T> Exito(T valor) => new(true, valor, Error.None);
    public static new Result<T> Fallo(Error error) => new(false, default, error);

    public static implicit operator Result<T>(T valor) => Exito(valor);
}
```

`IDomainEvent.cs`:
```csharp
using MediatR;

namespace CaseritoApp.BuildingBlocks.Domain;

public interface IDomainEvent : INotification;
```

`Entity.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Domain;

public abstract class Entity
{
    protected Entity() => Id = Guid.NewGuid();
    protected Entity(Guid id) => Id = id;

    public Guid Id { get; protected init; }
}
```

`AggregateRoot.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _eventos = [];

    protected AggregateRoot() { }
    protected AggregateRoot(Guid id) : base(id) { }

    public IReadOnlyCollection<IDomainEvent> EventosDeDominio => _eventos.AsReadOnly();

    protected void AgregarEvento(IDomainEvent evento) => _eventos.Add(evento);

    public void LimpiarEventos() => _eventos.Clear();
}
```

- [ ] **Step 5: Verificar GREEN**

Run: `dotnet test CaseritoApp.sln`
Expected: PASS. Si un analizador se queja del código, corrígelo de forma mínima (sin bajar severidades).

- [ ] **Step 6: Commit**

```bash
git add CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain CaseritoApp/tests CaseritoApp/CaseritoApp.sln
git commit -m "feat(bb): BuildingBlocks.Domain (Result, IDomainEvent, Entity/AggregateRoot)"
```

---

### Task 3: BuildingBlocks.Application (abstracciones CQRS + behaviors)

**Files:**
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/CaseritoApp.BuildingBlocks.Application.csproj`
- Create: `.../Messaging/ICommand.cs`, `.../Messaging/IQuery.cs`
- Create: `.../Abstractions/IUnitOfWork.cs`, `.../Abstractions/IDomainEventDispatcher.cs`
- Create: `.../Behaviors/ValidationBehavior.cs`, `.../Behaviors/LoggingBehavior.cs`, `.../Behaviors/UnitOfWorkBehavior.cs`
- Test: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/BuildingBlocks/UnitOfWorkBehaviorTests.cs`
- Test: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/BuildingBlocks/ValidationBehaviorTests.cs`

**Interfaces:**
- Consumes: `CaseritoApp.BuildingBlocks.Domain`, MediatR, FluentValidation.
- Produces:
  - `ICommand : IRequest<Result>`, `ICommand<TResponse> : IRequest<Result<TResponse>>`, `IQuery<TResponse> : IRequest<TResponse>` y sus handlers (`ICommandHandler<...>`, `IQueryHandler<...>`).
  - `IUnitOfWork { Task<int> GuardarCambiosAsync(CancellationToken) }`.
  - `IDomainEventDispatcher { Task PublicarAsync(IEnumerable<IDomainEvent>, CancellationToken) }`.
  - `ValidationBehavior<TRequest,TResponse>`, `LoggingBehavior<TRequest,TResponse>`, `UnitOfWorkBehavior<TRequest,TResponse>` (todos `IPipelineBehavior`). El `UnitOfWorkBehavior` ejecuta el handler, luego `GuardarCambiosAsync` (una vez), y NO despacha eventos por su cuenta en este esqueleto (los agregados se conectarán en la fase de features); el dispatcher se registra y se prueba de forma aislada.

- [ ] **Step 1: Crear proyecto y referencias**

Run (desde `CaseritoApp/`):
```bash
cd CaseritoApp
dotnet new classlib -n CaseritoApp.BuildingBlocks.Application -o src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application
dotnet sln add src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application
rm src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Class1.cs
dotnet add src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application reference src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain
dotnet add tests/CaseritoApp.ArchitectureTests reference src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application
```
Añade a `CaseritoApp.BuildingBlocks.Application.csproj` (sin `Version=`):
```xml
  <ItemGroup>
    <PackageReference Include="MediatR" />
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>
```
Y añade `Microsoft.Extensions.Logging.Abstractions` a `Directory.Packages.props` como `PackageVersion` (usa la versión que traiga .NET 10, p. ej. `10.0.0`).

- [ ] **Step 2: Escribir tests (RED)**

`.../BuildingBlocks/ValidationBehaviorTests.cs`:
```csharp
using CaseritoApp.BuildingBlocks.Application.Behaviors;
using FluentValidation;
using MediatR;
using Xunit;

namespace CaseritoApp.ArchitectureTests.BuildingBlocks;

public sealed class ValidationBehaviorTests
{
    private sealed record Comando(string Nombre) : IRequest<string>;

    private sealed class ComandoValidator : AbstractValidator<Comando>
    {
        public ComandoValidator() => RuleFor(c => c.Nombre).NotEmpty();
    }

    [Fact]
    public async Task Lanza_validation_exception_cuando_es_invalido()
    {
        var behavior = new ValidationBehavior<Comando, string>([new ComandoValidator()]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new Comando(""), () => Task.FromResult("ok"), CancellationToken.None));
    }

    [Fact]
    public async Task Continua_cuando_es_valido()
    {
        var behavior = new ValidationBehavior<Comando, string>([new ComandoValidator()]);

        var resultado = await behavior.Handle(new Comando("x"), () => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", resultado);
    }
}
```

`.../BuildingBlocks/UnitOfWorkBehaviorTests.cs`:
```csharp
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Application.Behaviors;
using MediatR;
using Xunit;

namespace CaseritoApp.ArchitectureTests.BuildingBlocks;

public sealed class UnitOfWorkBehaviorTests
{
    private sealed record Comando : IRequest<string>;

    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        public int Llamadas { get; private set; }
        public Task<int> GuardarCambiosAsync(CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(0);
        }
    }

    [Fact]
    public async Task Guarda_cambios_una_vez_tras_el_handler()
    {
        var uow = new UnitOfWorkFake();
        var behavior = new UnitOfWorkBehavior<Comando, string>(uow);

        var resultado = await behavior.Handle(new Comando(), () => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", resultado);
        Assert.Equal(1, uow.Llamadas);
    }
}
```

- [ ] **Step 3: Verificar RED**

Run: `dotnet test CaseritoApp.sln` → FAIL (no compila).

- [ ] **Step 4: Implementar**

`Messaging/ICommand.cs`:
```csharp
using CaseritoApp.BuildingBlocks.Domain;
using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Messaging;

public interface ICommand : IRequest<Result>;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
```

`Messaging/IQuery.cs`:
```csharp
using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Messaging;

public interface IQuery<TResponse> : IRequest<TResponse>;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
```

`Abstractions/IUnitOfWork.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> GuardarCambiosAsync(CancellationToken ct);
}
```

`Abstractions/IDomainEventDispatcher.cs`:
```csharp
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.BuildingBlocks.Application.Abstractions;

public interface IDomainEventDispatcher
{
    Task PublicarAsync(IEnumerable<IDomainEvent> eventos, CancellationToken ct);
}
```

`Behaviors/ValidationBehavior.cs`:
```csharp
using FluentValidation;
using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validadores)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var contexto = new ValidationContext<TRequest>(request);
        var fallos = validadores
            .Select(v => v.Validate(contexto))
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        if (fallos.Count != 0)
        {
            throw new ValidationException(fallos);
        }

        return await next();
    }
}
```

`Behaviors/LoggingBehavior.cs`:
```csharp
using MediatR;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.BuildingBlocks.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var nombre = typeof(TRequest).Name;
        logger.LogInformation("Procesando {Request}", nombre);
        var respuesta = await next();
        logger.LogInformation("Procesado {Request}", nombre);
        return respuesta;
    }
}
```

`Behaviors/UnitOfWorkBehavior.cs`:
```csharp
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Behaviors;

public sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var respuesta = await next();
        await unitOfWork.GuardarCambiosAsync(cancellationToken);
        return respuesta;
    }
}
```

> Nota de diseño: `ValidationBehavior` lanza `ValidationException`; la traducción a un `Result`/ProblemDetails para el cliente se hace en el host (Task 8). Los errores de dominio esperados usan `Result<T>` en los handlers. Esta separación (validación de entrada vía pipeline, reglas de dominio vía Result) se refinará por feature.

- [ ] **Step 5: Verificar GREEN**

Run: `dotnet test CaseritoApp.sln` → PASS.

- [ ] **Step 6: Commit**

```bash
git add CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application CaseritoApp/tests CaseritoApp/Directory.Packages.props CaseritoApp/CaseritoApp.sln
git commit -m "feat(bb): BuildingBlocks.Application (CQRS markers + behaviors + UoW)"
```

---

### Task 4: BuildingBlocks.Contracts (contratos de integración)

**Files:**
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Contracts/CaseritoApp.BuildingBlocks.Contracts.csproj`
- Create: `.../IntegrationEvent.cs`
- Create: `.../Orders/OrderStatusChanged.cs`
- Create: `.../Identity/UserVerified.cs`
- Create: `.../Catalog/ProductPublished.cs`

**Interfaces:**
- Produces: marcador `IIntegrationEvent` y los `record` de contrato que consumirán Payments/Shipping/Disputes/otros contextos en el futuro. Es un proyecto sin dependencias (solo BCL).

- [ ] **Step 1: Crear proyecto**

```bash
cd CaseritoApp
dotnet new classlib -n CaseritoApp.BuildingBlocks.Contracts -o src/BuildingBlocks/CaseritoApp.BuildingBlocks.Contracts
dotnet sln add src/BuildingBlocks/CaseritoApp.BuildingBlocks.Contracts
rm src/BuildingBlocks/CaseritoApp.BuildingBlocks.Contracts/Class1.cs
```

- [ ] **Step 2: Crear los contratos**

`IntegrationEvent.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Contracts;

/// <summary>Marcador de evento de integración entre bounded contexts.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OcurridoEn { get; }
}
```

`Orders/OrderStatusChanged.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Contracts.Orders;

public sealed record OrderStatusChanged(
    Guid EventId,
    DateTimeOffset OcurridoEn,
    Guid OrderId,
    string OldStatus,
    string NewStatus) : IIntegrationEvent;
```

`Identity/UserVerified.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Contracts.Identity;

public sealed record UserVerified(
    Guid EventId,
    DateTimeOffset OcurridoEn,
    Guid UserId) : IIntegrationEvent;
```

`Catalog/ProductPublished.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Contracts.Catalog;

public sealed record ProductPublished(
    Guid EventId,
    DateTimeOffset OcurridoEn,
    Guid ProductId,
    Guid SellerId) : IIntegrationEvent;
```

- [ ] **Step 3: Verificar compila**

Run (desde `CaseritoApp/`): `dotnet build CaseritoApp.sln`
Expected: 0 warnings / 0 errors.

- [ ] **Step 4: Commit**

```bash
git add CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Contracts CaseritoApp/CaseritoApp.sln
git commit -m "feat(bb): BuildingBlocks.Contracts (eventos de integración)"
```

---

### Task 5: BuildingBlocks.Infrastructure + migración de PiiRedaction

**Files:**
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/CaseritoApp.BuildingBlocks.Infrastructure.csproj`
- Create: `.../Security/IEncryptor.cs`
- Create: `.../Security/PassthroughEncryptor.cs` (impl. de desarrollo; la real se cablea al elegir KMS)
- Create: `.../Security/IPiiAccessAuditor.cs`
- Create: `.../Logging/PiiRedaction.cs` (movido desde SmokeLib)
- Delete: `CaseritoApp/src/CaseritoApp.SmokeLib/Logging/PiiRedaction.cs`
- Move: test `PiiRedactionTests.cs` para apuntar al nuevo namespace
- Test: `.../BuildingBlocks/EncryptorTests.cs`

**Interfaces:**
- Consumes: nada de contextos.
- Produces: `IEncryptor { string Cifrar(string); string Descifrar(string) }` (+ `PassthroughEncryptor` para dev), `IPiiAccessAuditor { Task RegistrarAccesoAsync(string recurso, string actor, CancellationToken) }`, y `PiiRedaction` (misma API que antes) ahora en `CaseritoApp.BuildingBlocks.Infrastructure.Logging`.

- [ ] **Step 1: Crear proyecto y referencias**

```bash
cd CaseritoApp
dotnet new classlib -n CaseritoApp.BuildingBlocks.Infrastructure -o src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure
dotnet sln add src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure
rm src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/Class1.cs
dotnet add src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure reference src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application
dotnet add tests/CaseritoApp.ArchitectureTests reference src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure
```

- [ ] **Step 2: Crear abstracciones y helpers**

`Security/IEncryptor.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Infrastructure.Security;

/// <summary>Cifra/descifra campos sensibles (envelope encryption). La implementación
/// real se cablea al elegir KMS/hosting; en dev se usa PassthroughEncryptor.</summary>
public interface IEncryptor
{
    string Cifrar(string textoPlano);
    string Descifrar(string textoCifrado);
}
```

`Security/PassthroughEncryptor.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Infrastructure.Security;

/// <summary>Implementación de desarrollo: NO cifra. Prohibido en producción;
/// se reemplaza por envelope encryption real al definir el KMS.</summary>
public sealed class PassthroughEncryptor : IEncryptor
{
    public string Cifrar(string textoPlano) => textoPlano;
    public string Descifrar(string textoCifrado) => textoCifrado;
}
```

`Security/IPiiAccessAuditor.cs`:
```csharp
namespace CaseritoApp.BuildingBlocks.Infrastructure.Security;

/// <summary>Registra en un log append-only cada acceso a PII sensible.</summary>
public interface IPiiAccessAuditor
{
    Task RegistrarAccesoAsync(string recurso, string actor, CancellationToken ct);
}
```

`Logging/PiiRedaction.cs`: copia el contenido de `src/CaseritoApp.SmokeLib/Logging/PiiRedaction.cs` cambiando el namespace a `CaseritoApp.BuildingBlocks.Infrastructure.Logging` (el resto idéntico: `CamposProhibidos` y `Redactar`).

- [ ] **Step 3: Migrar el test y borrar el original**

Mueve `tests/CaseritoApp.ArchitectureTests/PiiRedactionTests.cs` para que use `using CaseritoApp.BuildingBlocks.Infrastructure.Logging;` (cambia solo el `using`, conserva todos los casos). Luego:
```bash
git rm src/CaseritoApp.SmokeLib/Logging/PiiRedaction.cs
```

- [ ] **Step 4: Test del encryptor (dev)**

`.../BuildingBlocks/EncryptorTests.cs`:
```csharp
using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using Xunit;

namespace CaseritoApp.ArchitectureTests.BuildingBlocks;

public sealed class EncryptorTests
{
    [Fact]
    public void Passthrough_hace_roundtrip()
    {
        IEncryptor encryptor = new PassthroughEncryptor();
        var original = "12345678";
        Assert.Equal(original, encryptor.Descifrar(encryptor.Cifrar(original)));
    }
}
```

- [ ] **Step 5: Verificar GREEN**

Run: `dotnet test CaseritoApp.sln`
Expected: PASS (los tests de PII siguen pasando desde el nuevo namespace; SmokeLib ya no tiene PiiRedaction pero mantiene `SampleEntity`/`SampleService` para `LayeringTests`, que se sustituyen en la Task 8).

- [ ] **Step 6: Commit**

```bash
git add CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure CaseritoApp/tests CaseritoApp/src/CaseritoApp.SmokeLib CaseritoApp/CaseritoApp.sln
git commit -m "feat(bb): BuildingBlocks.Infrastructure (encryptor, auditor) + migrar PiiRedaction"
```

---

### Task 6: Crear los 6 bounded contexts (esqueleto de capas)

**Files (patrón repetido para cada `<Ctx>` en: Identity, Catalog, Chat, Orders, Reputation, Notifications):**
- Create: `CaseritoApp/src/<Ctx>/CaseritoApp.<Ctx>.Domain/CaseritoApp.<Ctx>.Domain.csproj`
- Create: `CaseritoApp/src/<Ctx>/CaseritoApp.<Ctx>.Application/CaseritoApp.<Ctx>.Application.csproj`
- Create: `CaseritoApp/src/<Ctx>/CaseritoApp.<Ctx>.Infrastructure/CaseritoApp.<Ctx>.Infrastructure.csproj`

**Interfaces:**
- Produces: 18 proyectos (6 contextos × 3 capas), agregados a la solución, compilando, vacíos de features. `Domain` referencia `BuildingBlocks.Domain`; `Application` referencia su `Domain` + `BuildingBlocks.Application`; `Infrastructure` referencia su `Application` + `BuildingBlocks.Infrastructure`.

- [ ] **Step 1: Generar los 18 proyectos**

Ejecuta este bucle desde `CaseritoApp/` (crea proyectos, borra `Class1.cs`, cablea referencias hacia adentro + BuildingBlocks, y agrega a la solución):
```bash
cd CaseritoApp
for Ctx in Identity Catalog Chat Orders Reputation Notifications; do
  dotnet new classlib -n CaseritoApp.$Ctx.Domain         -o src/$Ctx/CaseritoApp.$Ctx.Domain
  dotnet new classlib -n CaseritoApp.$Ctx.Application     -o src/$Ctx/CaseritoApp.$Ctx.Application
  dotnet new classlib -n CaseritoApp.$Ctx.Infrastructure  -o src/$Ctx/CaseritoApp.$Ctx.Infrastructure
  rm src/$Ctx/CaseritoApp.$Ctx.Domain/Class1.cs src/$Ctx/CaseritoApp.$Ctx.Application/Class1.cs src/$Ctx/CaseritoApp.$Ctx.Infrastructure/Class1.cs
  dotnet add src/$Ctx/CaseritoApp.$Ctx.Domain reference src/BuildingBlocks/CaseritoApp.BuildingBlocks.Domain
  dotnet add src/$Ctx/CaseritoApp.$Ctx.Application reference src/$Ctx/CaseritoApp.$Ctx.Domain src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application
  dotnet add src/$Ctx/CaseritoApp.$Ctx.Infrastructure reference src/$Ctx/CaseritoApp.$Ctx.Application src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure
  dotnet sln add src/$Ctx/CaseritoApp.$Ctx.Domain src/$Ctx/CaseritoApp.$Ctx.Application src/$Ctx/CaseritoApp.$Ctx.Infrastructure
done
```
Nota: un proyecto de biblioteca vacío (sin ningún `.cs`) compila sin problema; no añadas archivos placeholder.

- [ ] **Step 2: Verificar compila**

Run: `dotnet build CaseritoApp.sln`
Expected: 0 warnings / 0 errors, 18 proyectos nuevos compilados.

- [ ] **Step 3: Commit**

```bash
git add CaseritoApp/src CaseritoApp/CaseritoApp.sln
git commit -m "feat: esqueleto de los 6 bounded contexts (capas vacías)"
```

---

### Task 7: DbContext por contexto (schema propio)

**Files (para cada `<Ctx>`):**
- Create: `CaseritoApp/src/<Ctx>/CaseritoApp.<Ctx>.Infrastructure/<Ctx>DbContext.cs`
- Modify: cada `CaseritoApp.<Ctx>.Infrastructure.csproj` (añadir EF Core)
- Test: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/Persistence/SchemaPorContextoTests.cs`

**Interfaces:**
- Consumes: EF Core.
- Produces: un `<Ctx>DbContext : DbContext` por contexto con `modelBuilder.HasDefaultSchema("<ctx-en-minúscula>")`. Aún sin `DbSet` (los agregados llegan por feature). El schema por defecto es verificable vía `context.Model.GetDefaultSchema()` sin conectar a una BD.

- [ ] **Step 1: Añadir EF Core a cada Infrastructure de contexto**

Para cada `<Ctx>`, añade a `src/<Ctx>/CaseritoApp.<Ctx>.Infrastructure/CaseritoApp.<Ctx>.Infrastructure.csproj` (sin `Version=`):
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
  </ItemGroup>
```

- [ ] **Step 2: Crear cada DbContext**

Para cada `<Ctx>`, crea `src/<Ctx>/CaseritoApp.<Ctx>.Infrastructure/<Ctx>DbContext.cs` (ejemplo para `Identity`, replica cambiando `Identity`→`<Ctx>` y `identity`→schema en minúscula):
```csharp
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
```
Schemas: `identity`, `catalog`, `chat`, `orders`, `reputation`, `notifications`.

- [ ] **Step 3: Test de schema por contexto (TDD tras crear los DbContext)**

`.../Persistence/SchemaPorContextoTests.cs` (verifica el schema por defecto sin conectar a BD, usando el proveedor SqlServer):
```csharp
using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Notifications.Infrastructure;
using CaseritoApp.Orders.Infrastructure;
using CaseritoApp.Reputation.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CaseritoApp.ArchitectureTests.Persistence;

public sealed class SchemaPorContextoTests
{
    [Theory]
    [MemberData(nameof(Contextos))]
    public void Cada_contexto_tiene_su_schema(DbContext contexto, string schemaEsperado)
    {
        using (contexto)
        {
            Assert.Equal(schemaEsperado, contexto.Model.GetDefaultSchema());
        }
    }

    public static TheoryData<DbContext, string> Contextos() => new()
    {
        { new IdentityDbContext(Opciones<IdentityDbContext>()), "identity" },
        { new CatalogDbContext(Opciones<CatalogDbContext>()), "catalog" },
        { new ChatDbContext(Opciones<ChatDbContext>()), "chat" },
        { new OrdersDbContext(Opciones<OrdersDbContext>()), "orders" },
        { new ReputationDbContext(Opciones<ReputationDbContext>()), "reputation" },
        { new NotificationsDbContext(Opciones<NotificationsDbContext>()), "notifications" },
    };

    private static DbContextOptions<T> Opciones<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseSqlServer("Server=noop;Database=noop;").Options;
}
```
El proyecto de tests necesita referencias a los 6 `Infrastructure` y a EF Core SqlServer. Añade:
```bash
cd CaseritoApp
for Ctx in Identity Catalog Chat Orders Reputation Notifications; do
  dotnet add tests/CaseritoApp.ArchitectureTests reference src/$Ctx/CaseritoApp.$Ctx.Infrastructure
done
```
Y añade `<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />` al csproj de tests (sin `Version=`).

- [ ] **Step 4: Verificar GREEN**

Run: `dotnet test CaseritoApp.sln`
Expected: PASS (6 casos de schema verdes). `UseSqlServer` con una cadena dummy no abre conexión al construir el modelo.

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src CaseritoApp/tests CaseritoApp/CaseritoApp.sln
git commit -m "feat: DbContext por contexto con schema propio (SQL Server)"
```

---

### Task 8: Tests de arquitectura reales + retirar SmokeLib

**Files:**
- Create: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/Layering/CapasPorContextoTests.cs`
- Create: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/Layering/AislamientoEntreContextosTests.cs`
- Delete: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/LayeringTests.cs`
- Delete: proyecto `CaseritoApp/src/CaseritoApp.SmokeLib/` (y su referencia en tests y solución)

**Interfaces:**
- Consumes: los ensamblados de los 6 contextos.
- Produces: reglas reales — (a) por contexto: `Domain` no depende de `Application`/`Infrastructure`; (b) ningún contexto referencia el ensamblado de otro contexto.

- [ ] **Step 1: Escribir las reglas reales**

`.../Layering/CapasPorContextoTests.cs`:
```csharp
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace CaseritoApp.ArchitectureTests.Layering;

public sealed class CapasPorContextoTests
{
    private static readonly string[] Contextos =
        ["Identity", "Catalog", "Chat", "Orders", "Reputation", "Notifications"];

    [Fact]
    public void Domain_no_depende_de_Application_ni_Infrastructure()
    {
        foreach (var ctx in Contextos)
        {
            var domain = Assembly.Load($"CaseritoApp.{ctx}.Domain");
            var resultado = Types.InAssembly(domain)
                .Should().NotHaveDependencyOnAny(
                    $"CaseritoApp.{ctx}.Application",
                    $"CaseritoApp.{ctx}.Infrastructure")
                .GetResult();

            Assert.True(resultado.IsSuccessful,
                $"{ctx}.Domain viola capas: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
        }
    }
}
```

`.../Layering/AislamientoEntreContextosTests.cs`:
```csharp
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace CaseritoApp.ArchitectureTests.Layering;

public sealed class AislamientoEntreContextosTests
{
    private static readonly string[] Contextos =
        ["Identity", "Catalog", "Chat", "Orders", "Reputation", "Notifications"];

    [Fact]
    public void Ningun_contexto_depende_de_otro_contexto()
    {
        foreach (var ctx in Contextos)
        {
            var otros = Contextos.Where(c => c != ctx).Select(c => $"CaseritoApp.{c}").ToArray();
            foreach (var capa in new[] { "Domain", "Application", "Infrastructure" })
            {
                var ensamblado = Assembly.Load($"CaseritoApp.{ctx}.{capa}");
                var resultado = Types.InAssembly(ensamblado)
                    .Should().NotHaveDependencyOnAny(otros)
                    .GetResult();

                Assert.True(resultado.IsSuccessful,
                    $"{ctx}.{capa} depende de otro contexto: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
            }
        }
    }
}
```

- [ ] **Step 2: Retirar SmokeLib y su test smoke**

```bash
cd CaseritoApp
git rm -r src/CaseritoApp.SmokeLib
git rm tests/CaseritoApp.ArchitectureTests/LayeringTests.cs
dotnet sln remove src/CaseritoApp.SmokeLib/CaseritoApp.SmokeLib.csproj
```
Quita también la `ProjectReference` a `CaseritoApp.SmokeLib` del csproj de tests (si sigue presente).

- [ ] **Step 3: Verificar GREEN**

Run (desde `CaseritoApp/`):
```bash
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
```
Expected: build 0/0; tests verdes (las reglas reales pasan porque los contextos están vacíos y aislados; SmokeLib ya no existe). Confirma que ya no quedan referencias a `SampleEntity`/`SmokeLib`.

- [ ] **Step 4: Commit**

```bash
git add CaseritoApp/src CaseritoApp/tests CaseritoApp/CaseritoApp.sln
git commit -m "test: reglas de arquitectura reales por contexto + retirar SmokeLib"
```

---

### Task 9: Host Web API con /health y pipeline MediatR

**Files:**
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj` (SDK Web)
- Create: `.../Program.cs`
- Create: `.../appsettings.json`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj`
- Test: `.../HealthEndpointTests.cs`

**Interfaces:**
- Consumes: BuildingBlocks.Application (behaviors), MediatR.
- Produces: host que arranca sin conectar a BD, expone `GET /health` → 200, y registra MediatR + los behaviors (`LoggingBehavior`, `ValidationBehavior`, `UnitOfWorkBehavior`) en orden. `Program` debe ser parcial/público para `WebApplicationFactory` (usar top-level statements + `public partial class Program;`).

- [ ] **Step 1: Crear el host**

```bash
cd CaseritoApp
dotnet new web -n CaseritoApp.Host -o src/Host/CaseritoApp.Host
dotnet sln add src/Host/CaseritoApp.Host
dotnet add src/Host/CaseritoApp.Host reference src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure
```
Añade a `CaseritoApp.Host.csproj` (sin `Version=`): `<PackageReference Include="MediatR" />`.

`Program.cs`:
```csharp
using CaseritoApp.BuildingBlocks.Application.Behaviors;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CaseritoApp.BuildingBlocks.Application.Abstractions.IUnitOfWork>());

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

app.Run();

public partial class Program;
```
> No se registran DbContexts ni conexión a SQL Server en este ciclo (los contextos aún no tienen entidades); el host arranca sin BD.

`appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 2: Crear el proyecto de integración y el test (TDD)**

```bash
cd CaseritoApp
dotnet new xunit -n CaseritoApp.IntegrationTests -o tests/CaseritoApp.IntegrationTests
dotnet sln add tests/CaseritoApp.IntegrationTests
dotnet add tests/CaseritoApp.IntegrationTests reference src/Host/CaseritoApp.Host
rm tests/CaseritoApp.IntegrationTests/UnitTest1.cs
```
Edita `CaseritoApp.IntegrationTests.csproj` para (a) quitar `Version=` de los `PackageReference` (CPM), (b) añadir `<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />`, y (c) marcar `<IsTestProject>true</IsTestProject>`.

`.../HealthEndpointTests.cs`:
```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_responde_200()
    {
        var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
```

- [ ] **Step 3: Verificar RED→GREEN**

Run: `dotnet test CaseritoApp.sln`
- Antes de `Program.cs` correcto / con el test primero: FAIL.
- Con el host implementado: PASS (health 200). Todos los tests (arquitectura + unidad + integración) verdes.

- [ ] **Step 4: Commit**

```bash
git add CaseritoApp/src/Host CaseritoApp/tests/CaseritoApp.IntegrationTests CaseritoApp/CaseritoApp.sln
git commit -m "feat: host Web API con /health y pipeline MediatR"
```

---

### Task 10: Completar la skill nuevo-caso-de-uso (patrón CQRS-lite)

**Files:**
- Modify: `.claude/skills/nuevo-caso-de-uso/SKILL.md` (raíz `dev_Caserito/`)

**Interfaces:**
- Produces: skill funcional que genera un caso de uso siguiendo el patrón ya decidido (command/query + handler con `Result`, validator FluentValidation, test).

- [ ] **Step 1: Reescribir el SKILL.md**

Reemplaza el placeholder por una skill funcional. Contenido de `.claude/skills/nuevo-caso-de-uso/SKILL.md`:
```markdown
---
name: nuevo-caso-de-uso
description: Genera un caso de uso (command o query) de un bounded context de CaseritoApp siguiendo el patrón CQRS-lite (MediatR + Result + FluentValidation) y su test. Úsalo al añadir una operación a un contexto.
---

# Nuevo caso de uso

Genera un command o query en `CaseritoApp/src/<Ctx>/CaseritoApp.<Ctx>.Application`.
Recibe: contexto `<Ctx>`, nombre del caso `<Caso>`, y si es command o query.

## Patrón (command con Result)

`Application/<Area>/<Caso>Command.cs`:
- `public sealed record <Caso>Command(...) : ICommand;` (o `ICommand<TResp>` si devuelve valor).
- `internal sealed class <Caso>Handler : ICommandHandler<<Caso>Command> { ... devuelve Result.Exito()/Result.Fallo(error) ... }`
- `public sealed class <Caso>Validator : AbstractValidator<<Caso>Command> { ... }`

`ICommand`, `ICommandHandler`, `IQuery`, `IQueryHandler`, `Result` vienen de
`CaseritoApp.BuildingBlocks.Application.Messaging` y `.Domain`.

## Reglas

- Errores esperados → `Result.Fallo(new Error("codigo", "mensaje"))`; nunca excepciones para flujo esperado.
- Validación de entrada → un `AbstractValidator`; el `ValidationBehavior` la ejecuta.
- Un handler no llama a otro contexto directamente; se comunica por Id o emitiendo un evento.
- Nombres y comentarios en español; el handler `internal sealed`.

## Test

Añade en el proyecto de tests del contexto un test que ejerza el handler con un doble
de sus dependencias, verificando el `Result` (éxito y fallo esperado).

## Verificar

Desde `CaseritoApp/`: `dotnet build CaseritoApp.sln && dotnet test CaseritoApp.sln` en verde.
```

- [ ] **Step 2: Commit**

```bash
git add .claude/skills/nuevo-caso-de-uso/SKILL.md
git commit -m "chore: completar skill nuevo-caso-de-uso con patrón CQRS-lite"
```

---

## Verificación end-to-end (al terminar)

Desde `CaseritoApp/`:
1. `dotnet build CaseritoApp.sln` → 0 warnings / 0 errors.
2. `dotnet format CaseritoApp.sln --verify-no-changes` → sin cambios.
3. `dotnet test CaseritoApp.sln` → todos verdes (BuildingBlocks + arquitectura reales + schema por contexto + integración /health).
4. `dotnet run --project src/Host/CaseritoApp.Host` y `GET /health` → 200 (`{ "estado": "ok" }`).
5. No queda rastro de `SmokeLib`; los 6 contextos existen como proyectos por capa vacíos con su DbContext/schema; `BuildingBlocks` provee Result, behaviors, contratos y abstracciones de PII.
