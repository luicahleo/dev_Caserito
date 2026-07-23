using CaseritoApp.BuildingBlocks.Application.Messaging;

namespace CaseritoApp.Chat.Application.Conversaciones;

public sealed record PuedeRecibirTiempoRealQuery(
    Guid ConversacionId,
    Guid UsuarioId) : IQuery<bool>;

public sealed class PuedeRecibirTiempoRealQueryHandler(IConsultaConversaciones consulta)
    : IQueryHandler<PuedeRecibirTiempoRealQuery, bool>
{
    public Task<bool> Handle(
        PuedeRecibirTiempoRealQuery request,
        CancellationToken cancellationToken) =>
        consulta.PuedeRecibirTiempoRealAsync(
            request.ConversacionId,
            request.UsuarioId,
            cancellationToken);
}
