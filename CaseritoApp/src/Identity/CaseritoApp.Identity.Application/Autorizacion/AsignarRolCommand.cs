using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Autorizacion;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Asigna un rol a un usuario. <paramref name="AdminId"/> es el administrador que ejecuta la acción (auditoría).</summary>
public sealed record AsignarRolCommand(Guid AdminId, Guid TargetUserId, string Rol) : ICommand;

/// <summary>Handler de <see cref="AsignarRolCommand"/>: valida existencia, asigna (idempotente) y audita sin PII.</summary>
public sealed partial class AsignarRolCommandHandler(
    IRepositorioRolesUsuario repositorio,
    ILogger<AsignarRolCommandHandler> logger)
    : ICommandHandler<AsignarRolCommand>
{
    public async Task<Result> Handle(AsignarRolCommand request, CancellationToken cancellationToken)
    {
        if (!await repositorio.ExisteUsuarioAsync(request.TargetUserId, cancellationToken))
        {
            RegistrarCambioRol(logger, "asignar", request.AdminId, request.TargetUserId, request.Rol, CodigosErrorRoles.UsuarioNoEncontrado);
            return Result.Fallo(new Error(CodigosErrorRoles.UsuarioNoEncontrado, "El usuario no existe."));
        }

        var resultado = await repositorio.AgregarRolAsync(request.TargetUserId, request.Rol, cancellationToken);

        RegistrarCambioRol(
            logger, "asignar", request.AdminId, request.TargetUserId, request.Rol,
            resultado.EsExito ? "ok" : resultado.Error.Code);

        return resultado;
    }

    // Auditoría sin PII: solo ids y rol; nunca email ni datos sensibles.
    [LoggerMessage(Level = LogLevel.Information, Message = "Cambio de rol {Accion}: admin={AdminId} target={TargetUserId} rol={Rol} resultado={Resultado}")]
    private static partial void RegistrarCambioRol(
        ILogger logger, string accion, Guid adminId, Guid targetUserId, string rol, string resultado);
}

/// <summary>Guardrails estáticos: solo roles conocidos y <c>Sistema</c> no asignable vía API.</summary>
public sealed class AsignarRolCommandValidator : AbstractValidator<AsignarRolCommand>
{
    public AsignarRolCommandValidator()
    {
        RuleFor(c => c.Rol)
            .Must(rol => RolesApp.Todos.Contains(rol))
            .WithMessage("El rol indicado no existe.")
            .Must(rol => rol != RolesApp.Sistema)
            .WithMessage("El rol Sistema no puede asignarse vía API.");
    }
}
