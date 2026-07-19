using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Domain.Moderacion;

namespace CaseritoApp.Catalog.Application.Moderacion;

public interface IRepositorioReportesAviso
{
    public Task<bool> ExistePendienteAsync(Guid avisoId, Guid reportanteId, CancellationToken ct);
    public Task<ReporteAviso?> ObtenerAsync(Guid reporteId, CancellationToken ct);
    public Task<IReadOnlyList<ReporteAviso>> ListarPendientesAsync(Guid avisoId, CancellationToken ct);
    public Task<ResultadoPaginado<AvisoReportadoResumenDto>> ListarAgrupadosAsync(
        EstadoReporteAviso estado, int pagina, int tamano, CancellationToken ct);
    public Task<AvisoReportadoDto?> ObtenerDetalleAsync(
        Guid avisoId, EstadoReporteAviso estado, CancellationToken ct);
    public void Agregar(ReporteAviso reporte);
}
