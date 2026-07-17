using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Concurrencia optimista del KYC: token incremental (int, IsConcurrencyToken) en la raíz + mapeo a 409.</summary>
public sealed class KycConcurrenciaTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string Email(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

    // Mecanismo: dos aprobaciones concurrentes del mismo agregado → la segunda pierde la carrera.
    [Fact]
    public async Task Dos_aprobaciones_concurrentes_del_mismo_agregado_la_segunda_lanza_concurrencia()
    {
        var usuarioId = Guid.NewGuid();
        Guid solicitudId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var verificacion = VerificacionKyc.Crear(usuarioId);
            var envio = verificacion.EnviarSolicitud("doc-0", "selfie-0", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
            solicitudId = envio.Valor.Id;
            db.VerificacionesKyc.Add(verificacion);
            await db.SaveChangesAsync();
        }

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var verA = await dbA.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);
        var verB = await dbB.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);

        verA.Aprobar(solicitudId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        verB.Aprobar(solicitudId, Guid.NewGuid(), DateTimeOffset.UtcNow);

        await dbA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    // Mecanismo: dos solicitudes nuevas concurrentes sobre un agregado existente (tras rechazo) →
    // la segunda pierde. Valida que "añadir una hija" también bumpea la raíz (touch-root).
    [Fact]
    public async Task Dos_solicitudes_concurrentes_sobre_agregado_existente_la_segunda_lanza_concurrencia()
    {
        var usuarioId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var verificacion = VerificacionKyc.Crear(usuarioId);
            var envio = verificacion.EnviarSolicitud("doc-0", "selfie-0", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
            verificacion.Rechazar(envio.Valor.Id, Guid.NewGuid(), "ilegible", DateTimeOffset.UtcNow);
            db.VerificacionesKyc.Add(verificacion);
            await db.SaveChangesAsync();
        }

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var verA = await dbA.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);
        var verB = await dbB.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);

        verA.EnviarSolicitud("doc-a", "selfie-a", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
        verB.EnviarSolicitud("doc-b", "selfie-b", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);

        await dbA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    // Surfacing end-to-end: dos aprobaciones concurrentes por HTTP → exactamente una 204 y una 409.
    [Fact]
    public async Task Dos_aprobaciones_concurrentes_por_http_una_204_y_una_409()
    {
        using var cliente = factory.CreateClient();
        var email = Email("kyc-conc");
        var tokenUsuario = await RegistrarYLoguearAsync(cliente, email, rolExtra: null);
        var tokenAdmin = await RegistrarYLoguearAsync(cliente, Email("kyc-conc-admin"), RolesApp.AdminKyc);

        using var subir = Autorizada(HttpMethod.Post, "/api/kyc/", tokenUsuario);
        subir.Content = Formulario();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subir)).StatusCode);

        using var listar = Autorizada(HttpMethod.Get, "/api/admin/kyc/?estado=Pendiente&tamano=100", tokenAdmin);
        var pagina = await (await cliente.SendAsync(listar)).Content.ReadFromJsonAsync<PaginaKycResponse>();

        Guid usuarioId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            usuarioId = (await userManager.FindByEmailAsync(email))!.Id;
        }
        var solicitudId = pagina!.Items.Single(s => s.UsuarioId == usuarioId).SolicitudId;

        var url = $"/api/admin/kyc/{solicitudId}/aprobar";
        var respuestas = await Task.WhenAll(
            cliente.SendAsync(Autorizada(HttpMethod.Post, url, tokenAdmin)),
            cliente.SendAsync(Autorizada(HttpMethod.Post, url, tokenAdmin)));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
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
}

// Copias file-scoped de los DTO de deserialización (los de KycFlujoTests son `file`, no compartibles).
sealed file record SolicitudKycResponse(
    Guid SolicitudId, Guid UsuarioId, string Estado, string TipoDocumento, DateTimeOffset EnviadaEn, DateTimeOffset? ResueltaEn);

sealed file record PaginaKycResponse(SolicitudKycResponse[] Items, int Pagina, int Tamano, int Total);
