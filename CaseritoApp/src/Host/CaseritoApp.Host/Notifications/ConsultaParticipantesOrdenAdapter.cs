using CaseritoApp.Notifications.Application.Notificaciones;
using CaseritoApp.Orders.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Host.Notifications;

public sealed class ConsultaParticipantesOrdenAdapter(OrdersDbContext db)
    : IConsultaParticipantesOrden
{
    public Task<ParticipantesOrdenDto?> ObtenerAsync(Guid ordenId, CancellationToken ct) =>
        db.Orders
            .AsNoTracking()
            .Where(orden => orden.Id == ordenId)
            .Select(orden => new ParticipantesOrdenDto(orden.CompradorId, orden.VendedorId))
            .FirstOrDefaultAsync(ct);
}
