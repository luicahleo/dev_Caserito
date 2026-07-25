using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Contracts.Orders;
using CaseritoApp.Orders.Domain.Ordenes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Orders.Infrastructure;

public sealed class UnitOfWorkOrders(
    OrdersDbContext db,
    IPublicadorEventosIntegracion publicador) : IUnitOfWork
{
    public async Task<int> GuardarCambiosAsync(CancellationToken ct)
    {
        var agregados = db.ChangeTracker
            .Entries<Orden>()
            .Select(entrada => entrada.Entity)
            .Where(orden => orden.EventosDeDominio.Count > 0)
            .ToArray();
        var cambios = agregados
            .SelectMany(orden => orden.EventosDeDominio)
            .OfType<EstadoOrdenCambiado>()
            .ToArray();

        int afectados;
        try
        {
            afectados = await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            throw new ConflictoConcurrenciaException(
                "Conflicto de concurrencia al persistir Orders.", ex);
        }
        catch (DbUpdateException ex) when (EsConflictoUnicidad(ex))
        {
            db.ChangeTracker.Clear();
            throw new ConflictoUnicidadOrdersException(
                "Conflicto de unicidad al persistir Orders.");
        }

        foreach (var cambio in cambios)
        {
            await publicador.PublicarAsync(new OrderStatusChanged(
                Guid.NewGuid(),
                cambio.OcurridoEn,
                cambio.OrdenId,
                cambio.EstadoAnterior.ToString(),
                cambio.EstadoNuevo.ToString()), ct);
        }

        foreach (var agregado in agregados)
        {
            agregado.LimpiarEventos();
        }

        return afectados;
    }

    private static bool EsConflictoUnicidad(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
