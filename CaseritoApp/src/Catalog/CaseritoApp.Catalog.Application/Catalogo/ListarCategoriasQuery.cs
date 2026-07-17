using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Catalog.Application.Avisos;

namespace CaseritoApp.Catalog.Application.Catalogo;

/// <summary>Lista las categorías activas del catálogo.</summary>
public sealed record ListarCategoriasQuery : IQuery<IReadOnlyList<CategoriaDto>>;

/// <summary>Handler de <see cref="ListarCategoriasQuery"/>.</summary>
public sealed class ListarCategoriasQueryHandler(IConsultaCatalogo catalogo)
    : IQueryHandler<ListarCategoriasQuery, IReadOnlyList<CategoriaDto>>
{
    public Task<IReadOnlyList<CategoriaDto>> Handle(ListarCategoriasQuery request, CancellationToken cancellationToken) =>
        catalogo.ListarCategoriasAsync(cancellationToken);
}
