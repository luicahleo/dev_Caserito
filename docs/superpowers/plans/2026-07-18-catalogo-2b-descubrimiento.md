# Descubrimiento de avisos (Catálogo 2B) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** exponer endpoints anónimos que permitan al público buscar/filtrar avisos Activos y ver el detalle de uno, sin exponer datos del vendedor.

**Architecture:** read-side puro sobre el contexto Catalog. Un puerto de Application (`IConsultaAvisosPublica`) con adaptador EF Core que consulta `CatalogDbContext`, dos queries CQRS-lite (MediatR), y dos endpoints minimal-API anónimos en el host. Sin cambios de dominio ni de esquema. La búsqueda de texto es `LIKE` tokenizado con colación acento-insensible; los filtros se aplican como `Where` condicionales.

**Tech Stack:** .NET 10, EF Core (SQL Server), MediatR, FluentValidation, minimal APIs, xUnit + Testcontainers.MsSql.

## Global Constraints

- `nullable enable`, warnings-as-errors, analizadores .NET + Roslynator + Sonar: nada compila si viola las reglas.
- Namespaces file-scoped; `using` fuera del namespace, `System` primero.
- `PascalCase` tipos/miembros; `camelCase` locales; `_camelCase` campos privados; `I` en interfaces.
- Versiones de paquetes solo en `Directory.Packages.props` (CPM). Nunca `Version=` en un `.csproj`. (Este bloque no añade paquetes.)
- Textos de UI/errores y comentarios en **español**. Archivos guardados en **UTF-8 correcto** (cuidar mojibake en acentos).
- **Anti-PII:** jamás loguear datos sensibles. Este bloque no loguea; no introducir logs con contenido de avisos ni ids de usuario.
- **Aislamiento de contexto:** `Catalog` NO referencia otros bounded contexts (verificado por `CaseritoApp.ArchitectureTests`).
- Guardrail de commits del implementer: solo `git add <archivos>` + `git commit`. Nada de `reset`/`rebase`/`checkout`/`amend`.
- Rama de trabajo: `feat/catalogo-2b-descubrimiento` (ya creada; el spec está commiteado en ella).

## Referencias del repo (patrones a imitar)

- Query paginada existente: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ListarMisAvisosQuery.cs`.
- `ResultadoPaginado<T>`: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ResultadoPaginado.cs`.
- DTOs de 2A: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAviso.cs`.
- Adaptador de consulta EF: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaCatalogoEfCore.cs` y `RepositorioAvisosEfCore.cs`.
- Registro DI: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/DependencyInjection.cs`.
- Endpoints y helper de validación: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs`, `CatalogoEndpoints.cs`.
- Registro de endpoints: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs:80-90`.
- Test de integración de 2A: `CaseritoApp/tests/CaseritoApp.IntegrationTests/AvisosFlujoTests.cs`.
- Unit tests de validators de 2A: `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/CrearAvisoCommandValidatorTests.cs`.

## File Structure

**Crear:**
- `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAvisoPublico.cs` — `AvisoPublicoResumenDto`, `AvisoPublicoDto`.
- `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/IConsultaAvisosPublica.cs` — puerto + `FiltroBusquedaAvisos`.
- `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/BuscarAvisosQuery.cs` — query + handler + validator.
- `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ObtenerAvisoPublicoQuery.cs` — query + handler.
- `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs` — adaptador EF.
- `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PublicoEndpoints.cs` — endpoints anónimos.
- `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/BuscarAvisosQueryValidatorTests.cs`
- `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/BuscarAvisosQueryHandlerTests.cs`
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/DescubrimientoAvisosTests.cs`

**Modificar:**
- `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/DependencyInjection.cs` — registrar el nuevo adaptador.
- `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs` — `app.MapPublicoEndpoints();`.
- `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` y `web/src/api/schema.d.ts` — regenerados (Task 7).
- (Task 8) `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs`, `CrearAvisoCommand.cs`, `EditarAvisoCommand.cs`, `Domain/Avisos/Categoria.cs`, `Ciudad.cs`.

---

### Task 1: Contratos de Application (DTOs públicos + puerto)

**Files:**
- Create: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAvisoPublico.cs`
- Create: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/IConsultaAvisosPublica.cs`

**Interfaces:**
- Consumes: `CondicionArticulo` (`CaseritoApp.Catalog.Domain.Avisos`), `ResultadoPaginado<T>`.
- Produces: `AvisoPublicoResumenDto`, `AvisoPublicoDto`, `FiltroBusquedaAvisos`, `IConsultaAvisosPublica` (usados por las Tasks 2, 3, 4).

- [ ] **Step 1: Crear los DTOs públicos**

`CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAvisoPublico.cs`:

```csharp
namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Resumen de un aviso para el descubrimiento público (listados/búsqueda).</summary>
public sealed record AvisoPublicoResumenDto(
    Guid Id,
    string Titulo,
    decimal Monto,
    string Moneda,
    string NombreCategoria,
    string NombreCiudad,
    string Condicion,
    DateTime FechaCreacion);

/// <summary>Detalle público de un aviso Activo. No expone vendedor ni estado.</summary>
public sealed record AvisoPublicoDto(
    Guid Id,
    string Titulo,
    string Descripcion,
    decimal Monto,
    string Moneda,
    string NombreCategoria,
    string NombreCiudad,
    string Condicion,
    DateTime FechaCreacion);
```

- [ ] **Step 2: Crear el puerto y el filtro**

`CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/IConsultaAvisosPublica.cs`:

```csharp
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Criterios de búsqueda ya normalizados por el handler.</summary>
public sealed record FiltroBusquedaAvisos(
    IReadOnlyList<string> Tokens,
    Guid? CategoriaId,
    Guid? CiudadId,
    decimal? PrecioMin,
    decimal? PrecioMax,
    CondicionArticulo? Condicion);

/// <summary>Puerto de lectura pública de avisos (solo estado Activo).</summary>
public interface IConsultaAvisosPublica
{
    /// <summary>Busca avisos Activos que cumplan el filtro, paginados y ordenados por recientes.</summary>
    public Task<ResultadoPaginado<AvisoPublicoResumenDto>> BuscarAsync(
        FiltroBusquedaAvisos filtro, int pagina, int tamano, CancellationToken ct);

    /// <summary>Obtiene el detalle público de un aviso Activo; null si no existe o no está Activo.</summary>
    public Task<AvisoPublicoDto?> ObtenerPublicoAsync(Guid id, CancellationToken ct);
}
```

- [ ] **Step 3: Verificar que compila**

Run: `dotnet build CaseritoApp/CaseritoApp.sln`
Expected: build correcto, 0 warnings/errors.

- [ ] **Step 4: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAvisoPublico.cs \
        CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/IConsultaAvisosPublica.cs
git commit -m "feat(catalog): contratos de descubrimiento público (DTOs + puerto IConsultaAvisosPublica)"
```

---

### Task 2: Query de búsqueda (BuscarAvisosQuery + handler + validator)

**Files:**
- Create: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/BuscarAvisosQuery.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/BuscarAvisosQueryValidatorTests.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/BuscarAvisosQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IConsultaAvisosPublica`, `FiltroBusquedaAvisos`, `AvisoPublicoResumenDto`, `ResultadoPaginado<T>`, `CondicionArticulo`, `IQuery`/`IQueryHandler` (`CaseritoApp.BuildingBlocks.Application.Messaging`).
- Produces: `BuscarAvisosQuery(string? Texto, Guid? CategoriaId, Guid? CiudadId, decimal? PrecioMin, decimal? PrecioMax, string? Condicion, int Pagina, int Tamano)` → `ResultadoPaginado<AvisoPublicoResumenDto>` (usado por el endpoint de la Task 5).

- [ ] **Step 1: Escribir los tests del validator (fallan)**

`CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/BuscarAvisosQueryValidatorTests.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class BuscarAvisosQueryValidatorTests
{
    private readonly BuscarAvisosQueryValidator _validator = new();

    private static BuscarAvisosQuery Query(
        int pagina = 1, int tamano = 20, decimal? min = null, decimal? max = null, string? condicion = null) =>
        new(null, null, null, min, max, condicion, pagina, tamano);

    [Fact]
    public void Sin_parametros_es_valida()
    {
        var r = _validator.Validate(Query());
        Assert.True(r.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Pagina_menor_a_uno_falla(int pagina)
    {
        var r = _validator.Validate(Query(pagina: pagina));
        Assert.False(r.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Tamano_fuera_de_rango_falla(int tamano)
    {
        var r = _validator.Validate(Query(tamano: tamano));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Tamano_en_el_limite_superior_es_valido()
    {
        var r = _validator.Validate(Query(tamano: 50));
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Precio_min_mayor_que_max_falla()
    {
        var r = _validator.Validate(Query(min: 100m, max: 10m));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Precio_negativo_falla()
    {
        var r = _validator.Validate(Query(min: -1m));
        Assert.False(r.IsValid);
    }

    [Theory]
    [InlineData("Nuevo")]
    [InlineData("usado")]
    [InlineData(null)]
    public void Condicion_valida_o_nula_pasa(string? condicion)
    {
        var r = _validator.Validate(Query(condicion: condicion));
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Condicion_invalida_falla()
    {
        var r = _validator.Validate(Query(condicion: "Reacondicionado"));
        Assert.False(r.IsValid);
    }
}
```

- [ ] **Step 2: Escribir el test del handler (falla)**

`CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/BuscarAvisosQueryHandlerTests.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class BuscarAvisosQueryHandlerTests
{
    private sealed class ConsultaFake : IConsultaAvisosPublica
    {
        public FiltroBusquedaAvisos? UltimoFiltro { get; private set; }
        public int UltimaPagina { get; private set; }
        public int UltimoTamano { get; private set; }

        public Task<ResultadoPaginado<AvisoPublicoResumenDto>> BuscarAsync(
            FiltroBusquedaAvisos filtro, int pagina, int tamano, CancellationToken ct)
        {
            UltimoFiltro = filtro;
            UltimaPagina = pagina;
            UltimoTamano = tamano;
            return Task.FromResult(new ResultadoPaginado<AvisoPublicoResumenDto>([], pagina, tamano, 0));
        }

        public Task<AvisoPublicoDto?> ObtenerPublicoAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<AvisoPublicoDto?>(null);
    }

    [Fact]
    public async Task Tokeniza_el_texto_y_parsea_la_condicion()
    {
        var fake = new ConsultaFake();
        var handler = new BuscarAvisosQueryHandler(fake);

        await handler.Handle(
            new BuscarAvisosQuery("  bici  montaña ", null, null, null, null, "usado", 2, 10),
            CancellationToken.None);

        Assert.NotNull(fake.UltimoFiltro);
        Assert.Equal(["bici", "montaña"], fake.UltimoFiltro!.Tokens);
        Assert.Equal(CondicionArticulo.Usado, fake.UltimoFiltro.Condicion);
        Assert.Equal(2, fake.UltimaPagina);
        Assert.Equal(10, fake.UltimoTamano);
    }

    [Fact]
    public async Task Texto_vacio_produce_cero_tokens()
    {
        var fake = new ConsultaFake();
        var handler = new BuscarAvisosQueryHandler(fake);

        await handler.Handle(
            new BuscarAvisosQuery("   ", null, null, null, null, null, 1, 20),
            CancellationToken.None);

        Assert.Empty(fake.UltimoFiltro!.Tokens);
        Assert.Null(fake.UltimoFiltro.Condicion);
    }
}
```

- [ ] **Step 3: Correr los tests para verificar que fallan**

Run: `dotnet test CaseritoApp/CaseritoApp.sln --filter "FullyQualifiedName~BuscarAvisosQuery"`
Expected: FAIL de compilación (no existe `BuscarAvisosQuery`/`BuscarAvisosQueryValidator`/`BuscarAvisosQueryHandler`).

- [ ] **Step 4: Implementar la query, el handler y el validator**

`CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/BuscarAvisosQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Catalog.Domain.Avisos;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Búsqueda pública de avisos Activos por texto y filtros, paginada.</summary>
public sealed record BuscarAvisosQuery(
    string? Texto,
    Guid? CategoriaId,
    Guid? CiudadId,
    decimal? PrecioMin,
    decimal? PrecioMax,
    string? Condicion,
    int Pagina,
    int Tamano) : IQuery<ResultadoPaginado<AvisoPublicoResumenDto>>;

/// <summary>Handler de <see cref="BuscarAvisosQuery"/>: normaliza el texto/condición y delega en el puerto.</summary>
public sealed class BuscarAvisosQueryHandler(IConsultaAvisosPublica consulta)
    : IQueryHandler<BuscarAvisosQuery, ResultadoPaginado<AvisoPublicoResumenDto>>
{
    public Task<ResultadoPaginado<AvisoPublicoResumenDto>> Handle(
        BuscarAvisosQuery request, CancellationToken cancellationToken)
    {
        var tokens = Tokenizar(request.Texto);
        CondicionArticulo? condicion =
            Enum.TryParse<CondicionArticulo>(request.Condicion, ignoreCase: true, out var c) ? c : null;

        var filtro = new FiltroBusquedaAvisos(
            tokens, request.CategoriaId, request.CiudadId, request.PrecioMin, request.PrecioMax, condicion);

        return consulta.BuscarAsync(filtro, request.Pagina, request.Tamano, cancellationToken);
    }

    private static IReadOnlyList<string> Tokenizar(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>Valida la paginación y los filtros de <see cref="BuscarAvisosQuery"/>.</summary>
public sealed class BuscarAvisosQueryValidator : AbstractValidator<BuscarAvisosQuery>
{
    public BuscarAvisosQueryValidator()
    {
        RuleFor(q => q.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(q => q.Tamano).InclusiveBetween(1, 50).WithMessage("El tamaño debe estar entre 1 y 50.");

        RuleFor(q => q.PrecioMin)
            .GreaterThanOrEqualTo(0).When(q => q.PrecioMin.HasValue)
            .WithMessage("El precio mínimo no puede ser negativo.");
        RuleFor(q => q.PrecioMax)
            .GreaterThanOrEqualTo(0).When(q => q.PrecioMax.HasValue)
            .WithMessage("El precio máximo no puede ser negativo.");
        RuleFor(q => q)
            .Must(q => !(q.PrecioMin.HasValue && q.PrecioMax.HasValue) || q.PrecioMin <= q.PrecioMax)
            .WithMessage("El precio mínimo no puede ser mayor al máximo.");

        RuleFor(q => q.Condicion)
            .Must(v => v is null || Enum.TryParse<CondicionArticulo>(v, ignoreCase: true, out _))
            .WithMessage("La condición no es válida.");
    }
}
```

- [ ] **Step 5: Correr los tests para verificar que pasan**

Run: `dotnet test CaseritoApp/CaseritoApp.sln --filter "FullyQualifiedName~BuscarAvisosQuery"`
Expected: PASS (todos los casos del validator y del handler).

- [ ] **Step 6: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/BuscarAvisosQuery.cs \
        CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/BuscarAvisosQueryValidatorTests.cs \
        CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/BuscarAvisosQueryHandlerTests.cs
git commit -m "feat(catalog): query de búsqueda pública de avisos (tokenización + validación)"
```

---

### Task 3: Query de detalle público (ObtenerAvisoPublicoQuery + handler)

**Files:**
- Create: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ObtenerAvisoPublicoQuery.cs`
- Test: (se cubre el flujo real en integración, Task 6; aquí un unit con fake)

**Interfaces:**
- Consumes: `IConsultaAvisosPublica`, `AvisoPublicoDto`, `IQuery`/`IQueryHandler`.
- Produces: `ObtenerAvisoPublicoQuery(Guid Id)` → `AvisoPublicoDto?` (usado por el endpoint de la Task 5).

- [ ] **Step 1: Escribir el test del handler (falla)**

Añadir a `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/BuscarAvisosQueryHandlerTests.cs` una clase nueva en un archivo aparte `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/ObtenerAvisoPublicoQueryHandlerTests.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

public sealed class ObtenerAvisoPublicoQueryHandlerTests
{
    private sealed class ConsultaFake(AvisoPublicoDto? dto) : IConsultaAvisosPublica
    {
        public Task<ResultadoPaginado<AvisoPublicoResumenDto>> BuscarAsync(
            FiltroBusquedaAvisos filtro, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<AvisoPublicoResumenDto>([], pagina, tamano, 0));

        public Task<AvisoPublicoDto?> ObtenerPublicoAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(dto);
    }

    [Fact]
    public async Task Devuelve_el_dto_cuando_existe()
    {
        var esperado = new AvisoPublicoDto(
            Guid.NewGuid(), "Bici", "desc", 100m, "BOB", "Deportes", "La Paz", "Usado", DateTime.UtcNow);
        var handler = new ObtenerAvisoPublicoQueryHandler(new ConsultaFake(esperado));

        var dto = await handler.Handle(new ObtenerAvisoPublicoQuery(esperado.Id), CancellationToken.None);

        Assert.Equal(esperado, dto);
    }

    [Fact]
    public async Task Devuelve_null_cuando_no_existe()
    {
        var handler = new ObtenerAvisoPublicoQueryHandler(new ConsultaFake(null));

        var dto = await handler.Handle(new ObtenerAvisoPublicoQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(dto);
    }
}
```

- [ ] **Step 2: Correr el test para verificar que falla**

Run: `dotnet test CaseritoApp/CaseritoApp.sln --filter "FullyQualifiedName~ObtenerAvisoPublicoQueryHandler"`
Expected: FAIL de compilación (no existe `ObtenerAvisoPublicoQuery`/`ObtenerAvisoPublicoQueryHandler`).

- [ ] **Step 3: Implementar la query y el handler**

`CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ObtenerAvisoPublicoQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Detalle público de un aviso Activo. Devuelve null si no existe o no está Activo.</summary>
public sealed record ObtenerAvisoPublicoQuery(Guid Id) : IQuery<AvisoPublicoDto?>;

/// <summary>Handler de <see cref="ObtenerAvisoPublicoQuery"/>.</summary>
public sealed class ObtenerAvisoPublicoQueryHandler(IConsultaAvisosPublica consulta)
    : IQueryHandler<ObtenerAvisoPublicoQuery, AvisoPublicoDto?>
{
    public Task<AvisoPublicoDto?> Handle(ObtenerAvisoPublicoQuery request, CancellationToken cancellationToken) =>
        consulta.ObtenerPublicoAsync(request.Id, cancellationToken);
}
```

- [ ] **Step 4: Correr el test para verificar que pasa**

Run: `dotnet test CaseritoApp/CaseritoApp.sln --filter "FullyQualifiedName~ObtenerAvisoPublicoQueryHandler"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ObtenerAvisoPublicoQuery.cs \
        CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/ObtenerAvisoPublicoQueryHandlerTests.cs
git commit -m "feat(catalog): query de detalle público de aviso"
```

---

### Task 4: Adaptador EF Core + registro DI

**Files:**
- Create: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/DependencyInjection.cs:26-28`

**Interfaces:**
- Consumes: `CatalogDbContext`, `IConsultaAvisosPublica`, `FiltroBusquedaAvisos`, `AvisoPublicoResumenDto`, `AvisoPublicoDto`, `EstadoAviso`, `ResultadoPaginado<T>`.
- Produces: `ConsultaAvisosPublicaEfCore` registrado como `IConsultaAvisosPublica`.

- [ ] **Step 1: Implementar el adaptador**

`CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure.Avisos;

/// <summary>
/// Adaptador EF Core de <see cref="IConsultaAvisosPublica"/>. Solo consulta avisos Activos.
/// La búsqueda de texto usa LIKE con colación acento/mayúscula-insensible.
/// </summary>
public sealed class ConsultaAvisosPublicaEfCore(CatalogDbContext db) : IConsultaAvisosPublica
{
    // Insensible a mayúsculas (CI) y acentos (AI), independiente de la colación de la columna.
    private const string Colacion = "Latin1_General_CI_AI";

    public async Task<ResultadoPaginado<AvisoPublicoResumenDto>> BuscarAsync(
        FiltroBusquedaAvisos filtro, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = db.Avisos.Where(a => a.Estado == EstadoAviso.Activo);

        if (filtro.CategoriaId is { } categoria)
        {
            consulta = consulta.Where(a => a.CategoriaId == categoria);
        }

        if (filtro.CiudadId is { } ciudad)
        {
            consulta = consulta.Where(a => a.CiudadId == ciudad);
        }

        if (filtro.PrecioMin is { } min)
        {
            consulta = consulta.Where(a => a.Precio.Monto >= min);
        }

        if (filtro.PrecioMax is { } max)
        {
            consulta = consulta.Where(a => a.Precio.Monto <= max);
        }

        if (filtro.Condicion is { } condicion)
        {
            consulta = consulta.Where(a => a.Condicion == condicion);
        }

        foreach (var token in filtro.Tokens)
        {
            var patron = $"%{EscaparComodines(token)}%";
            consulta = consulta.Where(a =>
                EF.Functions.Like(EF.Functions.Collate(a.Titulo, Colacion), patron) ||
                EF.Functions.Like(EF.Functions.Collate(a.Descripcion, Colacion), patron));
        }

        var total = await consulta.CountAsync(ct);

        var items = await consulta
            .OrderByDescending(a => a.FechaCreacion)
            .ThenBy(a => a.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(a => new AvisoPublicoResumenDto(
                a.Id,
                a.Titulo,
                a.Precio.Monto,
                a.Precio.Moneda.ToString(),
                db.Categorias.Where(c => c.Id == a.CategoriaId).Select(c => c.Nombre).First(),
                db.Ciudades.Where(c => c.Id == a.CiudadId).Select(c => c.Nombre).First(),
                a.Condicion.ToString(),
                a.FechaCreacion))
            .ToListAsync(ct);

        return new ResultadoPaginado<AvisoPublicoResumenDto>(items, pagina, tamano, total);
    }

    public Task<AvisoPublicoDto?> ObtenerPublicoAsync(Guid id, CancellationToken ct) =>
        db.Avisos
            .Where(a => a.Id == id && a.Estado == EstadoAviso.Activo)
            .Select(a => new AvisoPublicoDto(
                a.Id,
                a.Titulo,
                a.Descripcion,
                a.Precio.Monto,
                a.Precio.Moneda.ToString(),
                db.Categorias.Where(c => c.Id == a.CategoriaId).Select(c => c.Nombre).First(),
                db.Ciudades.Where(c => c.Id == a.CiudadId).Select(c => c.Nombre).First(),
                a.Condicion.ToString(),
                a.FechaCreacion))
            .FirstOrDefaultAsync(ct);

    // Neutraliza los comodines de LIKE en el término del usuario usando clases de caracteres.
    private static string EscaparComodines(string token) =>
        token.Replace("[", "[[]", StringComparison.Ordinal)
             .Replace("%", "[%]", StringComparison.Ordinal)
             .Replace("_", "[_]", StringComparison.Ordinal);
}
```

- [ ] **Step 2: Registrar el adaptador en DI**

En `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/DependencyInjection.cs`, tras la línea `servicios.AddScoped<IConsultaCatalogo, ConsultaCatalogoEfCore>();` añadir:

```csharp
        servicios.AddScoped<IConsultaAvisosPublica, ConsultaAvisosPublicaEfCore>();
```

- [ ] **Step 3: Verificar que compila**

Run: `dotnet build CaseritoApp/CaseritoApp.sln`
Expected: build correcto, 0 warnings/errors.

- [ ] **Step 4: Commit**

```bash
git add CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs \
        CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/DependencyInjection.cs
git commit -m "feat(catalog): adaptador EF de búsqueda pública (LIKE colación CI_AI + filtros)"
```

---

### Task 5: Endpoints anónimos + registro en el host

**Files:**
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PublicoEndpoints.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs:88`

**Interfaces:**
- Consumes: `BuscarAvisosQuery`, `ObtenerAvisoPublicoQuery`, `AvisoPublicoResumenDto`, `AvisoPublicoDto`, `ResultadoPaginado<T>`, `ISender`, `ValidationException`.
- Produces: `PublicoEndpoints.MapPublicoEndpoints(this IEndpointRouteBuilder)`; rutas `GET /api/publico/avisos` y `GET /api/publico/avisos/{id:guid}`.

- [ ] **Step 1: Crear el archivo de endpoints**

`CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PublicoEndpoints.cs`:

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Endpoints anónimos de descubrimiento de avisos bajo <c>/api/publico/avisos</c>.</summary>
public static class PublicoEndpoints
{
    /// <summary>Mapea los endpoints públicos de descubrimiento (anónimos, solo avisos Activos).</summary>
    public static IEndpointRouteBuilder MapPublicoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/publico/avisos").AllowAnonymous();

        grupo.MapGet("/", BuscarAsync)
            .Produces<ResultadoPaginado<AvisoPublicoResumenDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .Produces<AvisoPublicoDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> BuscarAsync(
        ISender sender,
        CancellationToken ct,
        string? q = null,
        Guid? categoriaId = null,
        Guid? ciudadId = null,
        decimal? precioMin = null,
        decimal? precioMax = null,
        string? condicion = null,
        int pagina = 1,
        int tamano = 20)
    {
        try
        {
            var resultado = await sender.Send(
                new BuscarAvisosQuery(q, categoriaId, ciudadId, precioMin, precioMax, condicion, pagina, tamano), ct);
            return Results.Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return Results.ValidationProblem(ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }
    }

    private static async Task<IResult> ObtenerAsync(Guid id, ISender sender, CancellationToken ct)
    {
        var dto = await sender.Send(new ObtenerAvisoPublicoQuery(id), ct);
        return dto is not null ? Results.Ok(dto) : Results.NotFound();
    }
}
```

- [ ] **Step 2: Registrar en Program.cs**

En `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`, después de `app.MapCatalogoEndpoints();` (línea 88) añadir:

```csharp
app.MapPublicoEndpoints();
```

- [ ] **Step 3: Verificar que compila**

Run: `dotnet build CaseritoApp/CaseritoApp.sln`
Expected: build correcto, 0 warnings/errors.

- [ ] **Step 4: Commit**

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PublicoEndpoints.cs \
        CaseritoApp/src/Host/CaseritoApp.Host/Program.cs
git commit -m "feat(catalog): endpoints anónimos de descubrimiento (/api/publico/avisos)"
```

---

### Task 6: Tests de integración (Testcontainers)

**Files:**
- Create: `CaseritoApp/tests/CaseritoApp.IntegrationTests/DescubrimientoAvisosTests.cs`

**Interfaces:**
- Consumes: `CaseritoApiFactory`, endpoints de auth/kyc/avisos de 2A (para sembrar avisos vía API), endpoint público de la Task 5.

Notas de patrón (imitar `AvisosFlujoTests.cs`): usar `CaseritoApiFactory` como `IClassFixture`, `UsuarioVerificadoAsync` para obtener un token que pueda crear avisos, y los ids de referencia sembrados `_categoria`/`_ciudad`. Los endpoints públicos se consultan **sin** header de autorización. `_categoria2` es una segunda categoría sembrada; si el seeder solo garantiza una, usar `_categoria` en todos y omitir el test de filtro por categoría cruzada (ver Step 1: incluye un helper que descubre una segunda categoría vía `/api/catalogo/categorias` con token).

- [ ] **Step 1: Escribir los tests de integración**

`CaseritoApp/tests/CaseritoApp.IntegrationTests/DescubrimientoAvisosTests.cs`:

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

/// <summary>Descubrimiento público: solo avisos Activos, filtros, acento-insensibilidad, paginación y detalle 404.</summary>
public sealed class DescubrimientoAvisosTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad = new("22222222-2222-2222-2222-000000000001");
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string Email(string p) => $"{p}-{Guid.NewGuid():N}@caserito.test";

    private static HttpRequestMessage Con(HttpMethod m, string url, string token)
    {
        var s = new HttpRequestMessage(m, url);
        s.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return s;
    }

    private async Task<string> UsuarioVerificadoAsync(HttpClient cliente)
    {
        var email = Email("disc-user");
        var reg = await cliente.PostAsJsonAsync("/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        var adminEmail = Email("disc-admin");
        var regAdmin = await cliente.PostAsJsonAsync("/api/auth/register",
            new RegistroRequest(adminEmail, "Password123!", "Admin", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, regAdmin.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await um.AddToRoleAsync((await um.FindByEmailAsync(adminEmail))!, RolesApp.AdminKyc);
        }

        var tokenAdmin = await LoguearAsync(cliente, adminEmail);
        var token = await LoguearAsync(cliente, email);

        var form = new MultipartFormDataContent();
        var doc = new ByteArrayContent(_png); doc.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(doc, "documento", "ci.png");
        var self = new ByteArrayContent(_png); self.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(self, "selfie", "selfie.png");
        using (var subir = Con(HttpMethod.Post, "/api/kyc/", token))
        {
            subir.Content = form;
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subir)).StatusCode);
        }

        Guid usuarioId;
        using (var scope = factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            usuarioId = (await um.FindByEmailAsync(email))!.Id;
        }

        using (var listar = Con(HttpMethod.Get, "/api/admin/kyc/?estado=Pendiente&tamano=100", tokenAdmin))
        {
            var pagina = await (await cliente.SendAsync(listar)).Content.ReadFromJsonAsync<PaginaKycDisc>();
            var solicitud = pagina!.Items.Single(s => s.UsuarioId == usuarioId);
            using var aprobar = Con(HttpMethod.Post, $"/api/admin/kyc/{solicitud.SolicitudId}/aprobar", tokenAdmin);
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(aprobar)).StatusCode);
        }

        return await LoguearAsync(cliente, email);
    }

    private static async Task<string> LoguearAsync(HttpClient cliente, string email)
    {
        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<TokenAccesoDisc>())!.AccessToken;
    }

    private static async Task<Guid> CrearAvisoAsync(
        HttpClient cliente, string token, string titulo, string descripcion, decimal monto, string condicion)
    {
        using var crear = Con(HttpMethod.Post, "/api/avisos/", token);
        crear.Content = JsonContent.Create(
            new CrearAvisoRequest(titulo, descripcion, monto, condicion, _categoria, _ciudad));
        var resp = await cliente.SendAsync(crear);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<AvisoCreadoResponse>())!.Id;
    }

    [Fact]
    public async Task Solo_lista_avisos_activos()
    {
        using var cliente = factory.CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        var marca = Guid.NewGuid().ToString("N");
        var activo = await CrearAvisoAsync(cliente, token, $"Activo {marca}", "visible", 100m, "Usado");
        var pausado = await CrearAvisoAsync(cliente, token, $"Pausado {marca}", "oculto", 100m, "Usado");
        var eliminado = await CrearAvisoAsync(cliente, token, $"Eliminado {marca}", "oculto", 100m, "Usado");

        using (var p = Con(HttpMethod.Post, $"/api/avisos/{pausado}/pausar", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(p)).StatusCode);
        }
        using (var e = Con(HttpMethod.Delete, $"/api/avisos/{eliminado}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(e)).StatusCode);
        }

        var pagina = await cliente.GetFromJsonAsync<PaginaPublica>($"/api/publico/avisos?q={marca}");
        Assert.Contains(pagina!.Items, a => a.Id == activo);
        Assert.DoesNotContain(pagina.Items, a => a.Id == pausado);
        Assert.DoesNotContain(pagina.Items, a => a.Id == eliminado);
    }

    [Fact]
    public async Task Busqueda_es_insensible_a_acentos_y_mayusculas()
    {
        using var cliente = factory.CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        var marca = Guid.NewGuid().ToString("N");
        var id = await CrearAvisoAsync(cliente, token, $"Cámara réflex {marca}", "poco uso", 500m, "Usado");

        var pagina = await cliente.GetFromJsonAsync<PaginaPublica>($"/api/publico/avisos?q=camara {marca}");
        Assert.Contains(pagina!.Items, a => a.Id == id);
    }

    [Fact]
    public async Task Filtra_por_rango_de_precio()
    {
        using var cliente = factory.CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        var marca = Guid.NewGuid().ToString("N");
        var barato = await CrearAvisoAsync(cliente, token, $"Barato {marca}", "d", 50m, "Usado");
        var caro = await CrearAvisoAsync(cliente, token, $"Caro {marca}", "d", 5000m, "Usado");

        var pagina = await cliente.GetFromJsonAsync<PaginaPublica>(
            $"/api/publico/avisos?q={marca}&precioMin=100&precioMax=1000");
        Assert.DoesNotContain(pagina!.Items, a => a.Id == barato);
        Assert.DoesNotContain(pagina.Items, a => a.Id == caro);
    }

    [Fact]
    public async Task Filtra_por_condicion()
    {
        using var cliente = factory.CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        var marca = Guid.NewGuid().ToString("N");
        var nuevo = await CrearAvisoAsync(cliente, token, $"Nuevo {marca}", "d", 100m, "Nuevo");
        var usado = await CrearAvisoAsync(cliente, token, $"Usado {marca}", "d", 100m, "Usado");

        var pagina = await cliente.GetFromJsonAsync<PaginaPublica>(
            $"/api/publico/avisos?q={marca}&condicion=Nuevo");
        Assert.Contains(pagina!.Items, a => a.Id == nuevo);
        Assert.DoesNotContain(pagina.Items, a => a.Id == usado);
    }

    [Fact]
    public async Task Detalle_publico_solo_para_activos()
    {
        using var cliente = factory.CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        var id = await CrearAvisoAsync(cliente, token, "Detalle", "descripción visible", 100m, "Usado");

        var dto = await cliente.GetFromJsonAsync<AvisoPublicoDetalle>($"/api/publico/avisos/{id}");
        Assert.Equal(id, dto!.Id);
        Assert.Equal("La Paz", dto.NombreCiudad);

        using (var e = Con(HttpMethod.Delete, $"/api/avisos/{id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(e)).StatusCode);
        }

        var resp = await cliente.GetAsync($"/api/publico/avisos/{id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Detalle_de_id_inexistente_da_404()
    {
        using var cliente = factory.CreateClient();
        var resp = await cliente.GetAsync($"/api/publico/avisos/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Validacion_de_paginacion_da_400()
    {
        using var cliente = factory.CreateClient();
        var resp = await cliente.GetAsync("/api/publico/avisos?tamano=500");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}

sealed file record AvisoPublicoResumen(
    Guid Id, string Titulo, decimal Monto, string Moneda,
    string NombreCategoria, string NombreCiudad, string Condicion, DateTime FechaCreacion);
sealed file record PaginaPublica(AvisoPublicoResumen[] Items, int Pagina, int Tamano, int Total);
sealed file record AvisoPublicoDetalle(
    Guid Id, string Titulo, string Descripcion, decimal Monto, string Moneda,
    string NombreCategoria, string NombreCiudad, string Condicion, DateTime FechaCreacion);
sealed file record TokenAccesoDisc(string AccessToken);
sealed file record SolicitudKycDisc(Guid SolicitudId, Guid UsuarioId, string Estado);
sealed file record PaginaKycDisc(SolicitudKycDisc[] Items, int Pagina, int Tamano, int Total);
```

- [ ] **Step 2: Correr los tests de integración**

Run: `dotnet test CaseritoApp/CaseritoApp.sln --filter "FullyQualifiedName~DescubrimientoAvisosTests"`
Expected: PASS (requiere Docker para Testcontainers). Los 7 tests en verde.

Nota de diagnóstico: si el test de acentos falla, verificar que la imagen de SQL Server de Testcontainers acepta `COLLATE Latin1_General_CI_AI` (es una colación estándar disponible en SQL Server 2022; no requiere feature adicional). Si `RegistroRequest`/`LoginRequest`/`CrearAvisoRequest` tuvieran otra firma, alinear con las definiciones reales en `Endpoints/*.cs` (ver `AvisosFlujoTests.cs`).

- [ ] **Step 3: Commit**

```bash
git add CaseritoApp/tests/CaseritoApp.IntegrationTests/DescubrimientoAvisosTests.cs
git commit -m "test(catalog): integración del descubrimiento público (activos/filtros/acentos/paginación/404)"
```

---

### Task 7: Regenerar contrato OpenAPI + cliente TS

**Files:**
- Modify (generados): `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`, `web/src/api/schema.d.ts`

**Interfaces:**
- Consumes: los endpoints anotados de la Task 5.
- Produces: contrato y tipos TS actualizados con `/api/publico/avisos`, `ResultadoPaginadoOfAvisoPublicoResumenDto`, `AvisoPublicoDto`.

- [ ] **Step 1: Regenerar el contrato OpenAPI (opt-in, entorno Testing)**

Run (desde el repo root):
```bash
ASPNETCORE_ENVIRONMENT=Testing dotnet build CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj -p:GenerateOpenApi=true
```
Expected: `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` regenerado; `git diff` muestra las rutas nuevas `/api/publico/avisos` y `/api/publico/avisos/{id}` y los schemas `AvisoPublicoDto` / `ResultadoPaginadoOfAvisoPublicoResumenDto`.

- [ ] **Step 2: Regenerar el cliente TS**

Run (desde `web/`):
```bash
npm run generate:api
```
Expected: `web/src/api/schema.d.ts` actualizado con los nuevos paths y schemas.

- [ ] **Step 3: Verificar typecheck del frontend**

Run (desde `web/`): `npm run typecheck`
Expected: sin errores.

- [ ] **Step 4: Commit**

```bash
git add CaseritoApp/artifacts/openapi/CaseritoApp.Host.json web/src/api/schema.d.ts
git commit -m "chore(catalog): contrato OpenAPI + tipos TS de los endpoints de descubrimiento"
```

---

### Task 8: Barrer follow-ups menores de 2A

Cambios de higiene aprovechando que 2B toca estos archivos. Cada uno es independiente; van juntos en un commit de refactor.

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/CrearAvisoCommand.cs:48`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/EditarAvisoCommand.cs:53`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/ErroresAviso.cs` (añadir `CondicionInvalida`)
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Categoria.cs:9-11`
- Modify: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Ciudad.cs:9-11`

- [ ] **Step 1: Eliminar el helper `UserId()` (Guid.Empty silencioso) en AvisosEndpoints**

En `AvisosEndpoints.cs`, reemplazar los tres mapeos que usan `UserId(u)` por lambdas que parsean el userId y cortan con 401 si falta. Reemplazar el bloque de las líneas 45-63:

```csharp
        grupo.MapPost("/{id:guid}/pausar", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => TransicionAsync(usuario => new PausarAvisoCommand(id, usuario), u, s, ct))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/reactivar", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => TransicionAsync(usuario => new ReactivarAvisoCommand(id, usuario), u, s, ct))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapDelete("/{id:guid}", (Guid id, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => TransicionAsync(usuario => new EliminarAvisoCommand(id, usuario), u, s, ct))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
```

Reemplazar el método `TransicionAsync` (líneas 127-137) para que reciba una fábrica de comando que toma el userId ya parseado:

```csharp
    private static async Task<IResult> TransicionAsync(
        Func<Guid, IRequest<Result>> fabricaComando, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(fabricaComando(userId), ct);
        return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
    }
```

Eliminar el método `UserId` (línea 170):

```csharp
    // (borrar) private static Guid UserId(ClaimsPrincipal usuario) => ...
```

- [ ] **Step 2: `Enum.TryParse` defensivo en los handlers Crear/Editar**

Primero añadir la constante de error. En `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/ErroresAviso.cs`, tras `CiudadInvalida` (línea 19) añadir:

```csharp
    /// <summary>La condición del artículo no es válida (400).</summary>
    public const string CondicionInvalida = "avisos.condicion_invalida";
```

En `CrearAvisoCommand.cs`, reemplazar la línea 48 (`var condicion = Enum.Parse<CondicionArticulo>(request.Condicion);`) por:

```csharp
        if (!Enum.TryParse<CondicionArticulo>(request.Condicion, out var condicion))
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.CondicionInvalida, "La condición no es válida."));
        }
```

En `EditarAvisoCommand.cs`, reemplazar la línea 53 (`var condicion = Enum.Parse<CondicionArticulo>(request.Condicion);`) por:

```csharp
        if (!Enum.TryParse<CondicionArticulo>(request.Condicion, out var condicion))
        {
            return Result.Fallo(new Error(ErroresAviso.CondicionInvalida, "La condición no es válida."));
        }
```

- [ ] **Step 3: Alinear el pragma S1144 en Categoria/Ciudad con Aviso**

`Aviso.cs` tiene el constructor privado de EF **sin** pragma. Alinear `Categoria.cs` y `Ciudad.cs` quitando el `#pragma warning disable/restore S1144`. En ambos archivos dejar el constructor así:

```csharp
    // Constructor para EF Core.
    private Categoria() => Nombre = null!;
```
(y análogamente `private Ciudad() => Nombre = null!;`).

- [ ] **Step 4: Verificar build + toda la suite**

Run: `dotnet build CaseritoApp/CaseritoApp.sln`
Expected: 0 warnings/errors. Si S1144 reaparece como error en Categoria/Ciudad (warnings-as-errors), revertir el Step 3 en el/los archivo(s) afectado(s) y dejar el pragma (el análisis lo requería): en ese caso el "arreglo" es simplemente documentar que el pragma es necesario. Registrar cuál fue el resultado.

Run: `dotnet test CaseritoApp/CaseritoApp.sln`
Expected: toda la suite en verde (152 previos + los nuevos de este bloque).

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs \
        CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/CrearAvisoCommand.cs \
        CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/EditarAvisoCommand.cs \
        CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/ErroresAviso.cs \
        CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Categoria.cs \
        CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Ciudad.cs
git commit -m "refactor(catalog): barre follow-ups de 2A (UserId, Enum.TryParse en handlers, pragma S1144)"
```

---

## Self-Review (cobertura del spec)

- §2 Alcance (solo Activos, difiere fotos/moderación/UI): Tasks 4 (Where Activo) y 6 (test solo-activos). ✅
- §3 Endpoints anónimos (búsqueda + detalle, 404 uniforme): Task 5; verificado en Task 6. ✅
- §4 DTOs públicos con nombres, sin VendedorId/Estado: Task 1. ✅
- §5 LIKE tokenizado + colación CI_AI + filtros + orden recientes + paginación 1–50: Tasks 2 (tokeniza/valida), 4 (LIKE/collate/filtros/orden). ✅
- §6 Capas (puerto, queries, adaptador, DI, sin cambios de dominio, aislamiento): Tasks 1–4; aislamiento por ArchitectureTests (build). ✅
- §7 Contrato OpenAPI + cliente TS: Task 7. ✅
- §8 Tests (unit validators + integración): Tasks 2, 3, 6. ✅
- §9 Follow-ups de 2A: Task 8. ✅
- §10 Decisiones: reflejadas en todo el plan. ✅

Sin placeholders. Firmas consistentes entre tareas (`BuscarAvisosQuery`, `ObtenerAvisoPublicoQuery`, `IConsultaAvisosPublica.BuscarAsync/ObtenerPublicoAsync`, DTOs).
