using CaseritoApp.BuildingBlocks.Contracts.Chat;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace CaseritoApp.Host.Chat;

public sealed record EstadoMensajesActualizadoDto(
    Guid ConversacionId,
    long UltimaSecuenciaEntregada,
    long UltimaSecuenciaLeida);

public interface IPublicadorEventosGlobalesChat
{
    public Task PublicarContadorAsync(Guid usuarioId, CancellationToken ct);

    public Task PublicarEstadoAsync(
        Guid usuarioId,
        EstadoMensajesActualizadoDto estado,
        CancellationToken ct);
}

public sealed class PublicadorEventosGlobalesChat(IHubContext<ChatHub> hub)
    : IPublicadorEventosGlobalesChat
{
    public Task PublicarContadorAsync(Guid usuarioId, CancellationToken ct) =>
        hub.Clients.Group(GruposChat.ParaUsuario(usuarioId))
            .SendCoreAsync("ContadorChatActualizado", [], ct);

    public Task PublicarEstadoAsync(
        Guid usuarioId,
        EstadoMensajesActualizadoDto estado,
        CancellationToken ct) =>
        hub.Clients.Group(GruposChat.ParaUsuario(usuarioId))
            .SendAsync("EstadoMensajesActualizado", estado, ct);
}

public sealed class ActualizarContadorChatHandler(IPublicadorEventosGlobalesChat publicador)
    : INotificationHandler<ChatMessageSent>
{
    public Task Handle(ChatMessageSent notification, CancellationToken cancellationToken) =>
        publicador.PublicarContadorAsync(notification.DestinatarioId, cancellationToken);
}
