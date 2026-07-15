using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Verifica el pipeline RBAC end-to-end sobre el endpoint de ejemplo <c>/api/admin/ping</c>:
/// un Cliente sin el permiso recibe 403; un usuario con rol AdminPlataforma recibe 200.
/// </summary>
public sealed class AdminEndpointTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private async Task<string> RegistrarYLoguearAsync(HttpClient cliente, string email, string? rolExtra)
    {
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        if (rolExtra is not null)
        {
            using var scope = factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(usuario!, rolExtra);
        }

        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<TokenAccesoResponse>();
        return body!.AccessToken;
    }

    [Fact]
    public async Task Cliente_sin_permiso_recibe_403()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, $"admin-403-{Guid.NewGuid():N}@caserito.test", rolExtra: null);

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/admin/ping");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task AdminPlataforma_con_permiso_recibe_200()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(
            cliente, $"admin-200-{Guid.NewGuid():N}@caserito.test", RolesApp.AdminPlataforma);

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/admin/ping");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_token_recibe_401()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/admin/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
