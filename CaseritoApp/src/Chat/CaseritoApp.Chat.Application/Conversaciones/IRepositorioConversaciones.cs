using CaseritoApp.Chat.Domain.Conversaciones;

namespace CaseritoApp.Chat.Application.Conversaciones;

public interface IRepositorioConversaciones
{
    public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
        Guid compradorId,
        Guid avisoId,
        CancellationToken ct);

    public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct);

    public void Agregar(Conversacion conversacion);
}
