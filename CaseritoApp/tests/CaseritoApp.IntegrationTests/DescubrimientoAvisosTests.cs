using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.IntegrationTests.Infrastructure;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Descubrimiento público: solo avisos Activos, filtros, acento-insensibilidad, paginación y detalle 404.</summary>
public sealed class DescubrimientoAvisosTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad = new("22222222-2222-2222-2222-000000000001");
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string Email(string p) => $"{p}-{Guid.NewGuid():N}@caserito.test";

    private static HttpRequestMessage Con(HttpMethod m, string url, string token)
    {
        var s = new HttpRequestMessage(m, url);
        s.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return s;
    }

    private static async Task<string> UsuarioVerificadoAsync(HttpClient cliente)
    {
        var email = Email("disc-user");
        var reg = await cliente.PostAsJsonAsync("/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        var token = await LoguearAsync(cliente, email);

        var form = new MultipartFormDataContent();
        var doc = new ByteArrayContent(_png); doc.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(doc, "documento", "ci.png");
        var self = new ByteArrayContent(_png); self.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(self, "selfie", "selfie.png");
        using (var subir = Con(HttpMethod.Post, "/api/kyc/", token))
        {
            subir.Content = form;
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subir)).StatusCode);
        }

        // Re-login para que el JWT traiga el claim verificado=true.
        return await LoguearAsync(cliente, email);
    }

    private static async Task<string> LoguearAsync(HttpClient cliente, string email)
    {
        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<TokenAccesoDisc>())!.AccessToken;
    }

    private static async Task<Guid> CrearAvisoAsync(
        HttpClient cliente, string token, string titulo, string descripcion, decimal monto, string condicion)
    {
        using var crear = Con(HttpMethod.Post, "/api/avisos/", token);
        crear.Content = JsonContent.Create(
            new CrearAvisoRequest(titulo, descripcion, monto, condicion, _categoria, _ciudad));
        var resp = await cliente.SendAsync(crear);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<AvisoCreadoResponse>())!.Id;
    }

    [Fact]
    public async Task Solo_lista_avisos_activos()
    {
        using var cliente = factory.ConAprobadorArgos().CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        var marca = Guid.NewGuid().ToString("N");
        var activo = await CrearAvisoAsync(cliente, token, $"Activo {marca}", "visible", 100m, "Usado");
        var pausado = await CrearAvisoAsync(cliente, token, $"Pausado {marca}", "oculto", 100m, "Usado");
        var eliminado = await CrearAvisoAsync(cliente, token, $"Eliminado {marca}", "oculto", 100m, "Usado");

        using (var p = Con(HttpMethod.Post, $"/api/avisos/{pausado}/pausar", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(p)).StatusCode);
        }
        using (var e = Con(HttpMethod.Delete, $"/api/avisos/{eliminado}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(e)).StatusCode);
        }

        var pagina = await cliente.GetFromJsonAsync<PaginaPublica>($"/api/publico/avisos?q={marca}");
        Assert.Contains(pagina!.Items, a => a.Id == activo);
        Assert.DoesNotContain(pagina.Items, a => a.Id == pausado);
        Assert.DoesNotContain(pagina.Items, a => a.Id == eliminado);
    }

    [Fact]
    public async Task Busqueda_es_insensible_a_acentos_y_mayusculas()
    {
        using var cliente = factory.ConAprobadorArgos().CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        var marca = Guid.NewGuid().ToString("N");
        var id = await CrearAvisoAsync(cliente, token, $"Cámara réflex {marca}", "poco uso", 500m, "Usado");

        var pagina = await cliente.GetFromJsonAsync<PaginaPublica>($"/api/publico/avisos?q=camara {marca}");
        Assert.Contains(pagina!.Items, a => a.Id == id);
    }

    [Fact]
    public async Task Filtra_por_rango_de_precio()
    {
        using var cliente = factory.ConAprobadorArgos().CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        var marca = Guid.NewGuid().ToString("N");
        var barato = await CrearAvisoAsync(cliente, token, $"Barato {marca}", "d", 50m, "Usado");
        var enRango = await CrearAvisoAsync(cliente, token, $"En rango {marca}", "d", 500m, "Usado");
        var caro = await CrearAvisoAsync(cliente, token, $"Caro {marca}", "d", 5000m, "Usado");

        var pagina = await cliente.GetFromJsonAsync<PaginaPublica>(
            $"/api/publico/avisos?q={marca}&precioMin=100&precioMax=1000");
        Assert.DoesNotContain(pagina!.Items, a => a.Id == barato);
        Assert.Contains(pagina.Items, a => a.Id == enRango);
        Assert.DoesNotContain(pagina.Items, a => a.Id == caro);
    }

    [Fact]
    public async Task Filtra_por_condicion()
    {
        using var cliente = factory.ConAprobadorArgos().CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        var marca = Guid.NewGuid().ToString("N");
        var nuevo = await CrearAvisoAsync(cliente, token, $"Nuevo {marca}", "d", 100m, "Nuevo");
        var usado = await CrearAvisoAsync(cliente, token, $"Usado {marca}", "d", 100m, "Usado");

        var pagina = await cliente.GetFromJsonAsync<PaginaPublica>(
            $"/api/publico/avisos?q={marca}&condicion=Nuevo");
        Assert.Contains(pagina!.Items, a => a.Id == nuevo);
        Assert.DoesNotContain(pagina.Items, a => a.Id == usado);
    }

    [Fact]
    public async Task Detalle_publico_solo_para_activos()
    {
        using var cliente = factory.ConAprobadorArgos().CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        var id = await CrearAvisoAsync(cliente, token, "Detalle", "descripción visible", 100m, "Usado");

        var dto = await cliente.GetFromJsonAsync<AvisoPublicoDetalle>($"/api/publico/avisos/{id}");
        Assert.Equal(id, dto!.Id);
        Assert.NotEqual(Guid.Empty, dto.VendedorId);
        Assert.Equal("Cochabamba", dto.NombreCiudad);

        using (var e = Con(HttpMethod.Delete, $"/api/avisos/{id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(e)).StatusCode);
        }

        var resp = await cliente.GetAsync($"/api/publico/avisos/{id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Detalle_de_id_inexistente_da_404()
    {
        using var cliente = factory.CreateClient();
        var resp = await cliente.GetAsync($"/api/publico/avisos/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Validacion_de_paginacion_da_400()
    {
        using var cliente = factory.CreateClient();
        var resp = await cliente.GetAsync("/api/publico/avisos?tamano=500");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}

sealed file record AvisoPublicoResumen(
    Guid Id, string Titulo, decimal Monto, string Moneda,
    string NombreCategoria, string NombreCiudad, string Condicion, DateTime FechaCreacion);
sealed file record PaginaPublica(AvisoPublicoResumen[] Items, int Pagina, int Tamano, int Total);
sealed file record AvisoPublicoDetalle(
    Guid Id, Guid VendedorId, string Titulo, string Descripcion, decimal Monto, string Moneda,
    string NombreCategoria, string NombreCiudad, string Condicion, DateTime FechaCreacion);
sealed file record TokenAccesoDisc(string AccessToken);
