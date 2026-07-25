using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

public sealed record ValidarAvisoParaVentaQuery(Guid AvisoId, Guid VendedorId)
    : IQuery<Result>;

public sealed class ValidarAvisoParaVentaQueryHandler(IRepositorioAvisos repositorio)
    : IQueryHandler<ValidarAvisoParaVentaQuery, Result>
{
    public async Task<Result> Handle(
        ValidarAvisoParaVentaQuery request,
        CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.AvisoId, cancellationToken);
        if (aviso is null || aviso.VendedorId != request.VendedorId)
        {
            return NoEncontrado();
        }

        if (aviso.Estado == EstadoAviso.Eliminado
            || aviso.EstadoModeracion == EstadoModeracionAviso.EliminadoPorModeracion)
        {
            return Result.Fallo(new Error(
                ErroresAviso.TransicionInvalida,
                "El aviso no admite esta transición."));
        }

        return Result.Exito();
    }

    private static Result NoEncontrado() =>
        Result.Fallo(new Error(
            ErroresAviso.NoEncontrado,
            "El aviso no está disponible."));
}
