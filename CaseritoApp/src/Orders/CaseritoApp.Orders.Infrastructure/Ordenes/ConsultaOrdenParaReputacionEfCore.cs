using CaseritoApp.Orders.Application.Ordenes;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Orders.Infrastructure.Ordenes;

public sealed class ConsultaOrdenParaReputacionEfCore(OrdersDbContext db)
    : IConsultaOrdenParaReputacion
{
    public Task<ReferenciaOrdenParaReputacion?> ObtenerAsync(
        Guid orderId,
        Guid actorId,
        CancellationToken ct) =>
        db.Orders
            .AsNoTracking()
            .Where(orden => orden.Id == orderId
                && (orden.CompradorId == actorId || orden.VendedorId == actorId))
            .Select(orden => new ReferenciaOrdenParaReputacion(
                orden.Id,
                orden.CompradorId,
                orden.VendedorId,
                orden.Estado.ToString()))
            .FirstOrDefaultAsync(ct);
}
