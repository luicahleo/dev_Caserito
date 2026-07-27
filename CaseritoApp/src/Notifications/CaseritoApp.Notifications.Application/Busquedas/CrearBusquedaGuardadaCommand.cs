using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Notifications.Domain.Busquedas;
using FluentValidation;

namespace CaseritoApp.Notifications.Application.Busquedas;

public sealed record CrearBusquedaGuardadaCommand(
    Guid UsuarioId,
    string? PalabraClave,
    string? Categoria,
    string? Ciudad,
    decimal? PrecioMinimo,
    decimal? PrecioMaximo,
    string? EstadoProducto) : ICommand<Guid>;

public sealed class CrearBusquedaGuardadaCommandHandler(
    IBusquedaGuardadaRepository repositorio)
    : ICommandHandler<CrearBusquedaGuardadaCommand, Guid>
{
    private const int LimiteBusquedasPorUsuario = 20;

    public async Task<Result<Guid>> Handle(
        CrearBusquedaGuardadaCommand request,
        CancellationToken cancellationToken)
    {
        var resultado = BusquedaGuardada.Crear(
            request.UsuarioId,
            request.PalabraClave,
            request.Categoria,
            request.Ciudad,
            request.PrecioMinimo,
            request.PrecioMaximo,
            request.EstadoProducto,
            DateTimeOffset.UtcNow);

        if (!resultado.EsExito)
        {
            return Result.Fallo<Guid>(resultado.Error);
        }

        var agregado = await repositorio.AgregarConLimiteAsync(
            resultado.Valor,
            LimiteBusquedasPorUsuario,
            cancellationToken);

        return agregado.EsExito
            ? Result.Exito(resultado.Valor.Id)
            : Result.Fallo<Guid>(agregado.Error);
    }
}

public sealed class CrearBusquedaGuardadaCommandValidator : AbstractValidator<CrearBusquedaGuardadaCommand>
{
    public CrearBusquedaGuardadaCommandValidator()
    {
        RuleFor(c => c.UsuarioId).NotEmpty();
        RuleFor(c => c.PrecioMinimo)
            .GreaterThanOrEqualTo(0).When(c => c.PrecioMinimo.HasValue)
            .WithMessage("El precio mínimo no puede ser negativo.");
        RuleFor(c => c.PrecioMaximo)
            .GreaterThanOrEqualTo(0).When(c => c.PrecioMaximo.HasValue)
            .WithMessage("El precio máximo no puede ser negativo.");
        RuleFor(c => c)
            .Must(c => !(c.PrecioMinimo.HasValue && c.PrecioMaximo.HasValue) || c.PrecioMinimo <= c.PrecioMaximo)
            .WithMessage("El precio mínimo no puede ser mayor al máximo.");
    }
}
