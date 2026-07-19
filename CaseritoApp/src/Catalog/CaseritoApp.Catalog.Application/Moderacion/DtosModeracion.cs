using CaseritoApp.Catalog.Application.Fotos;

namespace CaseritoApp.Catalog.Application.Moderacion;

public sealed record AvisoReportadoResumenDto(
    Guid AvisoId, string Titulo, string EstadoAviso, string EstadoModeracion,
    int CantidadReportes, IReadOnlyList<string> Motivos, DateTime ReporteMasAntiguo);

public sealed record ReporteAvisoDto(
    Guid Id, string Motivo, string? Detalle, string Estado,
    DateTime FechaCreacion, DateTime? FechaResolucion);

public sealed record AvisoReportadoDto(
    Guid AvisoId, string Titulo, string Descripcion, string EstadoAviso,
    string EstadoModeracion, IReadOnlyList<FotoAvisoDto> Fotos,
    IReadOnlyList<ReporteAvisoDto> Reportes);
