using CaseritoApp.Notifications.Domain.Busquedas;

namespace CaseritoApp.Notifications.Application.Busquedas;

public interface IBusquedaGuardadaRepository
{
    public void Agregar(BusquedaGuardada busqueda);

    public Task<BusquedaGuardada?> ObtenerAsync(Guid id, Guid usuarioId, CancellationToken ct);

    public Task<IReadOnlyList<BusquedaGuardada>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct);

    public Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct);

    public void Eliminar(BusquedaGuardada busqueda);

    public Task<IReadOnlyList<BusquedaGuardada>> ListarCoincidenciasAsync(
        string? tituloProducto,
        string? categoriaProducto,
        string? ciudadProducto,
        decimal? precioProducto,
        string? estadoProducto,
        CancellationToken ct);
}
