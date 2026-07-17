# Bloque 2A — Dominio Aviso + CRUD del dueño — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Poblar el bounded context Catalog con el agregado `Aviso` y los casos de uso del dueño (crear/editar/pausar/reactivar/eliminar) + lectura propia y catálogos de referencia, sobre backend .NET.

**Architecture:** Clean Architecture + CQRS-lite (MediatR + `Result` + FluentValidation), igual que el contexto Identity. Schema propio `catalog` sin FK cruzada; `VendedorId` es un `Guid` opaco. Los endpoints entran al contrato OpenAPI y al cliente TS vía el job `contract` del CI.

**Tech Stack:** .NET 10, EF Core 10 (SQL Server), MediatR 12, FluentValidation 11, xUnit + Testcontainers.MsSql, Microsoft.AspNetCore.OpenApi.

## Global Constraints

Copiadas del spec y de `CaseritoApp/.editorconfig` / `Directory.Build.props`. Aplican a TODAS las tareas:

- `TargetFramework` = `net10.0`; `Nullable` enable; `ImplicitUsings` enable; **warnings-as-errors** (analizadores .NET + Roslynator + Sonar). Nada compila si viola las reglas.
- Namespaces **file-scoped**; `using` fuera del namespace, **System primero**.
- `PascalCase` tipos/miembros; `camelCase` locales; `_camelCase` campos privados; `I` en interfaces. **Nombres de dominio en español** (p. ej. `Aviso`, `Dinero`, `SembrarCatalogoAsync`).
- Versiones de paquetes **solo** en `CaseritoApp/Directory.Packages.props` (CPM). **Nunca** `Version=` en un `.csproj`.
- **Anti-PII en logs**: jamás loguear contenido libre del usuario ni datos de identidad; solo ids (aviso, vendedor, categoría, ciudad), estado, acción, resultado.
- **Aislamiento de contexto**: Catalog no referencia ningún otro contexto (Identity, etc.). Verificado por `AislamientoEntreContextosTests`. La condición "verificado" del vendedor entra como **dato** (bool derivado del claim JWT en el endpoint), nunca como dependencia a Identity.
- Schema EF = `catalog` (ya fijado en `CatalogDbContext.Schema`). Sin FK a otros schemas.
- Textos de UI/comentarios en **español**.
- Comandos desde `CaseritoApp/`: build `dotnet build CaseritoApp.sln`; test `dotnet test CaseritoApp.sln`; formato `dotnet format CaseritoApp.sln --verify-no-changes`. Tests de integración requieren **Docker**.

## Convención de nombres respecto al spec

El spec menciona el value object como `Money`; por la convención de dominio en español del repo se implementa como **`Dinero`** (mismo concepto: `Monto` + `Moneda`).

## File Structure

**`src/Catalog/CaseritoApp.Catalog.Domain/`** (solo referencia BuildingBlocks.Domain):
- `Avisos/EstadoAviso.cs` — enum `Activo | Pausado | Eliminado`.
- `Avisos/CondicionArticulo.cs` — enum `Nuevo | Usado`.
- `Avisos/Moneda.cs` — enum `BOB`.
- `Avisos/Dinero.cs` — value object (record) `Monto` + `Moneda`, factory `Crear` → `Result<Dinero>`.
- `Avisos/ErroresAviso.cs` — códigos de error (const strings).
- `Avisos/EventosAviso.cs` — eventos de dominio (`AvisoPublicado`, `AvisoEditado`, `AvisoPausado`, `AvisoReactivado`, `AvisoEliminado`).
- `Avisos/Aviso.cs` — raíz de agregado + máquina de estados.
- `Avisos/Categoria.cs` — entidad de referencia.
- `Avisos/Ciudad.cs` — entidad de referencia.

**`src/Catalog/CaseritoApp.Catalog.Application/`** (referencia Domain + BuildingBlocks.Application):
- `Avisos/ResultadoPaginado.cs` — página de resultados (local al contexto).
- `Avisos/DtosAviso.cs` — `AvisoDto`, `AvisoResumenDto`, `CategoriaDto`, `CiudadDto`.
- `Avisos/IRepositorioAvisos.cs` — puerto de persistencia de avisos.
- `Avisos/IConsultaCatalogo.cs` — puerto de lectura/existencia de categorías y ciudades.
- `Avisos/CrearAvisoCommand.cs` — command + handler + validator.
- `Avisos/EditarAvisoCommand.cs` — command + handler + validator.
- `Avisos/PausarAvisoCommand.cs` — command + handler.
- `Avisos/ReactivarAvisoCommand.cs` — command + handler.
- `Avisos/EliminarAvisoCommand.cs` — command + handler.
- `Avisos/ListarMisAvisosQuery.cs` — query + handler + validator.
- `Avisos/ObtenerMiAvisoQuery.cs` — query + handler.
- `Catalogo/ListarCategoriasQuery.cs` — query + handler.
- `Catalogo/ListarCiudadesQuery.cs` — query + handler.

**`src/Catalog/CaseritoApp.Catalog.Infrastructure/`** (referencia Application + BuildingBlocks.Infrastructure):
- `CatalogDbContext.cs` — **modificar**: DbSets + `OnModelCreating`.
- `Avisos/ConfiguracionCatalog.cs` — configuración EF de `Aviso` (+ `Dinero` owned), `Categoria`, `Ciudad`.
- `Avisos/RepositorioAvisosEfCore.cs` — adaptador de `IRepositorioAvisos`.
- `Avisos/ConsultaCatalogoEfCore.cs` — adaptador de `IConsultaCatalogo`.
- `UnitOfWorkCatalog.cs` — `IUnitOfWork` del contexto.
- `DependencyInjection.cs` — `AgregarCatalog`.
- `SeedCatalogoExtensions.cs` — `SembrarCatalogoAsync` (idempotente).
- `DesignTimeCatalogDbContextFactory.cs` — factory de diseño para `dotnet ef`.
- `Migrations/..._CatalogInicial.cs` — generada por `dotnet ef`.

**`src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Behaviors/UnitOfWorkBehavior.cs`** — **modificar**: resolver `IEnumerable<IUnitOfWork>` (multi-contexto).

**`src/Host/CaseritoApp.Host/`**:
- `CaseritoApp.Host.csproj` — **modificar**: agregar `ProjectReference` a Catalog.Infrastructure.
- `Program.cs` — **modificar**: MediatR/validators de Catalog, `AgregarCatalog`, migrate+seed de Catalog, mapear endpoints.
- `Endpoints/AvisosEndpoints.cs` — endpoints `/api/avisos`.
- `Endpoints/CatalogoEndpoints.cs` — endpoints `/api/catalogo`.

**`tests/`**:
- `CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj` — **modificar**: refs a Catalog.Domain + Catalog.Application.
- `CaseritoApp.UnitTests/Catalog/*.cs` — tests de dominio, handlers y validators.
- `CaseritoApp.ArchitectureTests/BuildingBlocks/UnitOfWorkBehaviorTests.cs` — **modificar**: multi-UoW.
- `CaseritoApp.IntegrationTests/Infrastructure/CaseritoApiFactory.cs` — **modificar**: registrar+migrar CatalogDbContext, sembrar catálogo.
- `CaseritoApp.IntegrationTests/AvisosFlujoTests.cs` — flujo end-to-end.

**`CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`** y **`web/src/api/schema.d.ts`** — **regenerar** (Task final).

---

## Task 1: Dominio — enums, `Dinero`, `ErroresAviso`, eventos

**Files:**
- Create: `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/EstadoAviso.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/CondicionArticulo.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Moneda.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/ErroresAviso.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Dinero.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/EventosAviso.cs`
- Modify: `tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj`
- Test: `tests/CaseritoApp.UnitTests/Catalog/DineroTests.cs`

**Interfaces:**
- Consumes: `CaseritoApp.BuildingBlocks.Domain` (`Result`, `Error`, `IDomainEvent`).
- Produces: `EstadoAviso`, `CondicionArticulo`, `Moneda` enums; `Dinero` con `decimal Monto`, `Moneda Moneda`, `static Result<Dinero> Crear(decimal, Moneda)`; `ErroresAviso` const strings; eventos `AvisoPublicado(Guid AvisoId, Guid VendedorId)`, `AvisoEditado/AvisoPausado/AvisoReactivado/AvisoEliminado(Guid AvisoId)`.

- [ ] **Step 1: Agregar refs de Catalog al proyecto de UnitTests**

En `tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj`, dentro del `ItemGroup` que ya tiene los `ProjectReference` de Identity, añadir:

```xml
    <ProjectReference Include="..\..\src\Catalog\CaseritoApp.Catalog.Domain\CaseritoApp.Catalog.Domain.csproj" />
    <ProjectReference Include="..\..\src\Catalog\CaseritoApp.Catalog.Application\CaseritoApp.Catalog.Application.csproj" />
```

- [ ] **Step 2: Escribir el test de `Dinero` (falla)**

Crear `tests/CaseritoApp.UnitTests/Catalog/DineroTests.cs`:

```csharp
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class DineroTests
{
    [Fact]
    public void Crear_con_monto_positivo_es_exito()
    {
        var resultado = Dinero.Crear(150.50m, Moneda.BOB);

        Assert.True(resultado.EsExito);
        Assert.Equal(150.50m, resultado.Valor.Monto);
        Assert.Equal(Moneda.BOB, resultado.Valor.Moneda);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Crear_con_monto_no_positivo_falla_con_precio_invalido(decimal monto)
    {
        var resultado = Dinero.Crear(monto, Moneda.BOB);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.PrecioInvalido, resultado.Error.Code);
    }

    [Fact]
    public void Dos_dineros_iguales_por_valor_son_iguales()
    {
        var a = Dinero.Crear(10m, Moneda.BOB).Valor;
        var b = Dinero.Crear(10m, Moneda.BOB).Valor;

        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 3: Verificar que falla por compilación**

Run: `dotnet build CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: FAIL — `Dinero`, `Moneda`, `ErroresAviso` no existen.

- [ ] **Step 4: Crear los enums**

`src/Catalog/CaseritoApp.Catalog.Domain/Avisos/EstadoAviso.cs`:

```csharp
namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Estado del ciclo de vida de un aviso.</summary>
public enum EstadoAviso
{
    /// <summary>Visible y disponible.</summary>
    Activo,

    /// <summary>Oculto temporalmente por el dueño.</summary>
    Pausado,

    /// <summary>Eliminado (soft-delete); estado terminal.</summary>
    Eliminado,
}
```

`src/Catalog/CaseritoApp.Catalog.Domain/Avisos/CondicionArticulo.cs`:

```csharp
namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Condición del artículo publicado.</summary>
public enum CondicionArticulo
{
    /// <summary>Artículo nuevo.</summary>
    Nuevo,

    /// <summary>Artículo usado.</summary>
    Usado,
}
```

`src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Moneda.cs`:

```csharp
namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Moneda del precio. En el MVP solo se admite boliviano.</summary>
public enum Moneda
{
    /// <summary>Boliviano (Bs).</summary>
    BOB,
}
```

- [ ] **Step 5: Crear `ErroresAviso`**

`src/Catalog/CaseritoApp.Catalog.Domain/Avisos/ErroresAviso.cs`:

```csharp
namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Códigos de error de dominio del contexto Catalog. Sirven de título en el mapeo a HTTP.</summary>
public static class ErroresAviso
{
    /// <summary>El aviso no existe o está eliminado (404).</summary>
    public const string NoEncontrado = "avisos.no_encontrado";

    /// <summary>El usuario no es el dueño del aviso (403).</summary>
    public const string NoEsPropietario = "avisos.no_es_propietario";

    /// <summary>El usuario no está verificado y no puede publicar (403).</summary>
    public const string NoVerificado = "avisos.no_verificado";

    /// <summary>La categoría no existe o está inactiva (400).</summary>
    public const string CategoriaInvalida = "avisos.categoria_invalida";

    /// <summary>La ciudad no existe o está inactiva (400).</summary>
    public const string CiudadInvalida = "avisos.ciudad_invalida";

    /// <summary>El precio no es válido (400).</summary>
    public const string PrecioInvalido = "avisos.precio_invalido";

    /// <summary>La transición de estado solicitada no es válida para el estado actual (409).</summary>
    public const string TransicionInvalida = "avisos.transicion_invalida";
}
```

- [ ] **Step 6: Crear `Dinero`**

`src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Dinero.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Value object de precio: un monto positivo en una moneda. Igualdad por valor.</summary>
public sealed record Dinero
{
    // Constructor sin parámetros para EF Core (owned type).
    private Dinero()
    {
    }

    /// <summary>Monto del precio; siempre mayor a cero.</summary>
    public decimal Monto { get; private init; }

    /// <summary>Moneda del precio.</summary>
    public Moneda Moneda { get; private init; }

    /// <summary>Crea un <see cref="Dinero"/> validando que el monto sea mayor a cero.</summary>
    public static Result<Dinero> Crear(decimal monto, Moneda moneda) =>
        monto <= 0
            ? Result.Fallo<Dinero>(new Error(ErroresAviso.PrecioInvalido, "El precio debe ser mayor a cero."))
            : Result.Exito(new Dinero { Monto = monto, Moneda = moneda });
}
```

- [ ] **Step 7: Crear los eventos de dominio**

`src/Catalog/CaseritoApp.Catalog.Domain/Avisos/EventosAviso.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Se publicó un aviso nuevo (gancho del contrato de integración ProductPublished).</summary>
public sealed record AvisoPublicado(Guid AvisoId, Guid VendedorId) : IDomainEvent;

/// <summary>Se editaron los datos de un aviso.</summary>
public sealed record AvisoEditado(Guid AvisoId) : IDomainEvent;

/// <summary>Se pausó un aviso.</summary>
public sealed record AvisoPausado(Guid AvisoId) : IDomainEvent;

/// <summary>Se reactivó un aviso.</summary>
public sealed record AvisoReactivado(Guid AvisoId) : IDomainEvent;

/// <summary>Se eliminó (soft-delete) un aviso.</summary>
public sealed record AvisoEliminado(Guid AvisoId) : IDomainEvent;
```

- [ ] **Step 8: Verificar que el test pasa**

Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~DineroTests"` (desde `CaseritoApp/`)
Expected: PASS (3 casos, incl. los `Theory`).

- [ ] **Step 9: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos CaseritoApp/tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/DineroTests.cs
git commit -m "feat(catalog): value object Dinero, enums y errores del aviso"
```

---

## Task 2: Dominio — agregado `Aviso` + `Categoria`/`Ciudad`

**Files:**
- Create: `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Aviso.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Categoria.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Ciudad.cs`
- Test: `tests/CaseritoApp.UnitTests/Catalog/AvisoTests.cs`

**Interfaces:**
- Consumes: `AggregateRoot`, `Entity`, `Result`, `Error` (BuildingBlocks.Domain); `Dinero`, enums, eventos, `ErroresAviso` (Task 1).
- Produces:
  - `Aviso.Crear(Guid vendedorId, string titulo, string descripcion, Dinero precio, Guid categoriaId, Guid ciudadId, CondicionArticulo condicion, DateTime ahoraUtc) : Aviso`
  - Instancia: `Guid VendedorId`, `string Titulo`, `string Descripcion`, `Dinero Precio`, `Guid CategoriaId`, `Guid CiudadId`, `CondicionArticulo Condicion`, `EstadoAviso Estado`, `DateTime FechaCreacion`, `DateTime FechaActualizacion`.
  - `Result Editar(string titulo, string descripcion, Dinero precio, Guid categoriaId, Guid ciudadId, CondicionArticulo condicion, DateTime ahoraUtc)`
  - `Result Pausar(DateTime ahoraUtc)`, `Result Reactivar(DateTime ahoraUtc)`, `Result Eliminar(DateTime ahoraUtc)`
  - `Categoria.Crear(Guid id, string nombre, int orden) : Categoria` con `string Nombre`, `bool Activa`, `int Orden`.
  - `Ciudad.Crear(Guid id, string nombre, int orden) : Ciudad` con `string Nombre`, `bool Activa`, `int Orden`.

- [ ] **Step 1: Escribir el test del agregado (falla)**

Crear `tests/CaseritoApp.UnitTests/Catalog/AvisoTests.cs`:

```csharp
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class AvisoTests
{
    private static readonly DateTime _ahora = new(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);

    private static Aviso NuevoAviso() => Aviso.Crear(
        vendedorId: Guid.NewGuid(),
        titulo: "Bicicleta de montaña",
        descripcion: "Rodado 29, poco uso",
        precio: Dinero.Crear(1200m, Moneda.BOB).Valor,
        categoriaId: Guid.NewGuid(),
        ciudadId: Guid.NewGuid(),
        condicion: CondicionArticulo.Usado,
        ahoraUtc: _ahora);

    [Fact]
    public void Crear_nace_activo_y_emite_AvisoPublicado()
    {
        var aviso = NuevoAviso();

        Assert.Equal(EstadoAviso.Activo, aviso.Estado);
        Assert.Equal(_ahora, aviso.FechaCreacion);
        Assert.Equal(_ahora, aviso.FechaActualizacion);
        Assert.Contains(aviso.EventosDeDominio, e => e is AvisoPublicado);
    }

    [Fact]
    public void Pausar_activo_pasa_a_pausado()
    {
        var aviso = NuevoAviso();

        var resultado = aviso.Pausar(_ahora.AddMinutes(5));

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoAviso.Pausado, aviso.Estado);
        Assert.Equal(_ahora.AddMinutes(5), aviso.FechaActualizacion);
    }

    [Fact]
    public void Pausar_ya_pausado_falla_con_transicion_invalida()
    {
        var aviso = NuevoAviso();
        aviso.Pausar(_ahora);

        var resultado = aviso.Pausar(_ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.TransicionInvalida, resultado.Error.Code);
    }

    [Fact]
    public void Reactivar_pausado_pasa_a_activo()
    {
        var aviso = NuevoAviso();
        aviso.Pausar(_ahora);

        var resultado = aviso.Reactivar(_ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoAviso.Activo, aviso.Estado);
    }

    [Fact]
    public void Eliminar_marca_eliminado_y_es_terminal()
    {
        var aviso = NuevoAviso();

        Assert.True(aviso.Eliminar(_ahora).EsExito);
        Assert.Equal(EstadoAviso.Eliminado, aviso.Estado);

        Assert.False(aviso.Eliminar(_ahora).EsExito);
        Assert.False(aviso.Pausar(_ahora).EsExito);
        Assert.False(aviso.Reactivar(_ahora).EsExito);
    }

    [Fact]
    public void Editar_eliminado_falla()
    {
        var aviso = NuevoAviso();
        aviso.Eliminar(_ahora);

        var resultado = aviso.Editar(
            "Nuevo titulo", "Nueva desc", Dinero.Crear(50m, Moneda.BOB).Valor,
            Guid.NewGuid(), Guid.NewGuid(), CondicionArticulo.Nuevo, _ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.TransicionInvalida, resultado.Error.Code);
    }

    [Fact]
    public void Editar_activo_actualiza_campos_y_fecha()
    {
        var aviso = NuevoAviso();
        var nuevaCategoria = Guid.NewGuid();

        var resultado = aviso.Editar(
            "Bici nueva", "Descripcion editada", Dinero.Crear(999m, Moneda.BOB).Valor,
            nuevaCategoria, aviso.CiudadId, CondicionArticulo.Nuevo, _ahora.AddHours(1));

        Assert.True(resultado.EsExito);
        Assert.Equal("Bici nueva", aviso.Titulo);
        Assert.Equal(999m, aviso.Precio.Monto);
        Assert.Equal(nuevaCategoria, aviso.CategoriaId);
        Assert.Equal(CondicionArticulo.Nuevo, aviso.Condicion);
        Assert.Equal(_ahora.AddHours(1), aviso.FechaActualizacion);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet build CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: FAIL — `Aviso` no existe.

- [ ] **Step 3: Crear el agregado `Aviso`**

`src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Aviso.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>
/// Raíz de agregado de un aviso del marketplace. Encapsula la máquina de estados
/// (<see cref="EstadoAviso.Activo"/> ⇄ <see cref="EstadoAviso.Pausado"/>, y
/// <see cref="EstadoAviso.Eliminado"/> como estado terminal por soft-delete).
/// </summary>
public sealed class Aviso : AggregateRoot
{
    // Constructor para EF Core.
    private Aviso()
    {
        Titulo = null!;
        Descripcion = null!;
        Precio = null!;
    }

    private Aviso(
        Guid vendedorId, string titulo, string descripcion, Dinero precio,
        Guid categoriaId, Guid ciudadId, CondicionArticulo condicion, DateTime ahoraUtc)
    {
        VendedorId = vendedorId;
        Titulo = titulo;
        Descripcion = descripcion;
        Precio = precio;
        CategoriaId = categoriaId;
        CiudadId = ciudadId;
        Condicion = condicion;
        Estado = EstadoAviso.Activo;
        FechaCreacion = ahoraUtc;
        FechaActualizacion = ahoraUtc;
    }

    /// <summary>Id opaco del dueño (claim <c>sub</c>); sin FK cruzada a Identity.</summary>
    public Guid VendedorId { get; private set; }

    /// <summary>Título del aviso.</summary>
    public string Titulo { get; private set; }

    /// <summary>Descripción del aviso.</summary>
    public string Descripcion { get; private set; }

    /// <summary>Precio del aviso.</summary>
    public Dinero Precio { get; private set; }

    /// <summary>Categoría (referencia al catálogo sembrado).</summary>
    public Guid CategoriaId { get; private set; }

    /// <summary>Ciudad (referencia al catálogo sembrado).</summary>
    public Guid CiudadId { get; private set; }

    /// <summary>Condición del artículo.</summary>
    public CondicionArticulo Condicion { get; private set; }

    /// <summary>Estado actual del aviso.</summary>
    public EstadoAviso Estado { get; private set; }

    /// <summary>Fecha de creación (UTC).</summary>
    public DateTime FechaCreacion { get; private set; }

    /// <summary>Fecha de última modificación (UTC).</summary>
    public DateTime FechaActualizacion { get; private set; }

    /// <summary>Crea un aviso nuevo en estado <see cref="EstadoAviso.Activo"/> y emite <see cref="AvisoPublicado"/>.</summary>
    public static Aviso Crear(
        Guid vendedorId, string titulo, string descripcion, Dinero precio,
        Guid categoriaId, Guid ciudadId, CondicionArticulo condicion, DateTime ahoraUtc)
    {
        var aviso = new Aviso(vendedorId, titulo, descripcion, precio, categoriaId, ciudadId, condicion, ahoraUtc);
        aviso.AgregarEvento(new AvisoPublicado(aviso.Id, vendedorId));
        return aviso;
    }

    /// <summary>Edita los datos del aviso. Prohibido si está eliminado.</summary>
    public Result Editar(
        string titulo, string descripcion, Dinero precio,
        Guid categoriaId, Guid ciudadId, CondicionArticulo condicion, DateTime ahoraUtc)
    {
        if (Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.TransicionInvalida, "No se puede editar un aviso eliminado."));
        }

        Titulo = titulo;
        Descripcion = descripcion;
        Precio = precio;
        CategoriaId = categoriaId;
        CiudadId = ciudadId;
        Condicion = condicion;
        Tocar(ahoraUtc);
        AgregarEvento(new AvisoEditado(Id));
        return Result.Exito();
    }

    /// <summary>Pausa un aviso activo.</summary>
    public Result Pausar(DateTime ahoraUtc)
    {
        if (Estado != EstadoAviso.Activo)
        {
            return Result.Fallo(new Error(ErroresAviso.TransicionInvalida, "Solo se puede pausar un aviso activo."));
        }

        Estado = EstadoAviso.Pausado;
        Tocar(ahoraUtc);
        AgregarEvento(new AvisoPausado(Id));
        return Result.Exito();
    }

    /// <summary>Reactiva un aviso pausado.</summary>
    public Result Reactivar(DateTime ahoraUtc)
    {
        if (Estado != EstadoAviso.Pausado)
        {
            return Result.Fallo(new Error(ErroresAviso.TransicionInvalida, "Solo se puede reactivar un aviso pausado."));
        }

        Estado = EstadoAviso.Activo;
        Tocar(ahoraUtc);
        AgregarEvento(new AvisoReactivado(Id));
        return Result.Exito();
    }

    /// <summary>Elimina (soft-delete) el aviso. No se puede eliminar dos veces.</summary>
    public Result Eliminar(DateTime ahoraUtc)
    {
        if (Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.TransicionInvalida, "El aviso ya está eliminado."));
        }

        Estado = EstadoAviso.Eliminado;
        Tocar(ahoraUtc);
        AgregarEvento(new AvisoEliminado(Id));
        return Result.Exito();
    }

    private void Tocar(DateTime ahoraUtc) => FechaActualizacion = ahoraUtc;
}
```

- [ ] **Step 4: Crear `Categoria` y `Ciudad`**

`src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Categoria.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Categoría de referencia del catálogo (sembrada, no gestionable por UI en el MVP).</summary>
public sealed class Categoria : Entity
{
    // Constructor para EF Core.
    private Categoria() => Nombre = null!;

    private Categoria(Guid id, string nombre, int orden) : base(id)
    {
        Nombre = nombre;
        Orden = orden;
        Activa = true;
    }

    /// <summary>Nombre visible de la categoría.</summary>
    public string Nombre { get; private set; }

    /// <summary>Indica si la categoría está disponible para publicar/filtrar.</summary>
    public bool Activa { get; private set; }

    /// <summary>Orden de presentación.</summary>
    public int Orden { get; private set; }

    /// <summary>Crea una categoría activa con id fijo (para el seeder idempotente).</summary>
    public static Categoria Crear(Guid id, string nombre, int orden) => new(id, nombre, orden);
}
```

`src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Ciudad.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>Ciudad de referencia del catálogo (sembrada, no gestionable por UI en el MVP).</summary>
public sealed class Ciudad : Entity
{
    // Constructor para EF Core.
    private Ciudad() => Nombre = null!;

    private Ciudad(Guid id, string nombre, int orden) : base(id)
    {
        Nombre = nombre;
        Orden = orden;
        Activa = true;
    }

    /// <summary>Nombre visible de la ciudad.</summary>
    public string Nombre { get; private set; }

    /// <summary>Indica si la ciudad está disponible para publicar/filtrar.</summary>
    public bool Activa { get; private set; }

    /// <summary>Orden de presentación.</summary>
    public int Orden { get; private set; }

    /// <summary>Crea una ciudad activa con id fijo (para el seeder idempotente).</summary>
    public static Ciudad Crear(Guid id, string nombre, int orden) => new(id, nombre, orden);
}
```

- [ ] **Step 5: Verificar que los tests pasan**

Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~AvisoTests"` (desde `CaseritoApp/`)
Expected: PASS (7 casos).

- [ ] **Step 6: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/AvisoTests.cs
git commit -m "feat(catalog): agregado Aviso con maquina de estados + Categoria/Ciudad"
```

---

## Task 3: `UnitOfWorkBehavior` multi-contexto

Con un segundo bounded context, el pipeline de MediatR debe poder persistir el `DbContext` de cualquier contexto tocado por el handler. Hoy el behavior inyecta un único `IUnitOfWork` (Identity); registrar un segundo (`UnitOfWorkCatalog`) haría que "el último registrado gane" y rompería Identity. Se cambia a resolver **todos** los `IUnitOfWork` y guardar en cada uno (guardar un contexto sin cambios es un no-op barato).

**Files:**
- Modify: `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Behaviors/UnitOfWorkBehavior.cs`
- Modify: `tests/CaseritoApp.ArchitectureTests/BuildingBlocks/UnitOfWorkBehaviorTests.cs`

**Interfaces:**
- Consumes: `IUnitOfWork` (BuildingBlocks.Application.Abstractions).
- Produces: `UnitOfWorkBehavior<TRequest,TResponse>(IEnumerable<IUnitOfWork>)`.

- [ ] **Step 1: Reescribir el test para multi-UoW (falla)**

Reemplazar el contenido de `tests/CaseritoApp.ArchitectureTests/BuildingBlocks/UnitOfWorkBehaviorTests.cs`:

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
    public async Task Guarda_cambios_una_vez_en_cada_unit_of_work_tras_el_handler()
    {
        var uow1 = new UnitOfWorkFake();
        var uow2 = new UnitOfWorkFake();
        var behavior = new UnitOfWorkBehavior<Comando, string>([uow1, uow2]);

        var resultado = await behavior.Handle(new Comando(), () => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", resultado);
        Assert.Equal(1, uow1.Llamadas);
        Assert.Equal(1, uow2.Llamadas);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet build CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: FAIL — el constructor de `UnitOfWorkBehavior` no acepta `IEnumerable<IUnitOfWork>`.

- [ ] **Step 3: Reescribir el behavior**

Reemplazar el contenido de `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Behaviors/UnitOfWorkBehavior.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using MediatR;

namespace CaseritoApp.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Behavior de MediatR que, tras ejecutar el handler, invoca
/// <see cref="IUnitOfWork.GuardarCambiosAsync"/> una vez en cada unidad de trabajo registrada.
/// En un monolito modular hay una por bounded context (Identity, Catalog, ...); guardar un
/// contexto sin cambios es un no-op, de modo que el handler solo persiste el contexto que tocó.
/// No despacha eventos de dominio por su cuenta: esa conexión se hará al implementar el dispatcher.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>(IEnumerable<IUnitOfWork> unidadesDeTrabajo)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var respuesta = await next();
        foreach (var unidad in unidadesDeTrabajo)
        {
            await unidad.GuardarCambiosAsync(cancellationToken);
        }

        return respuesta;
    }
}
```

- [ ] **Step 4: Verificar que la suite pasa (sin regresiones en Identity)**

Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~UnitOfWorkBehaviorTests"` (desde `CaseritoApp/`)
Expected: PASS. Luego correr `dotnet build CaseritoApp.sln` y confirmar 0 warnings/errors.

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Behaviors/UnitOfWorkBehavior.cs CaseritoApp/tests/CaseritoApp.ArchitectureTests/BuildingBlocks/UnitOfWorkBehaviorTests.cs
git commit -m "refactor(buildingblocks): UnitOfWorkBehavior guarda todos los contextos (multi-UoW)"
```

---

## Task 4: `CrearAvisoCommand` + puertos + DTOs

Introduce los puertos de Application, los DTOs y el primer caso de uso. La verificación KYC entra como el bool `EstaVerificado` (derivado del claim en el endpoint), sin acoplar Catalog a Identity.

**Files:**
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/ResultadoPaginado.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAviso.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/IRepositorioAvisos.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/IConsultaCatalogo.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/CrearAvisoCommand.cs`
- Test: `tests/CaseritoApp.UnitTests/Catalog/CrearAvisoCommandHandlerTests.cs`
- Test: `tests/CaseritoApp.UnitTests/Catalog/CrearAvisoCommandValidatorTests.cs`

**Interfaces:**
- Consumes: `ICommand<TResponse>`, `ICommandHandler<,>` (BuildingBlocks.Application.Messaging); `Result`, `Error` (BuildingBlocks.Domain); dominio de Task 1-2; `TimeProvider` (registrado ya en el host).
- Produces:
  - `ResultadoPaginado<T>(IReadOnlyList<T> Items, int Pagina, int Tamano, int Total)`
  - `AvisoDto`, `AvisoResumenDto`, `CategoriaDto`, `CiudadDto` (ver código).
  - `IRepositorioAvisos`: `Task<Aviso?> ObtenerAsync(Guid id, CancellationToken)`, `void Agregar(Aviso)`, `Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(Guid vendedorId, int pagina, int tamano, CancellationToken)`.
  - `IConsultaCatalogo`: `Task<bool> ExisteCategoriaActivaAsync(Guid, CancellationToken)`, `Task<bool> ExisteCiudadActivaAsync(Guid, CancellationToken)`, `Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken)`, `Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken)`.
  - `CrearAvisoCommand(Guid VendedorId, bool EstaVerificado, string Titulo, string Descripcion, decimal Monto, string Condicion, Guid CategoriaId, Guid CiudadId) : ICommand<Guid>`.

- [ ] **Step 1: Escribir los tests del handler (fallan)**

Crear `tests/CaseritoApp.UnitTests/Catalog/CrearAvisoCommandHandlerTests.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class CrearAvisoCommandHandlerTests
{
    private static readonly Guid _categoria = Guid.NewGuid();
    private static readonly Guid _ciudad = Guid.NewGuid();

    private sealed class RepositorioFake : IRepositorioAvisos
    {
        public Aviso? Agregado { get; private set; }

        public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult<Aviso?>(null);

        public void Agregar(Aviso aviso) => Agregado = aviso;

        public Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
            Guid vendedorId, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<AvisoResumenDto>([], pagina, tamano, 0));
    }

    private sealed class ConsultaFake(bool categoria, bool ciudad) : IConsultaCatalogo
    {
        public Task<bool> ExisteCategoriaActivaAsync(Guid id, CancellationToken ct) => Task.FromResult(categoria);
        public Task<bool> ExisteCiudadActivaAsync(Guid id, CancellationToken ct) => Task.FromResult(ciudad);
        public Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CategoriaDto>>([]);
        public Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CiudadDto>>([]);
    }

    private static CrearAvisoCommand Comando(bool verificado = true) => new(
        VendedorId: Guid.NewGuid(),
        EstaVerificado: verificado,
        Titulo: "Bicicleta",
        Descripcion: "Poco uso",
        Monto: 500m,
        Condicion: "Usado",
        CategoriaId: _categoria,
        CiudadId: _ciudad);

    private static CrearAvisoCommandHandler Handler(
        RepositorioFake repo, bool categoria = true, bool ciudad = true) =>
        new(repo, new ConsultaFake(categoria, ciudad), TimeProvider.System);

    [Fact]
    public async Task Crea_aviso_cuando_verificado_y_catalogo_valido()
    {
        var repo = new RepositorioFake();

        var resultado = await Handler(repo).Handle(Comando(), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.NotNull(repo.Agregado);
        Assert.Equal(resultado.Valor, repo.Agregado!.Id);
        Assert.Equal(EstadoAviso.Activo, repo.Agregado.Estado);
    }

    [Fact]
    public async Task Falla_con_no_verificado_si_usuario_no_esta_verificado()
    {
        var repo = new RepositorioFake();

        var resultado = await Handler(repo).Handle(Comando(verificado: false), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.NoVerificado, resultado.Error.Code);
        Assert.Null(repo.Agregado);
    }

    [Fact]
    public async Task Falla_con_categoria_invalida()
    {
        var repo = new RepositorioFake();

        var resultado = await Handler(repo, categoria: false).Handle(Comando(), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.CategoriaInvalida, resultado.Error.Code);
    }

    [Fact]
    public async Task Falla_con_ciudad_invalida()
    {
        var repo = new RepositorioFake();

        var resultado = await Handler(repo, ciudad: false).Handle(Comando(), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.CiudadInvalida, resultado.Error.Code);
    }
}
```

Crear `tests/CaseritoApp.UnitTests/Catalog/CrearAvisoCommandValidatorTests.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class CrearAvisoCommandValidatorTests
{
    private static CrearAvisoCommand Valido() => new(
        Guid.NewGuid(), true, "Titulo", "Descripcion", 100m, "Nuevo", Guid.NewGuid(), Guid.NewGuid());

    private readonly CrearAvisoCommandValidator _validator = new();

    [Fact]
    public void Comando_valido_pasa()
    {
        Assert.True(_validator.Validate(Valido()).IsValid);
    }

    [Fact]
    public void Titulo_vacio_falla()
    {
        Assert.False(_validator.Validate(Valido() with { Titulo = "" }).IsValid);
    }

    [Fact]
    public void Monto_no_positivo_falla()
    {
        Assert.False(_validator.Validate(Valido() with { Monto = 0 }).IsValid);
    }

    [Fact]
    public void Condicion_no_reconocida_falla()
    {
        Assert.False(_validator.Validate(Valido() with { Condicion = "Roto" }).IsValid);
    }

    [Fact]
    public void Categoria_vacia_falla()
    {
        Assert.False(_validator.Validate(Valido() with { CategoriaId = Guid.Empty }).IsValid);
    }
}
```

- [ ] **Step 2: Verificar que fallan por compilación**

Run: `dotnet build CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: FAIL — los tipos de Application no existen.

- [ ] **Step 3: Crear `ResultadoPaginado` y los DTOs**

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/ResultadoPaginado.cs`:

```csharp
namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Página de resultados con metadatos de paginación.</summary>
public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Items, int Pagina, int Tamano, int Total);
```

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAviso.cs`:

```csharp
namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Detalle de un aviso (vista del dueño).</summary>
public sealed record AvisoDto(
    Guid Id,
    Guid VendedorId,
    string Titulo,
    string Descripcion,
    decimal Monto,
    string Moneda,
    Guid CategoriaId,
    Guid CiudadId,
    string Condicion,
    string Estado,
    DateTime FechaCreacion,
    DateTime FechaActualizacion);

/// <summary>Resumen de un aviso para listados.</summary>
public sealed record AvisoResumenDto(
    Guid Id,
    string Titulo,
    decimal Monto,
    string Moneda,
    Guid CategoriaId,
    Guid CiudadId,
    string Condicion,
    string Estado,
    DateTime FechaCreacion);

/// <summary>Categoría de referencia.</summary>
public sealed record CategoriaDto(Guid Id, string Nombre);

/// <summary>Ciudad de referencia.</summary>
public sealed record CiudadDto(Guid Id, string Nombre);
```

- [ ] **Step 4: Crear los puertos**

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/IRepositorioAvisos.cs`:

```csharp
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Puerto de persistencia de avisos.</summary>
public interface IRepositorioAvisos
{
    /// <summary>Obtiene un aviso por id (incluye eliminados; el handler decide su tratamiento).</summary>
    Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>Marca un aviso nuevo para inserción.</summary>
    void Agregar(Aviso aviso);

    /// <summary>Lista paginada de avisos de un vendedor, excluyendo los eliminados.</summary>
    Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
        Guid vendedorId, int pagina, int tamano, CancellationToken ct);
}
```

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/IConsultaCatalogo.cs`:

```csharp
namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Puerto de lectura de catálogos de referencia (categorías y ciudades).</summary>
public interface IConsultaCatalogo
{
    /// <summary>Indica si existe una categoría activa con ese id.</summary>
    Task<bool> ExisteCategoriaActivaAsync(Guid categoriaId, CancellationToken ct);

    /// <summary>Indica si existe una ciudad activa con ese id.</summary>
    Task<bool> ExisteCiudadActivaAsync(Guid ciudadId, CancellationToken ct);

    /// <summary>Lista las categorías activas ordenadas.</summary>
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct);

    /// <summary>Lista las ciudades activas ordenadas.</summary>
    Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken ct);
}
```

- [ ] **Step 5: Crear `CrearAvisoCommand` (command + handler + validator)**

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/CrearAvisoCommand.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Crea un aviso a nombre del vendedor autenticado. Requiere estar verificado (KYC).</summary>
public sealed record CrearAvisoCommand(
    Guid VendedorId,
    bool EstaVerificado,
    string Titulo,
    string Descripcion,
    decimal Monto,
    string Condicion,
    Guid CategoriaId,
    Guid CiudadId) : ICommand<Guid>;

/// <summary>Handler de <see cref="CrearAvisoCommand"/>.</summary>
public sealed class CrearAvisoCommandHandler(
    IRepositorioAvisos repositorio, IConsultaCatalogo catalogo, TimeProvider reloj)
    : ICommandHandler<CrearAvisoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CrearAvisoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EstaVerificado)
        {
            return Result.Fallo<Guid>(new Error(
                ErroresAviso.NoVerificado, "Debe completar la verificación de identidad para publicar."));
        }

        var precio = Dinero.Crear(request.Monto, Moneda.BOB);
        if (!precio.EsExito)
        {
            return Result.Fallo<Guid>(precio.Error);
        }

        if (!await catalogo.ExisteCategoriaActivaAsync(request.CategoriaId, cancellationToken))
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.CategoriaInvalida, "La categoría no es válida."));
        }

        if (!await catalogo.ExisteCiudadActivaAsync(request.CiudadId, cancellationToken))
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.CiudadInvalida, "La ciudad no es válida."));
        }

        var condicion = Enum.Parse<CondicionArticulo>(request.Condicion);
        var aviso = Aviso.Crear(
            request.VendedorId, request.Titulo, request.Descripcion, precio.Valor,
            request.CategoriaId, request.CiudadId, condicion, reloj.GetUtcNow().UtcDateTime);

        repositorio.Agregar(aviso);
        return Result.Exito(aviso.Id);
    }
}

/// <summary>Valida <see cref="CrearAvisoCommand"/>.</summary>
public sealed class CrearAvisoCommandValidator : AbstractValidator<CrearAvisoCommand>
{
    public CrearAvisoCommandValidator()
    {
        RuleFor(c => c.Titulo)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(120).WithMessage("El título no puede superar los 120 caracteres.");

        RuleFor(c => c.Descripcion)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(2000).WithMessage("La descripción no puede superar los 2000 caracteres.");

        RuleFor(c => c.Monto)
            .GreaterThan(0).WithMessage("El precio debe ser mayor a cero.");

        RuleFor(c => c.Condicion)
            .Must(v => Enum.TryParse<CondicionArticulo>(v, out _))
            .WithMessage("La condición no es válida.");

        RuleFor(c => c.CategoriaId)
            .NotEmpty().WithMessage("La categoría es obligatoria.");

        RuleFor(c => c.CiudadId)
            .NotEmpty().WithMessage("La ciudad es obligatoria.");
    }
}
```

- [ ] **Step 6: Verificar que los tests pasan**

Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~CrearAviso"` (desde `CaseritoApp/`)
Expected: PASS (handler 4 + validator 5).

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/CrearAvisoCommandHandlerTests.cs CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/CrearAvisoCommandValidatorTests.cs
git commit -m "feat(catalog): CrearAvisoCommand con puertos, DTOs y validacion"
```

---

## Task 5: `EditarAvisoCommand`

**Files:**
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/EditarAvisoCommand.cs`
- Test: `tests/CaseritoApp.UnitTests/Catalog/EditarAvisoCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioAvisos`, `IConsultaCatalogo`, dominio, `TimeProvider`.
- Produces: `EditarAvisoCommand(Guid Id, Guid VendedorId, string Titulo, string Descripcion, decimal Monto, string Condicion, Guid CategoriaId, Guid CiudadId) : ICommand`; `EditarAvisoCommandValidator`.

Reglas del handler (orden importa):
1. `ObtenerAsync(Id)`; si es `null` o `Estado == Eliminado` → `Result.Fallo(NoEncontrado)` (404).
2. Si `aviso.VendedorId != VendedorId` → `Result.Fallo(NoEsPropietario)` (403).
3. `Dinero.Crear` → propaga error si falla.
4. Validar categoría/ciudad activas → `CategoriaInvalida`/`CiudadInvalida`.
5. `aviso.Editar(...)` → devuelve su `Result`.

- [ ] **Step 1: Escribir los tests del handler (fallan)**

Crear `tests/CaseritoApp.UnitTests/Catalog/EditarAvisoCommandHandlerTests.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class EditarAvisoCommandHandlerTests
{
    private static readonly DateTime _ahora = new(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc);

    private sealed class RepositorioFake(Aviso? aviso) : IRepositorioAvisos
    {
        public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(aviso);
        public void Agregar(Aviso a) { }
        public Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
            Guid vendedorId, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<AvisoResumenDto>([], pagina, tamano, 0));
    }

    private sealed class ConsultaFake : IConsultaCatalogo
    {
        public Task<bool> ExisteCategoriaActivaAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> ExisteCiudadActivaAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CategoriaDto>>([]);
        public Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CiudadDto>>([]);
    }

    private static Aviso AvisoDe(Guid vendedor) => Aviso.Crear(
        vendedor, "Titulo", "Desc", Dinero.Crear(10m, Moneda.BOB).Valor,
        Guid.NewGuid(), Guid.NewGuid(), CondicionArticulo.Nuevo, _ahora);

    private static EditarAvisoCommand Comando(Guid id, Guid vendedor) => new(
        id, vendedor, "Nuevo", "Nueva desc", 20m, "Usado", Guid.NewGuid(), Guid.NewGuid());

    private static EditarAvisoCommandHandler Handler(IRepositorioAvisos repo) =>
        new(repo, new ConsultaFake(), TimeProvider.System);

    [Fact]
    public async Task Edita_cuando_es_propietario()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);

        var resultado = await Handler(new RepositorioFake(aviso)).Handle(
            Comando(aviso.Id, vendedor), CancellationToken.None);

        Assert.True(resultado.EsExito);
        Assert.Equal("Nuevo", aviso.Titulo);
    }

    [Fact]
    public async Task Falla_404_si_no_existe()
    {
        var resultado = await Handler(new RepositorioFake(null)).Handle(
            Comando(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.NoEncontrado, resultado.Error.Code);
    }

    [Fact]
    public async Task Falla_404_si_esta_eliminado()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        aviso.Eliminar(_ahora);

        var resultado = await Handler(new RepositorioFake(aviso)).Handle(
            Comando(aviso.Id, vendedor), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.NoEncontrado, resultado.Error.Code);
    }

    [Fact]
    public async Task Falla_403_si_no_es_propietario()
    {
        var aviso = AvisoDe(Guid.NewGuid());

        var resultado = await Handler(new RepositorioFake(aviso)).Handle(
            Comando(aviso.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.NoEsPropietario, resultado.Error.Code);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet build CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: FAIL — `EditarAvisoCommand` no existe.

- [ ] **Step 3: Crear `EditarAvisoCommand`**

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/EditarAvisoCommand.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Edita un aviso propio.</summary>
public sealed record EditarAvisoCommand(
    Guid Id,
    Guid VendedorId,
    string Titulo,
    string Descripcion,
    decimal Monto,
    string Condicion,
    Guid CategoriaId,
    Guid CiudadId) : ICommand;

/// <summary>Handler de <see cref="EditarAvisoCommand"/>.</summary>
public sealed class EditarAvisoCommandHandler(
    IRepositorioAvisos repositorio, IConsultaCatalogo catalogo, TimeProvider reloj)
    : ICommandHandler<EditarAvisoCommand>
{
    public async Task<Result> Handle(EditarAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.Id, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        var precio = Dinero.Crear(request.Monto, Moneda.BOB);
        if (!precio.EsExito)
        {
            return precio;
        }

        if (!await catalogo.ExisteCategoriaActivaAsync(request.CategoriaId, cancellationToken))
        {
            return Result.Fallo(new Error(ErroresAviso.CategoriaInvalida, "La categoría no es válida."));
        }

        if (!await catalogo.ExisteCiudadActivaAsync(request.CiudadId, cancellationToken))
        {
            return Result.Fallo(new Error(ErroresAviso.CiudadInvalida, "La ciudad no es válida."));
        }

        var condicion = Enum.Parse<CondicionArticulo>(request.Condicion);
        return aviso.Editar(
            request.Titulo, request.Descripcion, precio.Valor,
            request.CategoriaId, request.CiudadId, condicion, reloj.GetUtcNow().UtcDateTime);
    }
}

/// <summary>Valida <see cref="EditarAvisoCommand"/>.</summary>
public sealed class EditarAvisoCommandValidator : AbstractValidator<EditarAvisoCommand>
{
    public EditarAvisoCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Titulo)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(120).WithMessage("El título no puede superar los 120 caracteres.");
        RuleFor(c => c.Descripcion)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(2000).WithMessage("La descripción no puede superar los 2000 caracteres.");
        RuleFor(c => c.Monto).GreaterThan(0).WithMessage("El precio debe ser mayor a cero.");
        RuleFor(c => c.Condicion)
            .Must(v => Enum.TryParse<CondicionArticulo>(v, out _))
            .WithMessage("La condición no es válida.");
        RuleFor(c => c.CategoriaId).NotEmpty().WithMessage("La categoría es obligatoria.");
        RuleFor(c => c.CiudadId).NotEmpty().WithMessage("La ciudad es obligatoria.");
    }
}
```

- [ ] **Step 4: Verificar que pasan**

Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~EditarAvisoCommandHandlerTests"` (desde `CaseritoApp/`)
Expected: PASS (4 casos).

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/EditarAvisoCommand.cs CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/EditarAvisoCommandHandlerTests.cs
git commit -m "feat(catalog): EditarAvisoCommand (dueno)"
```

---

## Task 6: `PausarAvisoCommand` + `ReactivarAvisoCommand` + `EliminarAvisoCommand`

Tres comandos de transición de estado, mismo esqueleto: cargar → 404 si null/eliminado → 403 si no dueño → invocar la transición del dominio (que devuelve `Result`, 409 en transición inválida).

**Files:**
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/PausarAvisoCommand.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/ReactivarAvisoCommand.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/EliminarAvisoCommand.cs`
- Test: `tests/CaseritoApp.UnitTests/Catalog/TransicionesAvisoHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioAvisos`, dominio, `TimeProvider`.
- Produces: `PausarAvisoCommand(Guid Id, Guid VendedorId) : ICommand`; `ReactivarAvisoCommand(Guid Id, Guid VendedorId) : ICommand`; `EliminarAvisoCommand(Guid Id, Guid VendedorId) : ICommand`; sus handlers.

Nota de tratamiento del estado `Eliminado`: en `Pausar`/`Reactivar`/`Editar` un aviso eliminado se trata como **404** (`NoEncontrado`). En `Eliminar`, si ya está eliminado también **404** (idempotencia desde la vista del dueño: ya no existe para él).

- [ ] **Step 1: Escribir los tests (fallan)**

Crear `tests/CaseritoApp.UnitTests/Catalog/TransicionesAvisoHandlerTests.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class TransicionesAvisoHandlerTests
{
    private static readonly DateTime _ahora = new(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc);

    private sealed class RepositorioFake(Aviso? aviso) : IRepositorioAvisos
    {
        public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(aviso);
        public void Agregar(Aviso a) { }
        public Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
            Guid vendedorId, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<AvisoResumenDto>([], pagina, tamano, 0));
    }

    private static Aviso AvisoDe(Guid vendedor) => Aviso.Crear(
        vendedor, "Titulo", "Desc", Dinero.Crear(10m, Moneda.BOB).Valor,
        Guid.NewGuid(), Guid.NewGuid(), CondicionArticulo.Nuevo, _ahora);

    [Fact]
    public async Task Pausar_ok_cuando_dueno_y_activo()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        var handler = new PausarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new PausarAvisoCommand(aviso.Id, vendedor), CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoAviso.Pausado, aviso.Estado);
    }

    [Fact]
    public async Task Pausar_403_si_no_dueno()
    {
        var aviso = AvisoDe(Guid.NewGuid());
        var handler = new PausarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new PausarAvisoCommand(aviso.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEsPropietario, r.Error.Code);
    }

    [Fact]
    public async Task Pausar_404_si_no_existe()
    {
        var handler = new PausarAvisoCommandHandler(new RepositorioFake(null), TimeProvider.System);

        var r = await handler.Handle(new PausarAvisoCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEncontrado, r.Error.Code);
    }

    [Fact]
    public async Task Reactivar_ok_cuando_pausado()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        aviso.Pausar(_ahora);
        var handler = new ReactivarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new ReactivarAvisoCommand(aviso.Id, vendedor), CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoAviso.Activo, aviso.Estado);
    }

    [Fact]
    public async Task Reactivar_409_si_ya_activo()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        var handler = new ReactivarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new ReactivarAvisoCommand(aviso.Id, vendedor), CancellationToken.None);

        Assert.Equal(ErroresAviso.TransicionInvalida, r.Error.Code);
    }

    [Fact]
    public async Task Eliminar_ok_cuando_dueno()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        var handler = new EliminarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new EliminarAvisoCommand(aviso.Id, vendedor), CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoAviso.Eliminado, aviso.Estado);
    }

    [Fact]
    public async Task Eliminar_404_si_ya_eliminado()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        aviso.Eliminar(_ahora);
        var handler = new EliminarAvisoCommandHandler(new RepositorioFake(aviso), TimeProvider.System);

        var r = await handler.Handle(new EliminarAvisoCommand(aviso.Id, vendedor), CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEncontrado, r.Error.Code);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet build CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: FAIL — los comandos no existen.

- [ ] **Step 3: Crear `PausarAvisoCommand`**

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/PausarAvisoCommand.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Pausa un aviso propio.</summary>
public sealed record PausarAvisoCommand(Guid Id, Guid VendedorId) : ICommand;

/// <summary>Handler de <see cref="PausarAvisoCommand"/>.</summary>
public sealed class PausarAvisoCommandHandler(IRepositorioAvisos repositorio, TimeProvider reloj)
    : ICommandHandler<PausarAvisoCommand>
{
    public async Task<Result> Handle(PausarAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.Id, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        return aviso.Pausar(reloj.GetUtcNow().UtcDateTime);
    }
}
```

- [ ] **Step 4: Crear `ReactivarAvisoCommand`**

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/ReactivarAvisoCommand.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Reactiva un aviso propio pausado.</summary>
public sealed record ReactivarAvisoCommand(Guid Id, Guid VendedorId) : ICommand;

/// <summary>Handler de <see cref="ReactivarAvisoCommand"/>.</summary>
public sealed class ReactivarAvisoCommandHandler(IRepositorioAvisos repositorio, TimeProvider reloj)
    : ICommandHandler<ReactivarAvisoCommand>
{
    public async Task<Result> Handle(ReactivarAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.Id, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        return aviso.Reactivar(reloj.GetUtcNow().UtcDateTime);
    }
}
```

- [ ] **Step 5: Crear `EliminarAvisoCommand`**

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/EliminarAvisoCommand.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Elimina (soft-delete) un aviso propio.</summary>
public sealed record EliminarAvisoCommand(Guid Id, Guid VendedorId) : ICommand;

/// <summary>Handler de <see cref="EliminarAvisoCommand"/>.</summary>
public sealed class EliminarAvisoCommandHandler(IRepositorioAvisos repositorio, TimeProvider reloj)
    : ICommandHandler<EliminarAvisoCommand>
{
    public async Task<Result> Handle(EliminarAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.Id, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        return aviso.Eliminar(reloj.GetUtcNow().UtcDateTime);
    }
}
```

- [ ] **Step 6: Verificar que pasan**

Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~TransicionesAvisoHandlerTests"` (desde `CaseritoApp/`)
Expected: PASS (7 casos).

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/PausarAvisoCommand.cs CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ReactivarAvisoCommand.cs CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/EliminarAvisoCommand.cs CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/TransicionesAvisoHandlerTests.cs
git commit -m "feat(catalog): pausar/reactivar/eliminar aviso (dueno)"
```

---

## Task 7: Queries del dueño — `ListarMisAvisosQuery` + `ObtenerMiAvisoQuery`

**Files:**
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/ListarMisAvisosQuery.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Avisos/ObtenerMiAvisoQuery.cs`
- Test: `tests/CaseritoApp.UnitTests/Catalog/QueriesAvisoHandlerTests.cs`

**Interfaces:**
- Consumes: `IQuery<TResponse>`, `IQueryHandler<,>`; `IRepositorioAvisos`; dominio; `Result`.
- Produces:
  - `ListarMisAvisosQuery(Guid VendedorId, int Pagina, int Tamano) : IQuery<ResultadoPaginado<AvisoResumenDto>>`; `ListarMisAvisosQueryValidator`.
  - `ObtenerMiAvisoQuery(Guid Id, Guid VendedorId) : IQuery<Result<AvisoDto>>`.
  - `IRepositorioAvisos.ObtenerAsync` ya existe (Task 4); `ObtenerMiAvisoQuery` mapea el `Aviso` a `AvisoDto` (helper `MapaAvisos.ADto(Aviso)` en el mismo archivo de DTOs — ver Step 3).

- [ ] **Step 1: Escribir los tests (fallan)**

Crear `tests/CaseritoApp.UnitTests/Catalog/QueriesAvisoHandlerTests.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class QueriesAvisoHandlerTests
{
    private static readonly DateTime _ahora = new(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc);

    private sealed class RepositorioFake(Aviso? aviso) : IRepositorioAvisos
    {
        public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(aviso);
        public void Agregar(Aviso a) { }
        public Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
            Guid vendedorId, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<AvisoResumenDto>(
                [new AvisoResumenDto(Guid.NewGuid(), "T", 1m, "BOB", Guid.NewGuid(), Guid.NewGuid(), "Nuevo", "Activo", _ahora)],
                pagina, tamano, 1));
    }

    private static Aviso AvisoDe(Guid vendedor) => Aviso.Crear(
        vendedor, "Titulo", "Desc", Dinero.Crear(10m, Moneda.BOB).Valor,
        Guid.NewGuid(), Guid.NewGuid(), CondicionArticulo.Nuevo, _ahora);

    [Fact]
    public async Task ListarMisAvisos_devuelve_pagina()
    {
        var handler = new ListarMisAvisosQueryHandler(new RepositorioFake(null));

        var r = await handler.Handle(new ListarMisAvisosQuery(Guid.NewGuid(), 1, 20), CancellationToken.None);

        Assert.Single(r.Items);
        Assert.Equal(1, r.Total);
    }

    [Fact]
    public async Task ObtenerMiAviso_ok_si_dueno()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        var handler = new ObtenerMiAvisoQueryHandler(new RepositorioFake(aviso));

        var r = await handler.Handle(new ObtenerMiAvisoQuery(aviso.Id, vendedor), CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Equal(aviso.Id, r.Valor.Id);
        Assert.Equal("BOB", r.Valor.Moneda);
    }

    [Fact]
    public async Task ObtenerMiAviso_404_si_eliminado()
    {
        var vendedor = Guid.NewGuid();
        var aviso = AvisoDe(vendedor);
        aviso.Eliminar(_ahora);
        var handler = new ObtenerMiAvisoQueryHandler(new RepositorioFake(aviso));

        var r = await handler.Handle(new ObtenerMiAvisoQuery(aviso.Id, vendedor), CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEncontrado, r.Error.Code);
    }

    [Fact]
    public async Task ObtenerMiAviso_403_si_no_dueno()
    {
        var aviso = AvisoDe(Guid.NewGuid());
        var handler = new ObtenerMiAvisoQueryHandler(new RepositorioFake(aviso));

        var r = await handler.Handle(new ObtenerMiAvisoQuery(aviso.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErroresAviso.NoEsPropietario, r.Error.Code);
    }

    [Fact]
    public void ListarMisAvisos_validator_rechaza_tamano_excesivo()
    {
        var validator = new ListarMisAvisosQueryValidator();
        Assert.False(validator.Validate(new ListarMisAvisosQuery(Guid.NewGuid(), 1, 500)).IsValid);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet build CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: FAIL — las queries no existen.

- [ ] **Step 3: Agregar el mapa a DTO en `DtosAviso.cs`**

Añadir al final de `src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAviso.cs`:

```csharp

/// <summary>Proyecciones de dominio a DTO reutilizables por queries.</summary>
public static class MapaAvisos
{
    /// <summary>Proyecta un <see cref="CaseritoApp.Catalog.Domain.Avisos.Aviso"/> a <see cref="AvisoDto"/>.</summary>
    public static AvisoDto ADto(CaseritoApp.Catalog.Domain.Avisos.Aviso aviso) => new(
        aviso.Id,
        aviso.VendedorId,
        aviso.Titulo,
        aviso.Descripcion,
        aviso.Precio.Monto,
        aviso.Precio.Moneda.ToString(),
        aviso.CategoriaId,
        aviso.CiudadId,
        aviso.Condicion.ToString(),
        aviso.Estado.ToString(),
        aviso.FechaCreacion,
        aviso.FechaActualizacion);
}
```

- [ ] **Step 4: Crear `ListarMisAvisosQuery`**

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/ListarMisAvisosQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Lista paginada de los avisos del vendedor autenticado (excluye eliminados).</summary>
public sealed record ListarMisAvisosQuery(Guid VendedorId, int Pagina, int Tamano)
    : IQuery<ResultadoPaginado<AvisoResumenDto>>;

/// <summary>Handler de <see cref="ListarMisAvisosQuery"/>.</summary>
public sealed class ListarMisAvisosQueryHandler(IRepositorioAvisos repositorio)
    : IQueryHandler<ListarMisAvisosQuery, ResultadoPaginado<AvisoResumenDto>>
{
    public Task<ResultadoPaginado<AvisoResumenDto>> Handle(
        ListarMisAvisosQuery request, CancellationToken cancellationToken) =>
        repositorio.ListarPorVendedorAsync(request.VendedorId, request.Pagina, request.Tamano, cancellationToken);
}

/// <summary>Valida la paginación de <see cref="ListarMisAvisosQuery"/>.</summary>
public sealed class ListarMisAvisosQueryValidator : AbstractValidator<ListarMisAvisosQuery>
{
    public ListarMisAvisosQueryValidator()
    {
        RuleFor(q => q.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(q => q.Tamano).InclusiveBetween(1, 100).WithMessage("El tamaño debe estar entre 1 y 100.");
    }
}
```

- [ ] **Step 5: Crear `ObtenerMiAvisoQuery`**

`src/Catalog/CaseritoApp.Catalog.Application/Avisos/ObtenerMiAvisoQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Obtiene el detalle de un aviso propio (para editar).</summary>
public sealed record ObtenerMiAvisoQuery(Guid Id, Guid VendedorId) : IQuery<Result<AvisoDto>>;

/// <summary>Handler de <see cref="ObtenerMiAvisoQuery"/>.</summary>
public sealed class ObtenerMiAvisoQueryHandler(IRepositorioAvisos repositorio)
    : IQueryHandler<ObtenerMiAvisoQuery, Result<AvisoDto>>
{
    public async Task<Result<AvisoDto>> Handle(ObtenerMiAvisoQuery request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.Id, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo<AvisoDto>(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo<AvisoDto>(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        return Result.Exito(MapaAvisos.ADto(aviso));
    }
}
```

- [ ] **Step 6: Verificar que pasan**

Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~QueriesAvisoHandlerTests"` (desde `CaseritoApp/`)
Expected: PASS (5 casos).

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ListarMisAvisosQuery.cs CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ObtenerMiAvisoQuery.cs CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAviso.cs CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/QueriesAvisoHandlerTests.cs
git commit -m "feat(catalog): queries del dueno (listar mis avisos + detalle)"
```

---

## Task 8: Queries de referencia — `ListarCategoriasQuery` + `ListarCiudadesQuery`

**Files:**
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Catalogo/ListarCategoriasQuery.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Application/Catalogo/ListarCiudadesQuery.cs`
- Test: `tests/CaseritoApp.UnitTests/Catalog/ReferenciaQueriesHandlerTests.cs`

**Interfaces:**
- Consumes: `IQuery<TResponse>`, `IQueryHandler<,>`; `IConsultaCatalogo`; `CategoriaDto`, `CiudadDto`.
- Produces: `ListarCategoriasQuery : IQuery<IReadOnlyList<CategoriaDto>>`; `ListarCiudadesQuery : IQuery<IReadOnlyList<CiudadDto>>`; sus handlers.

- [ ] **Step 1: Escribir los tests (fallan)**

Crear `tests/CaseritoApp.UnitTests/Catalog/ReferenciaQueriesHandlerTests.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Catalogo;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class ReferenciaQueriesHandlerTests
{
    private sealed class ConsultaFake : IConsultaCatalogo
    {
        public Task<bool> ExisteCategoriaActivaAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> ExisteCiudadActivaAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CategoriaDto>>([new CategoriaDto(Guid.NewGuid(), "Electrónica")]);
        public Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CiudadDto>>([new CiudadDto(Guid.NewGuid(), "Cochabamba")]);
    }

    [Fact]
    public async Task Categorias_devuelve_lista()
    {
        var handler = new ListarCategoriasQueryHandler(new ConsultaFake());
        var r = await handler.Handle(new ListarCategoriasQuery(), CancellationToken.None);
        Assert.Single(r);
    }

    [Fact]
    public async Task Ciudades_devuelve_lista()
    {
        var handler = new ListarCiudadesQueryHandler(new ConsultaFake());
        var r = await handler.Handle(new ListarCiudadesQuery(), CancellationToken.None);
        Assert.Single(r);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run: `dotnet build CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: FAIL — las queries no existen.

- [ ] **Step 3: Crear `ListarCategoriasQuery`**

`src/Catalog/CaseritoApp.Catalog.Application/Catalogo/ListarCategoriasQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Catalog.Application.Avisos;

namespace CaseritoApp.Catalog.Application.Catalogo;

/// <summary>Lista las categorías activas del catálogo.</summary>
public sealed record ListarCategoriasQuery : IQuery<IReadOnlyList<CategoriaDto>>;

/// <summary>Handler de <see cref="ListarCategoriasQuery"/>.</summary>
public sealed class ListarCategoriasQueryHandler(IConsultaCatalogo catalogo)
    : IQueryHandler<ListarCategoriasQuery, IReadOnlyList<CategoriaDto>>
{
    public Task<IReadOnlyList<CategoriaDto>> Handle(ListarCategoriasQuery request, CancellationToken cancellationToken) =>
        catalogo.ListarCategoriasAsync(cancellationToken);
}
```

- [ ] **Step 4: Crear `ListarCiudadesQuery`**

`src/Catalog/CaseritoApp.Catalog.Application/Catalogo/ListarCiudadesQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Catalog.Application.Avisos;

namespace CaseritoApp.Catalog.Application.Catalogo;

/// <summary>Lista las ciudades activas del catálogo.</summary>
public sealed record ListarCiudadesQuery : IQuery<IReadOnlyList<CiudadDto>>;

/// <summary>Handler de <see cref="ListarCiudadesQuery"/>.</summary>
public sealed class ListarCiudadesQueryHandler(IConsultaCatalogo catalogo)
    : IQueryHandler<ListarCiudadesQuery, IReadOnlyList<CiudadDto>>
{
    public Task<IReadOnlyList<CiudadDto>> Handle(ListarCiudadesQuery request, CancellationToken cancellationToken) =>
        catalogo.ListarCiudadesAsync(cancellationToken);
}
```

- [ ] **Step 5: Verificar que pasan**

Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~ReferenciaQueriesHandlerTests"` (desde `CaseritoApp/`)
Expected: PASS (2 casos).

- [ ] **Step 6: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Catalogo CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/ReferenciaQueriesHandlerTests.cs
git commit -m "feat(catalog): queries de referencia (categorias + ciudades)"
```

---

## Task 9: Infraestructura EF — DbContext, configuración, repos, UoW, DI, seeder

Sin tests unitarios propios (la lógica EF se cubre en integración, Task 12). Deliverable verificable por `dotnet build` (compila y el DI queda cableado). El agregado usa `Id` value-generated-never (igual que KYC, para evitar el UPDATE espurio por convención de PK Guid).

**Files:**
- Modify: `src/Catalog/CaseritoApp.Catalog.Infrastructure/CatalogDbContext.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConfiguracionCatalog.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/RepositorioAvisosEfCore.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaCatalogoEfCore.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Infrastructure/UnitOfWorkCatalog.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Infrastructure/SeedCatalogoExtensions.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Infrastructure/DependencyInjection.cs`
- Create: `src/Catalog/CaseritoApp.Catalog.Infrastructure/DesignTimeCatalogDbContextFactory.cs`

**Interfaces:**
- Consumes: `IRepositorioAvisos`, `IConsultaCatalogo`, DTOs, dominio; `IUnitOfWork`, `ConflictoConcurrenciaException` (BuildingBlocks.Application.Abstractions); EF Core.
- Produces: `CatalogDbContext.Avisos/Categorias/Ciudades`; `AgregarCatalog(IServiceCollection, IConfiguration)`; `SembrarCatalogoAsync(IServiceProvider, CancellationToken)`.

- [ ] **Step 1: Reescribir `CatalogDbContext`**

Reemplazar `src/Catalog/CaseritoApp.Catalog.Infrastructure/CatalogDbContext.cs`:

```csharp
using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Catalog.Infrastructure.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure;

/// <summary>DbContext del contexto Catalog (schema <c>catalog</c>).</summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    /// <summary>Avisos del marketplace.</summary>
    public DbSet<Aviso> Avisos => Set<Aviso>();

    /// <summary>Categorías de referencia.</summary>
    public DbSet<Categoria> Categorias => Set<Categoria>();

    /// <summary>Ciudades de referencia.</summary>
    public DbSet<Ciudad> Ciudades => Set<Ciudad>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        ConfiguracionCatalog.Configurar(modelBuilder);
    }
}
```

- [ ] **Step 2: Crear la configuración EF**

`src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConfiguracionCatalog.cs`:

```csharp
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure.Avisos;

/// <summary>Configuración EF Core de las entidades del contexto Catalog.</summary>
public static class ConfiguracionCatalog
{
    /// <summary>Configura <see cref="Aviso"/>, <see cref="Categoria"/> y <see cref="Ciudad"/>.</summary>
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<Aviso>(e =>
        {
            e.ToTable("Avisos");
            e.HasKey(a => a.Id);
            // Id asignado por el dominio (Entity base): sin value-generation para evitar UPDATE espurio.
            e.Property(a => a.Id).ValueGeneratedNever();
            e.Property(a => a.VendedorId).IsRequired();
            e.Property(a => a.Titulo).HasMaxLength(120).IsRequired();
            e.Property(a => a.Descripcion).HasMaxLength(2000).IsRequired();
            e.Property(a => a.CategoriaId).IsRequired();
            e.Property(a => a.CiudadId).IsRequired();
            e.Property(a => a.Condicion).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(a => a.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(a => a.FechaCreacion).IsRequired();
            e.Property(a => a.FechaActualizacion).IsRequired();

            e.OwnsOne(a => a.Precio, p =>
            {
                p.Property(x => x.Monto).HasColumnName("PrecioMonto").HasColumnType("decimal(18,2)").IsRequired();
                p.Property(x => x.Moneda).HasColumnName("PrecioMoneda").HasConversion<string>().HasMaxLength(3).IsRequired();
            });
            e.Navigation(a => a.Precio).IsRequired();

            e.HasIndex(a => a.VendedorId);
            e.HasIndex(a => a.Estado);
            e.HasIndex(a => new { a.VendedorId, a.Estado });

            // Los eventos de dominio no se persisten (dispatcher diferido).
            e.Ignore(a => a.EventosDeDominio);
        });

        builder.Entity<Categoria>(e =>
        {
            e.ToTable("Categorias");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Nombre).HasMaxLength(80).IsRequired();
            e.Property(c => c.Activa).IsRequired();
            e.Property(c => c.Orden).IsRequired();
        });

        builder.Entity<Ciudad>(e =>
        {
            e.ToTable("Ciudades");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Nombre).HasMaxLength(80).IsRequired();
            e.Property(c => c.Activa).IsRequired();
            e.Property(c => c.Orden).IsRequired();
        });
    }
}
```

- [ ] **Step 3: Crear `RepositorioAvisosEfCore`**

`src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/RepositorioAvisosEfCore.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure.Avisos;

/// <summary>Adaptador EF Core de <see cref="IRepositorioAvisos"/>.</summary>
public sealed class RepositorioAvisosEfCore(CatalogDbContext db) : IRepositorioAvisos
{
    public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Avisos.FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Agregar(Aviso aviso) => db.Avisos.Add(aviso);

    public async Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
        Guid vendedorId, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = db.Avisos
            .Where(a => a.VendedorId == vendedorId && a.Estado != EstadoAviso.Eliminado);

        var total = await consulta.CountAsync(ct);

        var items = await consulta
            .OrderByDescending(a => a.FechaCreacion)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(a => new AvisoResumenDto(
                a.Id,
                a.Titulo,
                a.Precio.Monto,
                a.Precio.Moneda.ToString(),
                a.CategoriaId,
                a.CiudadId,
                a.Condicion.ToString(),
                a.Estado.ToString(),
                a.FechaCreacion))
            .ToListAsync(ct);

        return new ResultadoPaginado<AvisoResumenDto>(items, pagina, tamano, total);
    }
}
```

- [ ] **Step 4: Crear `ConsultaCatalogoEfCore`**

`src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaCatalogoEfCore.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure.Avisos;

/// <summary>Adaptador EF Core de <see cref="IConsultaCatalogo"/>.</summary>
public sealed class ConsultaCatalogoEfCore(CatalogDbContext db) : IConsultaCatalogo
{
    public Task<bool> ExisteCategoriaActivaAsync(Guid categoriaId, CancellationToken ct) =>
        db.Categorias.AnyAsync(c => c.Id == categoriaId && c.Activa, ct);

    public Task<bool> ExisteCiudadActivaAsync(Guid ciudadId, CancellationToken ct) =>
        db.Ciudades.AnyAsync(c => c.Id == ciudadId && c.Activa, ct);

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct) =>
        await db.Categorias
            .Where(c => c.Activa)
            .OrderBy(c => c.Orden)
            .Select(c => new CategoriaDto(c.Id, c.Nombre))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken ct) =>
        await db.Ciudades
            .Where(c => c.Activa)
            .OrderBy(c => c.Orden)
            .Select(c => new CiudadDto(c.Id, c.Nombre))
            .ToListAsync(ct);
}
```

- [ ] **Step 5: Crear `UnitOfWorkCatalog`**

`src/Catalog/CaseritoApp.Catalog.Infrastructure/UnitOfWorkCatalog.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure;

/// <summary>
/// Implementación de <see cref="IUnitOfWork"/> para el contexto Catalog: delega en
/// <see cref="CatalogDbContext.SaveChangesAsync(CancellationToken)"/> y traduce el conflicto de
/// concurrencia a la excepción neutral <see cref="ConflictoConcurrenciaException"/>.
/// </summary>
public sealed class UnitOfWorkCatalog(CatalogDbContext dbContext) : IUnitOfWork
{
    public async Task<int> GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            return await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoConcurrenciaException(
                "Conflicto de concurrencia al persistir los cambios.", ex);
        }
    }
}
```

- [ ] **Step 6: Crear `SeedCatalogoExtensions`**

`src/Catalog/CaseritoApp.Catalog.Infrastructure/SeedCatalogoExtensions.cs`:

```csharp
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.Catalog.Infrastructure;

/// <summary>Seeding idempotente de categorías y ciudades de referencia del MVP.</summary>
public static class SeedCatalogoExtensions
{
    private static readonly (Guid Id, string Nombre, int Orden)[] _categorias =
    [
        (new("11111111-1111-1111-1111-000000000001"), "Electrónica", 1),
        (new("11111111-1111-1111-1111-000000000002"), "Vehículos", 2),
        (new("11111111-1111-1111-1111-000000000003"), "Hogar y muebles", 3),
        (new("11111111-1111-1111-1111-000000000004"), "Moda", 4),
        (new("11111111-1111-1111-1111-000000000005"), "Deportes", 5),
        (new("11111111-1111-1111-1111-000000000006"), "Mascotas", 6),
        (new("11111111-1111-1111-1111-000000000007"), "Bebés y niños", 7),
        (new("11111111-1111-1111-1111-000000000008"), "Libros y música", 8),
        (new("11111111-1111-1111-1111-000000000009"), "Servicios", 9),
        (new("11111111-1111-1111-1111-00000000000a"), "Otros", 10),
    ];

    private static readonly (Guid Id, string Nombre, int Orden)[] _ciudades =
    [
        (new("22222222-2222-2222-2222-000000000001"), "Cochabamba", 1),
        (new("22222222-2222-2222-2222-000000000002"), "Santa Cruz de la Sierra", 2),
        (new("22222222-2222-2222-2222-000000000003"), "La Paz", 3),
        (new("22222222-2222-2222-2222-000000000004"), "El Alto", 4),
        (new("22222222-2222-2222-2222-000000000005"), "Sucre", 5),
        (new("22222222-2222-2222-2222-000000000006"), "Oruro", 6),
        (new("22222222-2222-2222-2222-000000000007"), "Tarija", 7),
        (new("22222222-2222-2222-2222-000000000008"), "Potosí", 8),
    ];

    /// <summary>
    /// Asegura que existan las categorías y ciudades del MVP (por id fijo). Idempotente: re-ejecutar
    /// no crea duplicados. Debe llamarse DESPUÉS de aplicar las migraciones.
    /// </summary>
    public static async Task SembrarCatalogoAsync(this IServiceProvider proveedor, CancellationToken ct = default)
    {
        using var scope = proveedor.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        foreach (var (id, nombre, orden) in _categorias)
        {
            if (!await db.Categorias.AnyAsync(c => c.Id == id, ct))
            {
                db.Categorias.Add(Categoria.Crear(id, nombre, orden));
            }
        }

        foreach (var (id, nombre, orden) in _ciudades)
        {
            if (!await db.Ciudades.AnyAsync(c => c.Id == id, ct))
            {
                db.Ciudades.Add(Ciudad.Crear(id, nombre, orden));
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 7: Crear `DependencyInjection`**

`src/Catalog/CaseritoApp.Catalog.Infrastructure/DependencyInjection.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Infrastructure.Avisos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.Catalog.Infrastructure;

/// <summary>Registro de persistencia y adaptadores del contexto Catalog.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el <see cref="CatalogDbContext"/> (si hay cadena de conexión), los repositorios y
    /// la unidad de trabajo del contexto. La unidad de trabajo se registra de forma incondicional
    /// (misma razón que en Identity: en tests el DbContext se cablea aparte).
    /// </summary>
    public static IServiceCollection AgregarCatalog(this IServiceCollection servicios, IConfiguration config)
    {
        var cadena = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(cadena))
        {
            servicios.AddDbContext<CatalogDbContext>(o => o.UseSqlServer(cadena));
        }

        servicios.AddScoped<IRepositorioAvisos, RepositorioAvisosEfCore>();
        servicios.AddScoped<IConsultaCatalogo, ConsultaCatalogoEfCore>();
        servicios.AddScoped<IUnitOfWork, UnitOfWorkCatalog>();

        return servicios;
    }
}
```

- [ ] **Step 8: Crear `DesignTimeCatalogDbContextFactory`**

`src/Catalog/CaseritoApp.Catalog.Infrastructure/DesignTimeCatalogDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaseritoApp.Catalog.Infrastructure;

/// <summary>
/// Factory de diseño para que <c>dotnet ef</c> cree el <see cref="CatalogDbContext"/> sin arrancar
/// el host. La cadena es solo para el proceso de diseño (generación de migraciones).
/// </summary>
public sealed class DesignTimeCatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=CaseritoDb;User Id=sa;Password=noop;TrustServerCertificate=True")
            .Options;
        return new CatalogDbContext(opciones);
    }
}
```

- [ ] **Step 9: Agregar el paquete Design a Catalog.Infrastructure (para `dotnet ef`)**

En `src/Catalog/CaseritoApp.Catalog.Infrastructure/CaseritoApp.Catalog.Infrastructure.csproj`, dentro del `ItemGroup` de `PackageReference`, añadir (sin `Version=`, por CPM):

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
```

- [ ] **Step 10: Verificar compilación**

Run: `dotnet build CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: PASS (0 warnings/errors). No hay tests nuevos en esta tarea; la suite existente sigue verde.

- [ ] **Step 11: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure
git commit -m "feat(catalog): persistencia EF (DbContext, repos, UoW, DI, seeder)"
```

---

## Task 10: Migración `CatalogInicial`

**Files:**
- Create: `src/Catalog/CaseritoApp.Catalog.Infrastructure/Migrations/*_CatalogInicial.cs` (generada)

**Interfaces:**
- Consumes: `CatalogDbContext`, `DesignTimeCatalogDbContextFactory`.
- Produces: migración inicial del schema `catalog` (tablas `Avisos`, `Categorias`, `Ciudades`).

- [ ] **Step 1: Generar la migración**

Desde `CaseritoApp/`:

```bash
dotnet ef migrations add CatalogInicial \
  --project src/Catalog/CaseritoApp.Catalog.Infrastructure \
  --startup-project src/Host/CaseritoApp.Host \
  --context CatalogDbContext \
  --output-dir Migrations
```

Nota: el `--startup-project` es el Host; requiere que el Host ya referencie Catalog.Infrastructure. Si aún no se hizo Task 11, agregar temporalmente la `ProjectReference` del Step 1 de Task 11 antes de generar, o ejecutar Task 11 Step 1 primero. **Recomendado: hacer el Step 1 de Task 11 (ref del csproj) antes de esta tarea.**

- [ ] **Step 2: Verificar la migración generada**

Confirmar que el archivo `*_CatalogInicial.cs` crea el schema `catalog` con las tablas `Avisos` (incl. columnas `PrecioMonto` decimal(18,2) y `PrecioMoneda` nvarchar(3)), `Categorias`, `Ciudades`, y los índices de `Avisos`.

Run: `dotnet build CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Migrations
git commit -m "feat(catalog): migracion inicial del schema catalog"
```

---

## Task 11: Wiring del Host — endpoints, Program.cs, factory de tests

Esta tarea cablea todo: registra Catalog en el host y **al mismo tiempo** registra `CatalogDbContext` en la `CaseritoApiFactory`. Deben ir juntas: al registrar `UnitOfWorkCatalog` como `IUnitOfWork`, el `UnitOfWorkBehavior` intentará resolverlo en cada request de MediatR (incluidos los de Identity), lo que exige que `CatalogDbContext` exista en todos los tests de integración.

**Files:**
- Modify: `src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj`
- Create: `src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs`
- Create: `src/Host/CaseritoApp.Host/Endpoints/CatalogoEndpoints.cs`
- Modify: `src/Host/CaseritoApp.Host/Program.cs`
- Modify: `tests/CaseritoApp.IntegrationTests/Infrastructure/CaseritoApiFactory.cs`

**Interfaces:**
- Consumes: commands/queries de Catalog.Application; `ISender`; `ClaimsPrincipal`; `AgregarCatalog`, `SembrarCatalogoAsync`, `CatalogDbContext`.
- Produces: grupos `/api/avisos` y `/api/catalogo`; `MapAvisosEndpoints`, `MapCatalogoEndpoints`.

- [ ] **Step 1: Referenciar Catalog.Infrastructure desde el Host**

En `src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj`, dentro del primer `ItemGroup` de `ProjectReference`, añadir:

```xml
    <ProjectReference Include="..\..\Catalog\CaseritoApp.Catalog.Infrastructure\CaseritoApp.Catalog.Infrastructure.csproj" />
```

- [ ] **Step 2: Crear `AvisosEndpoints`**

`src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Cuerpo para crear un aviso.</summary>
public sealed record CrearAvisoRequest(
    string Titulo, string Descripcion, decimal Monto, string Condicion, Guid CategoriaId, Guid CiudadId);

/// <summary>Cuerpo para editar un aviso.</summary>
public sealed record EditarAvisoRequest(
    string Titulo, string Descripcion, decimal Monto, string Condicion, Guid CategoriaId, Guid CiudadId);

/// <summary>Respuesta de creación de un aviso.</summary>
public sealed record AvisoCreadoResponse(Guid Id);

/// <summary>Endpoints del dueño sobre sus avisos, bajo <c>/api/avisos</c>.</summary>
public static class AvisosEndpoints
{
    /// <summary>Mapea los endpoints de avisos (todos <c>[Authorize]</c>).</summary>
    public static IEndpointRouteBuilder MapAvisosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/avisos").RequireAuthorization();

        grupo.MapPost("/", CrearAsync)
            .Accepts<CrearAvisoRequest>("application/json")
            .Produces<AvisoCreadoResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        grupo.MapPut("/{id:guid}", EditarAsync)
            .Accepts<EditarAvisoRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        grupo.MapPost("/{id:guid}/pausar", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => TransicionAsync(new PausarAvisoCommand(id, UserId(u)), u, s, ct))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/reactivar", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => TransicionAsync(new ReactivarAvisoCommand(id, UserId(u)), u, s, ct))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapDelete("/{id:guid}", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => TransicionAsync(new EliminarAvisoCommand(id, UserId(u)), u, s, ct))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapGet("/mios", ListarMiosAsync)
            .Produces<ResultadoPaginado<AvisoResumenDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        grupo.MapGet("/mios/{id:guid}", ObtenerMioAsync)
            .Produces<AvisoDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CrearAsync(
        CrearAvisoRequest request, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(
                new CrearAvisoCommand(
                    userId, EstaVerificado(usuario), request.Titulo, request.Descripcion,
                    request.Monto, request.Condicion, request.CategoriaId, request.CiudadId), ct);

            return resultado.EsExito
                ? Results.Created($"/api/avisos/mios/{resultado.Valor}", new AvisoCreadoResponse(resultado.Valor))
                : DesdeError(resultado.Error);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> EditarAsync(
        Guid id, EditarAvisoRequest request, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(
                new EditarAvisoCommand(
                    id, userId, request.Titulo, request.Descripcion,
                    request.Monto, request.Condicion, request.CategoriaId, request.CiudadId), ct);

            return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> TransicionAsync(
        IRequest<Result> comando, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out _))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(comando, ct);
        return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
    }

    private static async Task<IResult> ListarMiosAsync(
        ClaimsPrincipal usuario, ISender sender, CancellationToken ct, int pagina = 1, int tamano = 20)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new ListarMisAvisosQuery(userId, pagina, tamano), ct);
            return Results.Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> ObtenerMioAsync(
        Guid id, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new ObtenerMiAvisoQuery(id, userId), ct);
        return resultado.EsExito ? Results.Ok(resultado.Valor) : DesdeError(resultado.Error);
    }

    private static Guid UserId(ClaimsPrincipal usuario) => TryUserId(usuario, out var id) ? id : Guid.Empty;

    private static bool TryUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(valor, out userId);
    }

    private static bool EstaVerificado(ClaimsPrincipal usuario) =>
        string.Equals(usuario.FindFirstValue("verificado"), "true", StringComparison.OrdinalIgnoreCase);

    private static IResult ProblemaDeValidacion(ValidationException ex) =>
        Results.ValidationProblem(ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    // Mapea el Error de dominio de Catalog a códigos HTTP.
    private static IResult DesdeError(Error error) => error.Code switch
    {
        ErroresAviso.NoEncontrado =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status404NotFound),
        ErroresAviso.NoEsPropietario or ErroresAviso.NoVerificado =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status403Forbidden),
        ErroresAviso.TransicionInvalida =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status409Conflict),
        _ =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
    };
}
```

- [ ] **Step 3: Crear `CatalogoEndpoints`**

`src/Host/CaseritoApp.Host/Endpoints/CatalogoEndpoints.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Catalogo;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Endpoints de catálogos de referencia bajo <c>/api/catalogo</c>.</summary>
public static class CatalogoEndpoints
{
    /// <summary>Mapea los endpoints de referencia (todos <c>[Authorize]</c>).</summary>
    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/catalogo").RequireAuthorization();

        grupo.MapGet("/categorias", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListarCategoriasQuery(), ct)))
            .Produces<IReadOnlyList<CategoriaDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/ciudades", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListarCiudadesQuery(), ct)))
            .Produces<IReadOnlyList<CiudadDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}
```

- [ ] **Step 4: Modificar `Program.cs`**

En `src/Host/CaseritoApp.Host/Program.cs` aplicar cuatro cambios:

**(a)** Ampliar `AddMediatR` y `AddValidatorsFromAssembly` para incluir el ensamblado de Catalog.Application. Reemplazar el bloque `AddMediatR(...)` y la línea de validators por:

```csharp
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(
        typeof(CaseritoApp.BuildingBlocks.Application.Abstractions.IUnitOfWork).Assembly,
        typeof(ObtenerPerfilQuery).Assembly,
        typeof(CaseritoApp.Catalog.Application.Avisos.CrearAvisoCommand).Assembly));

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

builder.Services.AddValidatorsFromAssembly(typeof(ObtenerPerfilQuery).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(CaseritoApp.Catalog.Application.Avisos.CrearAvisoCommand).Assembly);
```

**(b)** Registrar Catalog junto a Identity. Después de `builder.Services.AgregarAutenticacionJwt(...)` añadir:

```csharp
builder.Services.AgregarCatalog(builder.Configuration);
```

(Y agregar el `using CaseritoApp.Catalog.Infrastructure;` al inicio del archivo.)

**(c)** Migrar + sembrar Catalog en el mismo bloque de arranque. Dentro del `if (ejecutarMigraciones && !string.IsNullOrWhiteSpace(cadenaConexion))`, después de migrar Identity y `SembrarRolesAsync()`, añadir:

```csharp
        using (var scopeCatalog = app.Services.CreateScope())
        {
            var dbCatalog = scopeCatalog.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await dbCatalog.Database.MigrateAsync();
        }

        await app.Services.SembrarCatalogoAsync();
```

**(d)** Mapear los endpoints. Después de `app.MapKycEndpoints();` añadir:

```csharp
app.MapAvisosEndpoints();
app.MapCatalogoEndpoints();
```

- [ ] **Step 5: Registrar `CatalogDbContext` en `CaseritoApiFactory`**

En `tests/CaseritoApp.IntegrationTests/Infrastructure/CaseritoApiFactory.cs`:

Añadir el `using`:

```csharp
using CaseritoApp.Catalog.Infrastructure;
```

En `ConfigureWebHost`, dentro de `builder.ConfigureServices(...)`, después del re-registro de `IdentityDbContext`, añadir:

```csharp
            servicios.RemoveAll<DbContextOptions<CatalogDbContext>>();
            servicios.AddDbContext<CatalogDbContext>(o => o.UseSqlServer(_sql.GetConnectionString()));
```

En `InitializeAsync`, dentro del `using (var scope = ...)` existente, después de migrar `IdentityDbContext`, añadir la migración de Catalog y, tras cerrar el scope, sembrar el catálogo:

```csharp
            var dbCatalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await dbCatalog.Database.MigrateAsync();
```

y después de `await Services.SembrarRolesAsync();` añadir:

```csharp
        await Services.SembrarCatalogoAsync();
```

- [ ] **Step 6: Verificar build + suite completa (sin regresiones)**

Run (desde `CaseritoApp/`, requiere Docker):
```bash
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
```
Expected: build 0/0; toda la suite existente (incl. Identity/KYC integración) sigue PASS. Aún no hay tests de avisos de integración (Task 12).

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Host CaseritoApp/tests/CaseritoApp.IntegrationTests/Infrastructure/CaseritoApiFactory.cs
git commit -m "feat(catalog): wiring del host (endpoints avisos/catalogo) + factory de tests"
```

---

## Task 12: Tests de integración — flujo de avisos

**Files:**
- Create: `tests/CaseritoApp.IntegrationTests/AvisosFlujoTests.cs`

**Interfaces:**
- Consumes: `CaseritoApiFactory`; endpoints `/api/auth/*`, `/api/kyc/*`, `/api/admin/kyc/*`, `/api/avisos/*`, `/api/catalogo/*`; records de request/response del Host.

Nota: para obtener un token **verificado** hay que aprobar el KYC (subir CI+selfie como usuario, aprobar como admin, re-loguear). Se reutiliza ese flujo. La categoría/ciudad se toman del seeder (ids fijos del Task 9).

- [ ] **Step 1: Escribir los tests de integración (fallan hasta correr contra la app cableada)**

Crear `tests/CaseritoApp.IntegrationTests/AvisosFlujoTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Flujo del dueño sobre sus avisos: crear (verificado), listar, detalle, pausar/reactivar, eliminar, y gates 403.</summary>
public sealed class AvisosFlujoTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad = new("22222222-2222-2222-2222-000000000001");
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string Email(string p) => $"{p}-{Guid.NewGuid():N}@caserito.test";

    private async Task<string> RegistrarYLoguearAsync(HttpClient cliente, string email, string? rolExtra = null)
    {
        var reg = await cliente.PostAsJsonAsync("/api/auth/register", new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        if (rolExtra is not null)
        {
            using var scope = factory.Services.CreateScope();
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await um.AddToRoleAsync((await um.FindByEmailAsync(email))!, rolExtra);
        }

        return await LoguearAsync(cliente, email);
    }

    private static async Task<string> LoguearAsync(HttpClient cliente, string email)
    {
        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
    }

    private static HttpRequestMessage Con(HttpMethod m, string url, string token)
    {
        var s = new HttpRequestMessage(m, url);
        s.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return s;
    }

    private static MultipartFormDataContent FormularioKyc()
    {
        var c = new MultipartFormDataContent();
        var doc = new ByteArrayContent(_png); doc.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        c.Add(doc, "documento", "ci.png");
        var self = new ByteArrayContent(_png); self.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        c.Add(self, "selfie", "selfie.png");
        return c;
    }

    // Registra un usuario, aprueba su KYC con un admin y devuelve un token ya verificado.
    private async Task<string> UsuarioVerificadoAsync(HttpClient cliente)
    {
        var email = Email("aviso-user");
        var token = await RegistrarYLoguearAsync(cliente, email);
        var tokenAdmin = await RegistrarYLoguearAsync(cliente, Email("aviso-admin"), RolesApp.AdminKyc);

        using var subir = Con(HttpMethod.Post, "/api/kyc/", token);
        subir.Content = FormularioKyc();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subir)).StatusCode);

        Guid usuarioId;
        using (var scope = factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            usuarioId = (await um.FindByEmailAsync(email))!.Id;
        }

        using var listar = Con(HttpMethod.Get, "/api/admin/kyc/?estado=Pendiente&tamano=100", tokenAdmin);
        var pagina = await (await cliente.SendAsync(listar)).Content.ReadFromJsonAsync<PaginaKyc>();
        var solicitud = pagina!.Items.Single(s => s.UsuarioId == usuarioId);

        using var aprobar = Con(HttpMethod.Post, $"/api/admin/kyc/{solicitud.SolicitudId}/aprobar", tokenAdmin);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(aprobar)).StatusCode);

        // Re-login para que el JWT traiga el claim verificado=true.
        return await LoguearAsync(cliente, email);
    }

    private static CrearAvisoRequest AvisoValido() =>
        new("Bicicleta", "Rodado 29, poco uso", 1200m, "Usado", _categoria, _ciudad);

    [Fact]
    public async Task Verificado_crea_lista_detalle_pausa_reactiva_y_elimina()
    {
        using var cliente = factory.CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        // Crear
        using var crear = Con(HttpMethod.Post, "/api/avisos/", token);
        crear.Content = JsonContent.Create(AvisoValido());
        var respCrear = await cliente.SendAsync(crear);
        Assert.Equal(HttpStatusCode.Created, respCrear.StatusCode);
        var creado = await respCrear.Content.ReadFromJsonAsync<AvisoCreadoResponse>();
        var avisoId = creado!.Id;

        // Listar
        using var listar = Con(HttpMethod.Get, "/api/avisos/mios", token);
        var lista = await (await cliente.SendAsync(listar)).Content.ReadFromJsonAsync<PaginaAvisos>();
        Assert.Contains(lista!.Items, a => a.Id == avisoId);

        // Detalle
        using var detalle = Con(HttpMethod.Get, $"/api/avisos/mios/{avisoId}", token);
        Assert.Equal(HttpStatusCode.OK, (await cliente.SendAsync(detalle)).StatusCode);

        // Pausar y reactivar
        using var pausar = Con(HttpMethod.Post, $"/api/avisos/{avisoId}/pausar", token);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(pausar)).StatusCode);
        using var reactivar = Con(HttpMethod.Post, $"/api/avisos/{avisoId}/reactivar", token);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(reactivar)).StatusCode);

        // Eliminar (soft-delete) → luego 404 en detalle y ausente del listado
        using var eliminar = Con(HttpMethod.Delete, $"/api/avisos/{avisoId}", token);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(eliminar)).StatusCode);
        using var detalle2 = Con(HttpMethod.Get, $"/api/avisos/mios/{avisoId}", token);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.SendAsync(detalle2)).StatusCode);
        using var listar2 = Con(HttpMethod.Get, "/api/avisos/mios", token);
        var lista2 = await (await cliente.SendAsync(listar2)).Content.ReadFromJsonAsync<PaginaAvisos>();
        Assert.DoesNotContain(lista2!.Items, a => a.Id == avisoId);
    }

    [Fact]
    public async Task No_verificado_no_puede_crear()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("aviso-noverif"));

        using var crear = Con(HttpMethod.Post, "/api/avisos/", token);
        crear.Content = JsonContent.Create(AvisoValido());
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.SendAsync(crear)).StatusCode);
    }

    [Fact]
    public async Task No_dueno_no_puede_editar()
    {
        using var cliente = factory.CreateClient();
        var dueno = await UsuarioVerificadoAsync(cliente);
        var otro = await UsuarioVerificadoAsync(cliente);

        using var crear = Con(HttpMethod.Post, "/api/avisos/", dueno);
        crear.Content = JsonContent.Create(AvisoValido());
        var avisoId = (await (await cliente.SendAsync(crear)).Content.ReadFromJsonAsync<AvisoCreadoResponse>())!.Id;

        using var editar = Con(HttpMethod.Put, $"/api/avisos/{avisoId}", otro);
        editar.Content = JsonContent.Create(new EditarAvisoRequest("Hack", "Intento", 1m, "Nuevo", _categoria, _ciudad));
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.SendAsync(editar)).StatusCode);
    }

    [Fact]
    public async Task Categorias_y_ciudades_de_referencia_estan_sembradas()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("aviso-ref"));

        using var cats = Con(HttpMethod.Get, "/api/catalogo/categorias", token);
        var categorias = await (await cliente.SendAsync(cats)).Content.ReadFromJsonAsync<CategoriaRef[]>();
        Assert.Contains(categorias!, c => c.Id == _categoria);

        using var ciudades = Con(HttpMethod.Get, "/api/catalogo/ciudades", token);
        var lista = await (await cliente.SendAsync(ciudades)).Content.ReadFromJsonAsync<CiudadRef[]>();
        Assert.Contains(lista!, c => c.Id == _ciudad);
    }
}

sealed file record TokenAccesoResponseA(string AccessToken);
sealed file record AvisoResumen(Guid Id, string Titulo, decimal Monto, string Moneda, Guid CategoriaId, Guid CiudadId, string Condicion, string Estado, DateTime FechaCreacion);
sealed file record PaginaAvisos(AvisoResumen[] Items, int Pagina, int Tamano, int Total);
sealed file record SolicitudKyc(Guid SolicitudId, Guid UsuarioId, string Estado, string TipoDocumento, DateTimeOffset EnviadaEn, DateTimeOffset? ResueltaEn);
sealed file record PaginaKyc(SolicitudKyc[] Items, int Pagina, int Tamano, int Total);
sealed file record CategoriaRef(Guid Id, string Nombre);
sealed file record CiudadRef(Guid Id, string Nombre);
```

Nota: si `RegistroRequest`/`LoginRequest`/`TokenAccesoResponse` ya son públicos en `CaseritoApp.Host.Endpoints` (usados por otros tests de integración), reutilizarlos y borrar los `file record` duplicados que no hagan falta. Los `file record` de arriba cubren solo los tipos no expuestos por el Host. Verificar contra los tipos existentes al implementar (p. ej. `TokenAccesoResponse` ya existe y se usa en `KycFlujoTests`).

- [ ] **Step 2: Ejecutar los tests de integración**

Run (desde `CaseritoApp/`, requiere Docker):
```bash
dotnet test CaseritoApp.sln --filter "FullyQualifiedName~AvisosFlujoTests"
```
Expected: PASS (4 casos).

- [ ] **Step 3: Ejecutar la suite completa**

Run: `dotnet test CaseritoApp.sln` (desde `CaseritoApp/`)
Expected: toda la suite PASS.

- [ ] **Step 4: Commit**

```bash
git add CaseritoApp/tests/CaseritoApp.IntegrationTests/AvisosFlujoTests.cs
git commit -m "test(catalog): integracion del flujo de avisos (crear/editar/pausar/eliminar + gates)"
```

---

## Task 13: Regenerar el contrato OpenAPI + cliente TS

Los endpoints nuevos deben entrar al contrato versionado (`CaseritoApp.Host.json`) y al cliente tipado (`web/src/api/schema.d.ts`), o el job `contract` del CI falla por deriva. La emisión es opt-in y requiere entorno `Testing` (por el fail-fast del encryptor al instanciar el host).

**Files:**
- Modify (generado): `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`
- Modify (generado): `web/src/api/schema.d.ts`

**Interfaces:**
- Consumes: los endpoints de Task 11.
- Produces: contrato + tipos TS actualizados con `/api/avisos/*` y `/api/catalogo/*`.

- [ ] **Step 1: Emitir el documento OpenAPI (opt-in, entorno Testing)**

Desde la raíz del repo, en PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
dotnet build CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj -p:GenerateOpenApi=true
```

Expected: build OK; `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` actualizado con los paths de avisos y catálogo.

- [ ] **Step 2: Regenerar el cliente TS**

Desde `web/`:

```bash
npm run generate:api
```

Expected: `web/src/api/schema.d.ts` incluye los nuevos paths y componentes (`AvisoDto`, `AvisoResumenDto`, `CrearAvisoRequest`, etc.).

- [ ] **Step 3: Verificar que no hay deriva y que el front sigue tipando**

Desde `web/`:

```bash
npm run typecheck
```

Expected: PASS (los tipos generados compilan; no se consume aún ningún endpoint nuevo desde el front —eso es 2E—, así que no debería haber errores).

- [ ] **Step 4: Commit**

```bash
git add CaseritoApp/artifacts/openapi/CaseritoApp.Host.json web/src/api/schema.d.ts
git commit -m "chore(catalog): contrato OpenAPI + cliente TS con endpoints de avisos/catalogo"
```

---

## Cierre de rama

Al terminar todas las tareas, antes de la revisión final:

- [ ] Ejecutar la suite completa desde `CaseritoApp/`: `dotnet test CaseritoApp.sln` (requiere Docker). Todo verde.
- [ ] Verificar formato como el CI: `dotnet format CaseritoApp.sln --verify-no-changes`.
- [ ] Confirmar que los tests de arquitectura pasan (aislamiento de contexto, capas, schema por contexto) — cubren Catalog automáticamente.
- [ ] Revisión final de rama con el subagente `code-reviewer-caserito` (modelo Opus), foco en: capas/aislamiento, anti-PII en logs, mapeo Result→HTTP, y que `UnitOfWorkBehavior` multi-contexto no rompa Identity.
- [ ] Merge local a `master` lo autoriza el usuario explícitamente (ver flujo de ramas).

---

## Self-Review (verificación del plan contra el spec)

**1. Cobertura del spec:**
- Dominio `Aviso` (título, descripción, precio, categoría, ciudad, condición, estado, fechas) → Task 1-2. ✓
- `Money`/`Dinero` VO solo BOB → Task 1. ✓
- Categorías catálogo fijo sembrado (`Categoria`) → Task 2, 9. ✓
- Ubicación ciudad de catálogo fijo (`Ciudad`) → Task 2, 9. ✓
- Máquina de estados Activo⇄Pausado + Eliminado soft-delete terminal → Task 2. ✓
- Gate KYC (`verificado`) en crear → Task 4 (handler) + Task 11 (claim en endpoint) + Task 12 (integración). ✓
- Casos de uso: crear/editar/pausar/reactivar/eliminar → Task 4,5,6. ✓
- Queries dueño: listar mis avisos + detalle propio → Task 7. ✓
- Endpoints de referencia categorías/ciudades → Task 8, 11. ✓
- Endpoints `/api/avisos` + `/api/catalogo` con contrato OpenAPI → Task 11, 13. ✓
- Persistencia schema `catalog` sin FK cruzada, migración, seeder → Task 9, 10. ✓
- Eventos de dominio (sin dispatcher) → Task 1-2. ✓
- Tests unit (dominio/handlers/validators) + integración + arquitectura → Task 1-8, 12. ✓
- Anti-PII → Global Constraints + sin logging de contenido en ningún handler/endpoint. ✓

**2. Placeholders:** ninguno pendiente. Nota: en Task 12 el `file record TokenAccesoResponseA` es un remanente no usado — el implementer debe **eliminarlo** y reutilizar los tipos públicos ya existentes del Host (`RegistroRequest`, `LoginRequest`, `TokenAccesoResponse`) al reconciliar contra el código existente (indicado en la nota del Step 1).

**3. Consistencia de tipos:** las firmas de `IRepositorioAvisos`/`IConsultaCatalogo` (Task 4) se usan idénticas en handlers (Task 4-7) y adaptadores EF (Task 9). `ResultadoPaginado<T>`, `AvisoDto`, `AvisoResumenDto`, `CategoriaDto`, `CiudadDto` definidos en Task 4 y consumidos consistentemente. `CrearAvisoCommand`/`EditarAvisoCommand` comparten forma de request en los endpoints (Task 11). `UnitOfWorkBehavior(IEnumerable<IUnitOfWork>)` (Task 3) consumido por el registro DI de Identity (existente) y Catalog (Task 9). ✓

**4. Orden de dependencias / suite verde por commit:** Task 3 (multi-UoW) aterriza antes de registrar el segundo `IUnitOfWork` (Task 9/11); el registro efectivo (Task 11) va junto con el alta de `CatalogDbContext` en la factory (mismo commit), evitando romper los tests de integración de Identity. ✓
