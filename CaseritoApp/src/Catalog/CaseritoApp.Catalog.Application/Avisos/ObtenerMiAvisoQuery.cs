using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Obtiene el detalle de un aviso propio (para editar).</summary>
public sealed record ObtenerMiAvisoQuery(Guid Id, Guid VendedorId) : IQuery<Result<AvisoDto>>;

/// <summary>Handler de <see cref="ObtenerMiAvisoQuery"/>.</summary>
public sealed class ObtenerMiAvisoQueryHandler(IRepositorioAvisos repositorio)
    : IQueryHandler<ObtenerMiAvisoQuery, Result<AvisoDto>>
{
    public async Task<Result<AvisoDto>> Handle(ObtenerMiAvisoQuery request, CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.Id, cancellationToken);
        if (aviso is null || aviso.Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo<AvisoDto>(new Error(ErroresAviso.NoEncontrado, "El aviso no existe."));
        }

        if (aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo<AvisoDto>(new Error(ErroresAviso.NoEsPropietario, "El aviso pertenece a otro usuario."));
        }

        return Result.Exito(MapaAvisos.ADto(aviso));
    }
}
