using CaseritoApp.Catalog.Application.Fotos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Resumen de un aviso para el descubrimiento p&#xfa;blico (listados/b&#xfa;squeda).</summary>
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

/// <summary>Detalle público de un aviso Activo. Expone solo el identificador opaco del vendedor.</summary>
public sealed record AvisoPublicoDto(
    Guid Id,
    Guid VendedorId,
    string Titulo,
    string Descripcion,
    decimal Monto,
    string Moneda,
    string NombreCategoria,
    string NombreCiudad,
    string Condicion,
    DateTime FechaCreacion,
    IReadOnlyList<FotoAvisoDto> Fotos);
