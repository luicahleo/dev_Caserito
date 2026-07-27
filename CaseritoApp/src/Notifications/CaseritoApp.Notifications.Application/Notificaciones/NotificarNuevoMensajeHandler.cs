using CaseritoApp.BuildingBlocks.Contracts.Chat;
using CaseritoApp.Notifications.Domain.Notificaciones;
using MediatR;

namespace CaseritoApp.Notifications.Application.Notificaciones;

public sealed class NotificarNuevoMensajeHandler(
    IMediator mediator,
    IConsultaEmailUsuario consultaEmail,
    IEmailSender emailSender)
    : INotificationHandler<ChatMessageSent>
{
    public async Task Handle(ChatMessageSent evento, CancellationToken cancellationToken)
    {
        const string Titulo = "Nuevo mensaje";
        var mensaje = $"Has recibido un nuevo mensaje en la conversación {evento.ConversacionId}.";

        var resultado = await mediator.Send(new CrearNotificacionCommand(
            evento.DestinatarioId,
            TipoNotificacion.NuevoMensaje,
            Titulo,
            mensaje,
            evento.ConversacionId), cancellationToken);

        if (!resultado.EsExito)
        {
            return;
        }

        var email = await consultaEmail.ObtenerEmailAsync(evento.DestinatarioId, cancellationToken);
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        await emailSender.EnviarAsync(
            email,
            Titulo,
            mensaje,
            ct: cancellationToken);
    }
}
