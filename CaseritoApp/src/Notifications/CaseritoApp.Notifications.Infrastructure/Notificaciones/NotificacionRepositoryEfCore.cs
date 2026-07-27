using CaseritoApp.Notifications.Application.Notificaciones;
using CaseritoApp.Notifications.Domain.Notificaciones;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure.Notificaciones;

public sealed class NotificacionRepositoryEfCore(NotificationsDbContext db)
    : INotificacionRepository
{
    public void Agregar(Notificacion notificacion) => db.Notifications.Add(notificacion);

    public Task<Notificacion?> ObtenerAsync(
        Guid id,
        Guid destinatarioId,
        CancellationToken ct)
    {
        return db.Notifications
            .FirstOrDefaultAsync(
                n => n.Id == id && n.DestinatarioId == destinatarioId,
                ct);
    }

    public async Task<PaginaNotificacionesDto> ListarAsync(
        Guid destinatarioId,
        bool soloNoLeidas,
        int pagina,
        int tamano,
        CancellationToken ct)
    {
        var query = db.Notifications
            .Where(n => n.DestinatarioId == destinatarioId);

        if (soloNoLeidas)
        {
            query = query.Where(n => !n.Leida);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreadaEn)
            .ThenBy(n => n.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(n => new NotificacionDto(
                n.Id,
                n.Tipo.ToString(),
                n.Titulo,
                n.Mensaje,
                n.EntidadRelacionadaId,
                n.Leida,
                n.CreadaEn))
            .ToListAsync(ct);

        return new PaginaNotificacionesDto(items, pagina, tamano, total);
    }

    public Task<int> ContarNoLeidasAsync(Guid destinatarioId, CancellationToken ct)
    {
        return db.Notifications
            .CountAsync(n => n.DestinatarioId == destinatarioId && !n.Leida, ct);
    }

    public Task<int> MarcarTodasLeidasAsync(Guid destinatarioId, CancellationToken ct)
    {
        return db.Notifications
            .Where(n => n.DestinatarioId == destinatarioId && !n.Leida)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(n => n.Leida, true),
                ct);
    }
}
