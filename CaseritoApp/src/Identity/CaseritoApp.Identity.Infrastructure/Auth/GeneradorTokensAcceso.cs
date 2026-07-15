using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Genera JWT de acceso para un <see cref="ApplicationUser"/> autenticado.</summary>
public interface IGeneradorTokensAcceso
{
    /// <summary>Genera un JWT firmado (HS256) con los claims del usuario y expiración configurable.</summary>
    public string Generar(ApplicationUser usuario);
}

/// <inheritdoc cref="IGeneradorTokensAcceso"/>
public sealed class GeneradorTokensAcceso(
    IOptions<OpcionesJwt> opciones, ProveedorClaveFirma proveedorClave, TimeProvider tiempo)
    : IGeneradorTokensAcceso
{
    private readonly OpcionesJwt _o = opciones.Value;

    /// <inheritdoc/>
    public string Generar(ApplicationUser usuario)
    {
        var credenciales = new SigningCredentials(proveedorClave.Clave, SecurityAlgorithms.HmacSha256);
        var ahora = tiempo.GetUtcNow();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, usuario.Nombre),
        };

        var token = new JwtSecurityToken(
            issuer: _o.Issuer,
            audience: _o.Audience,
            claims: claims,
            notBefore: ahora.UtcDateTime,
            expires: ahora.AddMinutes(_o.MinutosAcceso).UtcDateTime,
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
