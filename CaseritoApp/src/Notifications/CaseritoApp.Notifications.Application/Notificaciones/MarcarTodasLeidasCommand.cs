using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Notifications.Application.Notificaciones;

public sealed record MarcarTodasLeidasCommand(Guid DestinatarioId) : ICommand<int>;

public sealed class MarcarTodasLeidasCommandHandler(
    INotificacionRepository repositorio)
    : ICommandHandler<MarcarTodasLeidasCommand, int>
{
    public Task<Result<int>> Handle(
        MarcarTodasLeidasCommand request,
        CancellationToken cancellationToken)
    {
        return repositorio.MarcarTodasLeidasAsync(request.DestinatarioId, cancellationToken)
            .ContinueWith(t => Result.Exito(t.Result), TaskContinuationOptions.ExecuteSynchronously);
    }
}
