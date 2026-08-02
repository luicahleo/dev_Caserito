using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CaseritoApp.IntegrationTests;

public sealed class AuthExternaConfiguracionTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public void Mantiene_bearer_y_configura_cookie_externa_segura()
    {
        using var scope = factory.Services.CreateScope();
        var autenticacion = scope.ServiceProvider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var cookies = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ExternalScheme);

        Assert.Equal("Bearer", autenticacion.DefaultAuthenticateScheme);
        Assert.Equal("Bearer", autenticacion.DefaultChallengeScheme);
        Assert.True(cookies.Cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, cookies.Cookie.SameSite);
        Assert.Equal("/api/auth/external", cookies.Cookie.Path);
        Assert.Equal(TimeSpan.FromMinutes(10), cookies.ExpireTimeSpan);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, cookies.Cookie.SecurePolicy);
    }

    [Fact]
    public async Task Capacidad_anuncia_solo_proveedores_configurados()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/auth/external/providers");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var proveedores = await respuesta.Content.ReadFromJsonAsync<string[]>();
        Assert.NotNull(proveedores);
        Assert.Equal(["facebook", "google"], proveedores);
    }

    [Fact]
    public async Task Capacidad_no_anuncia_proveedores_sin_configuracion()
    {
        using var sinProveedores = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", string.Empty);
            builder.UseSetting("Authentication:Google:ClientSecret", string.Empty);
            builder.UseSetting("Authentication:Facebook:AppId", string.Empty);
            builder.UseSetting("Authentication:Facebook:AppSecret", string.Empty);
        });
        using var cliente = sinProveedores.CreateClient();

        var proveedores = await cliente.GetFromJsonAsync<string[]>("/api/auth/external/providers");

        Assert.Empty(proveedores!);
    }

    [Fact]
    public void Google_mapea_la_evidencia_de_email_verificado()
    {
        using var scope = factory.Services.CreateScope();
        var opciones = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(GoogleDefaults.AuthenticationScheme);
        using var datos = JsonDocument.Parse("""{"email_verified":true}""");
        var identidad = new ClaimsIdentity();

        foreach (var accion in opciones.ClaimActions)
        {
            accion.Run(datos.RootElement, identidad, GoogleDefaults.AuthenticationScheme);
        }

        Assert.True(bool.Parse(identidad.FindFirst("email_verified")!.Value));
    }
}
