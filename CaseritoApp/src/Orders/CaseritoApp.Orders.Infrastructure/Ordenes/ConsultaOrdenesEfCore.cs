using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Orders.Infrastructure.Ordenes;

public sealed class ConsultaOrdenesEfCore(OrdersDbContext db) : IConsultaOrdenes
{
    public async Task<ResultadoPaginadoOrdenes> ListarAsync(
        Guid actorId,
        string rol,
        EstadoOrden? estado,
        int pagina,
        int tamano,
        CancellationToken ct)
    {
        var consulta = db.Orders.AsNoTracking();
        consulta = rol == "comprador"
            ? consulta.Where(orden => orden.CompradorId == actorId)
            : consulta.Where(orden => orden.VendedorId == actorId);

        if (estado.HasValue)
        {
            consulta = consulta.Where(orden => orden.Estado == estado.Value);
        }

        var total = await consulta.CountAsync(ct);
        var items = await consulta
            .OrderByDescending(orden => orden.ActualizadaEn)
            .ThenByDescending(orden => orden.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(orden => new OrdenResumenDto(
                orden.Id,
                orden.AvisoId,
                orden.Estado.ToString(),
                orden.MontoAcordado,
                orden.Moneda,
                rol,
                orden.ActualizadaEn))
            .ToListAsync(ct);

        return new ResultadoPaginadoOrdenes(items, pagina, tamano, total);
    }

    public Task<OrdenDetalleDto?> ObtenerAsync(
        Guid ordenId,
        Guid actorId,
        CancellationToken ct) =>
        db.Orders
            .AsNoTracking()
            .Where(orden => orden.Id == ordenId
                && (orden.CompradorId == actorId || orden.VendedorId == actorId))
            .Select(orden => new OrdenDetalleDto(
                orden.Id,
                orden.AvisoId,
                orden.Estado.ToString(),
                orden.MontoAcordado,
                orden.Moneda,
                orden.CompradorId == actorId ? "comprador" : "vendedor",
                orden.CreadaEn,
                orden.ActualizadaEn))
            .FirstOrDefaultAsync(ct);
}
