using CaseritoApp.BuildingBlocks.Application.Messaging;

namespace CaseritoApp.Notifications.Application.Notificaciones;

public sealed record ListarNotificacionesQuery(
    Guid DestinatarioId,
    bool SoloNoLeidas,
    int Pagina,
    int Tamano) : IQuery<PaginaNotificacionesDto>;

public sealed class ListarNotificacionesQueryHandler(
    INotificacionRepository repositorio)
    : IQueryHandler<ListarNotificacionesQuery, PaginaNotificacionesDto>
{
    public Task<PaginaNotificacionesDto> Handle(
        ListarNotificacionesQuery request,
        CancellationToken cancellationToken)
    {
        return repositorio.ListarAsync(
            request.DestinatarioId,
            request.SoloNoLeidas,
            request.Pagina,
            request.Tamano,
            cancellationToken);
    }
}
