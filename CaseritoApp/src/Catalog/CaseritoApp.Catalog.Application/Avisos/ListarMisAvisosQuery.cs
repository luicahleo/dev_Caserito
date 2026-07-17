using CaseritoApp.BuildingBlocks.Application.Messaging;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Lista paginada de los avisos del vendedor autenticado (excluye eliminados).</summary>
public sealed record ListarMisAvisosQuery(Guid VendedorId, int Pagina, int Tamano)
    : IQuery<ResultadoPaginado<AvisoResumenDto>>;

/// <summary>Handler de <see cref="ListarMisAvisosQuery"/>.</summary>
public sealed class ListarMisAvisosQueryHandler(IRepositorioAvisos repositorio)
    : IQueryHandler<ListarMisAvisosQuery, ResultadoPaginado<AvisoResumenDto>>
{
    public Task<ResultadoPaginado<AvisoResumenDto>> Handle(
        ListarMisAvisosQuery request, CancellationToken cancellationToken) =>
        repositorio.ListarPorVendedorAsync(request.VendedorId, request.Pagina, request.Tamano, cancellationToken);
}

/// <summary>Valida la paginación de <see cref="ListarMisAvisosQuery"/>.</summary>
public sealed class ListarMisAvisosQueryValidator : AbstractValidator<ListarMisAvisosQuery>
{
    public ListarMisAvisosQueryValidator()
    {
        RuleFor(q => q.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(q => q.Tamano).InclusiveBetween(1, 100).WithMessage("El tamaño debe estar entre 1 y 100.");
    }
}
