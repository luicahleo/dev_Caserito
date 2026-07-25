using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using CaseritoApp.Orders.Domain.Ordenes;
using CaseritoApp.Orders.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class OrdersFlujoTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    private static readonly Guid _categoria =
        new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad =
        new("22222222-2222-2222-2222-000000000001");

    [Fact]
    public async Task Endpoints_requieren_autenticacion()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/orders?rol=comprador");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Participantes_listan_consultan_y_vendedor_acepta_pero_tercero_no_descubre()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "comprador");
        var vendedor = await RegistrarAsync(cliente, "vendedor");
        var tercero = await RegistrarAsync(cliente, "tercero");
        var orden = CrearOrden(comprador.Id, vendedor.Id);
        await PersistirAsync(orden);

        var listado = await EnviarAsync(
            cliente, HttpMethod.Get, "/api/orders?rol=comprador", comprador.Token);
        var detalleTercero = await EnviarAsync(
            cliente, HttpMethod.Get, $"/api/orders/{orden.Id}", tercero.Token);
        var aceptar = await EnviarAsync(
            cliente, HttpMethod.Post, $"/api/orders/{orden.Id}/aceptar", vendedor.Token);
        var detalleComprador = await EnviarAsync(
            cliente, HttpMethod.Get, $"/api/orders/{orden.Id}", comprador.Token);

        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detalleTercero.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, aceptar.StatusCode);
        Assert.Equal("Agreed", (await detalleComprador.Content
            .ReadFromJsonAsync<OrdenDetalleResponse>())!.Estado);
    }

    [Fact]
    public async Task Solicitud_no_verificada_es_403_y_filtro_invalido_es_400()
    {
        using var cliente = factory.CreateClient();
        var usuario = await RegistrarAsync(cliente, "no-verificado");

        var solicitud = await EnviarAsync(
            cliente,
            HttpMethod.Post,
            "/api/orders",
            usuario.Token,
            new SolicitarOrdenRequest(Guid.NewGuid()));
        var filtro = await EnviarAsync(
            cliente,
            HttpMethod.Get,
            "/api/orders?rol=tercero",
            usuario.Token);

        Assert.Equal(HttpStatusCode.Forbidden, solicitud.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, filtro.StatusCode);
    }

    [Fact]
    public async Task Participantes_verificados_crean_orden_con_instantanea_del_aviso()
    {
        using var cliente = factory.CreateClient();
        var vendedor = await RegistrarAsync(cliente, "crear-vendedor");
        var comprador = await RegistrarAsync(cliente, "crear-comprador");
        await VerificarAsync(vendedor.Id);
        await VerificarAsync(comprador.Id);
        var tokenVendedor = await LoguearAsync(cliente, vendedor.Email);
        var tokenComprador = await LoguearAsync(cliente, comprador.Email);

        var crearAviso = await EnviarAsync(
            cliente,
            HttpMethod.Post,
            "/api/avisos",
            tokenVendedor,
            new CrearAvisoRequest(
                "Artículo sintético",
                "Descripción sintética",
                75m,
                "Usado",
                _categoria,
                _ciudad));
        var aviso = await crearAviso.Content.ReadFromJsonAsync<AvisoCreadoResponse>();

        var crearOrden = await EnviarAsync(
            cliente,
            HttpMethod.Post,
            "/api/orders",
            tokenComprador,
            new SolicitarOrdenRequest(aviso!.Id));
        var orden = await crearOrden.Content.ReadFromJsonAsync<OrdenCreadaResponse>();

        Assert.Equal(HttpStatusCode.Created, crearOrden.StatusCode);
        Assert.Equal(75m, orden!.MontoAcordado);
        Assert.Equal("BOB", orden.Moneda);
        Assert.Equal("Requested", orden.Estado);
    }

    [Fact]
    public async Task Comprador_cancela_solicitud_y_puede_resolicitar()
    {
        using var cliente = factory.CreateClient();
        var vendedor = await RegistrarAsync(cliente, "cancelar-resolicitar-vendedor");
        var comprador = await RegistrarAsync(cliente, "cancelar-resolicitar-comprador");
        await VerificarAsync(vendedor.Id);
        await VerificarAsync(comprador.Id);
        var tokenVendedor = await LoguearAsync(cliente, vendedor.Email);
        var tokenComprador = await LoguearAsync(cliente, comprador.Email);

        var crearAviso = await EnviarAsync(
            cliente,
            HttpMethod.Post,
            "/api/avisos",
            tokenVendedor,
            new CrearAvisoRequest(
                "Artículo sintético",
                "Descripción sintética",
                75m,
                "Usado",
                _categoria,
                _ciudad));
        var aviso = await crearAviso.Content.ReadFromJsonAsync<AvisoCreadoResponse>();

        var crearOrden = await EnviarAsync(
            cliente,
            HttpMethod.Post,
            "/api/orders",
            tokenComprador,
            new SolicitarOrdenRequest(aviso!.Id));
        var orden = await crearOrden.Content.ReadFromJsonAsync<OrdenCreadaResponse>();

        var cancelar = await EnviarAsync(
            cliente, HttpMethod.Post, $"/api/orders/{orden!.Id}/cancelar", tokenComprador);
        var resolicitar = await EnviarAsync(
            cliente,
            HttpMethod.Post,
            "/api/orders",
            tokenComprador,
            new SolicitarOrdenRequest(aviso.Id));

        Assert.Equal(HttpStatusCode.NoContent, cancelar.StatusCode);
        Assert.Equal(HttpStatusCode.Created, resolicitar.StatusCode);
    }

    [Fact]
    public async Task Cancelar_por_un_tercero_devuelve_404()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "cancelar-tercero-comprador");
        var vendedor = await RegistrarAsync(cliente, "cancelar-tercero-vendedor");
        var tercero = await RegistrarAsync(cliente, "cancelar-tercero-tercero");
        var orden = CrearOrden(comprador.Id, vendedor.Id);
        await PersistirAsync(orden);

        var respuesta = await EnviarAsync(
            cliente, HttpMethod.Post, $"/api/orders/{orden.Id}/cancelar", tercero.Token);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cancelar_dos_veces_es_idempotente()
    {
        using var cliente = factory.CreateClient();
        var comprador = await RegistrarAsync(cliente, "cancelar-idempotente-comprador");
        var vendedor = await RegistrarAsync(cliente, "cancelar-idempotente-vendedor");
        var orden = CrearOrden(comprador.Id, vendedor.Id);
        await PersistirAsync(orden);

        var primera = await EnviarAsync(
            cliente, HttpMethod.Post, $"/api/orders/{orden.Id}/cancelar", comprador.Token);
        var segunda = await EnviarAsync(
            cliente, HttpMethod.Post, $"/api/orders/{orden.Id}/cancelar", comprador.Token);

        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, segunda.StatusCode);
    }

    private async Task<(Guid Id, string Token, string Email)> RegistrarAsync(
        HttpClient cliente,
        string prefijo)
    {
        var email = $"{prefijo}-{Guid.NewGuid():N}@caserito.test";
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);
        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
        var token = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        using var scope = factory.Services.CreateScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return ((await usuarios.FindByEmailAsync(email))!.Id, token, email);
    }

    private static async Task<string> LoguearAsync(HttpClient cliente, string email)
    {
        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!"));
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

    private async Task PersistirAsync(Orden orden)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        db.Orders.Add(orden);
        await scope.ServiceProvider.GetRequiredService<UnitOfWorkOrders>()
            .GuardarCambiosAsync(CancellationToken.None);
    }

    private static Orden CrearOrden(Guid compradorId, Guid vendedorId)
    {
        var resultado = Orden.Crear(
            Guid.NewGuid(),
            compradorId,
            vendedorId,
            50m,
            "BOB",
            DateTimeOffset.UtcNow);
        Assert.True(resultado.EsExito);
        return resultado.Valor;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient cliente,
        HttpMethod metodo,
        string ruta,
        string token,
        object? contenido = null)
    {
        using var solicitud = new HttpRequestMessage(metodo, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (contenido is not null)
        {
            solicitud.Content = JsonContent.Create(contenido);
        }

        return await cliente.SendAsync(solicitud);
    }

    private sealed record OrdenDetalleResponse(string Estado);

    private sealed record OrdenCreadaResponse(
        Guid Id,
        string Estado,
        decimal MontoAcordado,
        string Moneda);
}
