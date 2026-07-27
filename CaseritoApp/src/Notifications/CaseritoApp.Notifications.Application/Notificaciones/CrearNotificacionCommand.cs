using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Notifications.Domain.Notificaciones;
using FluentValidation;

namespace CaseritoApp.Notifications.Application.Notificaciones;

public sealed record CrearNotificacionCommand(
    Guid DestinatarioId,
    TipoNotificacion Tipo,
    string Titulo,
    string Mensaje,
    Guid? EntidadRelacionadaId) : ICommand<Guid>;

public sealed class CrearNotificacionCommandHandler(
    INotificacionRepository repositorio)
    : ICommandHandler<CrearNotificacionCommand, Guid>
{
    public Task<Result<Guid>> Handle(
        CrearNotificacionCommand request,
        CancellationToken cancellationToken)
    {
        var resultado = Notificacion.Crear(
            request.DestinatarioId,
            request.Tipo,
            request.Titulo,
            request.Mensaje,
            request.EntidadRelacionadaId,
            DateTimeOffset.UtcNow);

        if (!resultado.EsExito)
        {
            return Task.FromResult(Result.Fallo<Guid>(resultado.Error));
        }

        repositorio.Agregar(resultado.Valor);
        return Task.FromResult(Result.Exito(resultado.Valor.Id));
    }
}

public sealed class CrearNotificacionCommandValidator : AbstractValidator<CrearNotificacionCommand>
{
    public CrearNotificacionCommandValidator()
    {
        RuleFor(c => c.DestinatarioId).NotEmpty();
        RuleFor(c => c.Titulo).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Mensaje).NotEmpty().MaximumLength(500);
    }
}
