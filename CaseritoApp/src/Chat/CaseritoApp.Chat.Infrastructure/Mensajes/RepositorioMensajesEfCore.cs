using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Domain.Conversaciones;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Mensajes;

public sealed class RepositorioMensajesEfCore(ChatDbContext db) : IRepositorioMensajes
{
    public Task<Mensaje?> ObtenerPorClaveAsync(
        Guid conversacionId,
        Guid remitenteId,
        Guid clave,
        CancellationToken ct) => db.Mensajes.FirstOrDefaultAsync(
            m => m.ConversacionId == conversacionId &&
                m.RemitenteId == remitenteId &&
                m.ClaveIdempotencia == clave,
            ct);

    public async Task<long> ReservarSecuenciaAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await using var comando = db.Database.GetDbConnection().CreateCommand();
        comando.CommandText = "SELECT NEXT VALUE FOR [chat].[SecuenciaMensajes]";
        var resultado = await comando.ExecuteScalarAsync(ct);
        return (long)resultado!;
    }

    public void Agregar(Mensaje mensaje) => db.Mensajes.Add(mensaje);
}
