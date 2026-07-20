using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Conversaciones;

public sealed class RepositorioConversacionesEfCore(ChatDbContext db) : IRepositorioConversaciones
{
    public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
        Guid compradorId,
        Guid avisoId,
        CancellationToken ct) => db.Conversaciones.FirstOrDefaultAsync(
            c => c.CompradorId == compradorId && c.AvisoId == avisoId,
            ct);

    public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Conversaciones.FirstOrDefaultAsync(c => c.Id == id, ct);

    public void Agregar(Conversacion conversacion) => db.Conversaciones.Add(conversacion);
}
