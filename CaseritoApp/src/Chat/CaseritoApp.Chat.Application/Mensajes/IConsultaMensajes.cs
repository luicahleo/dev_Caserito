using CaseritoApp.Chat.Application.Paginacion;

namespace CaseritoApp.Chat.Application.Mensajes;

public interface IConsultaMensajes
{
    public Task<PaginaCursor<MensajeDto, long>?> ListarAsync(
        Guid conversacionId,
        Guid usuarioId,
        long? antesDe,
        int limite,
        CancellationToken ct);
}
