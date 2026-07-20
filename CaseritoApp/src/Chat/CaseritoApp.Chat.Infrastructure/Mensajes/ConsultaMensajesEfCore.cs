using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Application.Paginacion;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Mensajes;

public sealed class ConsultaMensajesEfCore(ChatDbContext db) : IConsultaMensajes
{
    public async Task<PaginaCursor<MensajeDto, long>?> ListarAsync(
        Guid conversacionId,
        Guid usuarioId,
        long? antesDe,
        long? despuesDe,
        int limite,
        CancellationToken ct)
    {
        var participa = await db.Conversaciones.AsNoTracking().AnyAsync(
            c => c.Id == conversacionId &&
                (c.CompradorId == usuarioId || c.VendedorId == usuarioId),
            ct);
        if (!participa)
        {
            return null;
        }

        var consulta = db.Mensajes.AsNoTracking().Where(m => m.ConversacionId == conversacionId);
        if (antesDe is { } frontera)
        {
            consulta = consulta.Where(m => m.Secuencia < frontera);
        }

        if (despuesDe is { } fronteraPosterior)
        {
            var itemsPosteriores = await consulta
                .Where(m => m.Secuencia > fronteraPosterior)
                .OrderBy(m => m.Secuencia)
                .Take(limite)
                .Select(m => new MensajeDto(
                    m.Id,
                    m.ConversacionId,
                    m.RemitenteId,
                    m.Secuencia,
                    m.Texto,
                    m.EnviadoEn))
                .ToListAsync(ct);
            return new PaginaCursor<MensajeDto, long>(itemsPosteriores, null);
        }

        var candidatos = await consulta
            .OrderByDescending(m => m.Secuencia)
            .Take(limite + 1)
            .Select(m => new MensajeDto(
                m.Id,
                m.ConversacionId,
                m.RemitenteId,
                m.Secuencia,
                m.Texto,
                m.EnviadoEn))
            .ToListAsync(ct);

        var hayMas = candidatos.Count > limite;
        var items = candidatos.Take(limite).OrderBy(m => m.Secuencia).ToList();
        long? siguiente = hayMas && items.Count > 0 ? items[0].Secuencia : null;
        return new PaginaCursor<MensajeDto, long>(items, siguiente);
    }
}
