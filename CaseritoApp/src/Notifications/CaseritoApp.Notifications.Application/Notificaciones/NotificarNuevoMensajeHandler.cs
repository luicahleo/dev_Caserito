using CaseritoApp.BuildingBlocks.Contracts.Chat;
using CaseritoApp.Notifications.Application.Push;
using CaseritoApp.Notifications.Domain.Notificaciones;
using CaseritoApp.Notifications.Domain.Push;
using MediatR;

namespace CaseritoApp.Notifications.Application.Notificaciones;

public sealed class NotificarNuevoMensajeHandler(
    IMediator mediator,
    IConsultaEmailUsuario consultaEmail,
    IEmailSender emailSender,
    IAlmacenIntencionesPush intenciones,
    TimeProvider reloj)
    : INotificationHandler<ChatMessageSent>
{
    public async Task Handle(ChatMessageSent evento, CancellationToken cancellationToken)
    {
        const string Titulo = "Nuevo mensaje";
        const string Mensaje = "Has recibido un nuevo mensaje.";

        var resultado = await mediator.Send(new CrearNotificacionCommand(
            evento.DestinatarioId,
            TipoNotificacion.NuevoMensaje,
            Titulo,
            Mensaje,
            evento.ConversacionId), cancellationToken);

        if (!resultado.EsExito)
        {
            return;
        }

        if (!await intenciones.ExisteEventoAsync(evento.EventId, cancellationToken))
        {
            var intencion = IntencionPush.Crear(
                evento.EventId,
                evento.DestinatarioId,
                evento.ConversacionId,
                evento.Secuencia,
                reloj.GetUtcNow());
            if (intencion.EsExito)
            {
                intenciones.Agregar(intencion.Valor);
            }
        }

        var email = await consultaEmail.ObtenerEmailAsync(evento.DestinatarioId, cancellationToken);
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        await emailSender.EnviarAsync(
            email,
            Titulo,
            Mensaje,
            ct: cancellationToken);
    }
}
