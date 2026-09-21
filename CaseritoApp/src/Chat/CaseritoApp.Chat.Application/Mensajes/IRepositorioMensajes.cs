using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Application.Mensajes;

public interface IRepositorioMensajes
{
    public Task<Mensaje?> ObtenerAsync(Guid id, CancellationToken ct);

    public Task<Mensaje?> ObtenerPorClaveAsync(
        Guid conversacionId,
        Guid remitenteId,
        Guid clave,
        CancellationToken ct);

    public Task<long> ReservarSecuenciaAsync(CancellationToken ct);

    public void Agregar(Mensaje mensaje);

    /// <summary>Último mensaje de una conversación por secuencia, o null si no tiene ninguno.</summary>
    public Task<Mensaje?> ObtenerUltimoDeConversacionAsync(
        Guid conversacionId,
        CancellationToken ct);
}
