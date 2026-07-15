using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Autorizacion;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Quita un rol a un usuario. <paramref name="AdminId"/> es el administrador que ejecuta la acción (auditoría).</summary>
public sealed record QuitarRolCommand(Guid AdminId, Guid TargetUserId, string Rol) : ICommand;

/// <summary>
/// Handler de <see cref="QuitarRolCommand"/>: valida existencia, protege al último AdminPlataforma,
/// quita (idempotente) y audita sin PII.
/// </summary>
public sealed partial class QuitarRolCommandHandler(
    IRepositorioRolesUsuario repositorio,
    ILogger<QuitarRolCommandHandler> logger)
    : ICommandHandler<QuitarRolCommand>
{
    public async Task<Result> Handle(QuitarRolCommand request, CancellationToken cancellationToken)
    {
        if (!await repositorio.ExisteUsuarioAsync(request.TargetUserId, cancellationToken))
        {
            RegistrarCambioRol(logger, "quitar", request.AdminId, request.TargetUserId, request.Rol, CodigosErrorRoles.UsuarioNoEncontrado);
            return Result.Fallo(new Error(CodigosErrorRoles.UsuarioNoEncontrado, "El usuario no existe."));
        }

        // Guardrail: no dejar la plataforma sin ningún AdminPlataforma.
        if (request.Rol == RolesApp.AdminPlataforma
            && await repositorio.TieneRolAsync(request.TargetUserId, RolesApp.AdminPlataforma, cancellationToken)
            && await repositorio.ContarEnRolAsync(RolesApp.AdminPlataforma, cancellationToken) <= 1)
        {
            RegistrarCambioRol(logger, "quitar", request.AdminId, request.TargetUserId, request.Rol, CodigosErrorRoles.UltimoAdminPlataforma);
            return Result.Fallo(new Error(
                CodigosErrorRoles.UltimoAdminPlataforma,
                "No se puede quitar el rol al último administrador de plataforma."));
        }

        var resultado = await repositorio.QuitarRolAsync(request.TargetUserId, request.Rol, cancellationToken);

        RegistrarCambioRol(
            logger, "quitar", request.AdminId, request.TargetUserId, request.Rol,
            resultado.EsExito ? "ok" : resultado.Error.Code);

        return resultado;
    }

    // Auditoría sin PII: solo ids y rol; nunca email ni datos sensibles.
    [LoggerMessage(Level = LogLevel.Information, Message = "Cambio de rol {Accion}: admin={AdminId} target={TargetUserId} rol={Rol} resultado={Resultado}")]
    private static partial void RegistrarCambioRol(
        ILogger logger, string accion, Guid adminId, Guid targetUserId, string rol, string resultado);
}

/// <summary>
/// Guardrails estáticos: solo roles conocidos, <c>Sistema</c> no gestionable vía API, y un admin no
/// puede quitarse a sí mismo <c>AdminPlataforma</c>.
/// </summary>
public sealed class QuitarRolCommandValidator : AbstractValidator<QuitarRolCommand>
{
    public QuitarRolCommandValidator()
    {
        RuleFor(c => c.Rol)
            .Must(rol => RolesApp.Todos.Contains(rol))
            .WithMessage("El rol indicado no existe.")
            .Must(rol => rol != RolesApp.Sistema)
            .WithMessage("El rol Sistema no puede gestionarse vía API.");

        RuleFor(c => c)
            .Must(c => !(c.AdminId == c.TargetUserId && c.Rol == RolesApp.AdminPlataforma))
            .WithMessage("Un administrador no puede quitarse a sí mismo el rol AdminPlataforma.")
            .OverridePropertyName(nameof(QuitarRolCommand.Rol));
    }
}
