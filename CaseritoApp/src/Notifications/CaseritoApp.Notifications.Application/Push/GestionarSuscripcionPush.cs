using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Notifications.Domain.Push;
using FluentValidation;

namespace CaseritoApp.Notifications.Application.Push;

public sealed record RegistrarSuscripcionPushCommand(
    Guid UsuarioId,
    string DispositivoId,
    string Endpoint,
    string P256dh,
    string Auth) : ICommand;

public sealed class RegistrarSuscripcionPushHandler(
    IRepositorioSuscripcionesPush repositorio,
    TimeProvider reloj) : ICommandHandler<RegistrarSuscripcionPushCommand>
{
    public async Task<Result> Handle(
        RegistrarSuscripcionPushCommand request, CancellationToken cancellationToken)
    {
        var existente = await repositorio.ObtenerPorDispositivoAsync(
            request.UsuarioId, request.DispositivoId, cancellationToken);
        if (existente is not null)
        {
            return existente.Actualizar(
                request.UsuarioId, request.Endpoint, request.P256dh, request.Auth, reloj.GetUtcNow());
        }

        var resultado = SuscripcionPush.Crear(
            request.UsuarioId, request.DispositivoId, request.Endpoint,
            request.P256dh, request.Auth, reloj.GetUtcNow());
        if (resultado.EsExito)
        {
            repositorio.Agregar(resultado.Valor);
            return Result.Exito();
        }

        return Result.Fallo(resultado.Error);
    }
}

public sealed class RegistrarSuscripcionPushValidator
    : AbstractValidator<RegistrarSuscripcionPushCommand>
{
    public RegistrarSuscripcionPushValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.DispositivoId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.P256dh).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Auth).NotEmpty().MaximumLength(256);
    }
}

public sealed record RevocarSuscripcionPushCommand(
    Guid UsuarioId,
    string DispositivoId) : ICommand;

public sealed class RevocarSuscripcionPushHandler(
    IRepositorioSuscripcionesPush repositorio,
    TimeProvider reloj) : ICommandHandler<RevocarSuscripcionPushCommand>
{
    public async Task<Result> Handle(
        RevocarSuscripcionPushCommand request, CancellationToken cancellationToken)
    {
        var suscripcion = await repositorio.ObtenerPorDispositivoAsync(
            request.UsuarioId, request.DispositivoId, cancellationToken);
        return suscripcion is null
            ? Result.Exito()
            : suscripcion.Revocar(request.UsuarioId, reloj.GetUtcNow());
    }
}

public sealed class RevocarSuscripcionPushValidator
    : AbstractValidator<RevocarSuscripcionPushCommand>
{
    public RevocarSuscripcionPushValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.DispositivoId).NotEmpty().MaximumLength(128);
    }
}
