using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Domain.Conversaciones;
using FluentValidation;

namespace CaseritoApp.Chat.Application.Conversaciones;

public sealed record MarcarLecturaCommand(
    Guid ConversacionId,
    Guid UsuarioId,
    long HastaSecuencia) : ICommand<ActualizacionRecibosDto>;

public sealed class MarcarLecturaCommandHandler(
    IRepositorioConversaciones repositorio,
    TimeProvider reloj) : ICommandHandler<MarcarLecturaCommand, ActualizacionRecibosDto>
{
    public async Task<Result<ActualizacionRecibosDto>> Handle(
        MarcarLecturaCommand request,
        CancellationToken cancellationToken)
    {
        var conversacion = await repositorio.ObtenerAsync(request.ConversacionId, cancellationToken);
        if (conversacion is null || !conversacion.EsParticipante(request.UsuarioId))
        {
            return Result.Fallo<ActualizacionRecibosDto>(new Error(
                ErroresConversacion.NoEncontrada,
                "La conversación no está disponible."));
        }

        var resultado = conversacion.MarcarLectura(
            request.UsuarioId, request.HastaSecuencia, reloj.GetUtcNow());
        if (!resultado.EsExito)
        {
            return Result.Fallo<ActualizacionRecibosDto>(resultado.Error);
        }

        var esComprador = request.UsuarioId == conversacion.CompradorId;
        return Result.Exito(new ActualizacionRecibosDto(
            esComprador ? conversacion.VendedorId : conversacion.CompradorId,
            esComprador
                ? conversacion.UltimaSecuenciaEntregadaComprador
                : conversacion.UltimaSecuenciaEntregadaVendedor,
            esComprador
                ? conversacion.UltimaSecuenciaLeidaComprador
                : conversacion.UltimaSecuenciaLeidaVendedor));
    }
}

public sealed class MarcarLecturaCommandValidator : AbstractValidator<MarcarLecturaCommand>
{
    public MarcarLecturaCommandValidator()
    {
        RuleFor(c => c.ConversacionId)
            .NotEmpty().WithMessage("La conversación es obligatoria.");
        RuleFor(c => c.UsuarioId)
            .NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(c => c.HastaSecuencia)
            .GreaterThanOrEqualTo(0).WithMessage("La secuencia de lectura no es válida.");
    }
}
