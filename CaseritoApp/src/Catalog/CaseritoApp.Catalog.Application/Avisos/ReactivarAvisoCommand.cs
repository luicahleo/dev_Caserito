using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Reactiva un aviso propio pausado.</summary>
public sealed record ReactivarAvisoCommand(Guid Id, Guid VendedorId) : ICommand;

/// <summary>Handler de <see cref="ReactivarAvisoCommand"/>.</summary>
public sealed class ReactivarAvisoCommandHandler(IRepositorioAvisos repositorio, TimeProvider reloj)
    : ICommandHandler<ReactivarAvisoCommand>
{
    public async Task<Result> Handle(ReactivarAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.Id, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        return aviso.Reactivar(reloj.GetUtcNow().UtcDateTime);
    }
}
