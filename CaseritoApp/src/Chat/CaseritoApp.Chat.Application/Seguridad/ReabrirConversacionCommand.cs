using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Application.Seguridad;

public sealed record ReabrirConversacionCommand(Guid ConversacionId, Guid ActorId) : ICommand;

public sealed class ReabrirConversacionCommandHandler(
    IRepositorioConversaciones conversaciones,
    IRepositorioBloqueosUsuario bloqueos,
    TimeProvider reloj) : ICommandHandler<ReabrirConversacionCommand>
{
    public async Task<Result> Handle(
        ReabrirConversacionCommand request,
        CancellationToken cancellationToken)
    {
        var conversacion = await conversaciones.ObtenerAsync(request.ConversacionId, cancellationToken);
        if (conversacion is null || !conversacion.EsParticipante(request.ActorId))
        {
            return Result.Fallo(new Error(
                ErroresConversacion.NoEncontrada,
                "La conversación no está disponible."));
        }

        var contraparteId = request.ActorId == conversacion.CompradorId
            ? conversacion.VendedorId
            : conversacion.CompradorId;
        if (await bloqueos.ExisteEntreAsync(request.ActorId, contraparteId, cancellationToken))
        {
            return Result.Fallo(new Error(
                ErroresConversacion.NoDisponibleParaEnvio,
                "La conversación no está disponible."));
        }

        return conversacion.ReabrirPorParticipante(request.ActorId, reloj.GetUtcNow());
    }
}
