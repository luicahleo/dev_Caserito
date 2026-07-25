using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Orders.Domain.Ordenes;
using FluentValidation;

namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record CancelarOrdenCommand(Guid OrdenId, Guid ActorId) : ICommand;

public sealed class CancelarOrdenCommandHandler(IRepositorioOrdenes repositorio)
    : ICommandHandler<CancelarOrdenCommand>
{
    public async Task<Result> Handle(
        CancelarOrdenCommand request,
        CancellationToken cancellationToken)
    {
        var orden = await repositorio.ObtenerAsync(request.OrdenId, cancellationToken);
        if (orden is null)
        {
            return Result.Fallo(new Error(
                ErroresOrden.NoEncontrada,
                "La orden no está disponible."));
        }

        return orden.Cancelar(request.ActorId, DateTimeOffset.UtcNow);
    }
}

public sealed class CancelarOrdenCommandValidator : AbstractValidator<CancelarOrdenCommand>
{
    public CancelarOrdenCommandValidator()
    {
        RuleFor(command => command.OrdenId).NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
    }
}
