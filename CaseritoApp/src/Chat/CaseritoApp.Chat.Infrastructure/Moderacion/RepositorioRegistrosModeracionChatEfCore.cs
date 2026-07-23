using CaseritoApp.Chat.Application.Moderacion;
using CaseritoApp.Chat.Domain.Moderacion;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Moderacion;

public sealed class RepositorioRegistrosModeracionChatEfCore(ChatDbContext db)
    : IRepositorioRegistrosModeracionChat
{
    public void Agregar(RegistroModeracionChat registro) => db.RegistrosModeracion.Add(registro);
}

public sealed class AuditorModeracionChatEfCore(ChatDbContext db) : IAuditorModeracionChat
{
    public async Task<bool> RegistrarConsultaEvidenciaAsync(
        Guid reporteId,
        Guid conversacionId,
        Guid moderadorId,
        DateTimeOffset fecha,
        CancellationToken ct)
    {
        var creacion = RegistroModeracionChat.Crear(
            reporteId, conversacionId, moderadorId, AccionModeracionChat.ConsultarEvidencia, fecha);
        if (!creacion.EsExito)
        {
            return false;
        }

        db.RegistrosModeracion.Add(creacion.Valor);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
