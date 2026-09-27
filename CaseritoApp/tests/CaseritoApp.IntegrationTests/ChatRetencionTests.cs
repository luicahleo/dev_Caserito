using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatRetencionTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad = new("22222222-2222-2222-2222-000000000001");
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    [Fact]
    public async Task El_vendedor_no_percibe_la_conversacion_retenida()
    {
        var comprador = Guid.NewGuid();
        var vendedor = Guid.NewGuid();
        var aviso = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaConversaciones>();

        var conversacion = Conversacion.Crear(
            aviso, comprador, vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;
        var mensaje = conversacion.CrearMensaje(
            comprador, Guid.NewGuid(), 1, "hola", DateTimeOffset.UtcNow).Valor;
        db.Conversaciones.Add(conversacion);
        db.Mensajes.Add(mensaje);
        await db.SaveChangesAsync();

        // El vendedor no la ve por ninguna vía.
        var listadoVendedor = await consulta.ListarAsync(vendedor, null, 20, default);
        Assert.DoesNotContain(listadoVendedor.Items, c => c.Id == conversacion.Id);
        Assert.Equal(0, await consulta.ContarNoLeidosAsync(vendedor, default));
        Assert.False(await consulta.PuedeAccederAsync(conversacion.Id, vendedor, default));
        Assert.False(await consulta.PuedeRecibirTiempoRealAsync(conversacion.Id, vendedor, default));

        // El comprador sí.
        var listadoComprador = await consulta.ListarAsync(comprador, null, 20, default);
        Assert.Contains(listadoComprador.Items, c => c.Id == conversacion.Id);
        Assert.True(await consulta.PuedeAccederAsync(conversacion.Id, comprador, default));
    }

    [Fact]
    public async Task Tras_liberarla_el_vendedor_la_ve_con_sus_mensajes()
    {
        var comprador = Guid.NewGuid();
        var vendedor = Guid.NewGuid();
        var aviso = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaConversaciones>();

        var conversacion = Conversacion.Crear(
            aviso, comprador, vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;
        var mensaje = conversacion.CrearMensaje(
            comprador, Guid.NewGuid(), 1, "hola", DateTimeOffset.UtcNow).Valor;
        db.Conversaciones.Add(conversacion);
        db.Mensajes.Add(mensaje);
        await db.SaveChangesAsync();

        conversacion.LiberarPorVerificacion(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var listado = await consulta.ListarAsync(vendedor, null, 20, default);
        Assert.Contains(listado.Items, c => c.Id == conversacion.Id);
        Assert.Equal(1, await consulta.ContarNoLeidosAsync(vendedor, default));
    }

    [Fact]
    public async Task Comprador_sin_verificar_escribe_y_el_vendedor_lo_ve_tras_aprobarse_el_kyc()
    {
        using var cliente = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ =>
                new VerificadorArgosEstatico(Result.Exito(
                    new VerificacionFacialResultado(
                        Coinciden: true, SimilitudPercent: 95, MotivoRechazo: null))));
        })).CreateClient();

        // 1. Vendedor verificado con un aviso publicado.
        var emailVendedor = Email("chat-vendedor");
        var tokenVendedor = await RegistrarYLoguearAsync(cliente, emailVendedor, null);
        tokenVendedor = await VerificarYRelogearAsync(cliente, emailVendedor, tokenVendedor, "1234570");
        var avisoId = await PublicarAvisoAsync(cliente, tokenVendedor);

        // 2. Comprador SIN verificar inicia conversación y envía un mensaje.
        var emailComprador = Email("chat-comprador");
        var tokenComprador = await RegistrarYLoguearAsync(cliente, emailComprador, null);

        using var iniciar = Autorizada(HttpMethod.Post, "/api/chat/conversaciones", tokenComprador);
        iniciar.Content = JsonContent.Create(new IniciarConversacionRequest(avisoId));
        var respuestaIniciar = await cliente.SendAsync(iniciar);
        Assert.Equal(HttpStatusCode.Created, respuestaIniciar.StatusCode);
        var iniciada = await respuestaIniciar.Content.ReadFromJsonAsync<ConversacionDto>();
        var conversacionId = iniciada!.Id;

        using var enviar = Autorizada(
            HttpMethod.Post, $"/api/chat/conversaciones/{conversacionId}/mensajes", tokenComprador);
        enviar.Content = JsonContent.Create(
            new EnviarMensajeRequest(Guid.NewGuid(), "hola, sigue disponible?"));
        Assert.Equal(HttpStatusCode.Created, (await cliente.SendAsync(enviar)).StatusCode);

        // 3 y 4. El vendedor no percibe nada a través de la API.
        using var listar = Autorizada(HttpMethod.Get, "/api/chat/conversaciones", tokenVendedor);
        var listado = await (await cliente.SendAsync(listar)).Content.ReadAsStringAsync();
        Assert.DoesNotContain(conversacionId.ToString(), listado, StringComparison.OrdinalIgnoreCase);

        using var noLeidos = Autorizada(HttpMethod.Get, "/api/chat/no-leidos", tokenVendedor);
        var conteo = await (await cliente.SendAsync(noLeidos)).Content.ReadAsStringAsync();
        Assert.Contains("0", conteo, StringComparison.Ordinal);

        // 5. El comprador se verifica: ARGOS aprueba y el KYC se resuelve solo.
        await VerificarYRelogearAsync(cliente, emailComprador, tokenComprador, "1234571");

        // 6 y 7. Ahora el vendedor sí la ve, con el mensaje dentro.
        using var listarTras = Autorizada(HttpMethod.Get, "/api/chat/conversaciones", tokenVendedor);
        var listadoTras = await (await cliente.SendAsync(listarTras)).Content.ReadAsStringAsync();
        Assert.Contains(conversacionId.ToString(), listadoTras, StringComparison.OrdinalIgnoreCase);

        using var mensajes = Autorizada(
            HttpMethod.Get, $"/api/chat/conversaciones/{conversacionId}/mensajes", tokenVendedor);
        var cuerpoMensajes = await (await cliente.SendAsync(mensajes)).Content.ReadAsStringAsync();
        Assert.Contains("sigue disponible", cuerpoMensajes, StringComparison.Ordinal);
    }

    private static string Email(string prefijo) =>
        $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

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

    private async Task<string> RegistrarYLoguearAsync(
        HttpClient cliente, string email, string? rolExtra)
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

        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
    }

    private static async Task<string> VerificarYRelogearAsync(
        HttpClient cliente, string email, string token, string numeroCi)
    {
        using var subir = Autorizada(
            HttpMethod.Post,
            $"/api/kyc/?numeroCi={numeroCi}&departamentoExpedicion=LaPaz",
            token);
        subir.Content = Formulario();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subir)).StatusCode);

        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
    }

    private static async Task<Guid> PublicarAvisoAsync(HttpClient cliente, string token)
    {
        using var crear = Autorizada(HttpMethod.Post, "/api/avisos/", token);
        crear.Content = JsonContent.Create(new CrearAvisoRequest(
            "Bicicleta", "Rodado 29, poco uso", 1200m, "Usado", _categoria, _ciudad));
        var respuesta = await cliente.SendAsync(crear);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await respuesta.Content.ReadFromJsonAsync<AvisoCreadoResponse>();
        return creado!.Id;
    }

    private sealed class VerificadorArgosEstatico(Result<VerificacionFacialResultado> resultado)
        : IVerificadorIdentidadArgos
    {
        public Task<Result<VerificacionFacialResultado>> VerificarAsync(
            byte[] imagenDocumento, byte[] imagenSelfie, CancellationToken ct) =>
            Task.FromResult(resultado);
    }
}
