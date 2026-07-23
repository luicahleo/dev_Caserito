using CaseritoApp.Chat.Application.Moderacion;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Moderacion;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Moderacion;

public sealed class ConsultaModeracionChatEfCore(ChatDbContext db) : IConsultaModeracionChat
{
    public async Task<IReadOnlyList<ReporteChatColaDto>> ListarAsync(
        EstadoReporteChat? estado, int limite, CancellationToken ct) =>
        await db.Reportes.AsNoTracking()
            .Where(r => !estado.HasValue || r.Estado == estado.Value)
            .OrderBy(r => r.CreadoEn)
            .ThenBy(r => r.Id)
            .Take(limite)
            .Select(r => new ReporteChatColaDto(
                r.Id, r.ConversacionId, r.TipoObjetivo, r.MensajeId, r.Categoria,
                r.Estado, r.CreadoEn, r.TomadoEn, r.ResueltoEn))
            .ToListAsync(ct);

    public async Task<EvidenciaReporteChatDto?> ObtenerEvidenciaAsync(
        Guid reporteId, CancellationToken ct)
    {
        var reporte = await db.Reportes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reporteId, ct);
        if (reporte is null)
        {
            return null;
        }

        var conversacion = await db.Conversaciones.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == reporte.ConversacionId, ct);
        if (conversacion is null)
        {
            return null;
        }

        var mensajes = await ObtenerVentanaAsync(reporte, ct);
        var rolReportante = Rol(conversacion, reporte.ReportanteId);
        var rolObjetivo = reporte.TipoObjetivo switch
        {
            TipoObjetivoReporteChat.Participante => rolReportante == "Comprador" ? "Vendedor" : "Comprador",
            TipoObjetivoReporteChat.Mensaje => mensajes.FirstOrDefault(m => m.Id == reporte.MensajeId)?.AutorRol
                ?? "Mensaje",
            _ => "Conversación",
        };

        return new EvidenciaReporteChatDto(
            reporte.Id, reporte.TipoObjetivo, reporte.Categoria, reporte.Detalle,
            rolReportante, rolObjetivo, mensajes);
    }

    private async Task<IReadOnlyList<MensajeEvidenciaChatDto>> ObtenerVentanaAsync(
        ReporteChat reporte, CancellationToken ct)
    {
        List<Mensaje> mensajes;
        if (reporte.TipoObjetivo == TipoObjetivoReporteChat.Mensaje && reporte.MensajeId.HasValue)
        {
            var objetivo = await db.Mensajes.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == reporte.MensajeId.Value, ct);
            if (objetivo is null || objetivo.ConversacionId != reporte.ConversacionId)
            {
                return [];
            }

            var anteriores = await db.Mensajes.AsNoTracking()
                .Where(m => m.ConversacionId == reporte.ConversacionId && m.Secuencia < objetivo.Secuencia)
                .OrderByDescending(m => m.Secuencia).Take(5).ToListAsync(ct);
            anteriores.Reverse();
            var posteriores = await db.Mensajes.AsNoTracking()
                .Where(m => m.ConversacionId == reporte.ConversacionId && m.Secuencia > objetivo.Secuencia)
                .OrderBy(m => m.Secuencia).Take(5).ToListAsync(ct);
            mensajes = [.. anteriores, objetivo, .. posteriores];
        }
        else
        {
            mensajes = await db.Mensajes.AsNoTracking()
                .Where(m => m.ConversacionId == reporte.ConversacionId)
                .OrderByDescending(m => m.Secuencia).Take(10).ToListAsync(ct);
            mensajes.Reverse();
        }

        var conversacion = await db.Conversaciones.AsNoTracking()
            .FirstAsync(c => c.Id == reporte.ConversacionId, ct);
        return mensajes.Select(m => new MensajeEvidenciaChatDto(
            m.Id, m.Secuencia, Rol(conversacion, m.RemitenteId), m.Texto, m.EnviadoEn,
            m.Id == reporte.MensajeId)).ToList();
    }

    private static string Rol(Conversacion conversacion, Guid usuarioId) =>
        usuarioId == conversacion.CompradorId ? "Comprador" : "Vendedor";
}
