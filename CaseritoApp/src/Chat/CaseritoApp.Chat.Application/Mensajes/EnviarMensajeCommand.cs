using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Seguridad;
using CaseritoApp.Chat.Domain.Conversaciones;
using FluentValidation;

namespace CaseritoApp.Chat.Application.Mensajes;

public sealed record EnviarMensajeCommand(
    Guid ConversacionId,
    Guid RemitenteId,
    Guid ClaveIdempotencia,
    string Texto) : ICommand<EnviarMensajeResultadoDto>;

public sealed class EnviarMensajeCommandHandler(
    IRepositorioConversaciones conversaciones,
    IRepositorioMensajes mensajes,
    TimeProvider reloj,
    IRepositorioBloqueosUsuario bloqueos)
    : ICommandHandler<EnviarMensajeCommand, EnviarMensajeResultadoDto>
{
    public async Task<Result<EnviarMensajeResultadoDto>> Handle(
        EnviarMensajeCommand request,
        CancellationToken cancellationToken)
    {
        var conversacion = await conversaciones.ObtenerAsync(
            request.ConversacionId,
            cancellationToken);
        if (conversacion is null || !conversacion.EsParticipante(request.RemitenteId))
        {
            return NoEncontrada();
        }

        var contraparteId = request.RemitenteId == conversacion.CompradorId
            ? conversacion.VendedorId
            : conversacion.CompradorId;
        if (await bloqueos.ExisteEntreAsync(
            request.RemitenteId,
            contraparteId,
            cancellationToken))
        {
            return Result.Fallo<EnviarMensajeResultadoDto>(new Error(
                ErroresConversacion.NoDisponibleParaEnvio,
                "La conversación no está disponible para enviar mensajes."));
        }

        var existente = await mensajes.ObtenerPorClaveAsync(
            request.ConversacionId,
            request.RemitenteId,
            request.ClaveIdempotencia,
            cancellationToken);
        if (existente is not null)
        {
            if (!string.Equals(existente.Texto, request.Texto?.Trim(), StringComparison.Ordinal))
            {
                return Result.Fallo<EnviarMensajeResultadoDto>(new Error(
                    ErroresConversacion.ClaveIdempotenciaReutilizada,
                    "La solicitud de mensaje entra en conflicto con una anterior."));
            }

            return Result.Exito(new EnviarMensajeResultadoDto(
                MensajeDto.Desde(existente), contraparteId, false));
        }

        var secuencia = await mensajes.ReservarSecuenciaAsync(cancellationToken);
        var creacion = conversacion.CrearMensaje(
            request.RemitenteId,
            request.ClaveIdempotencia,
            secuencia,
            request.Texto,
            reloj.GetUtcNow());
        if (!creacion.EsExito)
        {
            return Result.Fallo<EnviarMensajeResultadoDto>(creacion.Error);
        }

        mensajes.Agregar(creacion.Valor);
        return Result.Exito(new EnviarMensajeResultadoDto(
            MensajeDto.Desde(creacion.Valor), contraparteId, true));
    }

    private static Result<EnviarMensajeResultadoDto> NoEncontrada() =>
        Result.Fallo<EnviarMensajeResultadoDto>(new Error(
            ErroresConversacion.NoEncontrada,
            "La conversación no está disponible."));
}

public sealed class EnviarMensajeCommandValidator : AbstractValidator<EnviarMensajeCommand>
{
    public EnviarMensajeCommandValidator()
    {
        RuleFor(c => c.ConversacionId)
            .NotEmpty().WithMessage("La conversación es obligatoria.");
        RuleFor(c => c.RemitenteId)
            .NotEmpty().WithMessage("El remitente es obligatorio.");
        RuleFor(c => c.ClaveIdempotencia)
            .NotEmpty().WithMessage("La clave de la solicitud es obligatoria.");
        RuleFor(c => c.Texto)
            .Must(EsTextoValido).WithMessage("El mensaje debe tener entre 1 y 2000 caracteres.");
    }

    private static bool EsTextoValido(string? texto)
    {
        var normalizado = texto?.Trim();
        return !string.IsNullOrWhiteSpace(normalizado) && normalizado.Length <= 2000;
    }
}
