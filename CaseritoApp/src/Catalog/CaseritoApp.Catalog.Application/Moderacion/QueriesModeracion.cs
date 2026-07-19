using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Moderacion;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Moderacion;

public sealed record ListarAvisosReportadosQuery(string Estado, int Pagina, int Tamano)
    : IQuery<ResultadoPaginado<AvisoReportadoResumenDto>>;

public sealed class ListarAvisosReportadosQueryHandler(IRepositorioReportesAviso reportes)
    : IQueryHandler<ListarAvisosReportadosQuery, ResultadoPaginado<AvisoReportadoResumenDto>>
{
    public Task<ResultadoPaginado<AvisoReportadoResumenDto>> Handle(
        ListarAvisosReportadosQuery request, CancellationToken cancellationToken) =>
        reportes.ListarAgrupadosAsync(
            Enum.Parse<EstadoReporteAviso>(request.Estado), request.Pagina, request.Tamano, cancellationToken);
}

public sealed class ListarAvisosReportadosQueryValidator : AbstractValidator<ListarAvisosReportadosQuery>
{
    public ListarAvisosReportadosQueryValidator()
    {
        RuleFor(x => x.Estado).Must(x => Enum.TryParse<EstadoReporteAviso>(x, out _));
        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Tamano).InclusiveBetween(1, 50);
    }
}

public sealed record ObtenerAvisoReportadoQuery(Guid AvisoId, string Estado) : IQuery<AvisoReportadoDto?>;

public sealed class ObtenerAvisoReportadoQueryHandler(IRepositorioReportesAviso reportes)
    : IQueryHandler<ObtenerAvisoReportadoQuery, AvisoReportadoDto?>
{
    public Task<AvisoReportadoDto?> Handle(
        ObtenerAvisoReportadoQuery request, CancellationToken cancellationToken) =>
        reportes.ObtenerDetalleAsync(
            request.AvisoId, Enum.Parse<EstadoReporteAviso>(request.Estado), cancellationToken);
}
