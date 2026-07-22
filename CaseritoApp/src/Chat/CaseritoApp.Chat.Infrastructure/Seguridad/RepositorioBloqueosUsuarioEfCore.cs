using CaseritoApp.Chat.Application.Seguridad;
using CaseritoApp.Chat.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Seguridad;

public sealed class RepositorioBloqueosUsuarioEfCore(ChatDbContext db) : IRepositorioBloqueosUsuario
{
    public Task<bool> ExisteEntreAsync(Guid usuarioA, Guid usuarioB, CancellationToken ct) =>
        db.BloqueosUsuario.AnyAsync(
            b => (b.BloqueadorId == usuarioA && b.BloqueadoId == usuarioB)
                || (b.BloqueadorId == usuarioB && b.BloqueadoId == usuarioA),
            ct);

    public Task<BloqueoUsuario?> ObtenerAsync(
        Guid bloqueadorId,
        Guid bloqueadoId,
        CancellationToken ct) =>
        db.BloqueosUsuario.FirstOrDefaultAsync(
            b => b.BloqueadorId == bloqueadorId && b.BloqueadoId == bloqueadoId,
            ct);

    public void Agregar(BloqueoUsuario bloqueo) => db.BloqueosUsuario.Add(bloqueo);

    public void Quitar(BloqueoUsuario bloqueo) => db.BloqueosUsuario.Remove(bloqueo);
}
