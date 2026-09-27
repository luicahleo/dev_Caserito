using System.Security.Claims;
using CaseritoApp.Identity.Domain.Autorizacion;

namespace CaseritoApp.Host.Autorizacion;

public static class ClaimsPrincipalExtensions
{
    /// <summary>KYC aprobado o exención por rol de plataforma, según el claim del token.</summary>
    public static bool TieneIdentidadHabilitada(this ClaimsPrincipal usuario) =>
        string.Equals(
            usuario.FindFirstValue(ClaimsApp.IdentidadHabilitada),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
