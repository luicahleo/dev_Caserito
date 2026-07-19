using CaseritoApp.Catalog.Application.Fotos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Detalle de un aviso (vista del due&#xf1;o).</summary>
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

/// <summary>Categor&#xed;a de referencia.</summary>
public sealed record CategoriaDto(Guid Id, string Nombre);

/// <summary>Ciudad de referencia.</summary>
public sealed record CiudadDto(Guid Id, string Nombre);

/// <summary>Proyecciones de dominio a DTO reutilizables por queries.</summary>
public static class MapaAvisos
{
    /// <summary>
    /// Proyecta un <see cref="CaseritoApp.Catalog.Domain.Avisos.Aviso"/> a <see cref="AvisoDto"/>.
    /// Requiere que la colecci&#xf3;n <c>Fotos</c> est&#xe9; cargada.
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
        [.. aviso.Fotos.OrderBy(f => f.Orden).Select(f => new FotoAvisoDto(f.Id, $"/api/fotos/{f.Clave}", f.Orden))]);
}
