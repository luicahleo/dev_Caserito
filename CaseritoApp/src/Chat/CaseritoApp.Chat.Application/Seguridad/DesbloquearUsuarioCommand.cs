using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Application.Seguridad;

public sealed record DesbloquearUsuarioCommand(Guid ConversacionId, Guid ActorId) : ICommand;

public sealed class DesbloquearUsuarioCommandHandler(
    IRepositorioConversaciones conversaciones,
    IRepositorioBloqueosUsuario bloqueos) : ICommandHandler<DesbloquearUsuarioCommand>
{
    public async Task<Result> Handle(
        DesbloquearUsuarioCommand request,
        CancellationToken cancellationToken)
    {
        var conversacion = await conversaciones.ObtenerAsync(
            request.ConversacionId,
            cancellationToken);
        if (conversacion is null || !conversacion.EsParticipante(request.ActorId))
        {
            return Result.Fallo(new Error(
                ErroresConversacion.NoEncontrada,
                "La conversación no está disponible."));
        }

        var contraparteId = request.ActorId == conversacion.CompradorId
            ? conversacion.VendedorId
            : conversacion.CompradorId;
        var bloqueo = await bloqueos.ObtenerAsync(
            request.ActorId,
            contraparteId,
            cancellationToken);
        if (bloqueo is not null)
        {
            bloqueos.Quitar(bloqueo);
        }

        return Result.Exito();
    }
}
