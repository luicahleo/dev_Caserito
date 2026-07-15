using System.Net;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Verifica que al registrarse, el usuario recibe el rol por defecto <c>Cliente</c>.</summary>
public sealed class RegistroAsignaRolTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Registro_asigna_rol_Cliente()
    {
        using var cliente = factory.CreateClient();
        var email = $"registro-rol-{Guid.NewGuid():N}@caserito.test";

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "Lima"));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        Assert.NotNull(usuario);

        var roles = await userManager.GetRolesAsync(usuario!);
        Assert.Contains(RolesApp.Cliente, roles);
    }
}
