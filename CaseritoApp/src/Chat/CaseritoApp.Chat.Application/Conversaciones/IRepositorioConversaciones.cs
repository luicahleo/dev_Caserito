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

    /// <summary>Conversaciones retenidas por verificación de un comprador concreto.</summary>
    public Task<IReadOnlyList<Conversacion>> ListarRetenidasDeCompradorAsync(
        Guid compradorId,
        CancellationToken ct);
}
