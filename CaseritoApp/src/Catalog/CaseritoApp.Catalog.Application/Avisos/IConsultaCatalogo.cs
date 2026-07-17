namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Puerto de lectura de catálogos de referencia (categorías y ciudades).</summary>
public interface IConsultaCatalogo
{
    /// <summary>Indica si existe una categoría activa con ese id.</summary>
    public Task<bool> ExisteCategoriaActivaAsync(Guid categoriaId, CancellationToken ct);

    /// <summary>Indica si existe una ciudad activa con ese id.</summary>
    public Task<bool> ExisteCiudadActivaAsync(Guid ciudadId, CancellationToken ct);

    /// <summary>Lista las categorías activas ordenadas.</summary>
    public Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct);

    /// <summary>Lista las ciudades activas ordenadas.</summary>
    public Task<IReadOnlyList<CiudadDto>> ListarCiudadesAsync(CancellationToken ct);
}
