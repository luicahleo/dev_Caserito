using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Paginacion;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Conversaciones;

public sealed class ConsultaConversacionesEfCore(ChatDbContext db) : IConsultaConversaciones
{
    public Task<bool> PuedeAccederAsync(
        Guid conversacionId,
        Guid usuarioId,
        CancellationToken ct) =>
        db.Conversaciones.AsNoTracking().AnyAsync(
            c => c.Id == conversacionId
                && (c.CompradorId == usuarioId || c.VendedorId == usuarioId),
            ct);

    public async Task<PaginaCursor<ConversacionResumenDto, FronteraConversaciones>> ListarAsync(
        Guid usuarioId,
        FronteraConversaciones? frontera,
        int limite,
        CancellationToken ct)
    {
        var consulta = db.Conversaciones.AsNoTracking()
            .Where(c => c.CompradorId == usuarioId || c.VendedorId == usuarioId);

        if (frontera is { } f)
        {
            consulta = consulta.Where(c =>
                c.UltimaActividadEn < f.UltimaActividadEn ||
                (c.UltimaActividadEn == f.UltimaActividadEn && c.Id.CompareTo(f.ConversacionId) > 0));
        }

        var candidatos = await consulta
            .OrderByDescending(c => c.UltimaActividadEn)
            .ThenBy(c => c.Id)
            .Select(c => new ConversacionResumenDto(
                c.Id,
                c.AvisoId,
                c.CompradorId == usuarioId ? c.VendedorId : c.CompradorId,
                c.CompradorId == usuarioId ? "Comprador" : "Vendedor",
                c.CreadaEn,
                c.UltimaActividadEn,
                c.UltimaSecuencia,
                db.Mensajes.Count(m =>
                    m.ConversacionId == c.Id &&
                    m.RemitenteId != usuarioId &&
                    m.Secuencia > (c.CompradorId == usuarioId
                        ? c.UltimaSecuenciaLeidaComprador
                        : c.UltimaSecuenciaLeidaVendedor))))
            .Take(limite + 1)
            .ToListAsync(ct);

        var hayMas = candidatos.Count > limite;
        var items = candidatos.Take(limite).ToList();
        FronteraConversaciones? siguiente = hayMas && items.Count > 0
            ? new FronteraConversaciones(items[^1].UltimaActividadEn, items[^1].Id)
            : null;
        return new PaginaCursor<ConversacionResumenDto, FronteraConversaciones>(items, siguiente);
    }
}
