using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Catalog.Domain.Avisos;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Avisos;

/// <summary>Búsqueda pública de avisos Activos por texto y filtros, paginada.</summary>
public sealed record BuscarAvisosQuery(
    string? Texto,
    Guid? CategoriaId,
    Guid? CiudadId,
    decimal? PrecioMin,
    decimal? PrecioMax,
    string? Condicion,
    int Pagina,
    int Tamano) : IQuery<ResultadoPaginado<AvisoPublicoResumenDto>>;

/// <summary>Handler de <see cref="BuscarAvisosQuery"/>: normaliza el texto/condición y delega en el puerto.</summary>
public sealed class BuscarAvisosQueryHandler(IConsultaAvisosPublica consulta)
    : IQueryHandler<BuscarAvisosQuery, ResultadoPaginado<AvisoPublicoResumenDto>>
{
    public Task<ResultadoPaginado<AvisoPublicoResumenDto>> Handle(
        BuscarAvisosQuery request, CancellationToken cancellationToken)
    {
        var tokens = Tokenizar(request.Texto);
        CondicionArticulo? condicion =
            Enum.TryParse<CondicionArticulo>(request.Condicion, ignoreCase: true, out var c) ? c : null;

        var filtro = new FiltroBusquedaAvisos(
            tokens, request.CategoriaId, request.CiudadId, request.PrecioMin, request.PrecioMax, condicion);

        return consulta.BuscarAsync(filtro, request.Pagina, request.Tamano, cancellationToken);
    }

    private static string[] Tokenizar(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>Valida la paginación y los filtros de <see cref="BuscarAvisosQuery"/>.</summary>
public sealed class BuscarAvisosQueryValidator : AbstractValidator<BuscarAvisosQuery>
{
    public BuscarAvisosQueryValidator()
    {
        RuleFor(q => q.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");
        RuleFor(q => q.Tamano).InclusiveBetween(1, 50).WithMessage("El tamaño debe estar entre 1 y 50.");

        RuleFor(q => q.PrecioMin)
            .GreaterThanOrEqualTo(0).When(q => q.PrecioMin.HasValue)
            .WithMessage("El precio mínimo no puede ser negativo.");
        RuleFor(q => q.PrecioMax)
            .GreaterThanOrEqualTo(0).When(q => q.PrecioMax.HasValue)
            .WithMessage("El precio máximo no puede ser negativo.");
        RuleFor(q => q)
            .Must(q => !(q.PrecioMin.HasValue && q.PrecioMax.HasValue) || q.PrecioMin <= q.PrecioMax)
            .WithMessage("El precio mínimo no puede ser mayor al máximo.");

        RuleFor(q => q.Condicion)
            .Must(v => v is null || Enum.TryParse<CondicionArticulo>(v, ignoreCase: true, out _))
            .WithMessage("La condición no es válida.");
    }
}
