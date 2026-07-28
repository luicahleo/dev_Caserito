using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.IntegrationTests.Infrastructure;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Tests de integración de subida, borrado y servicio de fotos de avisos.
/// </summary>
public sealed class FotosAvisoIntegrationTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad = new("22222222-2222-2222-2222-000000000001");

    // PNG mínimo válido (magic bytes correctos).
    private static readonly byte[] _pngValido =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    private static string Email(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

    private static HttpRequestMessage Autorizada(HttpMethod m, string url, string token)
    {
        var req = new HttpRequestMessage(m, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    /// <summary>Registra un usuario, sube KYC (con ARGOS mockeado a aprobar) y devuelve un token verificado.</summary>
    private static async Task<string> UsuarioVerificadoAsync(HttpClient cliente)
    {
        var email = Email("fotos-user");
        var reg = await cliente.PostAsJsonAsync("/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario Fotos", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        var token = await LoguearAsync(cliente, email);

        // Subir KYC; el verificador mockeado aprueba automáticamente.
        var formKyc = FormularioKyc();
        using var subKyc = Autorizada(HttpMethod.Post, "/api/kyc/", token);
        subKyc.Content = formKyc;
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subKyc)).StatusCode);

        // Re-login para que el JWT traiga el claim verificado=true.
        return await LoguearAsync(cliente, email);
    }

    private static async Task<string> LoguearAsync(HttpClient cliente, string email)
    {
        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
    }

    private static MultipartFormDataContent FormularioKyc()
    {
        var form = new MultipartFormDataContent();
        var doc = new ByteArrayContent(_pngValido); doc.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var selfie = new ByteArrayContent(_pngValido); selfie.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(doc, "documento", "ci.png");
        form.Add(selfie, "selfie", "selfie.png");
        return form;
    }

    private static MultipartFormDataContent FormularioFoto(byte[]? contenido = null)
    {
        var bytes = contenido ?? _pngValido;
        var form = new MultipartFormDataContent();
        var chunk = new ByteArrayContent(bytes);
        chunk.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(chunk, "file", "foto.png");
        return form;
    }

    private async Task<(HttpClient cliente, string token, Guid avisoId)> PrepararAsync()
    {
        var cliente = factory.ConAprobadorArgos().CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        using var crear = Autorizada(HttpMethod.Post, "/api/avisos/", token);
        crear.Content = JsonContent.Create(
            new CrearAvisoRequest("Aviso fotos", "Descripción para test de fotos.", 150m, "Nuevo", _categoria, _ciudad));
        var resp = await cliente.SendAsync(crear);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var avisoId = (await resp.Content.ReadFromJsonAsync<AvisoCreadoResponse>())!.Id;

        return (cliente, token, avisoId);
    }

    [Fact]
    public async Task SubirFoto_FotoValida_Devuelve201ConId()
    {
        var (cliente, token, avisoId) = await PrepararAsync();

        using var req = Autorizada(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos", token);
        req.Content = FormularioFoto();
        var resp = await cliente.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<FotoCreadaResponse>();
        Assert.NotEqual(Guid.Empty, body!.Id);
    }

    [Fact]
    public async Task SubirSextaFoto_DevuelveError400()
    {
        var (cliente, token, avisoId) = await PrepararAsync();

        for (var i = 0; i < 5; i++)
        {
            using var r = Autorizada(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos", token);
            r.Content = FormularioFoto();
            Assert.Equal(HttpStatusCode.Created, (await cliente.SendAsync(r)).StatusCode);
        }

        using var extra = Autorizada(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos", token);
        extra.Content = FormularioFoto();
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.SendAsync(extra)).StatusCode);
    }

    [Fact]
    public async Task SubirFoto_MagicBytesInvalidos_DevuelveError400()
    {
        var (cliente, token, avisoId) = await PrepararAsync();

        using var req = Autorizada(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos", token);
        req.Content = FormularioFoto([0x00, 0x01, 0x02, 0x03]);
        var resp = await cliente.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task BorrarFoto_PropiaYExistente_Devuelve204()
    {
        var (cliente, token, avisoId) = await PrepararAsync();

        // Subir.
        using var subir = Autorizada(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos", token);
        subir.Content = FormularioFoto();
        var subResp = await cliente.SendAsync(subir);
        Assert.Equal(HttpStatusCode.Created, subResp.StatusCode);
        var fotoId = (await subResp.Content.ReadFromJsonAsync<FotoCreadaResponse>())!.Id;

        // Borrar.
        using var borrar = Autorizada(HttpMethod.Delete, $"/api/avisos/{avisoId}/fotos/{fotoId}", token);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(borrar)).StatusCode);
    }

    [Fact]
    public async Task ObtenerFoto_ClaveValida_DevuelveBytesYContentType()
    {
        var (cliente, token, avisoId) = await PrepararAsync();

        // Subir foto.
        using var subir = Autorizada(HttpMethod.Post, $"/api/avisos/{avisoId}/fotos", token);
        subir.Content = FormularioFoto();
        Assert.Equal(HttpStatusCode.Created, (await cliente.SendAsync(subir)).StatusCode);

        // El detalle público debe incluir la foto con URL.
        var detalle = await cliente.GetFromJsonAsync<AvisoPublicoDetalleConFotos>(
            $"/api/publico/avisos/{avisoId}");
        Assert.NotNull(detalle);
        Assert.NotEmpty(detalle!.Fotos);

        // Descargar la foto por la URL del DTO.
        var fotoResp = await cliente.GetAsync(detalle.Fotos[0].Url);
        Assert.Equal(HttpStatusCode.OK, fotoResp.StatusCode);
        Assert.Equal("image/png", fotoResp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ObtenerFoto_ClaveInexistente_Devuelve404()
    {
        var cliente = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/api/fotos/clavequenoexiste")).StatusCode);
    }
}

sealed file record FotoRespuesta(Guid Id, string Url, int Orden);
sealed file record AvisoPublicoDetalleConFotos(Guid Id, string Titulo, string Descripcion, FotoRespuesta[] Fotos);
