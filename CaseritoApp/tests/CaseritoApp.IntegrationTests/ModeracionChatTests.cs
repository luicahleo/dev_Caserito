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
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task Tomar_y_liberar_reporte_actualiza_cola_y_auditoria()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-tomar-liberar", RolesApp.Moderador);
        var reporte = await CrearReporteAsync();

        using var tomar = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/moderacion/chat/reportes/{reporte.Id}/tomar");
        tomar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);
        var respuestaTomar = await cliente.SendAsync(tomar);

        Assert.Equal(HttpStatusCode.NoContent, respuestaTomar.StatusCode);
        Assert.Empty(await respuestaTomar.Content.ReadAsStringAsync());
        await ComprobarEstadoYAuditoriaAsync(
            reporte.Id,
            EstadoReporteChat.EnRevision,
            moderador.UsuarioId,
            [AccionModeracionChat.Tomar]);
        await ComprobarColaSinContenidoNiParticipantesAsync(
            cliente, moderador.Token, reporte, EstadoReporteChat.EnRevision);

        using var liberar = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/moderacion/chat/reportes/{reporte.Id}/liberar");
        liberar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);
        var respuestaLiberar = await cliente.SendAsync(liberar);

        Assert.Equal(HttpStatusCode.NoContent, respuestaLiberar.StatusCode);
        Assert.Empty(await respuestaLiberar.Content.ReadAsStringAsync());
        await ComprobarEstadoYAuditoriaAsync(
            reporte.Id,
            EstadoReporteChat.Pendiente,
            null,
            [AccionModeracionChat.Tomar, AccionModeracionChat.Liberar]);
        await ComprobarColaSinContenidoNiParticipantesAsync(
            cliente, moderador.Token, reporte, EstadoReporteChat.Pendiente);
    }

    [Fact]
    public async Task Consultar_evidencia_audita_antes_de_devolver_contenido()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-evidencia", RolesApp.Moderador);
        var reporte = await CrearReporteAsync();
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get, $"/api/admin/moderacion/chat/reportes/{reporte.Id}/evidencia");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var evidencia = await respuesta.Content.ReadFromJsonAsync<EvidenciaReporteChatDto>();
        Assert.Equal(reporte.Id, evidencia!.ReporteId);
        Assert.Equal(reporte.Detalle, evidencia.Detalle);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(reporte.ReportanteId.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var auditoria = await db.RegistrosModeracion.AsNoTracking()
            .Where(r => r.ReporteId == reporte.Id)
            .ToListAsync();
        var registro = Assert.Single(auditoria);
        Assert.Equal(AccionModeracionChat.ConsultarEvidencia, registro.Accion);
        Assert.Equal(moderador.UsuarioId, registro.ModeradorId);
    }

    private async Task ComprobarEstadoYAuditoriaAsync(
        Guid reporteId,
        EstadoReporteChat estado,
        Guid? moderadorAsignadoId,
        IReadOnlyList<AccionModeracionChat> acciones)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var persistido = await db.Reportes.AsNoTracking().SingleAsync(r => r.Id == reporteId);
        Assert.Equal(estado, persistido.Estado);
        Assert.Equal(moderadorAsignadoId, persistido.ModeradorAsignadoId);
        var auditoria = await db.RegistrosModeracion.AsNoTracking()
            .Where(r => r.ReporteId == reporteId)
            .OrderBy(r => r.CreadoEn)
            .Select(r => r.Accion)
            .ToListAsync();
        Assert.Equal(acciones, auditoria);
    }

    private static async Task ComprobarColaSinContenidoNiParticipantesAsync(
        HttpClient cliente,
        string token,
        ReporteChat reporte,
        EstadoReporteChat estado)
    {
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get, $"/api/admin/moderacion/chat/reportes?estado={estado}&limite=20");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cola = await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<ReporteChatColaDto>>();
        Assert.Contains(cola!, item => item.Id == reporte.Id && item.Estado == estado);
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
