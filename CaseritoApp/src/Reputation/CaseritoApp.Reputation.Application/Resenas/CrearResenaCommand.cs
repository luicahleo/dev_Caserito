using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Reputation.Domain.Resenas;
using FluentValidation;

namespace CaseritoApp.Reputation.Application.Resenas;

public sealed record CrearResenaCommand(
    Guid OrderId,
    Guid ActorId,
    int Puntuacion,
    string Comentario) : ICommand<ResenaCreadaDto>;

public sealed class CrearResenaCommandHandler(
    IRepositorioResenas repositorio,
    IConsultaOrdenCalificable consultaOrden)
    : ICommandHandler<CrearResenaCommand, ResenaCreadaDto>
{
    public async Task<Result<ResenaCreadaDto>> Handle(
        CrearResenaCommand request,
        CancellationToken cancellationToken)
    {
        var orden = await consultaOrden.ObtenerAsync(
            request.OrderId,
            request.ActorId,
            cancellationToken);
        if (orden is null)
        {
            return OrdenNoDisponible();
        }

        if (await repositorio.ExisteAsync(
                request.OrderId,
                request.ActorId,
                cancellationToken))
        {
            return Result.Fallo<ResenaCreadaDto>(new Error(
                ErroresResena.Duplicada,
                "La reseña ya fue enviada."));
        }

        var resultado = Resena.Crear(
            request.OrderId,
            orden.AutorId,
            orden.DestinatarioId,
            orden.RolAutor,
            request.Puntuacion,
            request.Comentario,
            DateTimeOffset.UtcNow);
        if (!resultado.EsExito)
        {
            return Result.Fallo<ResenaCreadaDto>(resultado.Error);
        }

        repositorio.Agregar(resultado.Valor);
        return Result.Exito(new ResenaCreadaDto(
            resultado.Valor.Id,
            resultado.Valor.CreadaEn));
    }

    private static Result<ResenaCreadaDto> OrdenNoDisponible() =>
        Result.Fallo<ResenaCreadaDto>(new Error(
            ErroresResena.OrdenNoDisponible,
            "La orden no está disponible para calificar."));
}

public sealed class CrearResenaCommandValidator : AbstractValidator<CrearResenaCommand>
{
    public CrearResenaCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.ActorId).NotEmpty();
        RuleFor(command => command.Puntuacion).InclusiveBetween(1, 5);
        RuleFor(command => command.Comentario)
            .NotEmpty()
            .Must(comentario =>
                comentario.Trim().Length is >= 10 and <= 500)
            .WithMessage("El comentario debe tener entre 10 y 500 caracteres.");
    }
}
