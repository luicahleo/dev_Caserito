using CaseritoApp.Chat.Application.Moderacion;
using CaseritoApp.Chat.Domain.Moderacion;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Moderacion;

public sealed class RepositorioReportesChatEfCore(ChatDbContext db) : IRepositorioReportesChat
{
    public Task<ReporteChat?> ObtenerAsync(Guid reporteId, CancellationToken ct) =>
        db.Reportes.FirstOrDefaultAsync(r => r.Id == reporteId, ct);

    public Task<bool> ExisteAbiertoAsync(
        Guid conversacionId,
        Guid reportanteId,
        TipoObjetivoReporteChat tipoObjetivo,
        Guid? mensajeId,
        CancellationToken ct) => db.Reportes.AnyAsync(
            r => r.ConversacionId == conversacionId
                && r.ReportanteId == reportanteId
                && r.TipoObjetivo == tipoObjetivo
                && r.MensajeId == mensajeId
                && (r.Estado == EstadoReporteChat.Pendiente || r.Estado == EstadoReporteChat.EnRevision),
            ct);

    public void Agregar(ReporteChat reporte) => db.Reportes.Add(reporte);
}
