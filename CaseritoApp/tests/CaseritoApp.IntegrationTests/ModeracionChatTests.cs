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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    public async Task Reporte_solo_puede_ser_tomado_por_un_moderador_concurrente()
    {
        using var cliente = factory.CreateClient();
        var primero = await RegistrarAsync(cliente, "chat-moderacion-toma-primero", RolesApp.Moderador);
        var segundo = await RegistrarAsync(cliente, "chat-moderacion-toma-segundo", RolesApp.Moderador);
        var reporte = await CrearReporteAsync();

        var respuestas = await Task.WhenAll(
            EnviarAccionAsync(cliente, primero.Token, reporte.Id, "tomar"),
            EnviarAccionAsync(cliente, segundo.Token, reporte.Id, "tomar"));

        Assert.Single(respuestas, respuesta => respuesta.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(respuestas, respuesta => respuesta.StatusCode == HttpStatusCode.Conflict);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var persistido = await db.Reportes.AsNoTracking().SingleAsync(r => r.Id == reporte.Id);
        Assert.Equal(EstadoReporteChat.EnRevision, persistido.Estado);
        Assert.True(
            persistido.ModeradorAsignadoId == primero.UsuarioId ||
            persistido.ModeradorAsignadoId == segundo.UsuarioId);
        Assert.Equal(
            1,
            await db.RegistrosModeracion.AsNoTracking().CountAsync(
                r => r.ReporteId == reporte.Id && r.Accion == AccionModeracionChat.Tomar));
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

    [Fact]
    public async Task Evidencia_de_mensaje_incluye_hasta_cinco_anteriores_y_cinco_posteriores()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-ventana-mensaje", RolesApp.Moderador);
        var (reporte, mensajes) = await CrearReporteConMensajesAsync(
            TipoObjetivoReporteChat.Mensaje, 13, 7);
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get, $"/api/admin/moderacion/chat/reportes/{reporte.Id}/evidencia");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var evidencia = (await respuesta.Content.ReadFromJsonAsync<EvidenciaReporteChatDto>())!;
        Assert.Equal(11, evidencia.Mensajes.Count);
        Assert.Equal(
            mensajes.Skip(1).Take(11).Select(m => m.Id),
            evidencia.Mensajes.Select(m => m.Id));
        Assert.Equal(7, Assert.Single(evidencia.Mensajes, m => m.EsObjetivo).Secuencia);
    }

    [Fact]
    public async Task Evidencia_de_conversacion_incluye_ultimos_diez_sin_participantes()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-ventana-conversacion", RolesApp.Moderador);
        var (reporte, mensajes) = await CrearReporteConMensajesAsync(
            TipoObjetivoReporteChat.Conversacion, 13);
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get, $"/api/admin/moderacion/chat/reportes/{reporte.Id}/evidencia");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var evidencia = (await respuesta.Content.ReadFromJsonAsync<EvidenciaReporteChatDto>())!;
        Assert.Equal(mensajes.Skip(3).Select(m => m.Id), evidencia.Mensajes.Select(m => m.Id));
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        using var scope = factory.Services.CreateScope();
        var conversacion = await scope.ServiceProvider.GetRequiredService<ChatDbContext>()
            .Conversaciones.AsNoTracking().SingleAsync(c => c.Id == reporte.ConversacionId);
        Assert.DoesNotContain(conversacion.CompradorId.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(conversacion.VendedorId.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evidencia_de_participante_incluye_ultimos_diez_con_roles_relativos()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-ventana-participante", RolesApp.Moderador);
        var (reporte, mensajes) = await CrearReporteConMensajesAsync(
            TipoObjetivoReporteChat.Participante, 13);
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get, $"/api/admin/moderacion/chat/reportes/{reporte.Id}/evidencia");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var evidencia = (await respuesta.Content.ReadFromJsonAsync<EvidenciaReporteChatDto>())!;
        Assert.Equal(mensajes.Skip(3).Select(m => m.Id), evidencia.Mensajes.Select(m => m.Id));
        Assert.Equal("Comprador", evidencia.RolReportante);
        Assert.Equal("Vendedor", evidencia.RolObjetivo);
        Assert.All(evidencia.Mensajes, mensaje =>
            Assert.True(mensaje.AutorRol is "Comprador" or "Vendedor"));
    }

    [Fact]
    public async Task Evidencia_no_se_devuelve_cuando_falla_la_auditoria()
    {
        await using var hostSinAuditoria = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(servicios =>
            {
                servicios.RemoveAll<IAuditorModeracionChat>();
                servicios.AddSingleton<IAuditorModeracionChat, AuditorQueFalla>();
            }));
        using var cliente = hostSinAuditoria.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-auditoria-fallida", RolesApp.Moderador);
        var (reporte, _) = await CrearReporteConMensajesAsync(
            TipoObjetivoReporteChat.Conversacion, 3);

        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get, $"/api/admin/moderacion/chat/reportes/{reporte.Id}/evidencia");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(reporte.Detalle!, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("mensaje 1", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(reporte.Id.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Atender_y_descartar_actualizan_estado_y_auditoria()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-resolver", RolesApp.Moderador);
        var reporteAtendido = await CrearReporteAsync();
        var reporteDescartado = await CrearReporteAsync();
        await TomarReporteAsync(cliente, moderador.Token, reporteAtendido.Id);
        await TomarReporteAsync(cliente, moderador.Token, reporteDescartado.Id);

        using var atender = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/moderacion/chat/reportes/{reporteAtendido.Id}/atender")
        {
            Content = JsonContent.Create(new { cerrarConversacion = false }),
        };
        atender.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);
        var respuestaAtender = await cliente.SendAsync(atender);

        Assert.Equal(HttpStatusCode.NoContent, respuestaAtender.StatusCode);
        Assert.Empty(await respuestaAtender.Content.ReadAsStringAsync());
        await ComprobarEstadoYAuditoriaAsync(
            reporteAtendido.Id,
            EstadoReporteChat.Atendido,
            moderador.UsuarioId,
            [AccionModeracionChat.Tomar, AccionModeracionChat.Atender]);
        await ComprobarColaSinContenidoNiParticipantesAsync(
            cliente, moderador.Token, reporteAtendido, EstadoReporteChat.Atendido);

        using var descartar = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/moderacion/chat/reportes/{reporteDescartado.Id}/descartar");
        descartar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);
        var respuestaDescartar = await cliente.SendAsync(descartar);

        Assert.Equal(HttpStatusCode.NoContent, respuestaDescartar.StatusCode);
        Assert.Empty(await respuestaDescartar.Content.ReadAsStringAsync());
        await ComprobarEstadoYAuditoriaAsync(
            reporteDescartado.Id,
            EstadoReporteChat.Descartado,
            moderador.UsuarioId,
            [AccionModeracionChat.Tomar, AccionModeracionChat.Descartar]);
        await ComprobarColaSinContenidoNiParticipantesAsync(
            cliente, moderador.Token, reporteDescartado, EstadoReporteChat.Descartado);
    }

    [Fact]
    public async Task Atender_con_cierre_confirma_reporte_conversacion_y_auditoria_juntos()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-atender-cierre", RolesApp.Moderador);
        var reporte = await CrearReporteAsync();
        await TomarReporteAsync(cliente, moderador.Token, reporte.Id);
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/moderacion/chat/reportes/{reporte.Id}/atender")
        {
            Content = JsonContent.Create(new { cerrarConversacion = true }),
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var persistido = await db.Reportes.AsNoTracking().SingleAsync(r => r.Id == reporte.Id);
        var conversacion = await db.Conversaciones.AsNoTracking()
            .SingleAsync(c => c.Id == reporte.ConversacionId);
        var acciones = await db.RegistrosModeracion.AsNoTracking()
            .Where(r => r.ReporteId == reporte.Id)
            .OrderBy(r => r.CreadoEn)
            .Select(r => r.Accion)
            .ToListAsync();
        Assert.Equal(EstadoReporteChat.Atendido, persistido.Estado);
        Assert.Equal(EstadoConversacion.CerradaPorModeracion, conversacion.Estado);
        Assert.Equal(3, acciones.Count);
        Assert.Contains(AccionModeracionChat.Tomar, acciones);
        Assert.Contains(AccionModeracionChat.CerrarConversacion, acciones);
        Assert.Contains(AccionModeracionChat.Atender, acciones);
    }

    [Fact]
    public async Task Descartar_reporte_no_cierra_la_conversacion()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-descartar-activa", RolesApp.Moderador);
        var reporte = await CrearReporteAsync();
        await TomarReporteAsync(cliente, moderador.Token, reporte.Id);

        var respuesta = await EnviarAccionAsync(
            cliente, moderador.Token, reporte.Id, "descartar");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var persistido = await db.Reportes.AsNoTracking().SingleAsync(r => r.Id == reporte.Id);
        var conversacion = await db.Conversaciones.AsNoTracking()
            .SingleAsync(c => c.Id == reporte.ConversacionId);
        Assert.Equal(EstadoReporteChat.Descartado, persistido.Estado);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
        Assert.DoesNotContain(
            await db.RegistrosModeracion.AsNoTracking()
                .Where(r => r.ReporteId == reporte.Id)
                .Select(r => r.Accion)
                .ToListAsync(),
            accion => accion == AccionModeracionChat.CerrarConversacion);
    }

    [Fact]
    public async Task Cerrar_y_reabrir_por_moderacion_actualizan_conversacion_y_auditoria()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-cierre", RolesApp.Moderador);
        var reporte = await CrearReporteAsync();
        await TomarReporteAsync(cliente, moderador.Token, reporte.Id);

        using var cerrar = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/admin/moderacion/chat/reportes/{reporte.Id}/cierre-conversacion");
        cerrar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);
        var respuestaCerrar = await cliente.SendAsync(cerrar);

        Assert.Equal(HttpStatusCode.NoContent, respuestaCerrar.StatusCode);
        Assert.Empty(await respuestaCerrar.Content.ReadAsStringAsync());
        await ComprobarEstadoConversacionYAuditoriaAsync(
            reporte,
            EstadoConversacion.CerradaPorModeracion,
            [AccionModeracionChat.Tomar, AccionModeracionChat.CerrarConversacion]);

        using var reabrir = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/admin/moderacion/chat/reportes/{reporte.Id}/cierre-conversacion");
        reabrir.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);
        var respuestaReabrir = await cliente.SendAsync(reabrir);

        Assert.Equal(HttpStatusCode.NoContent, respuestaReabrir.StatusCode);
        Assert.Empty(await respuestaReabrir.Content.ReadAsStringAsync());
        await ComprobarEstadoConversacionYAuditoriaAsync(
            reporte,
            EstadoConversacion.Activa,
            [
                AccionModeracionChat.Tomar,
                AccionModeracionChat.CerrarConversacion,
                AccionModeracionChat.ReabrirConversacion,
            ]);
    }

    [Fact]
    public async Task Resolver_reporte_asignado_a_otro_moderador_devuelve_409_generico()
    {
        using var cliente = factory.CreateClient();
        var asignado = await RegistrarAsync(cliente, "chat-moderacion-asignado", RolesApp.Moderador);
        var otro = await RegistrarAsync(cliente, "chat-moderacion-otro", RolesApp.Moderador);
        var reporte = await CrearReporteAsync();
        await TomarReporteAsync(cliente, asignado.Token, reporte.Id);
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/moderacion/chat/reportes/{reporte.Id}/descartar");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otro.Token);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(reporte.Id.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(reporte.Detalle!, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(asignado.UsuarioId.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Accion_sobre_reporte_inexistente_devuelve_404_generico()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-inexistente", RolesApp.Moderador);
        var reporteId = Guid.NewGuid();
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/moderacion/chat/reportes/{reporteId}/tomar");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);

        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(reporteId.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Limite_de_acciones_administrativas_devuelve_429_en_la_solicitud_once()
    {
        using var cliente = factory.CreateClient();
        var moderador = await RegistrarAsync(cliente, "chat-moderacion-limite", RolesApp.Moderador);
        var estados = new List<HttpStatusCode>();

        for (var i = 0; i < 11; i++)
        {
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/admin/moderacion/chat/reportes/{Guid.NewGuid()}/tomar");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderador.Token);
            estados.Add((await cliente.SendAsync(solicitud)).StatusCode);
        }

        Assert.All(estados.Take(10), estado => Assert.Equal(HttpStatusCode.NotFound, estado));
        Assert.Equal(HttpStatusCode.TooManyRequests, estados[10]);
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

    private static async Task TomarReporteAsync(HttpClient cliente, string token, Guid reporteId)
    {
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/moderacion/chat/reportes/{reporteId}/tomar");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await cliente.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    private static async Task<HttpResponseMessage> EnviarAccionAsync(
        HttpClient cliente,
        string token,
        Guid reporteId,
        string accion)
    {
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/moderacion/chat/reportes/{reporteId}/{accion}");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await cliente.SendAsync(solicitud);
    }

    private async Task ComprobarEstadoConversacionYAuditoriaAsync(
        ReporteChat reporte,
        EstadoConversacion estado,
        IReadOnlyList<AccionModeracionChat> acciones)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var conversacion = await db.Conversaciones.AsNoTracking()
            .SingleAsync(c => c.Id == reporte.ConversacionId);
        Assert.Equal(estado, conversacion.Estado);
        var auditoria = await db.RegistrosModeracion.AsNoTracking()
            .Where(r => r.ReporteId == reporte.Id)
            .OrderBy(r => r.CreadoEn)
            .Select(r => r.Accion)
            .ToListAsync();
        Assert.Equal(acciones, auditoria);
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

    private async Task<(ReporteChat Reporte, IReadOnlyList<Mensaje> Mensajes)>
        CrearReporteConMensajesAsync(
            TipoObjetivoReporteChat tipoObjetivo,
            int cantidad,
            long? secuenciaObjetivo = null)
    {
        var compradorId = Guid.NewGuid();
        var vendedorId = Guid.NewGuid();
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), compradorId, vendedorId, DateTimeOffset.UtcNow).Valor;
        var mensajes = Enumerable.Range(1, cantidad)
            .Select(secuencia => conversacion.CrearMensaje(
                secuencia % 2 == 0 ? vendedorId : compradorId,
                Guid.NewGuid(),
                secuencia,
                $"mensaje {secuencia}",
                DateTimeOffset.UtcNow.AddSeconds(secuencia)).Valor)
            .ToArray();
        var mensajeId = secuenciaObjetivo.HasValue
            ? mensajes.Single(m => m.Secuencia == secuenciaObjetivo.Value).Id
            : (Guid?)null;
        var reporte = ReporteChat.Crear(
            conversacion.Id,
            compradorId,
            tipoObjetivo,
            mensajeId,
            CategoriaReporteChat.Acoso,
            "detalle reservado",
            DateTimeOffset.UtcNow).Valor;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        db.Conversaciones.Add(conversacion);
        db.Mensajes.AddRange(mensajes);
        db.Reportes.Add(reporte);
        await db.SaveChangesAsync();
        return (reporte, mensajes);
    }

    private sealed record UsuarioPrueba(Guid UsuarioId, string Token);

    private sealed class AuditorQueFalla : IAuditorModeracionChat
    {
        public Task<bool> RegistrarConsultaEvidenciaAsync(
            Guid reporteId,
            Guid conversacionId,
            Guid moderadorId,
            DateTimeOffset fecha,
            CancellationToken ct) =>
            Task.FromResult(false);
    }
}
