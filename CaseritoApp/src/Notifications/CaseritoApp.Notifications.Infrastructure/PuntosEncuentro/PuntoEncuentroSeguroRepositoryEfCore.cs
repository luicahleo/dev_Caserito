using CaseritoApp.Notifications.Application.PuntosEncuentro;
using CaseritoApp.Notifications.Domain.PuntosEncuentro;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure.PuntosEncuentro;

public sealed class PuntoEncuentroSeguroRepositoryEfCore(NotificationsDbContext db)
    : IPuntoEncuentroSeguroRepository
{
    public Task<IReadOnlyList<PuntoEncuentroSeguro>> ListarActivosPorCiudadAsync(
        string ciudad,
        CancellationToken ct)
    {
        return db.PuntosEncuentroSeguros
            .AsNoTracking()
            .Where(p => p.Ciudad == ciudad && p.Activo)
            .OrderBy(p => p.Nombre)
            .ThenBy(p => p.Id)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<PuntoEncuentroSeguro>)t.Result, ct);
    }

    public Task<bool> ExisteAsync(Guid id, CancellationToken ct)
    {
        return db.PuntosEncuentroSeguros.AnyAsync(p => p.Id == id, ct);
    }

    public void Agregar(PuntoEncuentroSeguro punto) => db.PuntosEncuentroSeguros.Add(punto);
}
