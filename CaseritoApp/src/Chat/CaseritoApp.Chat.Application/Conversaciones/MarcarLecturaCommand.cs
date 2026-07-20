using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Domain.Conversaciones;
using FluentValidation;

namespace CaseritoApp.Chat.Application.Conversaciones;

public sealed record MarcarLecturaCommand(
    Guid ConversacionId,
    Guid UsuarioId,
    long HastaSecuencia) : ICommand;

public sealed class MarcarLecturaCommandHandler(
    IRepositorioConversaciones repositorio,
    TimeProvider reloj) : ICommandHandler<MarcarLecturaCommand>
{
    public async Task<Result> Handle(
        MarcarLecturaCommand request,
        CancellationToken cancellationToken)
    {
        var conversacion = await repositorio.ObtenerAsync(request.ConversacionId, cancellationToken);
        if (conversacion is null || !conversacion.EsParticipante(request.UsuarioId))
        {
            return Result.Fallo(new Error(
                ErroresConversacion.NoEncontrada,
                "La conversación no está disponible."));
        }

        return conversacion.MarcarLectura(request.UsuarioId, request.HastaSecuencia, reloj.GetUtcNow());
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
