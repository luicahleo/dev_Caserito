using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Notifications.Application.Busquedas;

public sealed record EliminarBusquedaGuardadaCommand(
    Guid Id,
    Guid UsuarioId) : ICommand;

public sealed class EliminarBusquedaGuardadaCommandHandler(
    IBusquedaGuardadaRepository repositorio)
    : ICommandHandler<EliminarBusquedaGuardadaCommand>
{
    public async Task<Result> Handle(
        EliminarBusquedaGuardadaCommand request,
        CancellationToken cancellationToken)
    {
        var busqueda = await repositorio.ObtenerAsync(request.Id, request.UsuarioId, cancellationToken);
        if (busqueda is null)
        {
            return Result.Fallo(new Error(
                "busqueda_guardada_no_encontrada",
                "La búsqueda guardada no existe o no pertenece al usuario."));
        }

        repositorio.Eliminar(busqueda);
        return Result.Exito();
    }
}
