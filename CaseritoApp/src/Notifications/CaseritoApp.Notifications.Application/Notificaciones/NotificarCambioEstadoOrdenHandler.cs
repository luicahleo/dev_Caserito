using CaseritoApp.BuildingBlocks.Contracts.Orders;
using CaseritoApp.Notifications.Domain.Notificaciones;
using MediatR;

namespace CaseritoApp.Notifications.Application.Notificaciones;

public sealed class NotificarCambioEstadoOrdenHandler(
    IMediator mediator,
    IConsultaParticipantesOrden consultaParticipantes,
    IConsultaEmailUsuario consultaEmail,
    IEmailSender emailSender)
    : INotificationHandler<OrderStatusChanged>
{
    public async Task Handle(OrderStatusChanged evento, CancellationToken cancellationToken)
    {
        var participantes = await consultaParticipantes.ObtenerAsync(evento.OrderId, cancellationToken);
        if (participantes is null)
        {
            return;
        }

        var titulo = "Actualización de orden";
        var mensaje = $"Tu orden {evento.OrderId} cambió de estado de {evento.OldStatus} a {evento.NewStatus}.";

        await NotificarAsync(participantes.CompradorId, titulo, mensaje, evento.OrderId, cancellationToken);
        await NotificarAsync(participantes.VendedorId, titulo, mensaje, evento.OrderId, cancellationToken);
    }

    private async Task NotificarAsync(
        Guid destinatarioId,
        string titulo,
        string mensaje,
        Guid ordenId,
        CancellationToken ct)
    {
        var resultado = await mediator.Send(new CrearNotificacionCommand(
            destinatarioId,
            TipoNotificacion.CambioEstadoOrden,
            titulo,
            mensaje,
            ordenId), ct);

        if (!resultado.EsExito)
        {
            return;
        }

        var email = await consultaEmail.ObtenerEmailAsync(destinatarioId, ct);
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        await emailSender.EnviarAsync(email, titulo, mensaje, ct: ct);
    }
}
