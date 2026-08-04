using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Auth;
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Adaptador de restablecimiento respaldado por ASP.NET Core Identity.</summary>
public sealed class RepositorioRestablecimientoPassword(UserManager<ApplicationUser> userManager)
    : IRepositorioRestablecimientoPassword
{
    private static readonly Error _enlaceInvalido = new(
        "Auth.EnlaceRestablecimientoInvalido",
        "El enlace no es válido o ha caducado.");

    public async Task<SolicitudRestablecimiento?> CrearSolicitudAsync(
        string email,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = await userManager.FindByEmailAsync(email);
        if (usuario is null || string.IsNullOrWhiteSpace(usuario.Email))
        {
            return null;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
        return new SolicitudRestablecimiento(usuario.Id, usuario.Email, usuario.Nombres, token);
    }

    public async Task<Result<Guid>> RestablecerAsync(
        Guid usuarioId,
        string token,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
        {
            return Result.Fallo<Guid>(_enlaceInvalido);
        }

        var resultado = await userManager.ResetPasswordAsync(usuario, token, password);
        return resultado.Succeeded
            ? Result.Exito(usuario.Id)
            : Result.Fallo<Guid>(_enlaceInvalido);
    }
}
