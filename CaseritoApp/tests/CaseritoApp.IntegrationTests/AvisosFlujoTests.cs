using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Flujo del dueño sobre sus avisos: crear (verificado), listar, detalle, pausar/reactivar, eliminar, y gates 403.</summary>
public sealed class AvisosFlujoTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad = new("22222222-2222-2222-2222-000000000001");
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string Email(string p) => $"{p}-{Guid.NewGuid():N}@caserito.test";

    private async Task<string> RegistrarYLoguearAsync(HttpClient cliente, string email, string? rolExtra = null)
    {
        var reg = await cliente.PostAsJsonAsync("/api/auth/register", new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        using var scope = factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = (await um.FindByEmailAsync(email))!;
        // Los flujos de KYC y avisos exigen correo confirmado (policy EmailConfirmado).
        usuario.EmailConfirmed = true;
        await um.UpdateAsync(usuario);

        if (rolExtra is not null)
        {
            await um.AddToRoleAsync(usuario, rolExtra);
        }

        return await LoguearAsync(cliente, email);
    }

    private static async Task<string> LoguearAsync(HttpClient cliente, string email)
    {
        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
    }

    private static HttpRequestMessage Con(HttpMethod m, string url, string token)
    {
        var s = new HttpRequestMessage(m, url);
        s.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return s;
    }

    private static MultipartFormDataContent FormularioKyc()
    {
        var c = new MultipartFormDataContent();
        var doc = new ByteArrayContent(_png); doc.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        c.Add(doc, "documento", "ci.png");
        var self = new ByteArrayContent(_png); self.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        c.Add(self, "selfie", "selfie.png");
        return c;
    }

    // Registra un usuario, sube KYC (con ARGOS mockeado a aprobar) y devuelve un token ya verificado.
    private async Task<string> UsuarioVerificadoAsync(HttpClient cliente)
    {
        var email = Email("aviso-user");
        var token = await RegistrarYLoguearAsync(cliente, email);

        using var subir = Con(HttpMethod.Post, "/api/kyc/", token);
        subir.Content = FormularioKyc();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subir)).StatusCode);

        // Re-login para que el JWT traiga el claim verificado=true.
        return await LoguearAsync(cliente, email);
    }

    private static CrearAvisoRequest AvisoValido() =>
        new("Bicicleta", "Rodado 29, poco uso", 1200m, "Usado", _categoria, _ciudad);

    [Fact]
    public async Task Verificado_crea_lista_detalle_pausa_reactiva_y_elimina()
    {
        using var cliente = factory.ConAprobadorArgos().CreateClient();
        var token = await UsuarioVerificadoAsync(cliente);

        // Crear
        using var crear = Con(HttpMethod.Post, "/api/avisos/", token);
        crear.Content = JsonContent.Create(AvisoValido());
        var respCrear = await cliente.SendAsync(crear);
        Assert.Equal(HttpStatusCode.Created, respCrear.StatusCode);
        var creado = await respCrear.Content.ReadFromJsonAsync<AvisoCreadoResponse>();
        var avisoId = creado!.Id;

        // Listar
        using var listar = Con(HttpMethod.Get, "/api/avisos/mios", token);
        var lista = await (await cliente.SendAsync(listar)).Content.ReadFromJsonAsync<PaginaAvisos>();
        Assert.Contains(lista!.Items, a => a.Id == avisoId);

        // Detalle
        using var detalle = Con(HttpMethod.Get, $"/api/avisos/mios/{avisoId}", token);
        Assert.Equal(HttpStatusCode.OK, (await cliente.SendAsync(detalle)).StatusCode);

        // Pausar y reactivar
        using var pausar = Con(HttpMethod.Post, $"/api/avisos/{avisoId}/pausar", token);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(pausar)).StatusCode);
        using var reactivar = Con(HttpMethod.Post, $"/api/avisos/{avisoId}/reactivar", token);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(reactivar)).StatusCode);

        // Eliminar (soft-delete) → luego 404 en detalle y ausente del listado
        using var eliminar = Con(HttpMethod.Delete, $"/api/avisos/{avisoId}", token);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(eliminar)).StatusCode);
        using var detalle2 = Con(HttpMethod.Get, $"/api/avisos/mios/{avisoId}", token);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.SendAsync(detalle2)).StatusCode);
        using var listar2 = Con(HttpMethod.Get, "/api/avisos/mios", token);
        var lista2 = await (await cliente.SendAsync(listar2)).Content.ReadFromJsonAsync<PaginaAvisos>();
        Assert.DoesNotContain(lista2!.Items, a => a.Id == avisoId);
    }

    [Fact]
    public async Task No_verificado_no_puede_crear()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("aviso-noverif"));

        using var crear = Con(HttpMethod.Post, "/api/avisos/", token);
        crear.Content = JsonContent.Create(AvisoValido());
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.SendAsync(crear)).StatusCode);
    }

    [Fact]
    public async Task AdminPlataforma_sin_kyc_puede_crear_sin_generar_verificacion_falsa()
    {
        using var cliente = factory.CreateClient();
        var email = Email("aviso-admin-exento");
        var token = await RegistrarYLoguearAsync(cliente, email, RolesApp.AdminPlataforma);

        using var crear = Con(HttpMethod.Post, "/api/avisos/", token);
        crear.Content = JsonContent.Create(AvisoValido());
        Assert.Equal(HttpStatusCode.Created, (await cliente.SendAsync(crear)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var consultaKyc = scope.ServiceProvider.GetRequiredService<IConsultaVerificacionKyc>();
        var usuario = (await usuarios.FindByEmailAsync(email))!;
        Assert.False(await consultaKyc.EstaVerificadoAsync(usuario.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData(RolesApp.AdminKyc)]
    [InlineData(RolesApp.Moderador)]
    public async Task Rol_no_exento_sin_kyc_continua_bloqueado(string rol)
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("aviso-rol-no-exento"), rol);

        using var crear = Con(HttpMethod.Post, "/api/avisos/", token);
        crear.Content = JsonContent.Create(AvisoValido());
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.SendAsync(crear)).StatusCode);
    }

    [Fact]
    public async Task No_dueno_no_puede_editar()
    {
        using var cliente = factory.ConAprobadorArgos().CreateClient();
        var dueno = await UsuarioVerificadoAsync(cliente);
        var otro = await UsuarioVerificadoAsync(cliente);

        using var crear = Con(HttpMethod.Post, "/api/avisos/", dueno);
        crear.Content = JsonContent.Create(AvisoValido());
        var avisoId = (await (await cliente.SendAsync(crear)).Content.ReadFromJsonAsync<AvisoCreadoResponse>())!.Id;

        using var editar = Con(HttpMethod.Put, $"/api/avisos/{avisoId}", otro);
        editar.Content = JsonContent.Create(new EditarAvisoRequest("Hack", "Intento", 1m, "Nuevo", _categoria, _ciudad));
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.SendAsync(editar)).StatusCode);
    }

    [Fact]
    public async Task Categorias_y_ciudades_de_referencia_estan_sembradas()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("aviso-ref"));

        using var cats = Con(HttpMethod.Get, "/api/catalogo/categorias", token);
        var categorias = await (await cliente.SendAsync(cats)).Content.ReadFromJsonAsync<CategoriaRef[]>();
        Assert.Contains(categorias!, c => c.Id == _categoria);

        using var ciudades = Con(HttpMethod.Get, "/api/catalogo/ciudades", token);
        var lista = await (await cliente.SendAsync(ciudades)).Content.ReadFromJsonAsync<CiudadRef[]>();
        Assert.Contains(lista!, c => c.Id == _ciudad);
    }

    [Fact]
    public async Task Catalogo_de_referencia_es_accesible_anonimo()
    {
        var cliente = factory.CreateClient();

        var respCats = await cliente.GetAsync("/api/catalogo/categorias");
        Assert.Equal(HttpStatusCode.OK, respCats.StatusCode);
        var categorias = await respCats.Content.ReadFromJsonAsync<CategoriaRef[]>();
        Assert.Contains(categorias!, c => c.Id == _categoria);

        var respCiudades = await cliente.GetAsync("/api/catalogo/ciudades");
        Assert.Equal(HttpStatusCode.OK, respCiudades.StatusCode);
        var ciudades = await respCiudades.Content.ReadFromJsonAsync<CiudadRef[]>();
        Assert.Contains(ciudades!, c => c.Id == _ciudad);
    }
}

sealed file record AvisoResumen(Guid Id, string Titulo, decimal Monto, string Moneda, Guid CategoriaId, Guid CiudadId, string Condicion, string Estado, DateTime FechaCreacion);
sealed file record PaginaAvisos(AvisoResumen[] Items, int Pagina, int Tamano, int Total);
sealed file record CategoriaRef(Guid Id, string Nombre);
sealed file record CiudadRef(Guid Id, string Nombre);
