using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Autorizacion;
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Emite la sesión propia de Caserito para un usuario autenticado.</summary>
public interface IEmisorSesion
{
    /// <summary>Genera el access token y un nuevo refresh token.</summary>
    public Task<SesionEmitida> EmitirAsync(ApplicationUser usuario, CancellationToken ct);
}

/// <summary>Tokens que componen una sesión emitida por Caserito.</summary>
public sealed record SesionEmitida(string AccessToken, string RefreshToken);

/// <inheritdoc cref="IEmisorSesion"/>
public sealed class EmisorSesion(
    UserManager<ApplicationUser> userManager,
    IGeneradorTokensAcceso generadorTokens,
    IServicioRefreshTokens servicioRefreshTokens,
    IConsultaVerificacionKyc consultaKyc) : IEmisorSesion
{
    /// <inheritdoc/>
    public async Task<SesionEmitida> EmitirAsync(ApplicationUser usuario, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(usuario);
        var permisos = MapaRolesPermisos.PermisosDe(roles);
        var verificado = await consultaKyc.EstaVerificadoAsync(usuario.Id, ct);
        var identidadHabilitada = verificado || roles.Contains(RolesApp.AdminPlataforma);
        var accessToken = generadorTokens.Generar(usuario, permisos, verificado, identidadHabilitada);
        var refreshToken = await servicioRefreshTokens.EmitirAsync(usuario.Id, ct);

        return new SesionEmitida(accessToken, refreshToken);
    }
}
