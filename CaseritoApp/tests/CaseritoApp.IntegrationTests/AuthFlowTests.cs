using System.Net;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.IntegrationTests.Infrastructure;
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
