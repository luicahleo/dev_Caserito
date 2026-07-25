using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;
using CaseritoApp.Reputation.Application.Resenas;
using CaseritoApp.Reputation.Domain.Resenas;

namespace CaseritoApp.Host.Reputation;

public sealed class ConsultaOrdenCalificableAdapter(
    IConsultaOrdenParaReputacion consulta) : IConsultaOrdenCalificable
{
    public async Task<OrdenCalificable?> ObtenerAsync(
        Guid orderId,
        Guid actorId,
        CancellationToken ct)
    {
        var orden = await consulta.ObtenerAsync(orderId, actorId, ct);
        if (orden is null
            || !string.Equals(
                orden.Estado,
                nameof(EstadoOrden.Completed),
                StringComparison.Ordinal))
        {
            return null;
        }

        var esComprador = orden.CompradorId == actorId;
        return new OrdenCalificable(
            orden.OrderId,
            actorId,
            esComprador ? orden.VendedorId : orden.CompradorId,
            esComprador ? RolAutorResena.Comprador : RolAutorResena.Vendedor);
    }
}
