using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Auth;
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>
/// Implementación de <see cref="IRepositorioConfirmacionEmail"/> apoyada en
/// <see cref="UserManager{TUser}"/> de ASP.NET Core Identity.
/// </summary>
public sealed class RepositorioConfirmacionEmail(UserManager<ApplicationUser> userManager)
    : IRepositorioConfirmacionEmail
{
    public async Task<Result> ConfirmarEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            return Result.Fallo(new Error("Auth.UsuarioNoEncontrado", "El usuario no existe."));
        }

        if (usuario.EmailConfirmed)
        {
            return Result.Exito();
        }

        usuario.EmailConfirmed = true;

        var resultado = await userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            var mensaje = string.Join("; ", resultado.Errors.Select(e => e.Description));
            return Result.Fallo(new Error("Auth.ErrorActualizandoUsuario", mensaje));
        }

        return Result.Exito();
    }
}
