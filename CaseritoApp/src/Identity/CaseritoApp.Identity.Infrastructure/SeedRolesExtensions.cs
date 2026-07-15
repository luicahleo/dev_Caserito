using System.Security.Claims;
using CaseritoApp.Identity.Domain.Autorizacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Seeding idempotente de los roles del MVP y sus RoleClaims de permiso.</summary>
public static class SeedRolesExtensions
{
    /// <summary>
    /// Asegura que existan los 6 roles de <see cref="RolesApp.Todos"/> y que cada uno tenga los
    /// RoleClaims <c>perm</c> del mapa <see cref="MapaRolesPermisos"/>. Idempotente: re-ejecutar no
    /// crea duplicados. Debe llamarse DESPUÉS de aplicar las migraciones.
    /// </summary>
    public static async Task SembrarRolesAsync(this IServiceProvider proveedor, CancellationToken ct = default)
    {
        using var scope = proveedor.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var nombreRol in RolesApp.Todos)
        {
            var rol = await roleManager.FindByNameAsync(nombreRol);
            if (rol is null)
            {
                rol = new IdentityRole<Guid>(nombreRol);
                await roleManager.CreateAsync(rol);
            }

            var permisosActuales = (await roleManager.GetClaimsAsync(rol))
                .Where(c => c.Type == ClaimsApp.Permiso)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var permiso in MapaRolesPermisos.PermisosDeRol(nombreRol).Where(permisosActuales.Add))
            {
                await roleManager.AddClaimAsync(rol, new Claim(ClaimsApp.Permiso, permiso));
            }
        }
    }
}
