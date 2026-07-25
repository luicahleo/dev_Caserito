using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Catalog.Domain.Avisos;
using FluentValidation;

namespace CaseritoApp.Catalog.Application.Avisos;

public sealed record MarcarAvisoVendidoCommand(
    Guid AvisoId,
    Guid OrdenId,
    Guid VendedorId) : ICommand;

public sealed class MarcarAvisoVendidoCommandHandler(
    IRepositorioAvisos repositorio,
    TimeProvider reloj) : ICommandHandler<MarcarAvisoVendidoCommand>
{
    public async Task<Result> Handle(
        MarcarAvisoVendidoCommand request,
        CancellationToken cancellationToken)
    {
        var aviso = await repositorio.ObtenerAsync(request.AvisoId, cancellationToken);
        if (aviso is null || aviso.VendedorId != request.VendedorId)
        {
            return Result.Fallo(new Error(
                ErroresAviso.NoEncontrado,
                "El aviso no está disponible."));
        }

        return aviso.MarcarVendido(
            request.OrdenId,
            request.VendedorId,
            reloj.GetUtcNow().UtcDateTime);
    }
}

public sealed class MarcarAvisoVendidoCommandValidator
    : AbstractValidator<MarcarAvisoVendidoCommand>
{
    public MarcarAvisoVendidoCommandValidator()
    {
        RuleFor(command => command.AvisoId).NotEmpty();
        RuleFor(command => command.OrdenId).NotEmpty();
        RuleFor(command => command.VendedorId).NotEmpty();
    }
}
