using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Domain.Moderacion;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatFlujoTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static string Email(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

    [Fact]
    public async Task Endpoints_requieren_autenticacion()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/chat/conversaciones", new IniciarConversacionRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Flujo_de_mensajes_autoriza_pagina_y_actualiza_no_leidos()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-vendedor");
        var tercero = await RegistrarAsync(cliente, "chat-tercero");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);

        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        Assert.Equal(HttpStatusCode.Created, inicio.StatusCode);
        var conversacion = await inicio.Content.ReadFromJsonAsync<ConversacionDto>();

        var repeticion = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        Assert.Equal(HttpStatusCode.OK, repeticion.StatusCode);
        Assert.Equal(conversacion!.Id, (await repeticion.Content.ReadFromJsonAsync<ConversacionDto>())!.Id);

        var clave = Guid.NewGuid();
        var envioComprador = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes", comprador.Token,
            new EnviarMensajeRequest(clave, " ¿Sigue disponible? "));
        Assert.Equal(HttpStatusCode.Created, envioComprador.StatusCode);
        var mensajeComprador = await envioComprador.Content.ReadFromJsonAsync<MensajeDto>();
        Assert.Equal("¿Sigue disponible?", mensajeComprador!.Texto);

        var reintento = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes", comprador.Token,
            new EnviarMensajeRequest(clave, "¿Sigue disponible?"));
        Assert.Equal(HttpStatusCode.OK, reintento.StatusCode);
        Assert.Equal(mensajeComprador.Id, (await reintento.Content.ReadFromJsonAsync<MensajeDto>())!.Id);

        var incompatible = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes", comprador.Token,
            new EnviarMensajeRequest(clave, "Otro texto"));
        Assert.Equal(HttpStatusCode.Conflict, incompatible.StatusCode);

        var envioVendedor = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes", vendedor.Token,
            new EnviarMensajeRequest(Guid.NewGuid(), "Sí"));
        Assert.Equal(HttpStatusCode.Created, envioVendedor.StatusCode);
        var mensajeVendedor = await envioVendedor.Content.ReadFromJsonAsync<MensajeDto>();

        var mensajesTercero = await EnviarAsync(cliente, HttpMethod.Get,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes", tercero.Token);
        var envioTercero = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes", tercero.Token,
            new EnviarMensajeRequest(Guid.NewGuid(), "Intrusión"));
        var lecturaTercero = await EnviarAsync(cliente, HttpMethod.Put,
            $"/api/chat/conversaciones/{conversacion.Id}/lectura", tercero.Token,
            new MarcarLecturaRequest(mensajeVendedor!.Secuencia));
        Assert.Equal(HttpStatusCode.NotFound, mensajesTercero.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, envioTercero.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, lecturaTercero.StatusCode);

        var paginaReciente = await EnviarAsync(cliente, HttpMethod.Get,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes?limite=1", comprador.Token);
        var pagina1 = await paginaReciente.Content.ReadFromJsonAsync<PaginaChatResponse<MensajeDto>>();
        Assert.Equal([mensajeVendedor.Id], pagina1!.Items.Select(m => m.Id));
        Assert.NotNull(pagina1.SiguienteCursor);
        var paginaAnterior = await EnviarAsync(cliente, HttpMethod.Get,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes?limite=1&cursor={pagina1.SiguienteCursor}",
            comprador.Token);
        var pagina2 = await paginaAnterior.Content.ReadFromJsonAsync<PaginaChatResponse<MensajeDto>>();
        Assert.Equal([mensajeComprador.Id], pagina2!.Items.Select(m => m.Id));

        var recuperacion = await EnviarAsync(cliente, HttpMethod.Get,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes?despuesDeSecuencia={mensajeComprador.Secuencia}",
            comprador.Token);
        var posteriores = await recuperacion.Content.ReadFromJsonAsync<PaginaChatResponse<MensajeDto>>();
        Assert.Equal([mensajeVendedor.Id], posteriores!.Items.Select(m => m.Id));
        Assert.Null(posteriores.SiguienteCursor);

        var fronterasIncompatibles = await EnviarAsync(cliente, HttpMethod.Get,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes?cursor={pagina1.SiguienteCursor}&despuesDeSecuencia=0",
            comprador.Token);
        Assert.Equal(HttpStatusCode.BadRequest, fronterasIncompatibles.StatusCode);

        var cursorInvalido = await EnviarAsync(cliente, HttpMethod.Get,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes?cursor=invalido", comprador.Token);
        Assert.Equal(HttpStatusCode.BadRequest, cursorInvalido.StatusCode);

        var listadoAntes = await ListarAsync(cliente, comprador.Token);
        Assert.Equal(1, Assert.Single(listadoAntes.Items).NoLeidos);

        var lectura = await EnviarAsync(cliente, HttpMethod.Put,
            $"/api/chat/conversaciones/{conversacion.Id}/lectura", comprador.Token,
            new MarcarLecturaRequest(mensajeVendedor.Secuencia));
        Assert.Equal(HttpStatusCode.NoContent, lectura.StatusCode);
        Assert.Equal(0, Assert.Single((await ListarAsync(cliente, comprador.Token)).Items).NoLeidos);
        var lecturaInexistente = await EnviarAsync(cliente, HttpMethod.Put,
            $"/api/chat/conversaciones/{conversacion.Id}/lectura", comprador.Token,
            new MarcarLecturaRequest(mensajeVendedor.Secuencia + 100));
        Assert.Equal(HttpStatusCode.BadRequest, lecturaInexistente.StatusCode);

        await EliminarAvisoAsync(aviso.Id);
        var despuesDeEliminar = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes", comprador.Token,
            new EnviarMensajeRequest(Guid.NewGuid(), "Continuamos"));
        Assert.Equal(HttpStatusCode.Created, despuesDeEliminar.StatusCode);
        Assert.Empty((await ListarAsync(cliente, tercero.Token)).Items);

        var textoInvalido = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes", comprador.Token,
            new EnviarMensajeRequest(Guid.NewGuid(), "   "));
        Assert.Equal(HttpStatusCode.BadRequest, textoInvalido.StatusCode);
    }

    [Fact]
    public async Task Inicio_respeta_estado_publico_evitar_autochat_y_resuelve_carrera()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-estados-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-estados-vendedor");
        var activo = await CrearAvisoAsync(vendedor.UsuarioId);
        var pausado = await CrearAvisoAsync(vendedor.UsuarioId, a => a.Pausar(DateTime.UtcNow));
        var oculto = await CrearAvisoAsync(vendedor.UsuarioId, a => a.OcultarPorModeracion(DateTime.UtcNow));
        var eliminado = await CrearAvisoAsync(vendedor.UsuarioId, a => a.Eliminar(DateTime.UtcNow));

        foreach (var aviso in new[] { pausado, oculto, eliminado })
        {
            var respuesta = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
                comprador.Token, new IniciarConversacionRequest(aviso.Id));
            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }

        var autochat = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            vendedor.Token, new IniciarConversacionRequest(activo.Id));
        Assert.Equal(HttpStatusCode.Conflict, autochat.StatusCode);

        var carreraAviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var respuestas = await Task.WhenAll(
            EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
                comprador.Token, new IniciarConversacionRequest(carreraAviso.Id)),
            EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
                comprador.Token, new IniciarConversacionRequest(carreraAviso.Id)));
        Assert.Contains(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(respuestas, r => Assert.Contains(
            r.StatusCode,
            new[] { HttpStatusCode.Created, HttpStatusCode.OK }));
        var ids = await Task.WhenAll(respuestas.Select(r => r.Content.ReadFromJsonAsync<ConversacionDto>()));
        Assert.Single(ids.Select(c => c!.Id).Distinct());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        Assert.Equal(1, await db.Conversaciones.CountAsync(
            c => c.CompradorId == comprador.UsuarioId && c.AvisoId == carreraAviso.Id));
    }

    [Fact]
    public async Task Limite_de_inicio_devuelve_429_en_la_solicitud_once()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-limite-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-limite-vendedor");
        var estados = new List<HttpStatusCode>();

        for (var i = 0; i < 11; i++)
        {
            var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
            var respuesta = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
                comprador.Token, new IniciarConversacionRequest(aviso.Id));
            estados.Add(respuesta.StatusCode);
        }

        Assert.All(estados.Take(10), estado => Assert.Equal(HttpStatusCode.Created, estado));
        Assert.Equal(HttpStatusCode.TooManyRequests, estados[10]);
    }

    [Fact]
    public async Task Carrera_de_misma_clave_crea_un_solo_mensaje()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-carrera-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-carrera-vendedor");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        var conversacion = (await inicio.Content.ReadFromJsonAsync<ConversacionDto>())!;
        var clave = Guid.NewGuid();
        var url = $"/api/chat/conversaciones/{conversacion.Id}/mensajes";

        var respuestas = await Task.WhenAll(
            EnviarAsync(cliente, HttpMethod.Post, url, comprador.Token,
                new EnviarMensajeRequest(clave, "Mensaje único")),
            EnviarAsync(cliente, HttpMethod.Post, url, comprador.Token,
                new EnviarMensajeRequest(clave, "Mensaje único")));

        Assert.Contains(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(respuestas, r => Assert.Contains(
            r.StatusCode,
            new[] { HttpStatusCode.Created, HttpStatusCode.OK }));
        var mensajes = await Task.WhenAll(respuestas.Select(r => r.Content.ReadFromJsonAsync<MensajeDto>()));
        Assert.Single(mensajes.Select(m => m!.Id).Distinct());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        Assert.Equal(1, await db.Mensajes.CountAsync(m =>
            m.ConversacionId == conversacion.Id && m.ClaveIdempotencia == clave));
    }

    [Fact]
    public async Task Reportar_conversacion_participante_devuelve_201()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-reporte-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-reporte-vendedor");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        var conversacion = (await inicio.Content.ReadFromJsonAsync<ConversacionDto>())!;

        var respuesta = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/reportes", comprador.Token,
            new
            {
                TipoObjetivo = TipoObjetivoReporteChat.Conversacion,
                Categoria = CategoriaReporteChat.Acoso,
                Detalle = "Detalle opcional",
            });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        Assert.Equal("id", Assert.Single(cuerpo!).Key, StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual(Guid.Empty, cuerpo!["id"]);
    }

    [Fact]
    public async Task Reportar_mensaje_de_la_conversacion_devuelve_201()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-reporte-mensaje-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-reporte-mensaje-vendedor");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        var conversacion = (await inicio.Content.ReadFromJsonAsync<ConversacionDto>())!;
        var envio = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/mensajes", comprador.Token,
            new EnviarMensajeRequest(Guid.NewGuid(), "Mensaje reportado"));
        var mensaje = (await envio.Content.ReadFromJsonAsync<MensajeDto>())!;

        var respuesta = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/reportes", comprador.Token,
            new
            {
                TipoObjetivo = TipoObjetivoReporteChat.Mensaje,
                MensajeId = mensaje.Id,
                Categoria = CategoriaReporteChat.Acoso,
                Detalle = "Detalle mínimo",
            });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        Assert.Equal("id", Assert.Single(cuerpo!).Key, StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual(Guid.Empty, cuerpo!["id"]);
    }

    [Fact]
    public async Task Reportar_contraparte_rechaza_identificador_de_usuario_objetivo()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-reporte-contraparte-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-reporte-contraparte-vendedor");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        var conversacion = (await inicio.Content.ReadFromJsonAsync<ConversacionDto>())!;

        var respuesta = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/reportes", comprador.Token,
            new
            {
                TipoObjetivo = TipoObjetivoReporteChat.Participante,
                UsuarioObjetivoId = vendedor.UsuarioId,
                Categoria = CategoriaReporteChat.Acoso,
                Detalle = "Detalle mínimo",
            });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reportar_contraparte_derivada_devuelve_201()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-reporte-derivado-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-reporte-derivado-vendedor");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        var conversacion = (await inicio.Content.ReadFromJsonAsync<ConversacionDto>())!;

        var respuesta = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/reportes", comprador.Token,
            new
            {
                TipoObjetivo = TipoObjetivoReporteChat.Participante,
                Categoria = CategoriaReporteChat.Acoso,
                Detalle = "Detalle mínimo",
            });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        Assert.Equal("id", Assert.Single(cuerpo!).Key, StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual(Guid.Empty, cuerpo!["id"]);
    }

    [Fact]
    public async Task Reportar_recurso_inexistente_o_ajeno_devuelve_404_indistinguible()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-reporte-oculto-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-reporte-oculto-vendedor");
        var compradorAjeno = await RegistrarAsync(cliente, "chat-reporte-oculto-comprador-ajeno");
        var vendedorAjeno = await RegistrarAsync(cliente, "chat-reporte-oculto-vendedor-ajeno");
        var avisoPropio = await CrearAvisoAsync(vendedor.UsuarioId);
        var avisoAjeno = await CrearAvisoAsync(vendedorAjeno.UsuarioId);
        var inicioPropio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(avisoPropio.Id));
        var inicioAjeno = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            compradorAjeno.Token, new IniciarConversacionRequest(avisoAjeno.Id));
        var conversacionPropia = (await inicioPropio.Content.ReadFromJsonAsync<ConversacionDto>())!;
        var conversacionAjena = (await inicioAjeno.Content.ReadFromJsonAsync<ConversacionDto>())!;
        var inexistente = Guid.NewGuid();
        var request = new
        {
            TipoObjetivo = TipoObjetivoReporteChat.Conversacion,
            Categoria = CategoriaReporteChat.Acoso,
            Detalle = "argumento secreto irrepetible",
        };

        var respuestaInexistente = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{inexistente}/reportes", comprador.Token, request);
        var respuestaAjena = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacionAjena.Id}/reportes", comprador.Token, request);

        Assert.Equal(HttpStatusCode.NotFound, respuestaInexistente.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, respuestaAjena.StatusCode);
        var problemaInexistente = (await respuestaInexistente.Content.ReadFromJsonAsync<ProblemDetails>())!;
        var problemaAjeno = (await respuestaAjena.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal(problemaInexistente.Status, problemaAjeno.Status);
        Assert.Equal(problemaInexistente.Title, problemaAjeno.Title);
        Assert.Equal(problemaInexistente.Detail, problemaAjeno.Detail);
        Assert.Equal(problemaInexistente.Type, problemaAjeno.Type);
        Assert.Equal("chat_conversacion_no_encontrada", problemaInexistente.Title);
        Assert.Equal("La conversación no está disponible.", problemaInexistente.Detail);

        var cuerpos = string.Concat(
            await respuestaInexistente.Content.ReadAsStringAsync(),
            await respuestaAjena.Content.ReadAsStringAsync());
        Assert.DoesNotContain(inexistente.ToString(), cuerpos, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(conversacionPropia.Id.ToString(), cuerpos, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(conversacionAjena.Id.ToString(), cuerpos, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request.Detalle, cuerpos, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reportar_objetivo_duplicado_devuelve_409_generico()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-reporte-duplicado-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-reporte-duplicado-vendedor");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        var conversacion = (await inicio.Content.ReadFromJsonAsync<ConversacionDto>())!;
        var request = new
        {
            TipoObjetivo = TipoObjetivoReporteChat.Conversacion,
            Categoria = CategoriaReporteChat.Spam,
            Detalle = "detalle duplicado confidencial",
        };
        var primero = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/reportes", comprador.Token, request);
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var duplicado = await EnviarAsync(cliente, HttpMethod.Post,
            $"/api/chat/conversaciones/{conversacion.Id}/reportes", comprador.Token, request);

        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        var problema = (await duplicado.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal("chat_reporte_transicion_invalida", problema.Title);
        Assert.Equal("El reporte no está disponible para esa acción.", problema.Detail);
        var cuerpo = await duplicado.Content.ReadAsStringAsync();
        Assert.DoesNotContain(conversacion.Id.ToString(), cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request.Detalle, cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reportar_sin_autenticacion_devuelve_401()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/chat/conversaciones/{Guid.NewGuid()}/reportes",
            new
            {
                TipoObjetivo = TipoObjetivoReporteChat.Conversacion,
                Categoria = CategoriaReporteChat.Acoso,
            });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Limite_de_reportes_devuelve_429_en_la_solicitud_once()
    {
        using var cliente = factory.CreateClient();
        var usuario = await RegistrarAsync(cliente, "chat-reporte-limite");
        var estados = new List<HttpStatusCode>();

        for (var i = 0; i < 11; i++)
        {
            var respuesta = await EnviarAsync(
                cliente,
                HttpMethod.Post,
                $"/api/chat/conversaciones/{Guid.NewGuid()}/reportes",
                usuario.Token,
                new
                {
                    TipoObjetivo = TipoObjetivoReporteChat.Conversacion,
                    Categoria = CategoriaReporteChat.Spam,
                });
            estados.Add(respuesta.StatusCode);
        }

        Assert.All(estados.Take(10), estado => Assert.Equal(HttpStatusCode.NotFound, estado));
        Assert.Equal(HttpStatusCode.TooManyRequests, estados[10]);
    }

    [Fact]
    public async Task Cerrar_conversacion_participante_devuelve_204()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-cierre-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-cierre-vendedor");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        var conversacion = (await inicio.Content.ReadFromJsonAsync<ConversacionDto>())!;

        var respuesta = await EnviarAsync(cliente, HttpMethod.Put,
            $"/api/chat/conversaciones/{conversacion.Id}/cierre", comprador.Token);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reabrir_conversacion_participante_devuelve_204()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-reapertura-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-reapertura-vendedor");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        var conversacion = (await inicio.Content.ReadFromJsonAsync<ConversacionDto>())!;
        var cierre = await EnviarAsync(cliente, HttpMethod.Put,
            $"/api/chat/conversaciones/{conversacion.Id}/cierre", comprador.Token);
        Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);

        var respuesta = await EnviarAsync(cliente, HttpMethod.Delete,
            $"/api/chat/conversaciones/{conversacion.Id}/cierre", comprador.Token);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task Bloquear_contraparte_derivada_devuelve_204()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-bloqueo-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-bloqueo-vendedor");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        var conversacion = (await inicio.Content.ReadFromJsonAsync<ConversacionDto>())!;

        var respuesta = await EnviarAsync(cliente, HttpMethod.Put,
            $"/api/chat/conversaciones/{conversacion.Id}/bloqueo", comprador.Token);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task Desbloquear_contraparte_derivada_devuelve_204()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "chat-desbloqueo-comprador");
        var vendedor = await RegistrarAsync(cliente, "chat-desbloqueo-vendedor");
        var aviso = await CrearAvisoAsync(vendedor.UsuarioId);
        var inicio = await EnviarAsync(cliente, HttpMethod.Post, "/api/chat/conversaciones",
            comprador.Token, new IniciarConversacionRequest(aviso.Id));
        var conversacion = (await inicio.Content.ReadFromJsonAsync<ConversacionDto>())!;
        var bloqueo = await EnviarAsync(cliente, HttpMethod.Put,
            $"/api/chat/conversaciones/{conversacion.Id}/bloqueo", comprador.Token);
        Assert.Equal(HttpStatusCode.NoContent, bloqueo.StatusCode);

        var respuesta = await EnviarAsync(cliente, HttpMethod.Delete,
            $"/api/chat/conversaciones/{conversacion.Id}/bloqueo", comprador.Token);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    private async Task<UsuarioPrueba> RegistrarAsync(HttpClient cliente, string prefijo)
    {
        var email = Email(prefijo);
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

    private async Task<Aviso> CrearAvisoAsync(Guid vendedorId, Action<Aviso>? configurar = null)
    {
        var aviso = Aviso.Crear(
            vendedorId,
            "Aviso chat",
            "Descripción",
            Dinero.Crear(100, Moneda.BOB).Valor,
            Guid.NewGuid(),
            Guid.NewGuid(),
            CondicionArticulo.Usado,
            DateTime.UtcNow);
        configurar?.Invoke(aviso);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        db.Avisos.Add(aviso);
        await db.SaveChangesAsync();
        return aviso;
    }

    private async Task EliminarAvisoAsync(Guid avisoId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var aviso = await db.Avisos.SingleAsync(a => a.Id == avisoId);
        aviso.Eliminar(DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

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

    private static async Task<PaginaChatResponse<ConversacionResumenDto>> ListarAsync(
        HttpClient cliente,
        string token)
    {
        var respuesta = await EnviarAsync(
            cliente, HttpMethod.Get, "/api/chat/conversaciones", token);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<PaginaChatResponse<ConversacionResumenDto>>())!;
    }

    private sealed record UsuarioPrueba(Guid UsuarioId, string Token);
}
