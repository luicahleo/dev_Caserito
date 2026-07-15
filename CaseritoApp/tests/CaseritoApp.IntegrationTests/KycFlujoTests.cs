using System.IdentityModel.Tokens.Jwt;
using System.Linq;
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

/// <summary>Flujo completo de KYC: subir → listar (admin) → aprobar → badge/claim verificado.</summary>
public sealed class KycFlujoTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string Email(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

    private async Task<string> RegistrarYLoguearAsync(HttpClient cliente, string email, string? rolExtra)
    {
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register", new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
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
        return (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
    }

    private static MultipartFormDataContent Formulario()
    {
        var contenido = new MultipartFormDataContent();
        var doc = new ByteArrayContent(_png);
        doc.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        contenido.Add(doc, "documento", "ci.png");
        var selfie = new ByteArrayContent(_png);
        selfie.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        contenido.Add(selfie, "selfie", "selfie.png");
        return contenido;
    }

    private static HttpRequestMessage Autorizada(HttpMethod metodo, string url, string token)
    {
        var solicitud = new HttpRequestMessage(metodo, url);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return solicitud;
    }

    [Fact]
    public async Task Subir_aprobar_y_ver_verificado_en_perfil_y_token()
    {
        using var cliente = factory.CreateClient();
        var email = Email("kyc-user");
        var tokenUsuario = await RegistrarYLoguearAsync(cliente, email, rolExtra: null);
        var tokenAdmin = await RegistrarYLoguearAsync(cliente, Email("kyc-admin"), RolesApp.AdminKyc);

        // 1) Subir CI + selfie.
        using var subir = Autorizada(HttpMethod.Post, "/api/kyc/", tokenUsuario);
        subir.Content = Formulario();
        var respSubir = await cliente.SendAsync(subir);
        Assert.Equal(HttpStatusCode.NoContent, respSubir.StatusCode);

        // 2) Admin lista pendientes y localiza la solicitud del usuario.
        using var listar = Autorizada(HttpMethod.Get, "/api/admin/kyc/?estado=Pendiente&tamano=100", tokenAdmin);
        var pagina = await (await cliente.SendAsync(listar)).Content.ReadFromJsonAsync<PaginaKycResponse>();
        Assert.NotNull(pagina);

        Guid usuarioId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            usuarioId = (await userManager.FindByEmailAsync(email))!.Id;
        }
        var solicitud = pagina!.Items.Single(s => s.UsuarioId == usuarioId);

        // 3) Admin accede al blob (queda auditado) y aprueba.
        using var verDoc = Autorizada(HttpMethod.Get, $"/api/admin/kyc/{solicitud.SolicitudId}/documento", tokenAdmin);
        Assert.Equal(HttpStatusCode.OK, (await cliente.SendAsync(verDoc)).StatusCode);

        using var aprobar = Autorizada(HttpMethod.Post, $"/api/admin/kyc/{solicitud.SolicitudId}/aprobar", tokenAdmin);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(aprobar)).StatusCode);

        // 4) Perfil del usuario muestra verificado=true.
        using var perfil = Autorizada(HttpMethod.Get, "/api/perfil/", tokenUsuario);
        var perfilBody = await (await cliente.SendAsync(perfil)).Content.ReadFromJsonAsync<PerfilResponse>();
        Assert.True(perfilBody!.Verificado);

        // 5) Al re-loguear, el JWT trae claim verificado=true.
        var relogin = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        var tokenNuevo = (await relogin.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenNuevo);
        Assert.Equal("true", jwt.Claims.Single(c => c.Type == "verificado").Value);
    }

    [Fact]
    public async Task Subir_dos_veces_sin_resolver_da_409()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("kyc-dup"), rolExtra: null);

        using var primera = Autorizada(HttpMethod.Post, "/api/kyc/", token);
        primera.Content = Formulario();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(primera)).StatusCode);

        using var segunda = Autorizada(HttpMethod.Post, "/api/kyc/", token);
        segunda.Content = Formulario();
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.SendAsync(segunda)).StatusCode);
    }

    [Fact]
    public async Task Listar_kyc_sin_permiso_da_403()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("kyc-noperm"), rolExtra: null);

        using var listar = Autorizada(HttpMethod.Get, "/api/admin/kyc/", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.SendAsync(listar)).StatusCode);
    }
}

sealed file record PerfilResponse(Guid Id, string Email, string Nombre, string Ciudad, bool Verificado);

sealed file record SolicitudKycResponse(
    Guid SolicitudId, Guid UsuarioId, string Estado, string TipoDocumento, DateTimeOffset EnviadaEn, DateTimeOffset? ResueltaEn);

sealed file record PaginaKycResponse(SolicitudKycResponse[] Items, int Pagina, int Tamano, int Total);
