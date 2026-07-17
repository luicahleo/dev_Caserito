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
