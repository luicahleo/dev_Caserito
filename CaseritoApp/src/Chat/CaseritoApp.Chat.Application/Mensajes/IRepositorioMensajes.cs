using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Application.Mensajes;

public interface IRepositorioMensajes
{
    public Task<Mensaje?> ObtenerPorClaveAsync(
        Guid conversacionId,
        Guid remitenteId,
        Guid clave,
        CancellationToken ct);

    public Task<long> ReservarSecuenciaAsync(CancellationToken ct);

    public void Agregar(Mensaje mensaje);
}
