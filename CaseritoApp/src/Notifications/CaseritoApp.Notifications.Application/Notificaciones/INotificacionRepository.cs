using CaseritoApp.Notifications.Domain.Notificaciones;

namespace CaseritoApp.Notifications.Application.Notificaciones;

public interface INotificacionRepository
{
    public void Agregar(Notificacion notificacion);

    public Task<Notificacion?> ObtenerAsync(Guid id, Guid destinatarioId, CancellationToken ct);

    public Task<PaginaNotificacionesDto> ListarAsync(
        Guid destinatarioId,
        bool soloNoLeidas,
        int pagina,
        int tamano,
        CancellationToken ct);

    public Task<int> ContarNoLeidasAsync(Guid destinatarioId, CancellationToken ct);

    public Task<int> MarcarTodasLeidasAsync(Guid destinatarioId, CancellationToken ct);
}
