using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Pausa un aviso propio.</summary>
public sealed record PausarAvisoCommand(Guid Id, Guid VendedorId) : ICommand;

/// <summary>Handler de <see cref="PausarAvisoCommand"/>.</summary>
public sealed class PausarAvisoCommandHandler(IRepositorioAvisos repositorio, TimeProvider reloj)
    : ICommandHandler<PausarAvisoCommand>
{
    public async Task<Result> Handle(PausarAvisoCommand request, CancellationToken cancellationToken)
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

        return aviso.Pausar(reloj.GetUtcNow().UtcDateTime);
    }
}
