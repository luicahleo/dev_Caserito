using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Catalog.Domain.Moderacion;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Moderacion;

public sealed record ReportarAvisoCommand(
    Guid AvisoId, Guid ReportanteId, string Motivo, string? Detalle) : ICommand<Guid>;

public sealed class ReportarAvisoCommandHandler(
    IRepositorioAvisos avisos, IRepositorioReportesAviso reportes, TimeProvider reloj)
    : ICommandHandler<ReportarAvisoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ReportarAvisoCommand request, CancellationToken cancellationToken)
    {
        var aviso = await avisos.ObtenerAsync(request.AvisoId, cancellationToken);
        if (aviso is null || aviso.Estado != EstadoAviso.Activo ||
            aviso.EstadoModeracion != EstadoModeracionAviso.Visible)
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.NoEncontrado, "El aviso no está disponible."));
        }

        if (aviso.VendedorId == request.ReportanteId)
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.AutorreporteNoPermitido, "No puedes reportar tu propio aviso."));
        }

        if (await reportes.ExistePendienteAsync(request.AvisoId, request.ReportanteId, cancellationToken))
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.ReporteDuplicado, "Ya existe un reporte pendiente."));
        }

        if (!Enum.TryParse<MotivoReporteAviso>(request.Motivo, out var motivo))
        {
            return Result.Fallo<Guid>(new Error(ErroresAviso.MotivoReporteInvalido, "El motivo no es válido."));
        }

        var reporte = ReporteAviso.Crear(
            request.AvisoId, request.ReportanteId, motivo,
            string.IsNullOrWhiteSpace(request.Detalle) ? null : request.Detalle.Trim(),
            reloj.GetUtcNow().UtcDateTime);
        reportes.Agregar(reporte);
        return Result.Exito(reporte.Id);
    }
}

public sealed class ReportarAvisoCommandValidator : AbstractValidator<ReportarAvisoCommand>
{
    public ReportarAvisoCommandValidator()
    {
        RuleFor(x => x.AvisoId).NotEmpty();
        RuleFor(x => x.ReportanteId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().Must(x => Enum.TryParse<MotivoReporteAviso>(x, out _));
        RuleFor(x => x.Detalle).MaximumLength(500);
    }
}
