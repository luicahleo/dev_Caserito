using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Reputation.Domain.Resenas;

namespace CaseritoApp.Reputation.Application.Resenas;

public sealed record ObtenerEstadoResenaOrdenQuery(Guid OrderId, Guid ActorId)
    : IQuery<Result<EstadoResenaOrdenDto>>;

public sealed class ObtenerEstadoResenaOrdenQueryHandler(
    IConsultaOrdenCalificable consultaOrden,
    IConsultaResenas consultaResenas)
    : IQueryHandler<ObtenerEstadoResenaOrdenQuery, Result<EstadoResenaOrdenDto>>
{
    public async Task<Result<EstadoResenaOrdenDto>> Handle(
        ObtenerEstadoResenaOrdenQuery request,
        CancellationToken cancellationToken)
    {
        var orden = await consultaOrden.ObtenerAsync(
            request.OrderId,
            request.ActorId,
            cancellationToken);
        if (orden is null)
        {
            return Result.Fallo<EstadoResenaOrdenDto>(new Error(
                ErroresResena.OrdenNoDisponible,
                "La orden no está disponible para calificar."));
        }

        var estado = await consultaResenas.ObtenerEstadoAsync(
            request.OrderId,
            orden.AutorId,
            orden.DestinatarioId,
            cancellationToken);
        return Result.Exito(new EstadoResenaOrdenDto(
            !estado.AutorYaCalifico,
            estado.AutorYaCalifico,
            estado.ContraparteYaCalifico,
            estado.AutorYaCalifico && estado.ContraparteYaCalifico,
            estado.EnviadaEn,
            orden.DestinatarioId));
    }
}
