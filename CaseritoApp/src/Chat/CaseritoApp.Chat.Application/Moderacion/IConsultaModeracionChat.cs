using CaseritoApp.Chat.Domain.Moderacion;

namespace CaseritoApp.Chat.Application.Moderacion;

public sealed record ReporteChatColaDto(
    Guid Id,
    Guid ConversacionId,
    TipoObjetivoReporteChat TipoObjetivo,
    Guid? MensajeId,
    CategoriaReporteChat Categoria,
    EstadoReporteChat Estado,
    DateTimeOffset CreadoEn,
    DateTimeOffset? TomadoEn,
    DateTimeOffset? ResueltoEn);

public sealed record MensajeEvidenciaChatDto(
    Guid Id, long Secuencia, string AutorRol, string Texto, DateTimeOffset EnviadoEn, bool EsObjetivo);

public sealed record EvidenciaReporteChatDto(
    Guid ReporteId,
    TipoObjetivoReporteChat TipoObjetivo,
    CategoriaReporteChat Categoria,
    string? Detalle,
    string RolReportante,
    string RolObjetivo,
    IReadOnlyList<MensajeEvidenciaChatDto> Mensajes);

public interface IConsultaModeracionChat
{
    public Task<IReadOnlyList<ReporteChatColaDto>> ListarAsync(
        EstadoReporteChat? estado, int limite, CancellationToken ct);

    public Task<EvidenciaReporteChatDto?> ObtenerEvidenciaAsync(Guid reporteId, CancellationToken ct);
}
