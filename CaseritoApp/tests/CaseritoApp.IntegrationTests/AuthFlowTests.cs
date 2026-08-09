using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Verifica de extremo a extremo, contra un SQL Server real (Testcontainers), el flujo completo
/// de autenticación expuesto en <c>/api/auth</c>: register → login → refresh → logout, incluyendo
/// que el refresh token quede inutilizable tras el logout y que una contraseña incorrecta se
/// rechace con 401.
/// </summary>
public sealed class AuthFlowTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private const string NombreCookie = "refreshToken";

    [Fact]
    public void Emisor_de_sesion_comun_esta_registrado()
    {
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmisorSesion>());
    }

    [Fact]
    public async Task Flujo_completo_register_login_refresh_logout()
    {
        using var cliente = factory.CreateClient();
        var email = $"auth-flow-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";

        // 1. Register.
        var registroRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, password, "Usuario de Prueba", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registroRespuesta.StatusCode);

        // 2. Login.
        var loginRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRespuesta.StatusCode);

        var loginBody = await loginRespuesta.Content.ReadFromJsonAsync<TokenAccesoResponse>();
        Assert.NotNull(loginBody);
        Assert.False(string.IsNullOrWhiteSpace(loginBody!.AccessToken));

        var cookieTrasLogin = ExtraerCookie(loginRespuesta, NombreCookie);
        Assert.False(string.IsNullOrWhiteSpace(cookieTrasLogin));

        // 3. Refresh usando la cookie emitida en login.
        using var solicitudRefresh = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        solicitudRefresh.Headers.Add("Cookie", $"{NombreCookie}={cookieTrasLogin}");
        var refreshRespuesta = await cliente.SendAsync(solicitudRefresh);
        Assert.Equal(HttpStatusCode.OK, refreshRespuesta.StatusCode);

        var refreshBody = await refreshRespuesta.Content.ReadFromJsonAsync<TokenAccesoResponse>();
        Assert.NotNull(refreshBody);
        Assert.False(string.IsNullOrWhiteSpace(refreshBody!.AccessToken));

        // No se compara con desigualdad estricta contra el access token de login: el JWT no lleva
        // "jti" y se firma con los mismos claims, por lo que si login y refresh caen en el mismo
        // segundo (mismo iat/exp) el token resultante es byte a byte idéntico; eso es correcto,
        // no un bug. Lo que sí se verifica es que la cookie de refresh sí rota (abajo).

        var cookieTrasRefresh = ExtraerCookie(refreshRespuesta, NombreCookie);
        Assert.False(string.IsNullOrWhiteSpace(cookieTrasRefresh));
        Assert.NotEqual(cookieTrasLogin, cookieTrasRefresh);
        Assert.True(refreshRespuesta.Headers.TryGetValues("Set-Cookie", out var cookiesRefresh));
        Assert.Contains(
            cookiesRefresh!,
            valor => valor.StartsWith($"{NombreCookie}=", StringComparison.Ordinal) &&
                valor.Contains("Max-Age=2592000", StringComparison.OrdinalIgnoreCase));

        using (var scope = factory.Services.CreateScope())
        {
            var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var usuario = await usuarios.FindByEmailAsync(email);
            Assert.NotNull(usuario);

            var tokens = await db.RefreshTokens
                .AsNoTracking()
                .Where(t => t.UserId == usuario!.Id)
                .ToListAsync();
            var reemplazado = Assert.Single(tokens, t => t.RevocadoEn is not null);
            Assert.NotNull(reemplazado.ReemplazadoPorHash);
        }

        // 4. Logout usando la cookie rotada.
        using var solicitudLogout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        solicitudLogout.Headers.Add("Cookie", $"{NombreCookie}={cookieTrasRefresh}");
        var logoutRespuesta = await cliente.SendAsync(solicitudLogout);
        Assert.Equal(HttpStatusCode.NoContent, logoutRespuesta.StatusCode);

        // 5. Refresh tras logout: el token ya fue revocado → 401.
        using var solicitudRefreshTrasLogout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        solicitudRefreshTrasLogout.Headers.Add("Cookie", $"{NombreCookie}={cookieTrasRefresh}");
        var refreshTrasLogoutRespuesta = await cliente.SendAsync(solicitudRefreshTrasLogout);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshTrasLogoutRespuesta.StatusCode);
    }

    [Fact]
    public async Task Login_con_password_incorrecta_devuelve_401()
    {
        using var cliente = factory.CreateClient();
        var email = $"auth-flow-bad-pwd-{Guid.NewGuid():N}@caserito.test";

        var registroRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario de Prueba", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registroRespuesta.StatusCode);

        var loginRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "PasswordIncorrecta!"));

        Assert.Equal(HttpStatusCode.Unauthorized, loginRespuesta.StatusCode);
    }

    [Fact]
    public async Task Login_establece_cookie_de_refresh_con_los_flags_esperados()
    {
        using var cliente = factory.CreateClient();
        var email = $"auth-flow-cookie-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";

        var registroRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, password, "Usuario de Prueba", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registroRespuesta.StatusCode);

        var loginRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRespuesta.StatusCode);

        Assert.True(loginRespuesta.Headers.TryGetValues("Set-Cookie", out var valoresSetCookie));
        var setCookieRefresh = valoresSetCookie!.FirstOrDefault(
            v => v.StartsWith($"{NombreCookie}=", StringComparison.Ordinal));
        Assert.NotNull(setCookieRefresh);

        // En Testing la cookie no es Secure (ver OpcionesCookieRefresh: solo fuera de
        // Development/Testing), así que ese flag no se exige aquí.
        Assert.Contains("HttpOnly", setCookieRefresh, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", setCookieRefresh, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Path=/api/auth", setCookieRefresh, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Max-Age=2592000", setCookieRefresh, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirmar_email_con_token_valido_marca_email_confirmado()
    {
        using var cliente = factory.CreateClient();
        var email = $"confirm-email-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";

        var registroRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, password, "Usuario de Prueba", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registroRespuesta.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var generadorToken = scope.ServiceProvider.GetRequiredService<IGeneradorTokenEmail>();

        var usuario = await userManager.FindByEmailAsync(email);
        Assert.NotNull(usuario);
        Assert.False(usuario!.EmailConfirmed);

        var token = generadorToken.Generar(usuario.Id);
        var confirmarRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new ConfirmarEmailRequest(usuario.Id, token));
        Assert.Equal(HttpStatusCode.NoContent, confirmarRespuesta.StatusCode);

        using var scopeConfirmado = factory.Services.CreateScope();
        var userManagerConfirmado = scopeConfirmado.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuarioConfirmado = await userManagerConfirmado.FindByEmailAsync(email);
        Assert.NotNull(usuarioConfirmado);
        Assert.True(usuarioConfirmado!.EmailConfirmed);
    }

    [Fact]
    public async Task Confirmar_email_con_token_invalido_devuelve_400()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new ConfirmarEmailRequest(Guid.NewGuid(), "token-invalido"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reenviar_confirmacion_sin_autenticacion_devuelve_401()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.PostAsync("/api/auth/resend-confirmation", null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reenviar_confirmacion_para_usuario_no_confirmado_devuelve_204()
    {
        using var cliente = factory.CreateClient();
        var email = $"resend-confirm-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";

        var registroRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, password, "Usuario de Prueba", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registroRespuesta.StatusCode);

        var loginRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRespuesta.StatusCode);

        var loginBody = await loginRespuesta.Content.ReadFromJsonAsync<TokenAccesoResponse>();
        Assert.NotNull(loginBody);
        Assert.False(string.IsNullOrWhiteSpace(loginBody!.AccessToken));

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);

        var respuesta = await cliente.PostAsync("/api/auth/resend-confirmation", null);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task Usuario_sin_email_confirmado_no_puede_enviar_kyc()
    {
        using var cliente = factory.CreateClient();
        var email = $"kyc-sin-confirmar-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";

        var registroRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, password, "Usuario de Prueba", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registroRespuesta.StatusCode);

        var loginRespuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRespuesta.StatusCode);
        var loginBody = await loginRespuesta.Content.ReadFromJsonAsync<TokenAccesoResponse>();

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/kyc/?numeroCi=1234567&departamentoExpedicion=LaPaz");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static string? ExtraerCookie(HttpResponseMessage respuesta, string nombreCookie)
    {
        if (!respuesta.Headers.TryGetValues("Set-Cookie", out var valores))
        {
            return null;
        }

        foreach (var valor in valores)
        {
            if (!valor.StartsWith($"{nombreCookie}=", StringComparison.Ordinal))
            {
                continue;
            }

            var sinNombre = valor[(nombreCookie.Length + 1)..];
            var finValor = sinNombre.IndexOf(';');
            return finValor >= 0 ? sinNombre[..finValor] : sinNombre;
        }

        return null;
    }
}
