using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Correo;

namespace CaseritoApp.Identity.Application.Auth;

/// <summary>Comando para confirmar el email de un usuario a partir de un token.</summary>
public sealed record ConfirmarEmailCommand(Guid UsuarioId, string Token) : ICommand;

/// <summary>Handler de <see cref="ConfirmarEmailCommand"/>.</summary>
public sealed class ConfirmarEmailCommandHandler(
    IRepositorioConfirmacionEmail repositorio,
    IGeneradorTokenEmail generadorToken)
    : ICommandHandler<ConfirmarEmailCommand>
{
    public async Task<Result> Handle(ConfirmarEmailCommand request, CancellationToken cancellationToken)
    {
        if (!generadorToken.Validar(request.Token, out var usuarioIdToken) || usuarioIdToken != request.UsuarioId)
        {
            return Result.Fallo(new Error(
                "Auth.TokenConfirmacionInvalido",
                "El enlace de confirmación no es válido o ha expirado."));
        }

        return await repositorio.ConfirmarEmailAsync(request.UsuarioId, cancellationToken);
    }
}
