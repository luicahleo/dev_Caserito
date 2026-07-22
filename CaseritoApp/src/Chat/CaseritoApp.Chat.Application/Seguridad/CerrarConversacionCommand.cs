using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Application.Seguridad;

public sealed record CerrarConversacionCommand(Guid ConversacionId, Guid ActorId) : ICommand;

public sealed class CerrarConversacionCommandHandler(
    IRepositorioConversaciones conversaciones,
    IRepositorioBloqueosUsuario bloqueos,
    TimeProvider reloj) : ICommandHandler<CerrarConversacionCommand>
{
    public async Task<Result> Handle(
        CerrarConversacionCommand request,
        CancellationToken cancellationToken)
    {
        var conversacion = await conversaciones.ObtenerAsync(request.ConversacionId, cancellationToken);
        if (conversacion is null || !conversacion.EsParticipante(request.ActorId))
        {
            return NoEncontrada();
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

        return conversacion.CerrarPorParticipante(request.ActorId, reloj.GetUtcNow());
    }

    private static Result NoEncontrada() => Result.Fallo(new Error(
        ErroresConversacion.NoEncontrada,
        "La conversación no está disponible."));
}
