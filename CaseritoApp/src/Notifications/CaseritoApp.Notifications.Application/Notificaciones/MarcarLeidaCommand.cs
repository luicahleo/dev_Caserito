using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Notifications.Application.Notificaciones;

public sealed record MarcarLeidaCommand(
    Guid NotificacionId,
    Guid DestinatarioId) : ICommand;

public sealed class MarcarLeidaCommandHandler(
    INotificacionRepository repositorio)
    : ICommandHandler<MarcarLeidaCommand>
{
    public async Task<Result> Handle(
        MarcarLeidaCommand request,
        CancellationToken cancellationToken)
    {
        var notificacion = await repositorio.ObtenerAsync(
            request.NotificacionId,
            request.DestinatarioId,
            cancellationToken);

        if (notificacion is null)
        {
            return Result.Fallo(new Error(
                "notificacion_no_disponible",
                "La notificación no está disponible."));
        }

        notificacion.MarcarComoLeida();
        return Result.Exito();
    }
}
