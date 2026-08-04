using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
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
    private static readonly Guid _cochabamba = new("22222222-2222-2222-2222-000000000001");
    private static readonly Guid _laPaz = new("22222222-2222-2222-2222-000000000003");

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

        using (var alcance = factory.Services.CreateScope())
        {
            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = await usuarios.FindByEmailAsync(email);
            Assert.NotNull(usuario);
            usuario.Nombres = "Ana María";
            usuario.Apellidos = "Quispe Flores";
            usuario.CiudadId = _cochabamba;
            Assert.True((await usuarios.UpdateAsync(usuario)).Succeeded);
        }

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
        Assert.Equal("Ana María", perfilInicial.Nombres);
        Assert.Equal("Quispe Flores", perfilInicial.Apellidos);
        Assert.Equal(_cochabamba, perfilInicial.CiudadId);
        Assert.Equal("Cochabamba", perfilInicial.NombreCiudad);
        Assert.False(perfilInicial.Verificado);

        // 4. PUT /api/perfil: 200 y actualiza nombre/ciudad.
        using var solicitudPut = new HttpRequestMessage(HttpMethod.Put, "/api/perfil")
        {
            Content = JsonContent.Create(new ActualizarPerfilRequest(
                "Ana",
                "Quispe",
                _laPaz)),
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
        Assert.Equal("Ana", perfilActualizado!.Nombres);
        Assert.Equal("Quispe", perfilActualizado.Apellidos);
        Assert.Equal(_laPaz, perfilActualizado.CiudadId);
        Assert.Equal("La Paz", perfilActualizado.NombreCiudad);
    }

    [Fact]
    public async Task Actualizar_perfil_rechaza_ciudad_inexistente()
    {
        using var cliente = factory.CreateClient();
        var email = $"perfil-ciudad-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, password, "Usuario", "Cochabamba"))).StatusCode);

        using (var alcance = factory.Services.CreateScope())
        {
            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = (await usuarios.FindByEmailAsync(email))!;
            usuario.Nombres = "Ana";
            usuario.Apellidos = "Quispe";
            usuario.CiudadId = _cochabamba;
            Assert.True((await usuarios.UpdateAsync(usuario)).Succeeded);
        }

        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var token = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        using var solicitud = new HttpRequestMessage(HttpMethod.Put, "/api/perfil")
        {
            Content = JsonContent.Create(new ActualizarPerfilRequest("Ana", "Quispe", Guid.NewGuid())),
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Solicitud_kyc_pendiente_bloquea_nombres_pero_no_ciudad()
    {
        using var cliente = factory.CreateClient();
        var email = $"perfil-kyc-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, password, "Usuario", "Cochabamba"))).StatusCode);

        using (var alcance = factory.Services.CreateScope())
        {
            var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = (await usuarios.FindByEmailAsync(email))!;
            usuario.Nombres = "Ana";
            usuario.Apellidos = "Quispe";
            usuario.CiudadId = _cochabamba;
            Assert.True((await usuarios.UpdateAsync(usuario)).Succeeded);

            var verificacion = VerificacionKyc.Crear(usuario.Id);
            Assert.True(verificacion.EnviarSolicitud(
                "referencia-documento-prueba",
                "referencia-selfie-prueba",
                TipoDocumento.CedulaIdentidad,
                DateTimeOffset.UtcNow).EsExito);
            var db = alcance.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.VerificacionesKyc.Add(verificacion);
            await db.SaveChangesAsync();
        }

        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var token = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;

        var identidad = await ActualizarAsync(cliente, token, "Otra", "Persona", _laPaz);
        var soloCiudad = await ActualizarAsync(cliente, token, "Ana", "Quispe", _laPaz);

        Assert.Equal(HttpStatusCode.BadRequest, identidad.StatusCode);
        Assert.Equal(HttpStatusCode.OK, soloCiudad.StatusCode);
    }

    [Fact]
    public async Task Get_perfil_sin_token_devuelve_401()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/perfil");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    private static async Task<HttpResponseMessage> ActualizarAsync(
        HttpClient cliente,
        string token,
        string nombres,
        string apellidos,
        Guid ciudadId)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Put, "/api/perfil")
        {
            Content = JsonContent.Create(new ActualizarPerfilRequest(nombres, apellidos, ciudadId)),
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await cliente.SendAsync(solicitud);
    }
}
