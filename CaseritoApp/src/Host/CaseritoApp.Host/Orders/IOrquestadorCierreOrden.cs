using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Host.Orders;

public interface IOrquestadorCierreOrden
{
    public Task<Result> MarcarVendidaAsync(
        Guid ordenId,
        Guid actorId,
        CancellationToken ct);
}
