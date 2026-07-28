using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Flujo completo de KYC automático con ARGOS mockeado vía <see cref="IVerificadorIdentidadArgos"/>.</summary>
public sealed class KycArgosFlujoTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string Email(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

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

    [Fact]
    public async Task Subir_con_coincidencia_automatica_aprueba_y_marca_verificado()
    {
        using var cliente = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ =>
                new VerificadorArgosEstatico(Result.Exito(new VerificacionFacialResultado(true, 91.2, null))));
        })).CreateClient();

        var email = Email("kyc-ok");
        var token = await RegistrarYLoguearAsync(cliente, email, null);

        using var subir = Autorizada(HttpMethod.Post, "/api/kyc/", token);
        subir.Content = Formulario();
        var resp = await cliente.SendAsync(subir);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var estado = Autorizada(HttpMethod.Get, "/api/kyc/estado", token);
        var dto = await (await cliente.SendAsync(estado)).Content.ReadFromJsonAsync<EstadoKycDto>();
        Assert.Equal("Aprobada", dto!.Estado);
    }

    [Fact]
    public async Task Subir_sin_coincidencia_automatica_rechaza()
    {
        using var cliente = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ =>
                new VerificadorArgosEstatico(Result.Exito(new VerificacionFacialResultado(false, 21.0, "Rostros no coinciden"))));
        })).CreateClient();

        var email = Email("kyc-fail");
        var token = await RegistrarYLoguearAsync(cliente, email, null);

        using var subir = Autorizada(HttpMethod.Post, "/api/kyc/", token);
        subir.Content = Formulario();
        var resp = await cliente.SendAsync(subir);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var estado = Autorizada(HttpMethod.Get, "/api/kyc/estado", token);
        var dto = await (await cliente.SendAsync(estado)).Content.ReadFromJsonAsync<EstadoKycDto>();
        Assert.Equal("Rechazada", dto!.Estado);
        Assert.Equal("Rostros no coinciden", dto.MotivoRechazo);
    }

    [Fact]
    public async Task Subir_con_argos_caido_devuelve_503()
    {
        using var cliente = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ =>
                new VerificadorArgosEstatico(Result.Fallo<VerificacionFacialResultado>(
                    new Error(ErroresKyc.ServicioVerificacionNoDisponible, "No disponible"))));
        })).CreateClient();

        var email = Email("kyc-down");
        var token = await RegistrarYLoguearAsync(cliente, email, null);

        using var subir = Autorizada(HttpMethod.Post, "/api/kyc/", token);
        subir.Content = Formulario();
        var resp = await cliente.SendAsync(subir);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
    }

    private sealed class VerificadorArgosEstatico(Result<VerificacionFacialResultado> resultado) : IVerificadorIdentidadArgos
    {
        public Task<Result<VerificacionFacialResultado>> VerificarAsync(
            byte[] imagenDocumento, byte[] imagenSelfie, CancellationToken ct) =>
            Task.FromResult(resultado);
    }
}
