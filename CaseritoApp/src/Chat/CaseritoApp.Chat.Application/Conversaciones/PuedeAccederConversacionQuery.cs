using CaseritoApp.BuildingBlocks.Application.Messaging;

namespace CaseritoApp.Chat.Application.Conversaciones;

public sealed record PuedeAccederConversacionQuery(
    Guid ConversacionId,
    Guid UsuarioId) : IQuery<bool>;

public sealed class PuedeAccederConversacionQueryHandler(IConsultaConversaciones consulta)
    : IQueryHandler<PuedeAccederConversacionQuery, bool>
{
    public Task<bool> Handle(
        PuedeAccederConversacionQuery request,
        CancellationToken cancellationToken) =>
        consulta.PuedeAccederAsync(
            request.ConversacionId,
            request.UsuarioId,
            cancellationToken);
}
