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
        var resumenes = await grupos.OrderBy(g => g.Min(r => r.FechaCreacion)).ThenBy(g => g.Key)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(g => new { AvisoId = g.Key, Cantidad = g.Count(), MasAntiguo = g.Min(r => r.FechaCreacion) })
            .ToListAsync(ct);

        var ids = resumenes.Select(r => r.AvisoId).ToArray();
        var avisos = await db.Avisos.AsNoTracking().Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.Titulo, a.Estado, a.EstadoModeracion })
            .ToDictionaryAsync(a => a.Id, ct);
        var motivos = (await db.ReportesAviso.AsNoTracking()
                .Where(r => r.Estado == estado && ids.Contains(r.AvisoId))
                .Select(r => new { r.AvisoId, r.Motivo })
                .ToListAsync(ct))
            .GroupBy(r => r.AvisoId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Motivo.ToString()).Distinct().ToList());

        var items = resumenes.Select(resumen =>
        {
            var aviso = avisos[resumen.AvisoId];
            return new AvisoReportadoResumenDto(
                resumen.AvisoId, aviso.Titulo, aviso.Estado.ToString(), aviso.EstadoModeracion.ToString(),
                resumen.Cantidad, motivos[resumen.AvisoId], resumen.MasAntiguo);
        }).ToList();
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
