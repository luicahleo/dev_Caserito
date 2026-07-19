using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Puerto de persistencia de avisos.</summary>
public interface IRepositorioAvisos
{
    /// <summary>Obtiene un aviso por id (incluye eliminados; el handler decide su tratamiento).</summary>
    public Task<Aviso?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>Obtiene un aviso por id incluyendo su colección de fotos cargada.</summary>
    public Task<Aviso?> ObtenerConFotosAsync(Guid id, CancellationToken ct);

    /// <summary>Marca un aviso nuevo para inserción.</summary>
    public void Agregar(Aviso aviso);

    /// <summary>Lista paginada de avisos de un vendedor, excluyendo los eliminados.</summary>
    public Task<ResultadoPaginado<AvisoResumenDto>> ListarPorVendedorAsync(
        Guid vendedorId, int pagina, int tamano, CancellationToken ct);
}
