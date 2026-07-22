using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Domain.Moderacion;

namespace CaseritoApp.Chat.Application.Moderacion;

public sealed record ListarReportesChatQuery(EstadoReporteChat? Estado, int Limite)
    : IQuery<IReadOnlyList<ReporteChatColaDto>>;

public sealed class ListarReportesChatQueryHandler(IConsultaModeracionChat consulta)
    : IQueryHandler<ListarReportesChatQuery, IReadOnlyList<ReporteChatColaDto>>
{
    public Task<IReadOnlyList<ReporteChatColaDto>> Handle(
        ListarReportesChatQuery request, CancellationToken cancellationToken) =>
        consulta.ListarAsync(request.Estado, Math.Clamp(request.Limite, 1, 100), cancellationToken);
}

public sealed record ObtenerEvidenciaReporteChatQuery(Guid ReporteId, Guid ModeradorId)
    : IQuery<Result<EvidenciaReporteChatDto>>;

public sealed class ObtenerEvidenciaReporteChatQueryHandler(
    IRepositorioReportesChat reportes,
    IAuditorModeracionChat auditor,
    IConsultaModeracionChat consulta,
    TimeProvider reloj) : IQueryHandler<ObtenerEvidenciaReporteChatQuery, Result<EvidenciaReporteChatDto>>
{
    public async Task<Result<EvidenciaReporteChatDto>> Handle(
        ObtenerEvidenciaReporteChatQuery request, CancellationToken cancellationToken)
    {
        var reporte = await reportes.ObtenerAsync(request.ReporteId, cancellationToken);
        if (reporte is null)
        {
            return Result.Fallo<EvidenciaReporteChatDto>(new Error(
                ErroresModeracionChat.NoEncontrado, "El reporte no está disponible."));
        }

        var auditado = await auditor.RegistrarConsultaEvidenciaAsync(
            reporte.Id, reporte.ConversacionId, request.ModeradorId, reloj.GetUtcNow(), cancellationToken);
        if (!auditado)
        {
            return Result.Fallo<EvidenciaReporteChatDto>(new Error(
                ErroresModeracionChat.AuditoriaNoDisponible, "La moderación no está disponible."));
        }

        var evidencia = await consulta.ObtenerEvidenciaAsync(reporte.Id, cancellationToken);
        return evidencia is null
            ? Result.Fallo<EvidenciaReporteChatDto>(new Error(
                ErroresModeracionChat.NoEncontrado, "El reporte no está disponible."))
            : Result.Exito(evidencia);
    }
}
