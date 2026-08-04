using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class AuthExternaVinculacionTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Email_coincidente_solo_se_vincula_tras_autenticar_al_dueno()
    {
        var email = $"dueno-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";
        var usuario = await CrearUsuarioAsync(email, password);
        var clave = $"google-{Guid.NewGuid():N}";
        var cookie = CrearCookiePendiente("google", clave, email);
        using var cliente = factory.CreateClient();
        using var completar = CrearPost("/api/auth/external/complete", cookie,
            new CompletarRegistroExternoRequest(null, "Lima", null));

        Assert.Equal(HttpStatusCode.Conflict, (await cliente.SendAsync(completar)).StatusCode);
        Assert.Null(await BuscarLoginAsync("google", clave));

        var token = await IniciarSesionAsync(cliente, email, password);
        using var vincular = CrearPost("/api/auth/external/link", cookie);
        vincular.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(vincular)).StatusCode);
        Assert.Equal(usuario.Id, (await BuscarLoginAsync("google", clave))?.Id);
    }

    [Fact]
    public async Task Usuario_distinto_no_puede_vincular_y_el_ticket_se_invalida()
    {
        var emailDueno = $"dueno-{Guid.NewGuid():N}@caserito.test";
        var emailAjeno = $"ajeno-{Guid.NewGuid():N}@caserito.test";
        await CrearUsuarioAsync(emailDueno, "Password123!");
        await CrearUsuarioAsync(emailAjeno, "Password123!");
        var clave = $"facebook-{Guid.NewGuid():N}";
        var cookie = CrearCookiePendiente("facebook", clave, emailDueno);
        using var cliente = factory.CreateClient();
        var tokenAjeno = await IniciarSesionAsync(cliente, emailAjeno, "Password123!");

        using var solicitud = CrearPost("/api/auth/external/link", cookie);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenAjeno);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null(await BuscarLoginAsync("facebook", clave));
        Assert.Contains("loginExternoPendiente=", respuesta.Headers.GetValues("Set-Cookie").Single());
    }

    [Fact]
    public async Task Dueno_puede_vincular_google_y_facebook_a_la_misma_cuenta()
    {
        var email = $"dos-proveedores-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";
        var usuario = await CrearUsuarioAsync(email, password);
        using var cliente = factory.CreateClient();
        var token = await IniciarSesionAsync(cliente, email, password);

        foreach (var proveedor in new[] { "google", "facebook" })
        {
            var clave = $"{proveedor}-{Guid.NewGuid():N}";
            using var solicitud = CrearPost("/api/auth/external/link", CrearCookiePendiente(proveedor, clave, email));
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(solicitud)).StatusCode);
            Assert.Equal(usuario.Id, (await BuscarLoginAsync(proveedor, clave))?.Id);
        }
    }

    [Fact]
    public async Task Identidad_vinculada_a_otra_cuenta_no_se_mueve()
    {
        var emailSolicitante = $"solicitante-{Guid.NewGuid():N}@caserito.test";
        var emailTitular = $"titular-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";
        var solicitante = await CrearUsuarioAsync(emailSolicitante, password);
        var titular = await CrearUsuarioAsync(emailTitular, password);
        var clave = $"google-{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var titularActual = await usuarios.FindByIdAsync(titular.Id.ToString());
            Assert.NotNull(titularActual);
            Assert.True((await usuarios.AddLoginAsync(
                titularActual!, new UserLoginInfo("google", clave, "google"))).Succeeded);
        }

        using var cliente = factory.CreateClient();
        var token = await IniciarSesionAsync(cliente, emailSolicitante, password);
        using var solicitud = CrearPost(
            "/api/auth/external/link", CrearCookiePendiente("google", clave, emailSolicitante));
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.SendAsync(solicitud)).StatusCode);
        Assert.Equal(titular.Id, (await BuscarLoginAsync("google", clave))?.Id);
        Assert.NotEqual(solicitante.Id, (await BuscarLoginAsync("google", clave))?.Id);
    }

    private async Task<ApplicationUser> CrearUsuarioAsync(string email, string password)
    {
        using var scope = factory.Services.CreateScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = new ApplicationUser { UserName = email, Email = email, Nombres = "Usuario", CiudadId = new Guid("22222222-2222-2222-2222-000000000001") };
        Assert.True((await usuarios.CreateAsync(usuario, password)).Succeeded);
        Assert.True((await usuarios.AddToRoleAsync(usuario, "Cliente")).Succeeded);
        return usuario;
    }

    private static async Task<string> IniciarSesionAsync(HttpClient cliente, string email, string password)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var body = await respuesta.Content.ReadFromJsonAsync<TokenAccesoResponse>();
        return body!.AccessToken;
    }

    private async Task<ApplicationUser?> BuscarLoginAsync(string proveedor, string clave)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByLoginAsync(proveedor, clave);
    }

    private string CrearCookiePendiente(string proveedor, string clave, string email)
    {
        using var scope = factory.Services.CreateScope();
        var gestor = scope.ServiceProvider.GetRequiredService<IGestorLoginExternoPendiente>();
        var ticket = gestor.Proteger(new LoginExternoPendiente(
            proveedor, clave, email, true, "Usuario", "/perfil", DateTimeOffset.UtcNow.AddMinutes(5)));
        return $"{GestorLoginExternoPendienteDataProtector.NombreCookie}={ticket}";
    }

    private static HttpRequestMessage CrearPost(string ruta, string cookie, object? body = null)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta);
        if (body is not null)
        {
            solicitud.Content = JsonContent.Create(body);
        }

        solicitud.Headers.Add("Cookie", cookie);
        return solicitud;
    }
}
