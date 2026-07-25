using CaseritoApp.Orders.Domain.Ordenes;

namespace CaseritoApp.Orders.Application.Ordenes;

public interface IConsultaOrdenes
{
    public Task<ResultadoPaginadoOrdenes> ListarAsync(
        Guid actorId,
        string rol,
        EstadoOrden? estado,
        int pagina,
        int tamano,
        CancellationToken ct);

    public Task<OrdenDetalleDto?> ObtenerAsync(
        Guid ordenId,
        Guid actorId,
        CancellationToken ct);
}
