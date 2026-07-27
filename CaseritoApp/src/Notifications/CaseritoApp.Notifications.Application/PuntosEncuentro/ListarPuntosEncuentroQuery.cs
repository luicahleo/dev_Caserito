using CaseritoApp.BuildingBlocks.Application.Messaging;

namespace CaseritoApp.Notifications.Application.PuntosEncuentro;

public sealed record ListarPuntosEncuentroQuery(string Ciudad)
    : IQuery<IReadOnlyList<PuntoEncuentroSeguroDto>>;

public sealed class ListarPuntosEncuentroQueryHandler(
    IPuntoEncuentroSeguroRepository repositorio)
    : IQueryHandler<ListarPuntosEncuentroQuery, IReadOnlyList<PuntoEncuentroSeguroDto>>
{
    public async Task<IReadOnlyList<PuntoEncuentroSeguroDto>> Handle(
        ListarPuntosEncuentroQuery request,
        CancellationToken cancellationToken)
    {
        var ciudadNormalizada = request.Ciudad.Trim().ToLowerInvariant();
        var puntos = await repositorio.ListarActivosPorCiudadAsync(
            ciudadNormalizada,
            cancellationToken);

        return puntos
            .Select(p => new PuntoEncuentroSeguroDto(
                p.Id,
                p.Nombre,
                p.Ciudad,
                p.Direccion,
                p.Activo))
            .ToList();
    }
}
