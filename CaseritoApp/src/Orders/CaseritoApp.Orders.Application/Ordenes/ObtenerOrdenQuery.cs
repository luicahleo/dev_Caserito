using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Orders.Domain.Ordenes;
using FluentValidation;

namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record ObtenerOrdenQuery(Guid OrdenId, Guid ActorId)
    : IQuery<Result<OrdenDetalleDto>>;

public sealed class ObtenerOrdenQueryHandler(IConsultaOrdenes consulta)
    : IQueryHandler<ObtenerOrdenQuery, Result<OrdenDetalleDto>>
{
    public async Task<Result<OrdenDetalleDto>> Handle(
        ObtenerOrdenQuery request,
        CancellationToken cancellationToken)
    {
        var orden = await consulta.ObtenerAsync(
            request.OrdenId,
            request.ActorId,
            cancellationToken);

        return orden is null
            ? Result.Fallo<OrdenDetalleDto>(new Error(
                ErroresOrden.NoEncontrada,
                "La orden no está disponible."))
            : Result.Exito(orden);
    }
}

public sealed class ObtenerOrdenQueryValidator : AbstractValidator<ObtenerOrdenQuery>
{
    public ObtenerOrdenQueryValidator()
    {
        RuleFor(query => query.OrdenId).NotEmpty();
        RuleFor(query => query.ActorId).NotEmpty();
    }
}
