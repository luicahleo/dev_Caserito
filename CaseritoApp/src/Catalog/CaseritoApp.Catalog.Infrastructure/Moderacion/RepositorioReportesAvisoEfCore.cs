using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Application.Moderacion;
using CaseritoApp.Catalog.Domain.Moderacion;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure.Moderacion;

public sealed class RepositorioReportesAvisoEfCore(CatalogDbContext db) : IRepositorioReportesAviso
{
    public Task<bool> ExistePendienteAsync(Guid avisoId, Guid reportanteId, CancellationToken ct) =>
        db.ReportesAviso.AnyAsync(r => r.AvisoId == avisoId && r.ReportanteId == reportanteId &&
            r.Estado == EstadoReporteAviso.Pendiente, ct);
    public Task<ReporteAviso?> ObtenerAsync(Guid reporteId, CancellationToken ct) =>
        db.ReportesAviso.FirstOrDefaultAsync(r => r.Id == reporteId, ct);
    public async Task<IReadOnlyList<ReporteAviso>> ListarPendientesAsync(Guid avisoId, CancellationToken ct) =>
        await db.ReportesAviso.Where(r => r.AvisoId == avisoId && r.Estado == EstadoReporteAviso.Pendiente).ToListAsync(ct);
    public void Agregar(ReporteAviso reporte) => db.ReportesAviso.Add(reporte);

    public async Task<ResultadoPaginado<AvisoReportadoResumenDto>> ListarAgrupadosAsync(
        EstadoReporteAviso estado, int pagina, int tamano, CancellationToken ct)
    {
        var grupos = db.ReportesAviso.Where(r => r.Estado == estado).GroupBy(r => r.AvisoId);
        var total = await grupos.CountAsync(ct);
        var items = await grupos.OrderBy(g => g.Min(r => r.FechaCreacion)).ThenBy(g => g.Key)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(g => new AvisoReportadoResumenDto(
                g.Key, db.Avisos.Where(a => a.Id == g.Key).Select(a => a.Titulo).First(),
                db.Avisos.Where(a => a.Id == g.Key).Select(a => a.Estado.ToString()).First(),
                db.Avisos.Where(a => a.Id == g.Key).Select(a => a.EstadoModeracion.ToString()).First(),
                g.Count(), g.Select(r => r.Motivo.ToString()).Distinct().ToList(), g.Min(r => r.FechaCreacion)))
            .ToListAsync(ct);
        return new(items, pagina, tamano, total);
    }

    public Task<AvisoReportadoDto?> ObtenerDetalleAsync(
        Guid avisoId, EstadoReporteAviso estado, CancellationToken ct) =>
        db.Avisos.Where(a => a.Id == avisoId && db.ReportesAviso.Any(r => r.AvisoId == avisoId && r.Estado == estado))
            .Select(a => new AvisoReportadoDto(
                a.Id, a.Titulo, a.Descripcion, a.Estado.ToString(), a.EstadoModeracion.ToString(),
                a.Fotos.OrderBy(f => f.Orden).Select(f => new FotoAvisoDto(f.Id, $"/api/fotos/{f.Clave}", f.Orden)).ToList(),
                db.ReportesAviso.Where(r => r.AvisoId == avisoId && r.Estado == estado)
                    .OrderBy(r => r.FechaCreacion)
                    .Select(r => new ReporteAvisoDto(r.Id, r.Motivo.ToString(), r.Detalle, r.Estado.ToString(), r.FechaCreacion, r.FechaResolucion)).ToList()))
            .FirstOrDefaultAsync(ct);
}
