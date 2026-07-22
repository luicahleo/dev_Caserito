using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Seguridad;

namespace CaseritoApp.Chat.Application.Seguridad;

public sealed record BloquearUsuarioCommand(Guid ConversacionId, Guid ActorId) : ICommand;

public sealed class BloquearUsuarioCommandHandler(
    IRepositorioConversaciones conversaciones,
    IRepositorioBloqueosUsuario bloqueos,
    TimeProvider reloj) : ICommandHandler<BloquearUsuarioCommand>
{
    public async Task<Result> Handle(
        BloquearUsuarioCommand request,
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
        var existente = await bloqueos.ObtenerAsync(
            request.ActorId,
            contraparteId,
            cancellationToken);
        if (existente is not null)
        {
            return Result.Exito();
        }

        var creacion = BloqueoUsuario.Crear(request.ActorId, contraparteId, reloj.GetUtcNow());
        if (!creacion.EsExito)
        {
            return Result.Fallo(creacion.Error);
        }

        bloqueos.Agregar(creacion.Valor);
        return Result.Exito();
    }
}
