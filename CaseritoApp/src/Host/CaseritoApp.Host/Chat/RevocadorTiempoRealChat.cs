using CaseritoApp.Chat.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Host.Chat;

public sealed class RevocadorTiempoRealChat(
    ChatDbContext db,
    RegistroConexionesChat registro,
    IHubContext<ChatHub> hub) : IRevocadorTiempoRealChat
{
    public async Task RevocarAsync(Guid conversacionId, CancellationToken cancellationToken)
    {
        var participantes = await db.Conversaciones
            .AsNoTracking()
            .Where(c => c.Id == conversacionId)
            .Select(c => new[] { c.CompradorId, c.VendedorId })
            .SingleOrDefaultAsync(cancellationToken);
        if (participantes is null)
        {
            return;
        }

        await registro.EjecutarExclusivoAsync(
            conversacionId,
            async () =>
            {
                var usuarios = participantes.ToHashSet();
                var conexiones = registro.ObtenerConexiones(usuarios, conversacionId);
                foreach (var connectionId in conexiones)
                {
                    await hub.Groups.RemoveFromGroupAsync(
                        connectionId,
                        GruposChat.ParaConversacion(conversacionId),
                        cancellationToken);
                    registro.Quitar(conversacionId, connectionId);
                }
            },
            cancellationToken);
    }

    public async Task RevocarConversacionesCompartidasAsync(
        Guid conversacionId,
        CancellationToken cancellationToken)
    {
        var participantes = await db.Conversaciones
            .AsNoTracking()
            .Where(c => c.Id == conversacionId)
            .Select(c => new { c.CompradorId, c.VendedorId })
            .SingleOrDefaultAsync(cancellationToken);
        if (participantes is null)
        {
            return;
        }

        var conversaciones = await db.Conversaciones
            .AsNoTracking()
            .Where(c =>
                (c.CompradorId == participantes.CompradorId
                    && c.VendedorId == participantes.VendedorId)
                || (c.CompradorId == participantes.VendedorId
                    && c.VendedorId == participantes.CompradorId))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in conversaciones)
        {
            await RevocarAsync(id, cancellationToken);
        }
    }

    public async Task RevocarPorReporteAsync(Guid reporteId, CancellationToken cancellationToken)
    {
        var conversacionId = await db.Reportes
            .AsNoTracking()
            .Where(r => r.Id == reporteId)
            .Select(r => (Guid?)r.ConversacionId)
            .SingleOrDefaultAsync(cancellationToken);
        if (conversacionId is { } id)
        {
            await RevocarAsync(id, cancellationToken);
        }
    }
}
