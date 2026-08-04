using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Application.Perfil;
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Identity.Infrastructure.Perfil;

/// <summary>
/// Implementación de <see cref="IRepositorioPerfil"/> apoyada en
/// <see cref="UserManager{TUser}"/> de ASP.NET Core Identity.
/// </summary>
public sealed class RepositorioPerfilUserManager(
    UserManager<ApplicationUser> userManager,
    IConsultaVerificacionKyc consultaKyc,
    IConsultaCiudadesPerfil consultaCiudades)
    : IRepositorioPerfil
{
    public async Task<PerfilDto?> ObtenerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            return null;
        }

        var verificado = await consultaKyc.EstaVerificadoAsync(usuario.Id, cancellationToken);
        var nombreCiudad = await consultaCiudades.ObtenerNombreActivaAsync(
            usuario.CiudadId,
            cancellationToken) ?? string.Empty;
        return new PerfilDto(
            usuario.Id,
            usuario.Email ?? string.Empty,
            usuario.Nombres,
            usuario.Apellidos,
            usuario.CiudadId,
            nombreCiudad,
            verificado);
    }

    public async Task<PerfilPublicoDto?> ObtenerPublicoAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            return null;
        }

        var verificado = await consultaKyc.EstaVerificadoAsync(usuario.Id, cancellationToken);
        return new PerfilPublicoDto(usuario.Id, usuario.Nombre, usuario.Ciudad, verificado);
    }

    public async Task<Result> ActualizarAsync(
        Guid userId,
        string nombres,
        string apellidos,
        Guid ciudadId,
        CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            return Result.Fallo(new Error("Perfil.NoEncontrado", "El usuario no existe."));
        }

        if (!await consultaCiudades.ExisteActivaAsync(ciudadId, cancellationToken))
        {
            return Result.Fallo(new Error("Perfil.CiudadInvalida", "La ciudad seleccionada no es válida."));
        }

        var cambiaIdentidad = !string.Equals(usuario.Nombres, nombres, StringComparison.Ordinal)
            || !string.Equals(usuario.Apellidos, apellidos, StringComparison.Ordinal);
        if (cambiaIdentidad && !await consultaKyc.PuedeEditarIdentidadAsync(usuario.Id, cancellationToken))
        {
            return Result.Fallo(new Error(
                "Perfil.IdentidadBloqueada",
                "Los datos de identidad no pueden modificarse en el estado actual."));
        }

        usuario.Nombres = nombres.Trim();
        usuario.Apellidos = apellidos.Trim();
        usuario.CiudadId = ciudadId;

        var resultado = await userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            return Result.Fallo(new Error(
                "Perfil.ActualizacionFallida",
                "No se pudo actualizar el perfil."));
        }

        return Result.Exito();
    }
}
