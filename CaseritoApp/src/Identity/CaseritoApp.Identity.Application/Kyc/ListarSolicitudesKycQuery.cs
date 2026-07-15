using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;
using FluentValidation;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Lista paginada de solicitudes KYC para el administrador, filtrable por estado.</summary>
public sealed record ListarSolicitudesKycQuery(string? Estado, int Pagina, int Tamano)
    : IQuery<ResultadoPaginado<SolicitudKycResumenDto>>;

/// <summary>Handler: traduce el filtro de estado y delega la paginación en el repositorio.</summary>
public sealed class ListarSolicitudesKycQueryHandler(IRepositorioVerificacionKyc repositorio)
    : IQueryHandler<ListarSolicitudesKycQuery, ResultadoPaginado<SolicitudKycResumenDto>>
{
    public Task<ResultadoPaginado<SolicitudKycResumenDto>> Handle(
        ListarSolicitudesKycQuery request, CancellationToken cancellationToken)
    {
        EstadoKyc? estado = Enum.TryParse<EstadoKyc>(request.Estado, out var e) ? e : null;
        return repositorio.ListarAsync(estado, request.Pagina, request.Tamano, cancellationToken);
    }
}

/// <summary>Valida los límites de paginación (mismo criterio que la búsqueda de usuarios).</summary>
public sealed class ListarSolicitudesKycQueryValidator : AbstractValidator<ListarSolicitudesKycQuery>
{
    public ListarSolicitudesKycQueryValidator()
    {
        RuleFor(q => q.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Tamano).InclusiveBetween(1, 100);
    }
}
