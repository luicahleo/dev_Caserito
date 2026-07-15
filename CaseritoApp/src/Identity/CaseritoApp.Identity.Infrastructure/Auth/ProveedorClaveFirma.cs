using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>
/// Fuente única de la clave de firma HS256. Registrado como singleton para que el generador de
/// tokens y la validación Bearer usen exactamente la misma clave. Si <c>Jwt:Key</c> está vacía
/// (solo permitido en Development/Testing, ver <see cref="DependencyInjection"/>), genera una clave
/// efímera de ≥ 256 bits una sola vez, evitando que firma y validación diverjan.
/// </summary>
public sealed class ProveedorClaveFirma
{
    /// <summary>Clave simétrica compartida para firmar y validar el access token.</summary>
    public SymmetricSecurityKey Clave { get; }

    public ProveedorClaveFirma(IOptions<OpcionesJwt> opciones)
    {
        var key = opciones.Value.Key;
        var texto = string.IsNullOrWhiteSpace(key)
            ? Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")
            : key;

        Clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(texto));
    }
}
