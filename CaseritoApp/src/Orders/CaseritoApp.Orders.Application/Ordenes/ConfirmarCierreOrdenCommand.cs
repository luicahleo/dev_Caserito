using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Orders.Domain.Ordenes;
using FluentValidation;

namespace CaseritoApp.Orders.Application.Ordenes;

public sealed record ConfirmarCierreOrdenCommand(Guid OrdenId, Guid ActorId) : ICommand;

public sealed class ConfirmarCierreOrdenCommandHandler(IRepositorioOrdenes repositorio)
    : ICommandHandler<ConfirmarCierreOrdenCommand>
{
    public async Task<Result> Handle(
        ConfirmarCierreOrdenCommand request,
        CancellationToken cancellationToken)
    {
        var orden = await repositorio.ObtenerAsync(request.OrdenId, cancellationToken);
        if (orden is null)
        {
            return Result.Fallo(new Error(
                ErroresOrden.NoEncontrada,
                "La orden no está disponible."));
        }

        return orden.ConfirmarCierre(request.ActorId, DateTimeOffset.UtcNow);
    }
}

public sealed class ConfirmarCierreOrdenCommandValidator
    : AbstractValidator<ConfirmarCierreOrdenCommand>
{
    public ConfirmarCierreOrdenCommandValidator()
    {
        RuleFor(command => command.OrdenId).NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
    }
}
