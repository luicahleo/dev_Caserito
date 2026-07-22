using CaseritoApp.Chat.Domain.Seguridad;

namespace CaseritoApp.Chat.Application.Seguridad;

public interface IRepositorioBloqueosUsuario
{
    public Task<bool> ExisteEntreAsync(Guid usuarioA, Guid usuarioB, CancellationToken ct);

    public Task<BloqueoUsuario?> ObtenerAsync(
        Guid bloqueadorId,
        Guid bloqueadoId,
        CancellationToken ct);

    public void Agregar(BloqueoUsuario bloqueo);

    public void Quitar(BloqueoUsuario bloqueo);
}
