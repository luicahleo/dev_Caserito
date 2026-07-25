using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Orders.Domain.Ordenes;
using FluentValidation;

namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record AceptarOrdenCommand(Guid OrdenId, Guid ActorId) : ICommand;

public sealed class AceptarOrdenCommandHandler(IRepositorioOrdenes repositorio)
    : ICommandHandler<AceptarOrdenCommand>
{
    public async Task<Result> Handle(
        AceptarOrdenCommand request,
        CancellationToken cancellationToken)
    {
        var orden = await repositorio.ObtenerAsync(request.OrdenId, cancellationToken);
        if (orden is null)
        {
            return Result.Fallo(new Error(
                ErroresOrden.NoEncontrada,
                "La orden no está disponible."));
        }

        return orden.Aceptar(request.ActorId, DateTimeOffset.UtcNow);
    }
}

public sealed class AceptarOrdenCommandValidator : AbstractValidator<AceptarOrdenCommand>
{
    public AceptarOrdenCommandValidator()
    {
        RuleFor(command => command.OrdenId).NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
    }
}
