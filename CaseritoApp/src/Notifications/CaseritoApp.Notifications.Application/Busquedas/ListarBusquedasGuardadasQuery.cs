using CaseritoApp.BuildingBlocks.Application.Messaging;

namespace CaseritoApp.Notifications.Application.Busquedas;

public sealed record ListarBusquedasGuardadasQuery(Guid UsuarioId) : IQuery<IReadOnlyList<BusquedaGuardadaDto>>;

public sealed class ListarBusquedasGuardadasQueryHandler(
    IBusquedaGuardadaRepository repositorio)
    : IQueryHandler<ListarBusquedasGuardadasQuery, IReadOnlyList<BusquedaGuardadaDto>>
{
    public async Task<IReadOnlyList<BusquedaGuardadaDto>> Handle(
        ListarBusquedasGuardadasQuery request,
        CancellationToken cancellationToken)
    {
        var busquedas = await repositorio.ListarPorUsuarioAsync(request.UsuarioId, cancellationToken);

        return busquedas
            .Select(b => new BusquedaGuardadaDto(
                b.Id,
                b.PalabraClave,
                b.Categoria,
                b.Ciudad,
                b.PrecioMinimo,
                b.PrecioMaximo,
                b.EstadoProducto,
                b.CreadaEn))
            .ToList();
    }
}
