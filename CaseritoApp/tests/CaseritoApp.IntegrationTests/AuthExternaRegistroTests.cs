using System.Net;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class AuthExternaRegistroTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Google_verificado_crea_usuario_cliente_asociado_y_sesion()
    {
        var email = $"google-{Guid.NewGuid():N}@caserito.test";
        var clave = $"clave-{Guid.NewGuid():N}";
        using var cliente = factory.CreateClient();
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/auth/external/complete")
        {
            Content = JsonContent.Create(new CompletarRegistroExternoRequest(null, "Lima", null)),
        };
        solicitud.Headers.Add("Cookie", CrearCookiePendiente("google", clave, email, true, "Nombre Google"));

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var scope = factory.Services.CreateScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await usuarios.FindByEmailAsync(email);
        Assert.NotNull(usuario);
        Assert.True(usuario!.EmailConfirmed);
        Assert.True(await usuarios.IsInRoleAsync(usuario, "Cliente"));
        Assert.NotNull(await usuarios.FindByLoginAsync("google", clave));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Facebook_con_o_sin_email_crea_usuario_no_confirmado(bool proveedorIncluyeEmail)
    {
        var email = $"facebook-{Guid.NewGuid():N}@caserito.test";
        var clave = $"clave-{Guid.NewGuid():N}";
        var cookie = CrearCookiePendiente(
            "facebook", clave, proveedorIncluyeEmail ? email : null, false, "Nombre Facebook");
        using var cliente = factory.CreateClient();
        using var solicitud = CrearSolicitud(
            new CompletarRegistroExternoRequest(proveedorIncluyeEmail ? null : email, "Lima", null), cookie);

        Assert.Equal(HttpStatusCode.OK, (await cliente.SendAsync(solicitud)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var usuario = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
        Assert.NotNull(usuario);
        Assert.False(usuario!.EmailConfirmed);
    }

    [Fact]
    public async Task Campos_invalidos_no_crean_usuario()
    {
        var clave = $"clave-{Guid.NewGuid():N}";
        var cookie = CrearCookiePendiente("facebook", clave, null, false, null);
        using var cliente = factory.CreateClient();
        using var solicitud = CrearSolicitud(new CompletarRegistroExternoRequest("invalido", "", ""), cookie);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByLoginAsync("facebook", clave));
    }

    [Fact]
    public async Task Repeticion_del_alta_es_idempotente()
    {
        var email = $"repetido-{Guid.NewGuid():N}@caserito.test";
        var clave = $"clave-{Guid.NewGuid():N}";
        var cookie = CrearCookiePendiente("google", clave, email, true, "Nombre");
        using var cliente = factory.CreateClient();
        for (var intento = 0; intento < 2; intento++)
        {
            using var solicitud = CrearSolicitud(new CompletarRegistroExternoRequest(null, "Lima", null), cookie);
            Assert.Equal(HttpStatusCode.OK, (await cliente.SendAsync(solicitud)).StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var emailNormalizado = email.ToUpperInvariant();
        Assert.Equal(1, db.Users.Count(usuario => usuario.NormalizedEmail == emailNormalizado));
        Assert.Equal(1, db.UserLogins.Count(login => login.LoginProvider == "google" && login.ProviderKey == clave));
    }

    private static HttpRequestMessage CrearSolicitud(CompletarRegistroExternoRequest request, string cookie)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/auth/external/complete")
        {
            Content = JsonContent.Create(request),
        };
        solicitud.Headers.Add("Cookie", cookie);
        return solicitud;
    }

    private string CrearCookiePendiente(
        string proveedor,
        string clave,
        string? email,
        bool confiable,
        string? nombre)
    {
        using var scope = factory.Services.CreateScope();
        var gestor = scope.ServiceProvider.GetRequiredService<IGestorLoginExternoPendiente>();
        var ticket = gestor.Proteger(new LoginExternoPendiente(
            proveedor, clave, email, confiable, nombre, "/perfil", DateTimeOffset.UtcNow.AddMinutes(5)));
        return $"{GestorLoginExternoPendienteDataProtector.NombreCookie}={ticket}";
    }
}
