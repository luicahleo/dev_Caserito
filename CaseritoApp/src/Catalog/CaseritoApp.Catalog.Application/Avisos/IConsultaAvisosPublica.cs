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

public sealed record ReferenciaAvisoContactableDto(
    Guid AvisoId,
    Guid VendedorId,
    string Titulo,
    decimal Monto,
    string Moneda);

/// <summary>Puerto de lectura pública de avisos (solo estado Activo).</summary>
public interface IConsultaAvisosPublica
{
    /// <summary>Busca avisos Activos que cumplan el filtro, paginados y ordenados por recientes.</summary>
    public Task<ResultadoPaginado<AvisoPublicoResumenDto>> BuscarAsync(
        FiltroBusquedaAvisos filtro, int pagina, int tamano, CancellationToken ct);

    /// <summary>Obtiene el detalle público de un aviso Activo; null si no existe o no está Activo.</summary>
    public Task<AvisoPublicoDto?> ObtenerPublicoAsync(Guid id, CancellationToken ct);

    public Task<ReferenciaAvisoContactableDto?> ObtenerReferenciaContactableAsync(
        Guid id,
        CancellationToken ct);
}
