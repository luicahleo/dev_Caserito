using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CaseritoApp.IntegrationTests;

public sealed class AuthExternaLoginTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Usuario_asociado_recibe_refresh_y_retorno_local()
    {
        var clave = $"google-{Guid.NewGuid():N}";
        await CrearUsuarioAsociadoAsync(clave);
        using var cliente = factory.CreateClient(new() { AllowAutoRedirect = false });
        var cookie = CrearCookieExterna("Google", clave, "usuario@externo.test", emailVerificado: true);

        using var callback = new HttpRequestMessage(HttpMethod.Get, "/api/auth/external/callback?returnUrl=/avisos");
        callback.Headers.Add("Cookie", cookie);
        var respuesta = await cliente.SendAsync(callback);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/auth/external/completado?returnUrl=%2Favisos", respuesta.Headers.Location?.OriginalString);
        var refresh = ExtraerCookie(respuesta, "refreshToken");
        using var solicitudRefresh = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        solicitudRefresh.Headers.Add("Cookie", $"refreshToken={refresh}");
        var respuestaRefresh = await cliente.SendAsync(solicitudRefresh);
        Assert.Equal(HttpStatusCode.OK, respuestaRefresh.StatusCode);
    }

    [Fact]
    public async Task Callback_cancelado_no_crea_usuario()
    {
        using var scopeInicial = factory.Services.CreateScope();
        var dbInicial = scopeInicial.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var cantidadInicial = dbInicial.Users.Count();
        using var cliente = factory.CreateClient(new() { AllowAutoRedirect = false });

        var respuesta = await cliente.GetAsync("/api/auth/external/callback");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/login?authExterna=cancelado", respuesta.Headers.Location?.OriginalString);
        using var scopeFinal = factory.Services.CreateScope();
        Assert.Equal(cantidadInicial, scopeFinal.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.Count());
    }

    [Fact]
    public async Task Retorno_externo_se_reemplaza_por_perfil()
    {
        var clave = $"google-{Guid.NewGuid():N}";
        await CrearUsuarioAsociadoAsync(clave);
        using var cliente = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var callback = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/auth/external/callback?returnUrl=https://sitio-no-local.test");
        callback.Headers.Add("Cookie", CrearCookieExterna("Google", clave, "usuario@externo.test", true));

        var respuesta = await cliente.SendAsync(callback);

        Assert.Equal("/auth/external/completado?returnUrl=%2Fperfil", respuesta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Callback_repetido_no_duplica_usuario_ni_login()
    {
        var clave = $"facebook-{Guid.NewGuid():N}";
        await CrearUsuarioAsociadoAsync(clave, "Facebook");
        using var cliente = factory.CreateClient(new() { AllowAutoRedirect = false });
        for (var intento = 0; intento < 2; intento++)
        {
            using var callback = new HttpRequestMessage(HttpMethod.Get, "/api/auth/external/callback");
            callback.Headers.Add("Cookie", CrearCookieExterna("Facebook", clave, "usuario@externo.test", false));
            Assert.Equal(HttpStatusCode.Redirect, (await cliente.SendAsync(callback)).StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(1, db.UserLogins.Count(login => login.LoginProvider == "Facebook" && login.ProviderKey == clave));
    }

    private async Task CrearUsuarioAsociadoAsync(string clave, string proveedor = "google")
    {
        using var scope = factory.Services.CreateScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"externo-{Guid.NewGuid():N}@caserito.test";
        var usuario = new ApplicationUser { UserName = email, Email = email, Nombre = "Usuario", Ciudad = "Lima" };
        Assert.True((await usuarios.CreateAsync(usuario)).Succeeded);
        Assert.True((await usuarios.AddToRoleAsync(usuario, "Cliente")).Succeeded);
        Assert.True((await usuarios.AddLoginAsync(usuario, new UserLoginInfo(proveedor.ToLowerInvariant(), clave, proveedor))).Succeeded);
    }

    private string CrearCookieExterna(string proveedor, string clave, string email, bool emailVerificado)
    {
        using var scope = factory.Services.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var opciones = monitor.Get(IdentityConstants.ExternalScheme);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, clave),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, "Usuario Externo"),
            new Claim("email_verified", emailVerificado ? "true" : "false"),
        };
        var propiedades = new AuthenticationProperties(new Dictionary<string, string?>
        {
            ["LoginProvider"] = proveedor,
        });
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, proveedor)), propiedades, IdentityConstants.ExternalScheme);
        return $"{opciones.Cookie.Name}={opciones.TicketDataFormat.Protect(ticket)}";
    }

    private static string ExtraerCookie(HttpResponseMessage respuesta, string nombre)
    {
        Assert.True(respuesta.Headers.TryGetValues("Set-Cookie", out var valores));
        var valor = valores!.Single(v => v.StartsWith($"{nombre}=", StringComparison.Ordinal));
        return valor.Split(';', 2)[0][(nombre.Length + 1)..];
    }
}
