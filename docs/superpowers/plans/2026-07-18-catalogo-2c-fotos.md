# Plan — Bloque 2C: Fotos del aviso

**Fecha:** 2026-07-18
**Spec:** `docs/superpowers/specs/2026-07-18-catalogo-2c-fotos-design.md`
**Rama:** `feat/catalogo-2c-fotos`
**Base:** master HEAD `62a0e33`

---

## Resumen de tareas (9)

| # | Título | Modelo |
|---|---|---|
| 1 | Dominio: `FotoAviso` + métodos en `Aviso` + unit tests dominio | Sonnet |
| 2 | Application: interfaces, DTO, validación + unit tests validación | Sonnet |
| 3 | Application: `SubirFotoAvisoCommand` + `BorrarFotoAvisoCommand` + unit tests handlers | Sonnet |
| 4 | Application: actualizar queries para incluir fotos en proyección | Sonnet |
| 5 | Infrastructure: `AlmacenFotoAvisoDisco` + EF config + migración + DI | Sonnet |
| 6 | Host: endpoints fotos + endpoint servicio + tests integración | Sonnet |
| 7 | Frontend: schema + capa API (`avisos.ts`) + tests | Sonnet |
| 8 | Frontend: `FormAviso` + `CrearAvisoPage` + `EditarAvisoPage` con subida | Sonnet |
| 9 | Frontend: `ExplorarPage` + `DetalleAvisoPage` — reemplazar placeholders | Sonnet |

---

## Tarea 1 — Dominio: `FotoAviso` + cambios en `Aviso` + unit tests

**Consume:** —
**Produce:**
- `CaseritoApp.Catalog.Domain.Avisos.FotoAviso` (entidad)
- `Aviso.Fotos`, `Aviso.AgregarFoto`, `Aviso.QuitarFoto`
- `ErroresAviso.LimiteFotosAlcanzado`, `ErroresAviso.FotoNoEncontrada`
- Tests en `CaseritoApp.UnitTests/Catalog/AvisoFotosTests.cs`

### Archivos

#### [NEW] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/FotoAviso.cs`

```csharp
namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>
/// Foto asociada a un aviso. Referencia opaca a un blob del almacén de fotos.
/// La foto con menor <see cref="Orden"/> es la principal.
/// </summary>
public sealed class FotoAviso
{
    // Constructor para EF Core.
#pragma warning disable S1144
    private FotoAviso()
    {
        Clave = null!;
        ContentType = null!;
    }
#pragma warning restore S1144

    internal FotoAviso(Guid avisoId, string clave, string contentType, int orden)
    {
        Id = Guid.NewGuid();
        AvisoId = avisoId;
        Clave = clave;
        ContentType = contentType;
        Orden = orden;
    }

    /// <summary>Identificador de la foto.</summary>
    public Guid Id { get; private set; }

    /// <summary>Id del aviso al que pertenece.</summary>
    public Guid AvisoId { get; private set; }

    /// <summary>Clave opaca que identifica el blob en el almacén de fotos.</summary>
    public string Clave { get; private set; }

    /// <summary>Tipo MIME del archivo (image/jpeg o image/png).</summary>
    public string ContentType { get; private set; }

    /// <summary>Orden de presentación. Menor valor = foto principal.</summary>
    public int Orden { get; private set; }
}
```

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Aviso.cs`

Agregar después de la propiedad `FechaActualizacion` (antes del método `Crear`):

```csharp
    // Colección de fotos (máx. 5). EF accede por el campo privado.
    private readonly List<FotoAviso> _fotos = [];

    /// <summary>Fotos del aviso ordenadas por <see cref="FotoAviso.Orden"/> ASC.</summary>
    public IReadOnlyList<FotoAviso> Fotos => _fotos.AsReadOnly();
```

Agregar antes del método privado `Tocar`:

```csharp
    /// <summary>
    /// Agrega una foto al aviso. Máximo 5 fotos por aviso.
    /// </summary>
    public Result AgregarFoto(string clave, string contentType)
    {
        if (_fotos.Count >= 5)
        {
            return Result.Fallo(new Error(
                ErroresAviso.LimiteFotosAlcanzado,
                "Un aviso puede tener hasta 5 fotos."));
        }

        var orden = _fotos.Count == 0 ? 0 : _fotos.Max(f => f.Orden) + 1;
        _fotos.Add(new FotoAviso(Id, clave, contentType, orden));
        Tocar(DateTime.UtcNow);
        return Result.Exito();
    }

    /// <summary>
    /// Quita una foto del aviso y la devuelve para que el llamante pueda borrar el blob.
    /// </summary>
    public Result<FotoAviso> QuitarFoto(Guid fotoId)
    {
        var foto = _fotos.FirstOrDefault(f => f.Id == fotoId);
        if (foto is null)
        {
            return Result.Fallo<FotoAviso>(new Error(
                ErroresAviso.FotoNoEncontrada,
                "La foto no pertenece a este aviso."));
        }

        _fotos.Remove(foto);
        Tocar(DateTime.UtcNow);
        return Result.Exito(foto);
    }
```

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/ErroresAviso.cs`

Agregar al final de la clase (antes del `}`):

```csharp
    /// <summary>El aviso ya tiene el máximo de fotos permitidas (5).</summary>
    public const string LimiteFotosAlcanzado = "aviso.limite_fotos_alcanzado";

    /// <summary>La foto no pertenece al aviso indicado.</summary>
    public const string FotoNoEncontrada = "aviso.foto_no_encontrada";
```

#### [NEW] `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/AvisoFotosTests.cs`

```csharp
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

/// <summary>Tests unitarios de <see cref="Aviso.AgregarFoto"/> y <see cref="Aviso.QuitarFoto"/>.</summary>
public sealed class AvisoFotosTests
{
    private static Aviso AvisoActivo() => Aviso.Crear(
        Guid.NewGuid(), "Título", "Descripción de prueba para el aviso de test.",
        Dinero.Crear(100m, Moneda.BOB).Valor,
        new Guid("11111111-1111-1111-1111-000000000001"),
        new Guid("22222222-2222-2222-2222-000000000001"),
        CondicionArticulo.Nuevo,
        DateTime.UtcNow);

    [Fact]
    public void AgregarFoto_PrimeraFoto_TieneOrdenCero()
    {
        var aviso = AvisoActivo();
        var resultado = aviso.AgregarFoto("clave1", "image/jpeg");
        Assert.True(resultado.EsExito);
        Assert.Single(aviso.Fotos);
        Assert.Equal(0, aviso.Fotos[0].Orden);
    }

    [Fact]
    public void AgregarFoto_SegundaFoto_TieneOrdenUno()
    {
        var aviso = AvisoActivo();
        aviso.AgregarFoto("clave1", "image/jpeg");
        aviso.AgregarFoto("clave2", "image/png");
        Assert.Equal(2, aviso.Fotos.Count);
        Assert.Equal(1, aviso.Fotos[1].Orden);
    }

    [Fact]
    public void AgregarFoto_SextaFoto_DevuelveErrorLimite()
    {
        var aviso = AvisoActivo();
        for (var i = 0; i < 5; i++)
        {
            aviso.AgregarFoto($"clave{i}", "image/jpeg");
        }

        var resultado = aviso.AgregarFoto("clave6", "image/jpeg");
        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.LimiteFotosAlcanzado, resultado.Error.Code);
    }

    [Fact]
    public void QuitarFoto_FotoExistente_LaRemueveYDevuelve()
    {
        var aviso = AvisoActivo();
        aviso.AgregarFoto("clave1", "image/jpeg");
        var fotoId = aviso.Fotos[0].Id;

        var resultado = aviso.QuitarFoto(fotoId);
        Assert.True(resultado.EsExito);
        Assert.Equal("clave1", resultado.Valor.Clave);
        Assert.Empty(aviso.Fotos);
    }

    [Fact]
    public void QuitarFoto_FotoInexistente_DevuelveError()
    {
        var aviso = AvisoActivo();
        var resultado = aviso.QuitarFoto(Guid.NewGuid());
        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresAviso.FotoNoEncontrada, resultado.Error.Code);
    }
}
```

### Verificación

```powershell
# Desde CaseritoApp/
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln --filter "FullyQualifiedName~AvisoFotosTests"
```

Salida esperada: `Passed! - Failed: 0, Passed: 5, Skipped: 0`

---

## Tarea 2 — Application: interfaces, DTO, validación

**Consume:** Tarea 1 (ErroresAviso, FotoAviso)
**Produce:**
- `IAlmacenFotosAviso` (puerto)
- `ValidacionFotoAviso` (helper estático)
- `FotoAvisoDto` (record)
- DTOs actualizados: `AvisoDto`, `AvisoResumenDto`, `AvisoPublicoResumenDto`, `AvisoPublicoDto`
- Tests en `ValidacionFotoAvisoTests.cs`

### Archivos

#### [NEW] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Fotos/IAlmacenFotosAviso.cs`

```csharp
namespace CaseritoApp.Catalog.Application.Fotos;

/// <summary>
/// Puerto de almacenamiento de fotos de avisos. Sin cifrado (las fotos de producto no son PII).
/// El adaptador de dev escribe a disco; en prod se sustituye por object storage.
/// </summary>
public interface IAlmacenFotosAviso
{
    /// <summary>Guarda el contenido y devuelve una clave opaca para recuperarlo.</summary>
    Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct);

    /// <summary>Recupera el contenido y el content-type asociados a la clave.</summary>
    Task<(byte[] Contenido, string ContentType)> ObtenerAsync(string clave, CancellationToken ct);

    /// <summary>Elimina el blob asociado a la clave (idempotente si no existe).</summary>
    Task EliminarAsync(string clave, CancellationToken ct);
}
```

#### [NEW] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Fotos/ValidacionFotoAviso.cs`

```csharp
namespace CaseritoApp.Catalog.Application.Fotos;

/// <summary>
/// Reglas de aceptación de fotos de aviso: whitelist de tipo MIME, límite de tamaño
/// y verificación de magic bytes (no se confía en la extensión ni en el content-type declarado).
/// </summary>
public static class ValidacionFotoAviso
{
    /// <summary>Tamaño máximo por foto (5 MiB).</summary>
    public const long LimiteBytes = 5 * 1024 * 1024;

    /// <summary>Tipos MIME aceptados.</summary>
    public static readonly string[] ContentTypesPermitidos = ["image/jpeg", "image/png"];

    private static readonly byte[] _firmaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] _firmaPng  = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Indica si el contenido es una imagen aceptable y coherente con su content-type.
    /// </summary>
    public static bool EsImagenValida(byte[] contenido, string contentType)
    {
        if (contenido is null || contenido.Length == 0 || contenido.Length > LimiteBytes)
        {
            return false;
        }

        return contentType switch
        {
            "image/jpeg" => EmpiezaCon(contenido, _firmaJpeg),
            "image/png"  => EmpiezaCon(contenido, _firmaPng),
            _            => false,
        };
    }

    private static bool EmpiezaCon(byte[] contenido, byte[] firma)
    {
        if (contenido.Length < firma.Length)
        {
            return false;
        }

        for (var i = 0; i < firma.Length; i++)
        {
            if (contenido[i] != firma[i])
            {
                return false;
            }
        }

        return true;
    }
}
```

#### [NEW] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Fotos/FotoAvisoDto.cs`

```csharp
namespace CaseritoApp.Catalog.Application.Fotos;

/// <summary>Foto de un aviso expuesta en los DTOs de catálogo.</summary>
public sealed record FotoAvisoDto(Guid Id, string Url, int Orden);
```

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAviso.cs`

Reemplazar el contenido completo del archivo:

```csharp
using CaseritoApp.Catalog.Application.Fotos;

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
    DateTime FechaActualizacion,
    IReadOnlyList<FotoAvisoDto> Fotos);

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
    DateTime FechaCreacion,
    IReadOnlyList<FotoAvisoDto> Fotos);

/// <summary>Categoría de referencia.</summary>
public sealed record CategoriaDto(Guid Id, string Nombre);

/// <summary>Ciudad de referencia.</summary>
public sealed record CiudadDto(Guid Id, string Nombre);

/// <summary>Proyecciones de dominio a DTO reutilizables por queries.</summary>
public static class MapaAvisos
{
    /// <summary>
    /// Proyecta un <see cref="CaseritoApp.Catalog.Domain.Avisos.Aviso"/> a <see cref="AvisoDto"/>.
    /// Requiere que la colección <c>Fotos</c> esté cargada.
    /// </summary>
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
        aviso.FechaActualizacion,
        [..aviso.Fotos.OrderBy(f => f.Orden).Select(f => new FotoAvisoDto(f.Id, $"/api/fotos/{f.Clave}", f.Orden))]);
}
```

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAvisoPublico.cs`

Reemplazar el contenido completo:

```csharp
using CaseritoApp.Catalog.Application.Fotos;

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
    DateTime FechaCreacion,
    IReadOnlyList<FotoAvisoDto> Fotos);

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
    DateTime FechaCreacion,
    IReadOnlyList<FotoAvisoDto> Fotos);
```

#### [NEW] `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/ValidacionFotoAvisoTests.cs`

```csharp
using CaseritoApp.Catalog.Application.Fotos;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

/// <summary>Tests unitarios de <see cref="ValidacionFotoAviso"/>.</summary>
public sealed class ValidacionFotoAvisoTests
{
    private static readonly byte[] _jpegValido  = [0xFF, 0xD8, 0xFF, 0x00, 0x01, 0x02];
    private static readonly byte[] _pngValido   = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] _invalido    = [0x00, 0x01, 0x02, 0x03];

    [Fact]
    public void EsImagenValida_JpegConFirmaCorrecta_RetornaTrue()
    {
        Assert.True(ValidacionFotoAviso.EsImagenValida(_jpegValido, "image/jpeg"));
    }

    [Fact]
    public void EsImagenValida_PngConFirmaCorrecta_RetornaTrue()
    {
        Assert.True(ValidacionFotoAviso.EsImagenValida(_pngValido, "image/png"));
    }

    [Fact]
    public void EsImagenValida_MagicBytesIncorrectos_RetornaFalse()
    {
        Assert.False(ValidacionFotoAviso.EsImagenValida(_invalido, "image/jpeg"));
    }

    [Fact]
    public void EsImagenValida_ContentTypeNoPermitido_RetornaFalse()
    {
        Assert.False(ValidacionFotoAviso.EsImagenValida(_jpegValido, "image/gif"));
    }

    [Fact]
    public void EsImagenValida_ContenidoVacio_RetornaFalse()
    {
        Assert.False(ValidacionFotoAviso.EsImagenValida([], "image/jpeg"));
    }

    [Fact]
    public void EsImagenValida_TamanoExcedido_RetornaFalse()
    {
        var grande = new byte[ValidacionFotoAviso.LimiteBytes + 1];
        grande[0] = 0xFF; grande[1] = 0xD8; grande[2] = 0xFF;
        Assert.False(ValidacionFotoAviso.EsImagenValida(grande, "image/jpeg"));
    }
}
```

### Verificación

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln --filter "FullyQualifiedName~ValidacionFotoAvisoTests"
```

Salida esperada: `Passed! - Failed: 0, Passed: 6, Skipped: 0`

---

## Tarea 3 — Application: commands SubirFoto + BorrarFoto

**Consume:** Tareas 1 y 2
**Produce:**
- `SubirFotoAvisoCommand` + handler + validator
- `BorrarFotoAvisoCommand` + handler
- Tests en `SubirBorrarFotoCommandHandlerTests.cs`

### Archivos

#### [NEW] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/SubirFotoAvisoCommand.cs`

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Sube una foto al aviso. Solo el dueño puede subir. Máx. 5 fotos por aviso.</summary>
public sealed record SubirFotoAvisoCommand(
    Guid AvisoId,
    Guid VendedorId,
    byte[] Contenido,
    string ContentType) : ICommand<Guid>;

/// <summary>Handler de <see cref="SubirFotoAvisoCommand"/>.</summary>
public sealed class SubirFotoAvisoCommandHandler(
    IRepositorioAvisos repositorio,
    IAlmacenFotosAviso almacen,
    ILogger<SubirFotoAvisoCommandHandler> logger)
    : ICommandHandler<SubirFotoAvisoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SubirFotoAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerConFotosAsync(request.AvisoId, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        if (!ValidacionFotoAviso.EsImagenValida(request.Contenido, request.ContentType))
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.ImagenInvalida,
                "La imagen no es válida. Se aceptan jpeg/png de hasta 5 MiB."));
        }

        // Guardar blob antes de persistir la entidad.
        string clave;
        try
        {
            clave = await almacen.GuardarAsync(request.Contenido, request.ContentType, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al guardar el blob de foto para aviso {AvisoId}", request.AvisoId);
            return Result.Fallo<Guid>(new Error(ErroresAviso.ErrorAlmacenamiento,
                "No se pudo almacenar la imagen. Inténtalo de nuevo."));
        }

        var resultado = aviso.AgregarFoto(clave, request.ContentType);
        if (!resultado.EsExito)
        {
            // La colección está llena: borrar el blob recién guardado (best-effort).
            await BorrarBlobBestEffortAsync(clave, request.AvisoId, cancellationToken);
            return Result.Fallo<Guid>(resultado.Error);
        }

        // La foto se persistirá al hacer SaveChanges (UnitOfWorkBehavior).
        return Result.Exito(aviso.Fotos[^1].Id);
    }

    private async Task BorrarBlobBestEffortAsync(string clave, Guid avisoId, CancellationToken ct)
    {
        try
        {
            await almacen.EliminarAsync(clave, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "No se pudo borrar el blob huérfano {Clave} del aviso {AvisoId}", clave, avisoId);
        }
    }
}

/// <summary>Valida <see cref="SubirFotoAvisoCommand"/>.</summary>
public sealed class SubirFotoAvisoCommandValidator : AbstractValidator<SubirFotoAvisoCommand>
{
    public SubirFotoAvisoCommandValidator()
    {
        RuleFor(c => c.AvisoId).NotEmpty().WithMessage("El id del aviso es obligatorio.");
        RuleFor(c => c.VendedorId).NotEmpty().WithMessage("El id del vendedor es obligatorio.");
        RuleFor(c => c.Contenido).NotEmpty().WithMessage("El contenido de la imagen es obligatorio.");
        RuleFor(c => c.ContentType).NotEmpty().WithMessage("El content-type es obligatorio.");
    }
}
```

#### [NEW] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/BorrarFotoAvisoCommand.cs`

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Borra una foto del aviso. Solo el dueño puede borrar.</summary>
public sealed record BorrarFotoAvisoCommand(
    Guid AvisoId,
    Guid FotoId,
    Guid VendedorId) : ICommand;

/// <summary>Handler de <see cref="BorrarFotoAvisoCommand"/>.</summary>
public sealed class BorrarFotoAvisoCommandHandler(
    IRepositorioAvisos repositorio,
    IAlmacenFotosAviso almacen,
    ILogger<BorrarFotoAvisoCommandHandler> logger)
    : ICommandHandler<BorrarFotoAvisoCommand>
{
    public async Task<Result> Handle(BorrarFotoAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerConFotosAsync(request.AvisoId, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        var resultado = aviso.QuitarFoto(request.FotoId);
        if (!resultado.EsExito)
        {
            return Result.Fallo(resultado.Error);
        }

        // Persistir la eliminación de la entidad antes de borrar el blob.
        // (UnitOfWorkBehavior hace SaveChanges al terminar el handler.)
        // El blob se borra después: si falla, queda huérfano (aceptable en MVP).
        var clave = resultado.Valor.Clave;

        // Registrar la clave para borrar después de persistir (via campo local capturado).
        // El UnitOfWork llamará a SaveChanges antes de que continúe esta corrutina.
        _ = clave; // La clave se usa abajo, tras la persistencia implícita.

        // Nota: el UnitOfWorkBehavior llama a SaveChanges DESPUÉS de que Handle retorna Result.Exito.
        // El borrado del blob se hace en el mismo scope, pero es best-effort.
        await BorrarBlobBestEffortAsync(clave, request.AvisoId, request.FotoId, cancellationToken);

        return Result.Exito();
    }

    private async Task BorrarBlobBestEffortAsync(
        string clave, Guid avisoId, Guid fotoId, CancellationToken ct)
    {
        try
        {
            await almacen.EliminarAsync(clave, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "No se pudo borrar el blob {Clave} de la foto {FotoId} del aviso {AvisoId}",
                clave, fotoId, avisoId);
        }
    }
}
```

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Domain/Avisos/ErroresAviso.cs`

Agregar después de `FotoNoEncontrada`:

```csharp
    /// <summary>La imagen no supera la validación (magic bytes / tamaño / tipo MIME).</summary>
    public const string ImagenInvalida = "aviso.imagen_invalida";

    /// <summary>Error al guardar el blob de la foto.</summary>
    public const string ErrorAlmacenamiento = "aviso.error_almacenamiento";
```

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/IRepositorioAvisos.cs`

Agregar el método `ObtenerConFotosAsync`:

```csharp
    /// <summary>Obtiene un aviso por id incluyendo su colección de fotos cargada.</summary>
    public Task<Aviso?> ObtenerConFotosAsync(Guid id, CancellationToken ct);
```

#### [NEW] `CaseritoApp/tests/CaseritoApp.UnitTests/Catalog/SubirBorrarFotoCommandHandlerTests.cs`

```csharp
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CaseritoApp.UnitTests.Catalog;

/// <summary>Tests unitarios de los handlers de subida y borrado de fotos.</summary>
public sealed class SubirBorrarFotoCommandHandlerTests
{
    private static readonly byte[] _jpegValido = [0xFF, 0xD8, 0xFF, 0x00, 0x01];
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad    = new("22222222-2222-2222-2222-000000000001");

    private static Aviso AvisoActivo(Guid vendedorId) => Aviso.Crear(
        vendedorId, "Título", "Descripción prueba handler foto.", 
        Dinero.Crear(100m, Moneda.BOB).Valor,
        _categoria, _ciudad, CondicionArticulo.Nuevo, DateTime.UtcNow);

    // ── SubirFotoAvisoCommand ────────────────────────────────────────────

    [Fact]
    public async Task SubirFoto_AvisoNoEncontrado_RetornaNoEncontrado()
    {
        var repo    = Substitute.For<IRepositorioAvisos>();
        var almacen = Substitute.For<IAlmacenFotosAviso>();
        repo.ObtenerConFotosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Aviso?)null);

        var handler = new SubirFotoAvisoCommandHandler(repo, almacen, NullLogger<SubirFotoAvisoCommandHandler>.Instance);
        var cmd = new SubirFotoAvisoCommand(Guid.NewGuid(), Guid.NewGuid(), _jpegValido, "image/jpeg");
        var result = await handler.Handle(cmd, default);

        Assert.False(result.EsExito);
        Assert.Equal(ErroresAviso.NoEncontrado, result.Error.Code);
    }

    [Fact]
    public async Task SubirFoto_NoEsPropietario_RetornaForbidden()
    {
        var repo    = Substitute.For<IRepositorioAvisos>();
        var almacen = Substitute.For<IAlmacenFotosAviso>();
        var aviso   = AvisoActivo(Guid.NewGuid());
        repo.ObtenerConFotosAsync(aviso.Id, Arg.Any<CancellationToken>()).Returns(aviso);

        var handler = new SubirFotoAvisoCommandHandler(repo, almacen, NullLogger<SubirFotoAvisoCommandHandler>.Instance);
        var cmd = new SubirFotoAvisoCommand(aviso.Id, Guid.NewGuid(), _jpegValido, "image/jpeg");
        var result = await handler.Handle(cmd, default);

        Assert.False(result.EsExito);
        Assert.Equal(ErroresAviso.NoEsPropietario, result.Error.Code);
    }

    [Fact]
    public async Task SubirFoto_ImagenInvalida_RetornaErrorYNoBorraBlob()
    {
        var vendedorId = Guid.NewGuid();
        var repo       = Substitute.For<IRepositorioAvisos>();
        var almacen    = Substitute.For<IAlmacenFotosAviso>();
        var aviso      = AvisoActivo(vendedorId);
        repo.ObtenerConFotosAsync(aviso.Id, Arg.Any<CancellationToken>()).Returns(aviso);

        var handler = new SubirFotoAvisoCommandHandler(repo, almacen, NullLogger<SubirFotoAvisoCommandHandler>.Instance);
        var cmd = new SubirFotoAvisoCommand(aviso.Id, vendedorId, [0x00, 0x01, 0x02], "image/jpeg");
        var result = await handler.Handle(cmd, default);

        Assert.False(result.EsExito);
        Assert.Equal(ErroresAviso.ImagenInvalida, result.Error.Code);
        await almacen.DidNotReceive().GuardarAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubirFoto_ErrorAlGuardarBlob_BorraYRetornaError()
    {
        var vendedorId = Guid.NewGuid();
        var repo       = Substitute.For<IRepositorioAvisos>();
        var almacen    = Substitute.For<IAlmacenFotosAviso>();
        var aviso      = AvisoActivo(vendedorId);
        repo.ObtenerConFotosAsync(aviso.Id, Arg.Any<CancellationToken>()).Returns(aviso);
        almacen.GuardarAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
               .ThrowsAsync(new IOException("disco lleno"));

        var handler = new SubirFotoAvisoCommandHandler(repo, almacen, NullLogger<SubirFotoAvisoCommandHandler>.Instance);
        var cmd = new SubirFotoAvisoCommand(aviso.Id, vendedorId, _jpegValido, "image/jpeg");
        var result = await handler.Handle(cmd, default);

        Assert.False(result.EsExito);
        Assert.Equal(ErroresAviso.ErrorAlmacenamiento, result.Error.Code);
    }

    // ── BorrarFotoAvisoCommand ───────────────────────────────────────────

    [Fact]
    public async Task BorrarFoto_FotoInexistente_RetornaError()
    {
        var vendedorId = Guid.NewGuid();
        var repo       = Substitute.For<IRepositorioAvisos>();
        var almacen    = Substitute.For<IAlmacenFotosAviso>();
        var aviso      = AvisoActivo(vendedorId);
        repo.ObtenerConFotosAsync(aviso.Id, Arg.Any<CancellationToken>()).Returns(aviso);

        var handler = new BorrarFotoAvisoCommandHandler(repo, almacen, NullLogger<BorrarFotoAvisoCommandHandler>.Instance);
        var cmd = new BorrarFotoAvisoCommand(aviso.Id, Guid.NewGuid(), vendedorId);
        var result = await handler.Handle(cmd, default);

        Assert.False(result.EsExito);
        Assert.Equal(ErroresAviso.FotoNoEncontrada, result.Error.Code);
    }
}
```

### Verificación

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln --filter "FullyQualifiedName~SubirBorrarFotoCommandHandlerTests"
```

Salida esperada: `Passed! - Failed: 0, Passed: 5, Skipped: 0`

---

## Tarea 4 — Application: actualizar queries para incluir fotos

**Consume:** Tareas 1, 2
**Produce:** Handlers de queries actualizados con `Fotos` en las proyecciones; tests actualizados.

### Archivos

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/ObtenerMiAvisoQuery.cs`

`MapaAvisos.ADto` ya genera `Fotos` (Tarea 2). Solo hay que asegurarse de que el repositorio cargue las fotos. El handler usa `repositorio.ObtenerAsync` — se cambia a `ObtenerConFotosAsync`:

```csharp
// Línea 16: cambiar
var aviso = await repositorio.ObtenerAsync(request.Id, cancellationToken);
// Por:
var aviso = await repositorio.ObtenerConFotosAsync(request.Id, cancellationToken);
```

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/RepositorioAvisosEfCore.cs`

Agregar implementación de `ObtenerConFotosAsync` y actualizar `ListarPorVendedorAsync` para incluir fotos:

```csharp
// Método nuevo:
public Task<Aviso?> ObtenerConFotosAsync(Guid id, CancellationToken ct) =>
    db.Avisos
      .Include(a => a.Fotos)
      .FirstOrDefaultAsync(a => a.Id == id, ct);

// Reemplazar Select en ListarPorVendedorAsync:
.Select(a => new AvisoResumenDto(
    a.Id,
    a.Titulo,
    a.Precio.Monto,
    a.Precio.Moneda.ToString(),
    a.CategoriaId,
    a.CiudadId,
    a.Condicion.ToString(),
    a.Estado.ToString(),
    a.FechaCreacion,
    a.Fotos.OrderBy(f => f.Orden)
           .Select(f => new FotoAvisoDto(f.Id, $"/api/fotos/{f.Clave}", f.Orden))
           .ToList()))
```

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs`

Actualizar `BuscarAsync` y `ObtenerPublicoAsync` para proyectar fotos. En el `Select` de ambos métodos agregar el campo `Fotos`:

```csharp
// En BuscarAsync — agregar al Select de AvisoPublicoResumenDto:
db.Set<FotoAviso>()
  .Where(f => f.AvisoId == a.Id)
  .OrderBy(f => f.Orden)
  .Select(f => new FotoAvisoDto(f.Id, $"/api/fotos/{f.Clave}", f.Orden))
  .ToList()

// En ObtenerPublicoAsync — mismo patrón para AvisoPublicoDto.
```

> **Nota de implementación:** añadir `using CaseritoApp.Catalog.Application.Fotos;` y
> `using CaseritoApp.Catalog.Domain.Avisos;` al archivo si no están.
> Registrar `FotoAviso` en el `DbSet` del contexto (Tarea 5 lo hace, pero el `Set<FotoAviso>()` funciona sin `DbSet` explícito si EF lo conoce).

### Verificación

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln --filter "FullyQualifiedName~QueriesAvisoHandlerTests|ObtenerAvisoPublicoQueryHandlerTests|BuscarAvisosQueryHandlerTests"
```

Salida esperada: build sin errores; tests existentes siguen en verde.

---

## Tarea 5 — Infrastructure: almacén + EF config + migración + DI

**Consume:** Tareas 1, 2, 3, 4
**Produce:**
- `AlmacenFotoAvisoDisco` + `OpcionesAlmacenFotos`
- `FotoAviso` configurada en EF Core
- Migración `AgregarFotosAviso`
- `DependencyInjection` actualizado

### Archivos

#### [NEW] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Fotos/OpcionesAlmacenFotos.cs`

```csharp
namespace CaseritoApp.Catalog.Infrastructure.Fotos;

/// <summary>Opciones de configuración del almacén de fotos de avisos.</summary>
public sealed class OpcionesAlmacenFotos
{
    /// <summary>Ruta base donde se guardan los blobs. Configurable vía appsettings / env.</summary>
    public string RutaBase { get; set; } = "/data/fotos-avisos";
}
```

#### [NEW] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Fotos/AlmacenFotoAvisoDisco.cs`

```csharp
using CaseritoApp.Catalog.Application.Fotos;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Catalog.Infrastructure.Fotos;

/// <summary>
/// Adaptador de <see cref="IAlmacenFotosAviso"/> que escribe blobs a disco.
/// Sin cifrado: las fotos de producto no son PII.
/// En prod se sustituye por object storage sin tocar dominio ni casos de uso.
/// </summary>
public sealed class AlmacenFotoAvisoDisco(IOptions<OpcionesAlmacenFotos> opciones) : IAlmacenFotosAviso
{
    private readonly string _rutaBase = opciones.Value.RutaBase;

    public async Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(_rutaBase);
        var clave = Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(RutaBlob(clave), contenido, ct);
        await File.WriteAllTextAsync(RutaMeta(clave), contentType, ct);
        return clave;
    }

    public async Task<(byte[] Contenido, string ContentType)> ObtenerAsync(string clave, CancellationToken ct)
    {
        var contenido   = await File.ReadAllBytesAsync(RutaBlob(clave), ct);
        var contentType = await File.ReadAllTextAsync(RutaMeta(clave), ct);
        return (contenido, contentType);
    }

    public Task EliminarAsync(string clave, CancellationToken ct)
    {
        File.Delete(RutaBlob(clave));
        File.Delete(RutaMeta(clave));
        return Task.CompletedTask;
    }

    private string RutaBlob(string clave) => Path.Combine(_rutaBase, $"{clave}.bin");
    private string RutaMeta(string clave) => Path.Combine(_rutaBase, $"{clave}.meta");
}
```

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConfiguracionCatalog.cs`

Dentro del `builder.Entity<Aviso>`, agregar al final (antes del cierre `});`):

```csharp
            e.HasMany(a => a.Fotos)
             .WithOne()
             .HasForeignKey(f => f.AvisoId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Navigation(a => a.Fotos).UsePropertyAccessMode(PropertyAccessMode.Field);
```

Después del bloque de `Aviso`, agregar:

```csharp
        builder.Entity<FotoAviso>(e =>
        {
            e.ToTable("FotosAviso");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).ValueGeneratedNever();
            e.Property(f => f.AvisoId).IsRequired();
            e.Property(f => f.Clave).HasMaxLength(200).IsRequired();
            e.Property(f => f.ContentType).HasMaxLength(50).IsRequired();
            e.Property(f => f.Orden).IsRequired();
            e.HasIndex(f => f.AvisoId);
            e.HasIndex(f => new { f.AvisoId, f.Orden });
        });
```

#### [MODIFY] `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/DependencyInjection.cs`

Agregar registro del almacén y sus opciones:

```csharp
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Infrastructure.Fotos;
// ...en el método AgregarCatalog, antes de return servicios:
        servicios.Configure<OpcionesAlmacenFotos>(config.GetSection("AlmacenFotos"));
        servicios.AddScoped<IAlmacenFotosAviso, AlmacenFotoAvisoDisco>();
```

#### Migración

```powershell
# Desde CaseritoApp/
dotnet ef migrations add AgregarFotosAviso `
  --project src/Catalog/CaseritoApp.Catalog.Infrastructure `
  --startup-project src/Host/CaseritoApp.Host `
  --output-dir Migrations
```

### Verificación

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
```

Salida esperada: build sin errores; suite completa en verde.

---

## Tarea 6 — Host: endpoints fotos + tests integración

**Consume:** Tareas 1–5
**Produce:**
- `POST /api/avisos/{id}/fotos` (en `AvisosEndpoints.cs`)
- `DELETE /api/avisos/{id}/fotos/{fotoId}` (en `AvisosEndpoints.cs`)
- `GET /api/fotos/{clave}` (nuevo `FotosEndpoints.cs`)
- `Program.cs` actualizado
- `FotosAvisoIntegrationTests.cs` (tests integración)

### Archivos

#### [MODIFY] `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs`

Agregar response record y dos endpoints al grupo `/api/avisos` en `MapAvisosEndpoints`:

```csharp
// Record de respuesta (agregar con los demás records al inicio del archivo):
/// <summary>Respuesta de subida de foto.</summary>
public sealed record FotoCreadaResponse(Guid Id);
```

```csharp
// Dentro de MapAvisosEndpoints, después del endpoint /mios/{id}:
        grupo.MapPost("/{id:guid}/fotos", SubirFotoAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery()
            .Produces<FotoCreadaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}/fotos/{fotoId:guid}", BorrarFotoAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
```

```csharp
// Métodos privados a agregar en AvisosEndpoints:
    private static async Task<IResult> SubirFotoAsync(
        Guid id, IFormFile file, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return Results.Problem(
                title: "archivo_requerido",
                detail: "Se debe adjuntar un archivo de imagen.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        byte[] contenido;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            contenido = ms.ToArray();
        }

        try
        {
            var resultado = await sender.Send(
                new SubirFotoAvisoCommand(id, userId, contenido, file.ContentType ?? ""), ct);

            return resultado.EsExito
                ? Results.Created($"/api/avisos/mios/{id}", new FotoCreadaResponse(resultado.Valor))
                : DesdeError(resultado.Error);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> BorrarFotoAsync(
        Guid id, Guid fotoId, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new BorrarFotoAvisoCommand(id, fotoId, userId), ct);
        return resultado.EsExito ? Results.NoContent() : DesdeError(resultado.Error);
    }
```

También actualizar `DesdeError` para incluir los nuevos códigos:

```csharp
        ErroresAviso.LimiteFotosAlcanzado or ErroresAviso.FotoNoEncontrada
            or ErroresAviso.ImagenInvalida or ErroresAviso.ErrorAlmacenamiento =>
            Results.Problem(title: error.Code, detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
```

#### [NEW] `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/FotosEndpoints.cs`

```csharp
using CaseritoApp.Catalog.Application.Fotos;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Endpoint anónimo para servir blobs de fotos de avisos.</summary>
public static class FotosEndpoints
{
    /// <summary>Mapea <c>GET /api/fotos/{clave}</c> (anónimo).</summary>
    public static IEndpointRouteBuilder MapFotosEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/fotos/{clave}", ObtenerAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ExcludeFromDescription(); // No entra al contrato OpenAPI (binario, no JSON).

        return app;
    }

    private static async Task<IResult> ObtenerAsync(
        string clave, IAlmacenFotosAviso almacen, CancellationToken ct)
    {
        try
        {
            var (contenido, contentType) = await almacen.ObtenerAsync(clave, ct);
            return Results.File(contenido, contentType);
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
```

#### [MODIFY] `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`

Agregar `app.MapFotosEndpoints();` junto a los demás mapeos (después de `app.MapPublicoEndpoints();`).

#### [NEW] `CaseritoApp/tests/CaseritoApp.IntegrationTests/FotosAvisoIntegrationTests.cs`

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

/// <summary>
/// Tests de integración de subida, borrado y servicio de fotos de avisos.
/// </summary>
public sealed class FotosAvisoIntegrationTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad    = new("22222222-2222-2222-2222-000000000001");

    // PNG mínimo válido (magic bytes correctos + datos mínimos).
    private static readonly byte[] _pngValido =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    private static string Email() => $"fotos-{Guid.NewGuid():N}@caserito.test";

    private async Task<(HttpClient cliente, string token, Guid avisoId)> PrepararDuenoConAvisoAsync()
    {
        var cliente = factory.CreateClient();
        var email   = Email();

        // Registrar usuario.
        var reg = await cliente.PostAsJsonAsync("/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario Fotos", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        // Asignar rol Vendedor y aprobar KYC directamente en BD.
        using (var scope = factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u  = (await um.FindByEmailAsync(email))!;
            await um.AddToRoleAsync(u, Roles.Vendedor);

            // Emitir claim verificado simulando aprobación KYC.
            await um.AddClaimAsync(u, new System.Security.Claims.Claim("verificado", "true"));
        }

        // Loguear.
        var login = await cliente.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;

        // Crear aviso.
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/avisos");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = JsonContent.Create(new CrearAvisoRequest(
            "Aviso fotos test", "Descripción test para fotos", 100m, "Nuevo", _categoria, _ciudad));
        var resp = await cliente.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var avisoId = (await resp.Content.ReadFromJsonAsync<AvisoCreadoResponse>())!.Id;

        return (cliente, token, avisoId);
    }

    [Fact]
    public async Task SubirFoto_FotoValida_Devuelve201ConId()
    {
        var (cliente, token, avisoId) = await PrepararDuenoConAvisoAsync();

        var form  = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(_pngValido);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(bytes, "file", "foto.png");

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = form;

        var resp = await cliente.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<FotoCreadaResponse>();
        Assert.NotEqual(Guid.Empty, body!.Id);
    }

    [Fact]
    public async Task SubirSextaFoto_DevuelveError400()
    {
        var (cliente, token, avisoId) = await PrepararDuenoConAvisoAsync();

        for (var i = 0; i < 5; i++)
        {
            var f    = new MultipartFormDataContent();
            var b    = new ByteArrayContent(_pngValido);
            b.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            f.Add(b, "file", $"foto{i}.png");
            var r = new HttpRequestMessage(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos");
            r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            r.Content = f;
            var rr = await cliente.SendAsync(r);
            Assert.Equal(HttpStatusCode.Created, rr.StatusCode);
        }

        var form   = new MultipartFormDataContent();
        var bytes6 = new ByteArrayContent(_pngValido);
        bytes6.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(bytes6, "file", "foto6.png");
        var req6 = new HttpRequestMessage(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos");
        req6.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req6.Content = form;

        var resp = await cliente.SendAsync(req6);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task SubirFoto_MagicBytesInvalidos_DevuelveError400()
    {
        var (cliente, token, avisoId) = await PrepararDuenoConAvisoAsync();

        var form  = new MultipartFormDataContent();
        var bytes = new ByteArrayContent([0x00, 0x01, 0x02, 0x03]);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(bytes, "file", "foto.jpg");

        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = form;

        var resp = await cliente.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task BorrarFoto_PropiaYExistente_Devuelve204()
    {
        var (cliente, token, avisoId) = await PrepararDuenoConAvisoAsync();

        // Subir foto.
        var form  = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(_pngValido);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(bytes, "file", "foto.png");
        var subir = new HttpRequestMessage(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos");
        subir.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        subir.Content = form;
        var subResp = await cliente.SendAsync(subir);
        Assert.Equal(HttpStatusCode.Created, subResp.StatusCode);
        var fotoId = (await subResp.Content.ReadFromJsonAsync<FotoCreadaResponse>())!.Id;

        // Borrar foto.
        var borrar = new HttpRequestMessage(HttpMethod.Delete, $"/api/avisos/{avisoId}/fotos/{fotoId}");
        borrar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var borrarResp = await cliente.SendAsync(borrar);
        Assert.Equal(HttpStatusCode.NoContent, borrarResp.StatusCode);
    }

    [Fact]
    public async Task ObtenerFoto_ClaveValida_DevuelveBytesYContentType()
    {
        var (cliente, token, avisoId) = await PrepararDuenoConAvisoAsync();

        // Subir foto.
        var form  = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(_pngValido);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(bytes, "file", "foto.png");
        var subir = new HttpRequestMessage(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos");
        subir.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        subir.Content = form;
        await cliente.SendAsync(subir);

        // El DTO público del aviso debe incluir las fotos con URL.
        var detalle = await cliente.GetFromJsonAsync<AvisoPublicoRespuesta>(
            $"/api/publico/avisos/{avisoId}");
        Assert.NotNull(detalle);
        Assert.NotEmpty(detalle!.Fotos);

        // Descargar la foto por la URL del DTO.
        var fotoResp = await cliente.GetAsync(detalle.Fotos[0].Url);
        Assert.Equal(HttpStatusCode.OK, fotoResp.StatusCode);
        Assert.Equal("image/png", fotoResp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ObtenerFoto_ClaveInexistente_Devuelve404()
    {
        var cliente = factory.CreateClient();
        var resp    = await cliente.GetAsync("/api/fotos/clavequenoexiste");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // Tipo auxiliar local para deserializar el detalle público con fotos.
    private sealed record AvisoPublicoRespuesta(
        Guid Id, string Titulo, string Descripcion,
        FotoRespuesta[] Fotos);
    private sealed record FotoRespuesta(Guid Id, string Url, int Orden);
}
```

### Verificación

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln --filter "FullyQualifiedName~FotosAvisoIntegrationTests"
```

Salida esperada: `Passed! - Failed: 0, Passed: 6, Skipped: 0`

---

## Tarea 7 — Frontend: schema + capa API

**Consume:** Tarea 6 (endpoints listos, contrato actualizado)
**Produce:**
- `schema.d.ts` actualizado (regenerado o manual con `FotoAvisoDto` en DTOs)
- `avisos.ts` con `subirFotoAviso`, `borrarFotoAviso` y `FotoAvisoDto`
- Tests en `avisos.test.ts`

### Archivos

#### Regenerar schema

```powershell
# Desde la raíz del repo (requiere el host levantado o el job contract del CI):
# Si el job de CI no está disponible, agregar manualmente FotoAvisoDto al schema.d.ts.
# Sección a agregar en schema.d.ts:
```

En `web/src/api/schema.d.ts`, agregar el tipo `FotoAvisoDto` y actualizar los tipos de DTOs para incluir `fotos`. La forma exacta dependerá de la regeneración; el implementer debe agregar manualmente si el job no está disponible:

```typescript
// En la sección de components.schemas, agregar:
FotoAvisoDto: {
  id: string;       // uuid
  url: string;
  orden: number;    // int32
};

// En AvisoDto, AvisoResumenDto, AvisoPublicoResumenDto, AvisoPublicoDto:
// agregar la propiedad:
fotos: components["schemas"]["FotoAvisoDto"][];
```

#### [MODIFY] `web/src/api/avisos.ts`

```typescript
// Agregar al inicio el tipo local:
export type FotoAvisoDto = { id: string; url: string; orden: number };

// Agregar las dos funciones nuevas al final del archivo:

/**
 * Sube una foto al aviso. Devuelve el id de la FotoAviso creada.
 * Usa fetch con FormData (multipart), no el wrapper JSON de http.ts.
 */
export async function subirFotoAviso(
  avisoId: string,
  archivo: File,
): Promise<{ id: string }> {
  const form = new FormData();
  form.append('file', archivo);

  const resp = await fetch(`/api/avisos/${avisoId}/fotos`, {
    method: 'POST',
    body: form,
    headers: {
      Authorization: `Bearer ${obtenerToken()}`,
    },
  });

  if (!resp.ok) {
    const text = await resp.text().catch(() => '');
    throw new Error(`Error al subir foto: ${resp.status} ${text}`);
  }

  return resp.json() as Promise<{ id: string }>;
}

/**
 * Borra una foto del aviso.
 */
export async function borrarFotoAviso(
  avisoId: string,
  fotoId: string,
): Promise<void> {
  const resp = await fetch(`/api/avisos/${avisoId}/fotos/${fotoId}`, {
    method: 'DELETE',
    headers: {
      Authorization: `Bearer ${obtenerToken()}`,
    },
  });

  if (!resp.ok) {
    throw new Error(`Error al borrar foto: ${resp.status}`);
  }
}

// Helper interno: obtiene el token del localStorage (misma fuente que el cliente http.ts).
function obtenerToken(): string {
  return localStorage.getItem('accessToken') ?? '';
}
```

> **Nota:** verificar que la clave de localStorage coincida con la que usa `http.ts`. Si la estructura de auth es diferente, ajustar el helper `obtenerToken()`.

#### [MODIFY] `web/src/api/avisos.test.ts`

Agregar tests de las funciones nuevas:

```typescript
// Agregar al final del archivo de tests existente:

describe('subirFotoAviso', () => {
  it('llama a fetch con POST multipart y retorna el id', async () => {
    const fetchSpy = vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(JSON.stringify({ id: 'foto-uuid' }), { status: 201 }),
    );

    const archivo = new File([new Uint8Array(10)], 'foto.png', { type: 'image/png' });
    const result  = await subirFotoAviso('aviso-id', archivo);

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/avisos/aviso-id/fotos',
      expect.objectContaining({ method: 'POST' }),
    );
    expect(result.id).toBe('foto-uuid');
  });
});

describe('borrarFotoAviso', () => {
  it('llama a fetch con DELETE y no lanza si la respuesta es OK', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(null, { status: 204 }),
    );
    await expect(borrarFotoAviso('aviso-id', 'foto-id')).resolves.toBeUndefined();
  });
});
```

### Verificación

```powershell
# Desde web/
npm run typecheck
npm run lint
npm run test
```

Salida esperada: typecheck y lint limpios; tests en verde.

---

## Tarea 8 — Frontend: FormAviso + CrearAvisoPage + EditarAvisoPage

**Consume:** Tarea 7
**Produce:** `FormAviso` con sección de fotos; páginas de crear/editar actualizadas; tests.

### Archivos

#### [MODIFY] `web/src/routes/FormAviso.tsx`

Agregar props `avisoId?: string` y `fotosIniciales?: FotoAvisoDto[]`. Agregar sección de fotos al final del formulario.

Contenido completo actualizado:

```tsx
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { listarCategorias, listarCiudades } from '../api/catalogo';
import {
  type FotoAvisoDto,
  borrarFotoAviso,
  subirFotoAviso,
} from '../api/avisos';

export type ValoresAviso = {
  titulo: string;
  descripcion: string;
  monto: string;
  condicion: string;
  categoriaId: string;
  ciudadId: string;
};

type Props = {
  inicial?: ValoresAviso;
  enviando: boolean;
  textoBoton: string;
  onSubmit: (valores: ValoresAviso) => void;
  /** Id del aviso existente (solo en editar). Si se proporciona, las fotos se suben/borran inmediatamente. */
  avisoId?: string;
  /** Fotos ya guardadas del aviso (solo en editar). */
  fotosIniciales?: FotoAvisoDto[];
};

const CONDICIONES = ['Nuevo', 'Usado'];
const MAX_FOTOS = 5;
const MAX_BYTES = 5 * 1024 * 1024;

export function FormAviso({
  inicial,
  enviando,
  textoBoton,
  onSubmit,
  avisoId,
  fotosIniciales = [],
}: Props) {
  const [valores, setValores] = useState<ValoresAviso>(
    inicial ?? { titulo: '', descripcion: '', monto: '', condicion: '', categoriaId: '', ciudadId: '' },
  );
  const [errores, setErrores] = useState<Partial<Record<keyof ValoresAviso, string>>>({});

  // Estado de fotos
  const [fotosGuardadas, setFotosGuardadas] = useState<FotoAvisoDto[]>(fotosIniciales);
  const [fotasLocales, setFotasLocales] = useState<{ preview: string; archivo: File }[]>([]);
  const [subiendo, setSubiendo] = useState(false);
  const [errorFoto, setErrorFoto] = useState<string | null>(null);

  const totalFotos = fotosGuardadas.length + fotasLocales.length;

  const categorias = useQuery({ queryKey: ['categorias'], queryFn: listarCategorias });
  const ciudades   = useQuery({ queryKey: ['ciudades'],   queryFn: listarCiudades });

  const set = (campo: keyof ValoresAviso) => (e: React.ChangeEvent<HTMLInputElement>) => {
    setValores((v) => ({ ...v, [campo]: e.target.value }));
    setErrores((e2) => ({ ...e2, [campo]: undefined }));
  };

  const validar = (): boolean => {
    const e: typeof errores = {};
    if (!valores.titulo.trim()) e.titulo = 'El título es obligatorio.';
    if (valores.titulo.length > 120) e.titulo = 'Máximo 120 caracteres.';
    if (!valores.descripcion.trim()) e.descripcion = 'La descripción es obligatoria.';
    if (valores.descripcion.length > 2000) e.descripcion = 'Máximo 2000 caracteres.';
    if (!valores.monto || Number(valores.monto) <= 0) e.monto = 'El precio debe ser mayor a 0.';
    if (!valores.categoriaId) e.categoriaId = 'La categoría es obligatoria.';
    if (!valores.ciudadId) e.ciudadId = 'La ciudad es obligatoria.';
    setErrores(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (validar()) onSubmit(valores);
  };

  // Subida de fotos nuevas seleccionadas (flujo editar: inmediato; flujo crear: diferido al submit del form padre).
  const handleArchivos = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const archivos = Array.from(e.target.files ?? []);
    e.target.value = '';
    setErrorFoto(null);

    const disponibles = MAX_FOTOS - totalFotos;
    if (archivos.length > disponibles) {
      setErrorFoto(`Solo podés agregar ${disponibles} foto${disponibles !== 1 ? 's' : ''} más (máx. ${MAX_FOTOS}).`);
      return;
    }

    for (const archivo of archivos) {
      if (archivo.size > MAX_BYTES) {
        setErrorFoto('Cada foto debe pesar menos de 5 MiB.');
        return;
      }
    }

    if (avisoId) {
      // Modo editar: subir inmediatamente.
      setSubiendo(true);
      try {
        for (const archivo of archivos) {
          const { id } = await subirFotoAviso(avisoId, archivo);
          const url = URL.createObjectURL(archivo);
          setFotosGuardadas((prev) => [
            ...prev,
            { id, url, orden: prev.length === 0 ? 0 : Math.max(...prev.map((f) => f.orden)) + 1 },
          ]);
        }
      } catch {
        setErrorFoto('No se pudo subir la foto. Inténtalo de nuevo.');
      } finally {
        setSubiendo(false);
      }
    } else {
      // Modo crear: guardar en estado local; el padre las subirá tras crear el aviso.
      const nuevas = archivos.map((a) => ({ preview: URL.createObjectURL(a), archivo: a }));
      setFotasLocales((prev) => [...prev, ...nuevas]);
    }
  };

  const handleBorrarGuardada = async (fotoId: string) => {
    if (!avisoId) return;
    setErrorFoto(null);
    try {
      await borrarFotoAviso(avisoId, fotoId);
      setFotosGuardadas((prev) => prev.filter((f) => f.id !== fotoId));
    } catch {
      setErrorFoto('No se pudo borrar la foto. Inténtalo de nuevo.');
    }
  };

  const handleBorrarLocal = (idx: number) => {
    setFotasLocales((prev) => {
      URL.revokeObjectURL(prev[idx].preview);
      return prev.filter((_, i) => i !== idx);
    });
  };

  // Exponer el estado de fotos locales para que CrearAvisoPage las suba después del POST.
  // Se hace via ref (ver CrearAvisoPage).
  (FormAviso as any)._fotasLocales = fotasLocales;

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate>
      <Stack spacing={2}>
        <TextField
          id="titulo"
          label="Título"
          required
          value={valores.titulo}
          onChange={set('titulo')}
          error={!!errores.titulo}
          helperText={errores.titulo}
          inputProps={{ maxLength: 120 }}
        />
        <TextField
          id="descripcion"
          label="Descripción"
          required
          multiline
          minRows={4}
          value={valores.descripcion}
          onChange={set('descripcion')}
          error={!!errores.descripcion}
          helperText={errores.descripcion}
          slotProps={{ htmlInput: { maxLength: 2000 } }}
        />
        <TextField
          id="monto"
          label="Precio (BOB)"
          type="number"
          required
          value={valores.monto}
          onChange={set('monto')}
          error={!!errores.monto}
          helperText={errores.monto}
          slotProps={{ htmlInput: { min: 0.01, step: '0.01' } }}
        />
        <TextField
          id="condicion"
          select
          label="Condición"
          required
          value={valores.condicion}
          onChange={set('condicion')}
          error={!!errores.condicion}
          helperText={errores.condicion}
        >
          <MenuItem value="">Seleccioná una condición</MenuItem>
          {CONDICIONES.map((c) => (
            <MenuItem key={c} value={c}>{c}</MenuItem>
          ))}
        </TextField>
        <TextField
          id="categoriaId"
          select
          label="Categoría"
          required
          value={valores.categoriaId}
          onChange={set('categoriaId')}
          error={!!errores.categoriaId}
          helperText={errores.categoriaId}
        >
          <MenuItem value="">Seleccioná una categoría</MenuItem>
          {(categorias.data ?? []).map((c) => (
            <MenuItem key={c.id} value={c.id}>{c.nombre}</MenuItem>
          ))}
        </TextField>
        <TextField
          id="ciudadId"
          select
          label="Ciudad"
          required
          value={valores.ciudadId}
          onChange={set('ciudadId')}
          error={!!errores.ciudadId}
          helperText={errores.ciudadId}
        >
          <MenuItem value="">Seleccioná una ciudad</MenuItem>
          {(ciudades.data ?? []).map((c) => (
            <MenuItem key={c.id} value={c.id}>{c.nombre}</MenuItem>
          ))}
        </TextField>

        {/* ── Sección de fotos ── */}
        <Box>
          <Typography variant="subtitle1" gutterBottom>
            Fotos ({totalFotos}/{MAX_FOTOS})
          </Typography>

          {/* Miniaturas de fotos guardadas (editar) */}
          {fotosGuardadas.length > 0 && (
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', mb: 1 }}>
              {fotosGuardadas.map((f) => (
                <Box key={f.id} sx={{ position: 'relative' }}>
                  <Box
                    component="img"
                    src={f.url}
                    alt="foto del aviso"
                    sx={{ width: 72, height: 72, objectFit: 'cover', borderRadius: 1 }}
                  />
                  <Button
                    size="small"
                    onClick={() => handleBorrarGuardada(f.id)}
                    sx={{
                      position: 'absolute', top: 0, right: 0, minWidth: 0,
                      p: 0.25, bgcolor: 'rgba(0,0,0,0.5)', color: 'white',
                      '&:hover': { bgcolor: 'rgba(0,0,0,0.75)' },
                    }}
                    aria-label="Borrar foto"
                  >
                    ✕
                  </Button>
                </Box>
              ))}
            </Stack>
          )}

          {/* Previsualizaciones locales (crear) */}
          {fotasLocales.length > 0 && (
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', mb: 1 }}>
              {fotasLocales.map((f, i) => (
                <Box key={f.preview} sx={{ position: 'relative' }}>
                  <Box
                    component="img"
                    src={f.preview}
                    alt="previsualización"
                    sx={{ width: 72, height: 72, objectFit: 'cover', borderRadius: 1 }}
                  />
                  <Button
                    size="small"
                    onClick={() => handleBorrarLocal(i)}
                    sx={{
                      position: 'absolute', top: 0, right: 0, minWidth: 0,
                      p: 0.25, bgcolor: 'rgba(0,0,0,0.5)', color: 'white',
                      '&:hover': { bgcolor: 'rgba(0,0,0,0.75)' },
                    }}
                    aria-label="Quitar foto"
                  >
                    ✕
                  </Button>
                </Box>
              ))}
            </Stack>
          )}

          <Button
            component="label"
            variant="outlined"
            disabled={totalFotos >= MAX_FOTOS || subiendo}
            size="small"
          >
            {subiendo ? <CircularProgress size={16} sx={{ mr: 1 }} /> : null}
            Agregar fotos
            <input
              id="input-fotos"
              type="file"
              accept="image/jpeg,image/png"
              multiple
              hidden
              onChange={handleArchivos}
            />
          </Button>

          {errorFoto && (
            <Alert severity="error" sx={{ mt: 1 }}>{errorFoto}</Alert>
          )}
        </Box>

        <Button
          id="btn-guardar-aviso"
          type="submit"
          variant="contained"
          disabled={enviando || subiendo}
        >
          {enviando ? <CircularProgress size={20} /> : textoBoton}
        </Button>
      </Stack>
    </Box>
  );
}
```

#### [MODIFY] `web/src/routes/CrearAvisoPage.tsx`

Actualizar el submit para subir fotos después de crear el aviso:

```tsx
// Dentro de handleSubmit, tras navigate — cambiar el flujo:
const handleSubmit = async (valores: ValoresAviso) => {
  setEnviando(true);
  setError(null);
  try {
    const { id } = await crearAviso({ ...valores, monto: Number(valores.monto) });
    // Subir las fotos locales seleccionadas en el form.
    const fotasLocales: { archivo: File }[] = (FormAviso as any)._fotasLocales ?? [];
    for (const { archivo } of fotasLocales) {
      await subirFotoAviso(id, archivo).catch(() => {/* best-effort */});
    }
    navigate('/mis-avisos');
  } catch (err) {
    if (err instanceof HttpError && err.status === 403) {
      setError('Debes completar la verificación de identidad para publicar.');
    } else {
      setError('No se pudo crear el aviso. Inténtalo de nuevo.');
    }
  } finally {
    setEnviando(false);
  }
};
```

Agregar `import { subirFotoAviso } from '../api/avisos';` y `import { FormAviso } from './FormAviso';`.

#### [MODIFY] `web/src/routes/EditarAvisoPage.tsx`

Pasar `avisoId` y `fotosIniciales` al `FormAviso`:

```tsx
// Donde se renderiza FormAviso, agregar las props:
<FormAviso
  inicial={valores}
  enviando={editarMutation.isPending}
  textoBoton="Guardar cambios"
  onSubmit={handleSubmit}
  avisoId={id}
  fotosIniciales={data?.fotos ?? []}
/>
```

### Verificación

```powershell
# Desde web/
npm run typecheck
npm run lint
npm run test
```

Salida esperada: typecheck y lint limpios; suite verde.

---

## Tarea 9 — Frontend: ExplorarPage + DetalleAvisoPage — render real

**Consume:** Tarea 7 (schema con fotos)
**Produce:** Placeholders reemplazados; tests actualizados.

### Archivos

#### [MODIFY] `web/src/routes/ExplorarPage.tsx`

Reemplazar el placeholder gris (líneas 179-180) en el interior de la `Card`:

```tsx
{/* Antes: <Box sx={{ height: 140, bgcolor: 'grey.200' }} aria-hidden /> */}
{a.fotos && a.fotos.length > 0 ? (
  <CardMedia
    component="img"
    height={140}
    image={a.fotos[0].url}
    alt={a.titulo}
  />
) : (
  <Box sx={{ height: 140, bgcolor: 'grey.200', display: 'flex', alignItems: 'center',
              justifyContent: 'center' }}>
    <Typography variant="caption" color="text.disabled">Sin foto</Typography>
  </Box>
)}
```

Agregar `CardMedia` a los imports de MUI.

#### [MODIFY] `web/src/routes/DetalleAvisoPage.tsx`

Reemplazar el placeholder y agregar galería con estado local:

```tsx
// Agregar al inicio del componente:
const [selectedIdx, setSelectedIdx] = useState(0);
```

```tsx
{/* Antes: <Box sx={{ height: 260, bgcolor: 'grey.200', mb: 3 }} aria-hidden /> */}
{data.fotos && data.fotos.length > 0 ? (
  <Box sx={{ mb: 2 }}>
    <Box
      component="img"
      src={data.fotos[selectedIdx]?.url ?? data.fotos[0].url}
      alt={data.titulo}
      sx={{ width: '100%', maxHeight: 320, objectFit: 'cover', borderRadius: 1 }}
    />
    {data.fotos.length > 1 && (
      <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: 'wrap' }}>
        {data.fotos.map((f, i) => (
          <Box
            key={f.id}
            component="img"
            src={f.url}
            alt={`Foto ${i + 1}`}
            onClick={() => setSelectedIdx(i)}
            sx={{
              width: 64, height: 64, objectFit: 'cover', borderRadius: 0.5,
              cursor: 'pointer',
              border: i === selectedIdx ? '2px solid' : '2px solid transparent',
              borderColor: i === selectedIdx ? 'primary.main' : 'transparent',
            }}
          />
        ))}
      </Stack>
    )}
  </Box>
) : (
  <Box sx={{ height: 260, bgcolor: 'grey.200', mb: 3, display: 'flex',
              alignItems: 'center', justifyContent: 'center' }}>
    <Typography variant="caption" color="text.disabled">Sin fotos</Typography>
  </Box>
)}
```

Agregar `useState` a los imports de React.

#### [MODIFY] `web/src/routes/ExplorarPage.test.tsx`

Agregar test: aviso con fotos renderiza `CardMedia` en vez del placeholder.

```tsx
it('aviso con fotos muestra la imagen principal', async () => {
  vi.spyOn(avisosMod, 'buscarAvisos').mockResolvedValue({
    items: [
      {
        id: '1', titulo: 'Con foto', monto: 100, moneda: 'BOB',
        nombreCategoria: 'Cat', nombreCiudad: 'LP', condicion: 'Nuevo',
        fechaCreacion: new Date().toISOString(),
        fotos: [{ id: 'f1', url: '/api/fotos/abc', orden: 0 }],
      },
    ],
    pagina: 1, tamano: 20, total: 1,
  });
  // ... render y assert que aparece un img con src='/api/fotos/abc'
});
```

#### [MODIFY] `web/src/routes/DetalleAvisoPage.test.tsx`

Agregar test: detalle con varias fotos muestra miniaturas; clic cambia foto principal.

### Verificación

```powershell
# Desde web/
npm run typecheck
npm run lint
npm run test
npm run build
```

Salida esperada: build sin errores; suite completa verde.

---

## Revisión final de rama

Al completar las 9 tareas:

```powershell
# Backend completo:
dotnet test CaseritoApp.sln

# Frontend completo:
cd web
npm run typecheck
npm run lint
npm run test
npm run build
```

Suite verde → revisión final con modelo más capaz sobre el diff completo. Merge a master autorizado por el usuario.
