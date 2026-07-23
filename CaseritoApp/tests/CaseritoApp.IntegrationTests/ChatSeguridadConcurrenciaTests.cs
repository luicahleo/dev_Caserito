using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Domain.Moderacion;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatSeguridadConcurrenciaTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Bloqueo_afecta_todas_las_conversaciones_compartidas_y_nuevo_inicio()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-bloqueo-global-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-bloqueo-global-vendedor");
        var avisos = await CrearAvisosAsync(vendedor.UsuarioId, 3);
        var primera = await IniciarAsync(cliente, comprador.Token, avisos[0].Id);
        var segunda = await IniciarAsync(cliente, comprador.Token, avisos[1].Id);
        var textoPrimero = $"contenido-privado-{Guid.NewGuid():N}";
        var textoSegundo = $"contenido-privado-{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.Created, await EnviarMensajeAsync(
            cliente, comprador.Token, primera.Id, textoPrimero));
        Assert.Equal(HttpStatusCode.Created, await EnviarMensajeAsync(
            cliente, vendedor.Token, segunda.Id, textoSegundo));

        var bloqueo = await EnviarAsync(
            cliente,
            HttpMethod.Put,
            $"/api/chat/conversaciones/{primera.Id}/bloqueo",
            comprador.Token);
        Assert.Equal(HttpStatusCode.NoContent, bloqueo.StatusCode);

        var envioPrimera = await EnviarMensajeRespuestaAsync(
            cliente, vendedor.Token, primera.Id, $"secreto-{Guid.NewGuid():N}");
        var envioSegunda = await EnviarMensajeRespuestaAsync(
            cliente, comprador.Token, segunda.Id, $"secreto-{Guid.NewGuid():N}");
        var nuevoInicio = await EnviarAsync(
            cliente,
            HttpMethod.Post,
            "/api/chat/conversaciones",
            comprador.Token,
            new IniciarConversacionRequest(avisos[2].Id));

        Assert.Equal(HttpStatusCode.Conflict, envioPrimera.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, envioSegunda.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, nuevoInicio.StatusCode);

        var problemas = await Task.WhenAll(
            LeerProblemaAsync(envioPrimera),
            LeerProblemaAsync(envioSegunda),
            LeerProblemaAsync(nuevoInicio));
        Assert.All(problemas, problema =>
        {
            Assert.Equal("La conversación no está disponible para enviar mensajes.", problema.Detail);
            Assert.DoesNotContain(primera.Id.ToString(), problema.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(segunda.Id.ToString(), problema.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(avisos[2].Id.ToString(), problema.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(comprador.UsuarioId.ToString(), problema.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(vendedor.UsuarioId.ToString(), problema.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(comprador.Token, problema.ToString(), StringComparison.Ordinal);
        });
        Assert.Single(problemas.Select(p => (p.Status, p.Title, p.Detail)).Distinct());

        var historialPrimera = await EnviarAsync(
            cliente,
            HttpMethod.Get,
            $"/api/chat/conversaciones/{primera.Id}/mensajes",
            comprador.Token);
        var historialSegunda = await EnviarAsync(
            cliente,
            HttpMethod.Get,
            $"/api/chat/conversaciones/{segunda.Id}/mensajes",
            vendedor.Token);
        Assert.Equal(HttpStatusCode.OK, historialPrimera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, historialSegunda.StatusCode);
        Assert.Contains(
            (await historialPrimera.Content.ReadFromJsonAsync<PaginaChatResponse<MensajeDto>>())!.Items,
            mensaje => mensaje.Texto == textoPrimero);
        Assert.Contains(
            (await historialSegunda.Content.ReadFromJsonAsync<PaginaChatResponse<MensajeDto>>())!.Items,
            mensaje => mensaje.Texto == textoSegundo);

        var listado = await EnviarAsync(
            cliente, HttpMethod.Get, "/api/chat/conversaciones", comprador.Token);
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var conversaciones =
            (await listado.Content.ReadFromJsonAsync<PaginaChatResponse<ConversacionResumenDto>>())!;
        Assert.Contains(conversaciones.Items, conversacion => conversacion.Id == primera.Id);
        Assert.Contains(conversaciones.Items, conversacion => conversacion.Id == segunda.Id);
    }

    [Fact]
    public async Task Carrera_bloquear_y_enviar_solo_confirma_un_orden_consistente()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-carrera-bloqueo-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-carrera-bloqueo-vendedor");
        var aviso = Assert.Single(await CrearAvisosAsync(vendedor.UsuarioId, 1));
        var conversacion = await IniciarAsync(cliente, comprador.Token, aviso.Id);

        var tareas = new[]
        {
            EnviarAsync(
                cliente,
                HttpMethod.Put,
                $"/api/chat/conversaciones/{conversacion.Id}/bloqueo",
                comprador.Token),
            EnviarMensajeRespuestaAsync(
                cliente,
                vendedor.Token,
                conversacion.Id,
                $"mensaje-carrera-{Guid.NewGuid():N}"),
        };
        var respuestas = await Task.WhenAll(tareas);

        Assert.Equal(HttpStatusCode.NoContent, respuestas[0].StatusCode);
        Assert.Contains(
            respuestas[1].StatusCode,
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });

        var envioPosterior = await EnviarMensajeRespuestaAsync(
            cliente,
            vendedor.Token,
            conversacion.Id,
            $"mensaje-posterior-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Conflict, envioPosterior.StatusCode);
    }

    [Fact]
    public async Task Carrera_cerrar_y_enviar_solo_confirma_un_orden_consistente()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-carrera-cierre-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-carrera-cierre-vendedor");
        var aviso = Assert.Single(await CrearAvisosAsync(vendedor.UsuarioId, 1));
        var conversacion = await IniciarAsync(cliente, comprador.Token, aviso.Id);

        var tareas = new[]
        {
            EnviarAsync(
                cliente,
                HttpMethod.Put,
                $"/api/chat/conversaciones/{conversacion.Id}/cierre",
                comprador.Token),
            EnviarMensajeRespuestaAsync(
                cliente,
                vendedor.Token,
                conversacion.Id,
                $"mensaje-carrera-{Guid.NewGuid():N}"),
        };
        var respuestas = await Task.WhenAll(tareas);

        Assert.Equal(HttpStatusCode.NoContent, respuestas[0].StatusCode);
        Assert.Contains(
            respuestas[1].StatusCode,
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });

        var envioPosterior = await EnviarMensajeRespuestaAsync(
            cliente,
            vendedor.Token,
            conversacion.Id,
            $"mensaje-posterior-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.Conflict, envioPosterior.StatusCode);
    }

    [Fact]
    public async Task Reporte_abierto_es_unico_ante_creacion_concurrente()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-reporte-unico-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-reporte-unico-vendedor");
        var aviso = Assert.Single(await CrearAvisosAsync(vendedor.UsuarioId, 1));
        var conversacion = await IniciarAsync(cliente, comprador.Token, aviso.Id);
        var reporte = new ReportarChatRequest(
            TipoObjetivoReporteChat.Conversacion,
            null,
            CategoriaReporteChat.Spam,
            null);

        var respuestas = await Task.WhenAll(
            EnviarAsync(
                cliente,
                HttpMethod.Post,
                $"/api/chat/conversaciones/{conversacion.Id}/reportes",
                comprador.Token,
                reporte),
            EnviarAsync(
                cliente,
                HttpMethod.Post,
                $"/api/chat/conversaciones/{conversacion.Id}/reportes",
                comprador.Token,
                reporte));

        Assert.Single(respuestas, respuesta => respuesta.StatusCode == HttpStatusCode.Created);
        Assert.Single(respuestas, respuesta => respuesta.StatusCode == HttpStatusCode.Conflict);
    }

    private async Task<UsuarioPrueba> RegistrarAsync(HttpClient cliente, string prefijo)
    {
        var email = $"{prefijo}-{Guid.NewGuid():N}@caserito.test";
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register", new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);
        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        return new UsuarioPrueba(usuario!.Id, token);
    }

    private async Task<IReadOnlyList<Aviso>> CrearAvisosAsync(Guid vendedorId, int cantidad)
    {
        var avisos = Enumerable.Range(0, cantidad)
            .Select(_ => Aviso.Crear(
                vendedorId,
                "Aviso chat",
                "Descripción",
                Dinero.Crear(100, Moneda.BOB).Valor,
                Guid.NewGuid(),
                Guid.NewGuid(),
                CondicionArticulo.Usado,
                DateTime.UtcNow))
            .ToArray();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        db.Avisos.AddRange(avisos);
        await db.SaveChangesAsync();
        return avisos;
    }

    private static async Task<ConversacionDto> IniciarAsync(
        HttpClient cliente,
        string token,
        Guid avisoId)
    {
        var respuesta = await EnviarAsync(
            cliente,
            HttpMethod.Post,
            "/api/chat/conversaciones",
            token,
            new IniciarConversacionRequest(avisoId));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<ConversacionDto>())!;
    }

    private static async Task<HttpStatusCode> EnviarMensajeAsync(
        HttpClient cliente,
        string token,
        Guid conversacionId,
        string texto)
    {
        using var respuesta = await EnviarMensajeRespuestaAsync(
            cliente, token, conversacionId, texto);
        return respuesta.StatusCode;
    }

    private static Task<HttpResponseMessage> EnviarMensajeRespuestaAsync(
        HttpClient cliente,
        string token,
        Guid conversacionId,
        string texto) =>
        EnviarAsync(
            cliente,
            HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacionId}/mensajes",
            token,
            new EnviarMensajeRequest(Guid.NewGuid(), texto));

    private static async Task<ProblemDetails> LeerProblemaAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<ProblemDetails>())!;

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient cliente,
        HttpMethod metodo,
        string url,
        string token,
        object? contenido = null)
    {
        using var solicitud = new HttpRequestMessage(metodo, url);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (contenido is not null)
        {
            solicitud.Content = JsonContent.Create(contenido);
        }

        return await cliente.SendAsync(solicitud);
    }

    private sealed record UsuarioPrueba(Guid UsuarioId, string Token);
}
