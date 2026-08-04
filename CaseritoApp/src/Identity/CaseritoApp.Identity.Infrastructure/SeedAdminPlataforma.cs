using CaseritoApp.Identity.Domain.Autorizacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Configuración opt-in del seed del administrador de plataforma.</summary>
public sealed record OpcionesSeedAdmin
{
    public const string Seccion = "SeedSettings";

    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminNombres { get; set; } = string.Empty;
    public string AdminApellidos { get; set; } = string.Empty;
    public Guid AdminCiudadId { get; set; }

    internal bool EstaCompleta =>
        !string.IsNullOrWhiteSpace(AdminEmail)
        && !string.IsNullOrWhiteSpace(AdminPassword)
        && !string.IsNullOrWhiteSpace(AdminNombres)
        && !string.IsNullOrWhiteSpace(AdminApellidos)
        && AdminCiudadId != Guid.Empty;
}

/// <summary>
/// Crea el primer administrador de plataforma sin modificar usuarios existentes.
/// Los fallos de Identity no impiden que arranque el sitio público.
/// </summary>
public sealed partial class SeedAdminPlataforma(
    UserManager<ApplicationUser> usuarios,
    ILogger<SeedAdminPlataforma> logger)
{
    public async Task EjecutarAsync(
        OpcionesSeedAdmin opciones,
        CancellationToken cancellationToken = default)
    {
        if (!opciones.EstaCompleta)
        {
            SeedOmitido(logger);
            return;
        }

        var existente = await usuarios.FindByEmailAsync(opciones.AdminEmail);
        if (existente is not null)
        {
            if (await usuarios.IsInRoleAsync(existente, RolesApp.AdminPlataforma))
            {
                SeedYaExistente(logger);
                return;
            }

            var rolReparado = await usuarios.AddToRoleAsync(existente, RolesApp.AdminPlataforma);
            if (!rolReparado.Succeeded)
            {
                SeedFallo(logger, Codigos(rolReparado));
                return;
            }

            SeedRolReparado(logger);
            return;
        }

        var usuario = new ApplicationUser
        {
            UserName = opciones.AdminEmail,
            Email = opciones.AdminEmail,
            Nombres = opciones.AdminNombres,
            Apellidos = opciones.AdminApellidos,
            CiudadId = opciones.AdminCiudadId,
            EmailConfirmed = true,
        };

        var creado = await usuarios.CreateAsync(usuario, opciones.AdminPassword);
        if (!creado.Succeeded)
        {
            SeedFallo(logger, Codigos(creado));
            return;
        }

        var asignado = await usuarios.AddToRoleAsync(usuario, RolesApp.AdminPlataforma);
        if (!asignado.Succeeded)
        {
            SeedFallo(logger, Codigos(asignado));
            return;
        }

        SeedCreado(logger);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string Codigos(IdentityResult resultado)
        => string.Join(", ", resultado.Errors.Select(error => error.Code));

    [LoggerMessage(
        EventId = 1110,
        Level = LogLevel.Warning,
        Message = "Seed de admin omitido: configuración SeedSettings incompleta.")]
    private static partial void SeedOmitido(ILogger logger);

    [LoggerMessage(
        EventId = 1111,
        Level = LogLevel.Debug,
        Message = "Seed de admin omitido: el usuario ya existe.")]
    private static partial void SeedYaExistente(ILogger logger);

    [LoggerMessage(
        EventId = 1112,
        Level = LogLevel.Error,
        Message = "Falló el seed de admin. Códigos Identity: {Codigos}.")]
    private static partial void SeedFallo(ILogger logger, string codigos);

    [LoggerMessage(
        EventId = 1113,
        Level = LogLevel.Information,
        Message = "Seed de admin creado con rol AdminPlataforma.")]
    private static partial void SeedCreado(ILogger logger);

    [LoggerMessage(
        EventId = 1114,
        Level = LogLevel.Information,
        Message = "Seed de admin reparado: rol AdminPlataforma asignado al usuario existente.")]
    private static partial void SeedRolReparado(ILogger logger);
}
