using Microsoft.AspNetCore.SignalR;

namespace CaseritoApp.Host.Chat;

public sealed class PublicadorSignalRMensajes(IHubContext<ChatHub> hub)
    : IPublicadorMensajesTiempoReal
{
    public Task PublicarAsync(
        MensajeTiempoRealDto mensaje,
        CancellationToken cancellationToken) =>
        hub.Clients.Group(GruposChat.ParaConversacion(mensaje.ConversacionId))
            .SendAsync("MensajeCreado", mensaje, cancellationToken);
}
