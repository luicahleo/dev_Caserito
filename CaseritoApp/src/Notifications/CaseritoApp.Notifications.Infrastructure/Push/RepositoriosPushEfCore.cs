using CaseritoApp.Notifications.Application.Push;
using CaseritoApp.Notifications.Domain.Push;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure.Push;

public sealed class RepositorioSuscripcionesPushEfCore(NotificationsDbContext db)
    : IRepositorioSuscripcionesPush
{
    public Task<SuscripcionPush?> ObtenerPorDispositivoAsync(
        Guid usuarioId, string dispositivoId, CancellationToken ct) =>
        db.SuscripcionesPush.SingleOrDefaultAsync(
            x => x.UsuarioId == usuarioId && x.DispositivoId == dispositivoId, ct);

    public async Task<IReadOnlyList<SuscripcionPush>> ListarActivasAsync(
        Guid usuarioId, CancellationToken ct) =>
        await db.SuscripcionesPush.Where(x => x.UsuarioId == usuarioId && x.Activa).ToListAsync(ct);

    public void Agregar(SuscripcionPush suscripcion) => db.SuscripcionesPush.Add(suscripcion);
}

public sealed class AlmacenIntencionesPushEfCore(NotificationsDbContext db)
    : IAlmacenIntencionesPush
{
    public Task<bool> ExisteEventoAsync(Guid eventoId, CancellationToken ct) =>
        db.IntencionesPush.AnyAsync(x => x.EventoId == eventoId, ct);

    public void Agregar(IntencionPush intencion) => db.IntencionesPush.Add(intencion);

    public async Task<IReadOnlyList<IntencionPush>> ReclamarAsync(
        int maximo, DateTimeOffset ahora, TimeSpan lease, CancellationToken ct)
    {
        var candidatas = await db.IntencionesPush
            .Where(x => x.ProcesadaEn == null && x.DisponibleEn <= ahora
                && (x.LeaseHasta == null || x.LeaseHasta <= ahora))
            .OrderBy(x => x.DisponibleEn)
            .Take(maximo)
            .ToListAsync(ct);
        return candidatas.Where(x => x.Reclamar(ahora, lease)).ToList();
    }
}
