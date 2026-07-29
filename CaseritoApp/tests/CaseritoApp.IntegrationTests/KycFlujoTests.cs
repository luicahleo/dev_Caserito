using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Flujo completo de KYC automático: subir → verificado en perfil y token.</summary>
public sealed class KycFlujoTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string Email(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

    private async Task<string> RegistrarYLoguearAsync(HttpClient cliente, string email, string? rolExtra)
    {
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register", new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        // Los flujos de KYC y avisos exigen correo confirmado (policy EmailConfirmado).
        usuario!.EmailConfirmed = true;
        await userManager.UpdateAsync(usuario);

        if (rolExtra is not null)
        {
            await userManager.AddToRoleAsync(usuario, rolExtra);
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

    private WebApplicationFactory<Program> FactoryConVerificador(Result<VerificacionFacialResultado> resultado) =>
        factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ => new VerificadorArgosEstatico(resultado));
        }));

    [Fact]
    public async Task Subir_con_aprobacion_automatica_verifica_perfil_y_token()
    {
        using var cliente = FactoryConVerificador(
            Result.Exito(new VerificacionFacialResultado(true, 94.5, null))).CreateClient();
        var email = Email("kyc-user");
        var tokenUsuario = await RegistrarYLoguearAsync(cliente, email, rolExtra: null);

        // 1) Subir CI + selfie; ARGOS mockeado aprueba automáticamente.
        using var subir = Autorizada(HttpMethod.Post, "/api/kyc/", tokenUsuario);
        subir.Content = Formulario();
        var respSubir = await cliente.SendAsync(subir);
        Assert.Equal(HttpStatusCode.NoContent, respSubir.StatusCode);

        // 2) Perfil del usuario muestra verificado=true.
        using var perfil = Autorizada(HttpMethod.Get, "/api/perfil/", tokenUsuario);
        var perfilBody = await (await cliente.SendAsync(perfil)).Content.ReadFromJsonAsync<PerfilResponse>();
        Assert.True(perfilBody!.Verificado);

        // 3) Al re-loguear, el JWT trae claim verificado=true.
        var relogin = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        var tokenNuevo = (await relogin.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenNuevo);
        Assert.Equal("true", jwt.Claims.Single(c => c.Type == "verificado").Value);
    }

    [Fact]
    public async Task Subir_dos_veces_da_409_por_ya_verificado()
    {
        using var cliente = FactoryConVerificador(
            Result.Exito(new VerificacionFacialResultado(true, 94.5, null))).CreateClient();
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
        using var cliente = FactoryConVerificador(
            Result.Exito(new VerificacionFacialResultado(true, 94.5, null))).CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("kyc-noperm"), rolExtra: null);

        using var listar = Autorizada(HttpMethod.Get, "/api/admin/kyc/", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.SendAsync(listar)).StatusCode);
    }

    private sealed class VerificadorArgosEstatico(Result<VerificacionFacialResultado> resultado) : IVerificadorIdentidadArgos
    {
        public Task<Result<VerificacionFacialResultado>> VerificarAsync(
            byte[] imagenDocumento, byte[] imagenSelfie, CancellationToken ct) =>
            Task.FromResult(resultado);
    }
}

sealed file record PerfilResponse(Guid Id, string Email, string Nombre, string Ciudad, bool Verificado);
