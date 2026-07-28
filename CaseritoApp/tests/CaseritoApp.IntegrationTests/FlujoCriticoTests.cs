using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Flujo crítico del piloto: registro → login → KYC aprobado → publicar aviso →
/// iniciar chat → crear orden → completar orden → calificar.
/// </summary>
public sealed class FlujoCriticoTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad = new("22222222-2222-2222-2222-000000000001");

    [Fact]
    public async Task Flujo_completo_de_piloto_es_exitoso()
    {
        using var cliente = factory.CreateClient();

        // 1. Registro de vendedor y comprador.
        var vendedor = await RegistrarAsync(cliente, "critico-vendedor");
        var comprador = await RegistrarAsync(cliente, "critico-comprador");

        // 2. KYC aprobado para ambos.
        await VerificarAsync(vendedor.UsuarioId);
        await VerificarAsync(comprador.UsuarioId);

        // 3. Login nuevamente para obtener tokens con claim verificado.
        var tokenVendedor = await LoguearAsync(cliente, vendedor.Email);
        var tokenComprador = await LoguearAsync(cliente, comprador.Email);

        // 4. Vendedor publica un aviso.
        var aviso = await CrearAvisoAsync(cliente, tokenVendedor);

        // 5. Comprador inicia una conversación por el aviso.
        _ = await IniciarChatAsync(cliente, tokenComprador, aviso.Id);

        // 6. Comprador crea una orden sobre el aviso.
        var orden = await CrearOrdenAsync(cliente, tokenComprador, aviso.Id);

        // 7. Vendedor acepta la orden y marca vendido; comprador confirma completado.
        await CompletarOrdenAsync(cliente, tokenVendedor, tokenComprador, orden.Id);

        // 8. Comprador califica al vendedor.
        var resena = await CalificarAsync(cliente, tokenComprador, orden.Id);

        Assert.NotEqual(Guid.Empty, resena.Id);
    }

    private async Task<UsuarioPrueba> RegistrarAsync(HttpClient cliente, string prefijo)
    {
        var email = $"{prefijo}-{Guid.NewGuid():N}@caserito.test";
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        Assert.NotNull(usuario);

        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;

        return new UsuarioPrueba(usuario.Id, token, email);
    }

    private static async Task<string> LoguearAsync(HttpClient cliente, string email)
    {
        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
    }

    private async Task VerificarAsync(Guid usuarioId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var verificacion = VerificacionKyc.Crear(usuarioId);
        var solicitud = verificacion.EnviarSolicitud(
            "referencia-documento-sintetica",
            "referencia-selfie-sintetica",
            TipoDocumento.CedulaIdentidad,
            DateTimeOffset.UtcNow);
        Assert.True(solicitud.EsExito);
        Assert.True(verificacion.Aprobar(
            solicitud.Valor.Id,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow).EsExito);
        db.VerificacionesKyc.Add(verificacion);
        await db.SaveChangesAsync();
    }

    private static async Task<AvisoCreadoResponse> CrearAvisoAsync(HttpClient cliente, string token)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/avisos/");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        solicitud.Content = JsonContent.Create(new CrearAvisoRequest(
            "Artículo de flujo crítico",
            "Descripción sintética para el flujo crítico del piloto.",
            120m,
            "Usado",
            _categoria,
            _ciudad));

        var respuesta = await cliente.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<AvisoCreadoResponse>())!;
    }

    private static async Task<ConversacionDto> IniciarChatAsync(
        HttpClient cliente,
        string token,
        Guid avisoId)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/chat/conversaciones");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        solicitud.Content = JsonContent.Create(new IniciarConversacionRequest(avisoId));

        var respuesta = await cliente.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<ConversacionDto>())!;
    }

    private static async Task<OrdenCreadaResponse> CrearOrdenAsync(
        HttpClient cliente,
        string token,
        Guid avisoId)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/orders");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        solicitud.Content = JsonContent.Create(new SolicitarOrdenRequest(avisoId));

        var respuesta = await cliente.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<OrdenCreadaResponse>())!;
    }

    private static async Task CompletarOrdenAsync(
        HttpClient cliente,
        string tokenVendedor,
        string tokenComprador,
        Guid ordenId)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await EnviarAutorizadoAsync(
            cliente,
            HttpMethod.Post,
            $"/api/orders/{ordenId}/aceptar",
            tokenVendedor)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await EnviarAutorizadoAsync(
            cliente,
            HttpMethod.Post,
            $"/api/orders/{ordenId}/marcar-vendido",
            tokenVendedor)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await EnviarAutorizadoAsync(
            cliente,
            HttpMethod.Post,
            $"/api/orders/{ordenId}/confirmar-completado",
            tokenComprador)).StatusCode);
    }

    private static async Task<ResenaCreadaResponse> CalificarAsync(
        HttpClient cliente,
        string token,
        Guid ordenId)
    {
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/reputacion/ordenes/{ordenId}/resenas");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        solicitud.Content = JsonContent.Create(new CrearResenaRequest(5, "Excelente experiencia."));

        var respuesta = await cliente.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<ResenaCreadaResponse>())!;
    }

    private static async Task<HttpResponseMessage> EnviarAutorizadoAsync(
        HttpClient cliente,
        HttpMethod metodo,
        string ruta,
        string token)
    {
        using var solicitud = new HttpRequestMessage(metodo, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await cliente.SendAsync(solicitud);
    }

    private sealed record UsuarioPrueba(Guid UsuarioId, string Token, string Email);

    private sealed record OrdenCreadaResponse(Guid Id, string Estado, decimal MontoAcordado, string Moneda);

    private sealed record ResenaCreadaResponse(Guid Id);
}
