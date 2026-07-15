using CaseritoApp.BuildingBlocks.Application.Messaging;
using FluentValidation;

namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Busca usuarios (por email/nombre) paginados, con sus roles.</summary>
public sealed record BuscarUsuariosQuery(string? Query, int Pagina, int Tamano)
    : IQuery<ResultadoPaginado<UsuarioConRolesDto>>;

/// <summary>Handler de <see cref="BuscarUsuariosQuery"/>: delega en <see cref="IRepositorioRolesUsuario"/>.</summary>
public sealed class BuscarUsuariosQueryHandler(IRepositorioRolesUsuario repositorio)
    : IQueryHandler<BuscarUsuariosQuery, ResultadoPaginado<UsuarioConRolesDto>>
{
    public Task<ResultadoPaginado<UsuarioConRolesDto>> Handle(
        BuscarUsuariosQuery request, CancellationToken cancellationToken) =>
        repositorio.BuscarUsuariosAsync(request.Query, request.Pagina, request.Tamano, cancellationToken);
}

/// <summary>Valida la paginación: página ≥ 1 y tamaño en 1..100.</summary>
public sealed class BuscarUsuariosQueryValidator : AbstractValidator<BuscarUsuariosQuery>
{
    public BuscarUsuariosQueryValidator()
    {
        RuleFor(q => q.Pagina)
            .GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");

        RuleFor(q => q.Tamano)
            .InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
