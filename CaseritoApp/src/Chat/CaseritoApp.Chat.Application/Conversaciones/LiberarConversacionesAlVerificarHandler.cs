using CaseritoApp.BuildingBlocks.Contracts.Chat;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.Chat.Application.Mensajes;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Chat.Application.Conversaciones;

/// <summary>
/// Al verificarse un comprador, libera sus conversaciones retenidas para que el vendedor pueda
/// verlas. Es el mecanismo principal; el respaldo está en IniciarConversacionCommand.
/// </summary>
public sealed partial class LiberarConversacionesAlVerificarHandler(
    IRepositorioConversaciones repositorio,
    IRepositorioMensajes mensajes,
    IPublisher publisher,
    TimeProvider reloj,
    ILogger<LiberarConversacionesAlVerificarHandler> logger)
    : INotificationHandler<UserVerified>
{
    public async Task Handle(UserVerified notification, CancellationToken cancellationToken)
    {
        var retenidas = await repositorio.ListarRetenidasDeCompradorAsync(
            notification.UserId, cancellationToken);
        if (retenidas.Count == 0)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        foreach (var conversacion in retenidas)
        {
            if (!conversacion.LiberarPorVerificacion(ahora).EsExito)
            {
                continue;
            }

            // Una sola notificación por conversación, referida al último mensaje: publicar una
            // por cada mensaje acumulado sería una ráfaga de push para el vendedor.
            var ultimo = await mensajes.ObtenerUltimoDeConversacionAsync(
                conversacion.Id, cancellationToken);
            if (ultimo is not null)
            {
                await publisher.Publish(
                    new ChatMessageSent(
                        Guid.NewGuid(),
                        ahora,
                        conversacion.Id,
                        ultimo.Id,
                        ultimo.Secuencia,
                        conversacion.CompradorId,
                        conversacion.VendedorId),
                    cancellationToken);
            }
        }

        RegistrarLiberacion(logger, notification.UserId, retenidas.Count);
    }

    // Auditoría sin PII: solo el usuario y cuántas conversaciones se liberaron.
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Conversaciones liberadas por verificación: usuario={Usuario} total={Total}")]
    private static partial void RegistrarLiberacion(ILogger logger, Guid usuario, int total);
}
