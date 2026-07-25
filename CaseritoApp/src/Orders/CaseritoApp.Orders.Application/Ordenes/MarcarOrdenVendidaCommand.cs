using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Orders.Domain.Ordenes;
using FluentValidation;

namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record ResultadoOrdenMarcadaVendida(Guid AvisoId);

public sealed record MarcarOrdenVendidaCommand(Guid OrdenId, Guid ActorId)
    : ICommand<ResultadoOrdenMarcadaVendida>;

public sealed class MarcarOrdenVendidaCommandHandler(IRepositorioOrdenes repositorio)
    : ICommandHandler<MarcarOrdenVendidaCommand, ResultadoOrdenMarcadaVendida>
{
    public async Task<Result<ResultadoOrdenMarcadaVendida>> Handle(
        MarcarOrdenVendidaCommand request,
        CancellationToken cancellationToken)
    {
        var orden = await repositorio.ObtenerAsync(request.OrdenId, cancellationToken);
        if (orden is null)
        {
            return NoEncontrada();
        }

        var eraReintento = orden.Estado is EstadoOrden.MarkedAsSold or EstadoOrden.Completed;
        var resultado = orden.MarcarComoVendida(request.ActorId, DateTimeOffset.UtcNow);
        if (!resultado.EsExito)
        {
            return Result.Fallo<ResultadoOrdenMarcadaVendida>(resultado.Error);
        }

        if (!eraReintento)
        {
            var competidoras = await repositorio.ObtenerAbiertasPorAvisoAsync(
                orden.AvisoId,
                orden.Id,
                cancellationToken);
            foreach (var competidora in competidoras)
            {
                var cancelacion = competidora.Cancelar(orden.VendedorId, DateTimeOffset.UtcNow);
                if (!cancelacion.EsExito)
                {
                    return Result.Fallo<ResultadoOrdenMarcadaVendida>(cancelacion.Error);
                }
            }
        }

        return Result.Exito(new ResultadoOrdenMarcadaVendida(orden.AvisoId));
    }

    private static Result<ResultadoOrdenMarcadaVendida> NoEncontrada() =>
        Result.Fallo<ResultadoOrdenMarcadaVendida>(new Error(
            ErroresOrden.NoEncontrada,
            "La orden no está disponible."));
}

public sealed class MarcarOrdenVendidaCommandValidator
    : AbstractValidator<MarcarOrdenVendidaCommand>
{
    public MarcarOrdenVendidaCommandValidator()
    {
        RuleFor(command => command.OrdenId).NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
    }
}
