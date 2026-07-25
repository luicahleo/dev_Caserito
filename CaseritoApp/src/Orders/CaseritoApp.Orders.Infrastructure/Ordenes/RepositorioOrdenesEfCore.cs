using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Orders.Infrastructure.Ordenes;

public sealed class RepositorioOrdenesEfCore(OrdersDbContext db) : IRepositorioOrdenes
{
    public Task<bool> ExisteAbiertaAsync(Guid avisoId, Guid compradorId, CancellationToken ct) =>
        db.Orders.AnyAsync(
            orden => orden.AvisoId == avisoId
                && orden.CompradorId == compradorId
                && orden.Estado != EstadoOrden.Cancelled,
            ct);

    public Task<Orden?> ObtenerAsync(Guid ordenId, CancellationToken ct) =>
        db.Orders.FirstOrDefaultAsync(orden => orden.Id == ordenId, ct);

    public async Task<IReadOnlyList<Orden>> ObtenerAbiertasPorAvisoAsync(
        Guid avisoId,
        Guid excluirOrdenId,
        CancellationToken ct) =>
        await db.Orders
            .Where(orden => orden.AvisoId == avisoId
                && orden.Id != excluirOrdenId
                && (orden.Estado == EstadoOrden.Requested
                    || orden.Estado == EstadoOrden.Agreed))
            .ToListAsync(ct);

    public void Agregar(Orden orden) => db.Orders.Add(orden);
}
