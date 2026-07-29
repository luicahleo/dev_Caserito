using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Notificación por correo de la resolución humana del KYC: al aprobar o rechazar una
/// solicitud pendiente se publica <c>KycResuelto</c> y el handler envía el correo al usuario.
/// </summary>
public sealed class KycNotificacionTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    /// <summary>Sink de prueba: captura los correos en memoria; nunca envía a un relay real.</summary>
    private sealed class ServicioCorreoCapturador : IServicioCorreo
    {
        public ConcurrentQueue<MensajeCorreo> Enviados { get; } = new();

        public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
        {
            Enviados.Enqueue(mensaje);
            return Task.CompletedTask;
        }
    }

    private WebApplicationFactory<Program> FactoryConCorreo(ServicioCorreoCapturador capturador) =>
        factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IServicioCorreo>(capturador);
        }));

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

    /// <summary>Siembra una solicitud pendiente de revisión humana para el usuario dado.</summary>
    private async Task<Guid> SembrarSolicitudPendienteAsync(string emailUsuario)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(emailUsuario);

        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var verificacion = VerificacionKyc.Crear(usuario!.Id);
        var envio = verificacion.EnviarSolicitud("doc-0", "selfie-0", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
        db.VerificacionesKyc.Add(verificacion);
        await db.SaveChangesAsync();
        return envio.Valor.Id;
    }

    private static HttpRequestMessage Autorizada(HttpMethod metodo, string url, string token)
    {
        var solicitud = new HttpRequestMessage(metodo, url);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return solicitud;
    }

    [Fact]
    public async Task Aprobar_kyc_envia_correo_de_aprobacion_al_usuario()
    {
        var capturador = new ServicioCorreoCapturador();
        using var cliente = FactoryConCorreo(capturador).CreateClient();
        var emailUsuario = $"kyc-notif-ok-{Guid.NewGuid():N}@caserito.test";
        await RegistrarYLoguearAsync(cliente, emailUsuario, rolExtra: null);
        var solicitudId = await SembrarSolicitudPendienteAsync(emailUsuario);
        var tokenAdmin = await RegistrarYLoguearAsync(
            cliente, $"kyc-notif-admin-{Guid.NewGuid():N}@caserito.test", RolesApp.AdminPlataforma);

        using var aprobar = Autorizada(HttpMethod.Post, $"/api/admin/kyc/{solicitudId}/aprobar", tokenAdmin);
        var respuesta = await cliente.SendAsync(aprobar);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Contains(capturador.Enviados, m => m.Para == emailUsuario && m.Asunto.Contains("verificada"));
    }

    [Fact]
    public async Task Rechazar_kyc_envia_correo_con_motivo_al_usuario()
    {
        var capturador = new ServicioCorreoCapturador();
        using var cliente = FactoryConCorreo(capturador).CreateClient();
        var emailUsuario = $"kyc-notif-re-{Guid.NewGuid():N}@caserito.test";
        await RegistrarYLoguearAsync(cliente, emailUsuario, rolExtra: null);
        var solicitudId = await SembrarSolicitudPendienteAsync(emailUsuario);
        var tokenAdmin = await RegistrarYLoguearAsync(
            cliente, $"kyc-notif-admin-{Guid.NewGuid():N}@caserito.test", RolesApp.AdminPlataforma);

        using var rechazar = Autorizada(HttpMethod.Post, $"/api/admin/kyc/{solicitudId}/rechazar", tokenAdmin);
        rechazar.Content = JsonContent.Create(new RechazarKycRequest("Documento ilegible"));
        var respuesta = await cliente.SendAsync(rechazar);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Contains(capturador.Enviados, m => m.Para == emailUsuario && m.CuerpoTexto.Contains("Documento ilegible"));
    }
}
