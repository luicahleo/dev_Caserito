using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Domain.Ordenes;
using MediatR;

namespace CaseritoApp.Host.Orders;

public sealed class OrquestadorCierreOrden(ISender sender) : IOrquestadorCierreOrden
{
    public async Task<Result> MarcarVendidaAsync(
        Guid ordenId,
        Guid actorId,
        CancellationToken ct)
    {
        var detalle = await sender.Send(new ObtenerOrdenQuery(ordenId, actorId), ct);
        if (!detalle.EsExito
            || !string.Equals(detalle.Valor.Rol, "vendedor", StringComparison.Ordinal))
        {
            return NoEncontrada();
        }

        var preflight = await sender.Send(new ValidarAvisoParaVentaQuery(
            detalle.Valor.AvisoId,
            actorId), ct);
        if (!preflight.EsExito)
        {
            return preflight;
        }

        var orders = await sender.Send(new MarcarOrdenVendidaCommand(ordenId, actorId), ct);
        if (!orders.EsExito)
        {
            return Result.Fallo(orders.Error);
        }

        try
        {
            return await sender.Send(new MarcarAvisoVendidoCommand(
                orders.Valor.AvisoId,
                ordenId,
                actorId), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CatalogPendienteException(
                "No se pudo completar la operación coordinada.",
                ex);
        }
    }

    private static Result NoEncontrada() =>
        Result.Fallo(new Error(
            ErroresOrden.NoEncontrada,
            "La orden no está disponible."));
}

public sealed class CatalogPendienteException : Exception
{
    public CatalogPendienteException()
    {
    }

    public CatalogPendienteException(string message)
        : base(message)
    {
    }

    public CatalogPendienteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
