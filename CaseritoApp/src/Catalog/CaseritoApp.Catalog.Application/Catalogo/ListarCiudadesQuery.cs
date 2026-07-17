using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Catalog.Application.Avisos;

namespace CaseritoApp.Catalog.Application.Catalogo;

/// <summary>Lista las ciudades activas del catálogo.</summary>
public sealed record ListarCiudadesQuery : IQuery<IReadOnlyList<CiudadDto>>;

/// <summary>Handler de <see cref="ListarCiudadesQuery"/>.</summary>
public sealed class ListarCiudadesQueryHandler(IConsultaCatalogo catalogo)
    : IQueryHandler<ListarCiudadesQuery, IReadOnlyList<CiudadDto>>
{
    public Task<IReadOnlyList<CiudadDto>> Handle(ListarCiudadesQuery request, CancellationToken cancellationToken) =>
        catalogo.ListarCiudadesAsync(cancellationToken);
}
