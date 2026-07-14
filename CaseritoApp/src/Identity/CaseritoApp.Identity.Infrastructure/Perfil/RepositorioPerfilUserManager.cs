using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Perfil;
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Identity.Infrastructure.Perfil;

/// <summary>
/// Implementación de <see cref="IRepositorioPerfil"/> apoyada en
/// <see cref="UserManager{TUser}"/> de ASP.NET Core Identity.
/// </summary>
public sealed class RepositorioPerfilUserManager(UserManager<ApplicationUser> userManager)
    : IRepositorioPerfil
{
    public async Task<PerfilDto?> ObtenerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());

        return usuario is null
            ? null
            : new PerfilDto(usuario.Id, usuario.Email ?? string.Empty, usuario.Nombre, usuario.Ciudad);
    }

    public async Task<Result> ActualizarAsync(
        Guid userId, string nombre, string ciudad, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            return Result.Fallo(new Error("Perfil.NoEncontrado", "El usuario no existe."));
        }

        usuario.Nombre = nombre;
        usuario.Ciudad = ciudad;

        var resultado = await userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            var mensaje = string.Join("; ", resultado.Errors.Select(e => e.Description));
            return Result.Fallo(new Error("Perfil.ActualizacionFallida", mensaje));
        }

        return Result.Exito();
    }
}
