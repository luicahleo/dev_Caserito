using CaseritoApp.BuildingBlocks.Application.Messaging;

namespace CaseritoApp.Notifications.Application.Notificaciones;

public sealed record ContarNoLeidasQuery(Guid DestinatarioId) : IQuery<int>;

public sealed class ContarNoLeidasQueryHandler(
    INotificacionRepository repositorio)
    : IQueryHandler<ContarNoLeidasQuery, int>
{
    public Task<int> Handle(
        ContarNoLeidasQuery request,
        CancellationToken cancellationToken)
    {
        return repositorio.ContarNoLeidasAsync(request.DestinatarioId, cancellationToken);
    }
}
