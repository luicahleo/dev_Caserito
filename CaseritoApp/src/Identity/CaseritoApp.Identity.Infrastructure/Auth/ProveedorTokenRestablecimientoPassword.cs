using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Opciones aisladas para no alterar la vigencia de otros tokens de Identity.</summary>
public sealed class OpcionesTokenRestablecimientoPassword : DataProtectionTokenProviderOptions
{
    public OpcionesTokenRestablecimientoPassword()
    {
        TokenLifespan = TimeSpan.FromMinutes(30);
    }
}

/// <summary>Proveedor Data Protection exclusivo para restablecer contraseñas.</summary>
public sealed class ProveedorTokenRestablecimientoPassword(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<OpcionesTokenRestablecimientoPassword> opciones,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider, opciones, logger)
{
    public const string Nombre = "RestablecimientoPassword";
}
