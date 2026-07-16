using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.IntegrationTests.Infrastructure;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Verifica de extremo a extremo, contra un SQL Server real (Testcontainers), el flujo de
/// perfil expuesto en <c>/api/perfil</c>: register → login → GET perfil (200, datos correctos) →
/// PUT perfil (200, cambia nombre/ciudad) → GET perfil (refleja el cambio); y que sin token de
/// acceso el GET responda 401.
/// </summary>
public sealed class PerfilTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Ver_y_editar_perfil_del_usuario_autenticado()
    {
        using var cliente = factory.CreateClient();
        var email = $"perfil-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";

        // 1. Register.
        var registroRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, password, "Usuario Original", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registroRespuesta.StatusCode);

        // 2. Login.
        var loginRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRespuesta.StatusCode);

        var loginBody = await loginRespuesta.Content.ReadFromJsonAsync<TokenAccesoResponse>();
        Assert.NotNull(loginBody);
        var accessToken = loginBody!.AccessToken;
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // 3. GET /api/perfil con Bearer: 200 y datos correctos.
        using var solicitudGet1 = new HttpRequestMessage(HttpMethod.Get, "/api/perfil");
        solicitudGet1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var getRespuesta1 = await cliente.SendAsync(solicitudGet1);
        Assert.Equal(HttpStatusCode.OK, getRespuesta1.StatusCode);

        var perfilInicial = await getRespuesta1.Content.ReadFromJsonAsync<PerfilDto>();
        Assert.NotNull(perfilInicial);
        Assert.Equal(email, perfilInicial!.Email);
        Assert.Equal("Usuario Original", perfilInicial.Nombre);
        Assert.Equal("Lima", perfilInicial.Ciudad);
        Assert.False(perfilInicial.Verificado);

        // 4. PUT /api/perfil: 200 y actualiza nombre/ciudad.
        using var solicitudPut = new HttpRequestMessage(HttpMethod.Put, "/api/perfil")
        {
            Content = JsonContent.Create(new ActualizarPerfilRequest("Usuario Actualizado", "Arequipa")),
        };
        solicitudPut.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var putRespuesta = await cliente.SendAsync(solicitudPut);
        Assert.Equal(HttpStatusCode.OK, putRespuesta.StatusCode);

        // 5. GET nuevamente: refleja el cambio.
        using var solicitudGet2 = new HttpRequestMessage(HttpMethod.Get, "/api/perfil");
        solicitudGet2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var getRespuesta2 = await cliente.SendAsync(solicitudGet2);
        Assert.Equal(HttpStatusCode.OK, getRespuesta2.StatusCode);

        var perfilActualizado = await getRespuesta2.Content.ReadFromJsonAsync<PerfilDto>();
        Assert.NotNull(perfilActualizado);
        Assert.Equal("Usuario Actualizado", perfilActualizado!.Nombre);
        Assert.Equal("Arequipa", perfilActualizado.Ciudad);
    }

    [Fact]
    public async Task Get_perfil_sin_token_devuelve_401()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/perfil");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
