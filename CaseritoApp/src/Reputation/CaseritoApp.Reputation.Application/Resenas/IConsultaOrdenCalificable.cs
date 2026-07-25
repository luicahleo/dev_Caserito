using CaseritoApp.Reputation.Domain.Resenas;

namespace CaseritoApp.Reputation.Application.Resenas;

public sealed record OrdenCalificable(
    Guid OrderId,
    Guid AutorId,
    Guid DestinatarioId,
    RolAutorResena RolAutor);

public interface IConsultaOrdenCalificable
{
    public Task<OrdenCalificable?> ObtenerAsync(
        Guid orderId,
        Guid actorId,
        CancellationToken ct);
}
