using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Moderacion;
using FluentValidation;

namespace CaseritoApp.Chat.Application.Moderacion;

public sealed record ReportarChatCommand(
    Guid ConversacionId,
    Guid ReportanteId,
    TipoObjetivoReporteChat TipoObjetivo,
    Guid? MensajeId,
    CategoriaReporteChat Categoria,
    string? Detalle) : ICommand<Guid>;

public sealed class ReportarChatCommandHandler(
    IRepositorioConversaciones conversaciones,
    IRepositorioMensajes mensajes,
    IRepositorioReportesChat reportes,
    TimeProvider reloj) : ICommandHandler<ReportarChatCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        ReportarChatCommand request,
        CancellationToken cancellationToken)
    {
        var conversacion = await conversaciones.ObtenerAsync(request.ConversacionId, cancellationToken);
        if (conversacion is null || !conversacion.EsParticipante(request.ReportanteId))
        {
            return Result.Fallo<Guid>(new Error(
                ErroresConversacion.NoEncontrada,
                "La conversación no está disponible."));
        }

        if (request.TipoObjetivo == TipoObjetivoReporteChat.Mensaje)
        {
            var mensaje = request.MensajeId.HasValue
                ? await mensajes.ObtenerAsync(request.MensajeId.Value, cancellationToken)
                : null;
            if (mensaje is null || mensaje.ConversacionId != conversacion.Id)
            {
                return Result.Fallo<Guid>(new Error(
                    ErroresConversacion.NoEncontrada,
                    "La conversación no está disponible."));
            }
        }

        if (await reportes.ExisteAbiertoAsync(
            conversacion.Id,
            request.ReportanteId,
            request.TipoObjetivo,
            request.MensajeId,
            cancellationToken))
        {
            return Result.Fallo<Guid>(new Error(
                ErroresModeracionChat.TransicionInvalida,
                "El reporte no está disponible para esa acción."));
        }

        var creacion = ReporteChat.Crear(
            conversacion.Id,
            request.ReportanteId,
            request.TipoObjetivo,
            request.MensajeId,
            request.Categoria,
            request.Detalle,
            reloj.GetUtcNow());
        if (!creacion.EsExito)
        {
            return Result.Fallo<Guid>(creacion.Error);
        }

        reportes.Agregar(creacion.Valor);
        return Result.Exito(creacion.Valor.Id);
    }
}

public sealed class ReportarChatCommandValidator : AbstractValidator<ReportarChatCommand>
{
    public ReportarChatCommandValidator()
    {
        RuleFor(x => x.ConversacionId).NotEmpty();
        RuleFor(x => x.ReportanteId).NotEmpty();
        RuleFor(x => x.TipoObjetivo).IsInEnum();
        RuleFor(x => x.Categoria).IsInEnum();
        RuleFor(x => x.Detalle).MaximumLength(1000);
        RuleFor(x => x.MensajeId)
            .NotEmpty()
            .When(x => x.TipoObjetivo == TipoObjetivoReporteChat.Mensaje);
        RuleFor(x => x.MensajeId)
            .Null()
            .When(x => x.TipoObjetivo != TipoObjetivoReporteChat.Mensaje);
    }
}
