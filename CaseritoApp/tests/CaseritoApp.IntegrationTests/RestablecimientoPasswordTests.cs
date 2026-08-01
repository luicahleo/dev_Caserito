using System.Net;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class RestablecimientoPasswordTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    private const string PasswordAnterior = "Password123!";
    private const string PasswordNueva = "Password456!";

    [Fact]
    public async Task Solicitud_existente_e_inexistente_devuelven_la_misma_respuesta()
    {
        using var cliente = factory.CreateClient();
        var email = await RegistrarAsync(cliente);

        var existente = await cliente.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new OlvidePasswordRequest(email));
        var inexistente = await cliente.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new OlvidePasswordRequest($"inexistente-{Guid.NewGuid():N}@caserito.test"));

        Assert.Equal(HttpStatusCode.NoContent, existente.StatusCode);
        Assert.Equal(existente.StatusCode, inexistente.StatusCode);
    }

    [Fact]
    public async Task Token_valido_cambia_password_conserva_confirmacion_y_no_puede_reutilizarse()
    {
        using var cliente = factory.CreateClient();
        var email = await RegistrarAsync(cliente);
        var (usuarioId, token, emailConfirmado) = await GenerarTokenAsync(email);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/reset-password",
            new RestablecerPasswordRequest(usuarioId, token, PasswordNueva));

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(cliente, email, PasswordAnterior)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(cliente, email, PasswordNueva)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());
        Assert.NotNull(usuario);
        Assert.Equal(emailConfirmado, usuario!.EmailConfirmed);

        var reutilizacion = await cliente.PostAsJsonAsync(
            "/api/auth/reset-password",
            new RestablecerPasswordRequest(usuarioId, token, "Password789!"));
        Assert.Equal(HttpStatusCode.BadRequest, reutilizacion.StatusCode);
    }

    [Fact]
    public async Task Primer_cambio_invalida_enlaces_anteriores_y_refresh_tokens()
    {
        using var cliente = factory.CreateClient();
        var email = await RegistrarAsync(cliente);
        var login = await LoginAsync(cliente, email, PasswordAnterior);
        var refreshToken = ExtraerCookie(login, "refreshToken");
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        var (usuarioId, primerToken, _) = await GenerarTokenAsync(email);
        var (_, segundoToken, _) = await GenerarTokenAsync(email);

        var primero = await cliente.PostAsJsonAsync(
            "/api/auth/reset-password",
            new RestablecerPasswordRequest(usuarioId, primerToken, PasswordNueva));
        Assert.Equal(HttpStatusCode.NoContent, primero.StatusCode);

        var segundo = await cliente.PostAsJsonAsync(
            "/api/auth/reset-password",
            new RestablecerPasswordRequest(usuarioId, segundoToken, "Password789!"));
        Assert.Equal(HttpStatusCode.BadRequest, segundo.StatusCode);

        using var solicitudRefresh = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        solicitudRefresh.Headers.Add("Cookie", $"refreshToken={refreshToken}");
        var refresh = await cliente.SendAsync(solicitudRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Usuario_o_token_invalidos_devuelven_el_mismo_problema_publico()
    {
        using var cliente = factory.CreateClient();
        var email = await RegistrarAsync(cliente);
        var (usuarioId, _, _) = await GenerarTokenAsync(email);

        var usuarioInvalido = await cliente.PostAsJsonAsync(
            "/api/auth/reset-password",
            new RestablecerPasswordRequest(Guid.NewGuid(), "token-sintetico", PasswordNueva));
        var tokenInvalido = await cliente.PostAsJsonAsync(
            "/api/auth/reset-password",
            new RestablecerPasswordRequest(usuarioId, "token-sintetico", PasswordNueva));

        Assert.Equal(HttpStatusCode.BadRequest, usuarioInvalido.StatusCode);
        Assert.Equal(usuarioInvalido.StatusCode, tokenInvalido.StatusCode);
        Assert.Equal(
            await usuarioInvalido.Content.ReadAsStringAsync(),
            await tokenInvalido.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Limites_de_solicitud_y_consumo_se_aplican_por_particion()
    {
        using var hostSolicitud = factory.WithWebHostBuilder(_ => { });
        using var clienteSolicitud = hostSolicitud.CreateClient();
        for (var intento = 0; intento < 3; intento++)
        {
            var permitida = await clienteSolicitud.PostAsJsonAsync(
                "/api/auth/forgot-password",
                new OlvidePasswordRequest($"limite-{Guid.NewGuid():N}@caserito.test"));
            Assert.Equal(HttpStatusCode.NoContent, permitida.StatusCode);
        }

        var solicitudLimitada = await clienteSolicitud.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new OlvidePasswordRequest($"limite-{Guid.NewGuid():N}@caserito.test"));
        Assert.Equal(HttpStatusCode.TooManyRequests, solicitudLimitada.StatusCode);

        using var hostConsumo = factory.WithWebHostBuilder(_ => { });
        using var clienteConsumo = hostConsumo.CreateClient();
        for (var intento = 0; intento < 10; intento++)
        {
            var permitida = await clienteConsumo.PostAsJsonAsync(
                "/api/auth/reset-password",
                new RestablecerPasswordRequest(Guid.NewGuid(), "token-sintetico", PasswordNueva));
            Assert.Equal(HttpStatusCode.BadRequest, permitida.StatusCode);
        }

        var consumoLimitado = await clienteConsumo.PostAsJsonAsync(
            "/api/auth/reset-password",
            new RestablecerPasswordRequest(Guid.NewGuid(), "token-sintetico", PasswordNueva));
        Assert.Equal(HttpStatusCode.TooManyRequests, consumoLimitado.StatusCode);
    }

    private async Task<(Guid UsuarioId, string Token, bool EmailConfirmado)> GenerarTokenAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        Assert.NotNull(usuario);
        var token = await userManager.GeneratePasswordResetTokenAsync(usuario!);
        return (usuario!.Id, token, usuario.EmailConfirmed);
    }

    private static async Task<string> RegistrarAsync(HttpClient cliente)
    {
        var email = $"reset-{Guid.NewGuid():N}@caserito.test";
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, PasswordAnterior, "Usuario de Prueba", "Lima"));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return email;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient cliente, string email, string password) =>
        cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

    private static string? ExtraerCookie(HttpResponseMessage respuesta, string nombreCookie)
    {
        if (!respuesta.Headers.TryGetValues("Set-Cookie", out var valores))
        {
            return null;
        }

        var valor = valores.FirstOrDefault(v => v.StartsWith($"{nombreCookie}=", StringComparison.Ordinal));
        if (valor is null)
        {
            return null;
        }

        var sinNombre = valor[(nombreCookie.Length + 1)..];
        var finValor = sinNombre.IndexOf(';');
        return finValor >= 0 ? sinNombre[..finValor] : sinNombre;
    }
}
