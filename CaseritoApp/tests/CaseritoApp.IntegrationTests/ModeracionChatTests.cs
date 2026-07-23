using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Chat.Application.Moderacion;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Domain.Moderacion;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class ModeracionChatTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Cola_sin_permiso_chat_moderar_devuelve_403()
    {
        using var cliente = factory.CreateClient();
        var usuario = await RegistrarAsync(cliente, "chat-moderacion-sin-permiso");
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get, "/api/admin/moderacion/chat/reportes");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", usuario.Token);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cola_administrativa_devuelve_referencias_sin_contenido_ni_participantes()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-cola", RolesApp.Moderador);
        var reporte = await CrearReporteAsync();
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get, "/api/admin/moderacion/chat/reportes?estado=Pendiente&limite=20");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cola = await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<ReporteChatColaDto>>();
        Assert.Contains(cola!, item => item.Id == reporte.Id);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(reporte.Detalle!, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(reporte.ReportanteId.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<UsuarioPrueba> RegistrarAsync(
        HttpClient cliente, string prefijo, string? rol = null)
    {
        var email = $"{prefijo}-{Guid.NewGuid():N}@caserito.test";
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register", new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        if (rol is not null)
        {
            Assert.True((await userManager.AddToRoleAsync(usuario!, rol)).Succeeded);
        }

        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        return new UsuarioPrueba(usuario!.Id, token);
    }

    private async Task<ReporteChat> CrearReporteAsync()
    {
        var compradorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, Guid.NewGuid(), DateTimeOffset.UtcNow).Valor;
        var reporte = ReporteChat.Crear(
            conversacion.Id, compradorId, TipoObjetivoReporteChat.Conversacion, null,
            CategoriaReporteChat.Acoso, "contenido reservado de la denuncia",
            DateTimeOffset.UtcNow).Valor;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        db.Conversaciones.Add(conversacion);
        db.Reportes.Add(reporte);
        await db.SaveChangesAsync();
        return reporte;
    }

    private sealed record UsuarioPrueba(Guid UsuarioId, string Token);
}
