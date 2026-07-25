using CaseritoApp.Orders.Domain.Ordenes;

namespace CaseritoApp.Orders.Application.Ordenes;

public interface IRepositorioOrdenes
{
    public Task<bool> ExisteAbiertaAsync(Guid avisoId, Guid compradorId, CancellationToken ct);

    public Task<Orden?> ObtenerAsync(Guid ordenId, CancellationToken ct);

    public Task<IReadOnlyList<Orden>> ObtenerAbiertasPorAvisoAsync(
        Guid avisoId,
        Guid excluirOrdenId,
        CancellationToken ct);

    public void Agregar(Orden orden);
}
