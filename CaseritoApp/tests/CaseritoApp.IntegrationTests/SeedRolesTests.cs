using System.Security.Claims;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Verifica que el seeder de roles deja los 6 roles del MVP con sus RoleClaims de permiso, y que
/// re-ejecutarlo es idempotente (no duplica roles ni claims).
/// </summary>
public sealed class SeedRolesTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Seeder_crea_los_roles_del_mvp_y_es_idempotente()
    {
        // La factory ya sembró una vez en InitializeAsync; ejecutar de nuevo no debe duplicar.
        await factory.Services.SembrarRolesAsync();

        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        Assert.Equal(RolesApp.Todos.Count, roleManager.Roles.Count());

        var adminKyc = await roleManager.FindByNameAsync(RolesApp.AdminKyc);
        Assert.NotNull(adminKyc);

        var claims = await roleManager.GetClaimsAsync(adminKyc!);
        var permisos = claims.Where(c => c.Type == ClaimsApp.Permiso).Select(c => c.Value).ToArray();

        Assert.Equal([Permisos.KycRevisar], permisos);
    }
}
