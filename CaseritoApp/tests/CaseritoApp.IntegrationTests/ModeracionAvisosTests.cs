using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Flujo HTTP de reportes, cola, decisiones y visibilidad pública.</summary>
public sealed class ModeracionAvisosTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly Guid _categoria = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid _ciudad = new("22222222-2222-2222-2222-000000000001");

    [Fact]
    public async Task Reportar_requiere_autenticacion_y_rechaza_autorreporte_y_duplicado()
    {
        using var cliente = factory.CreateClient();
        var vendedor = await RegistrarAsync(cliente, "vendedor");
        var reportante = await RegistrarAsync(cliente, "reportante");
        var avisoId = await CrearAvisoAsync(vendedor.Id);

        var sinToken = await cliente.PostAsJsonAsync(
            $"/api/avisos/{avisoId}/reportes", new ReportarAvisoRequest("Otro", null));
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);

        using var propio = Autorizada(HttpMethod.Post, $"/api/avisos/{avisoId}/reportes", vendedor.Token);
        propio.Content = JsonContent.Create(new ReportarAvisoRequest("Otro", "Propio"));
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.SendAsync(propio)).StatusCode);

        Assert.Equal(HttpStatusCode.Created,
            (await ReportarAsync(cliente, avisoId, reportante.Token, "EstafaOEngano")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await ReportarAsync(cliente, avisoId, reportante.Token, "DuplicadoOSpam")).StatusCode);
    }

    [Fact]
    public async Task Moderador_oculta_restaura_y_elimina_el_aviso_reportado()
    {
        using var cliente = factory.CreateClient();
        var vendedor = await RegistrarAsync(cliente, "vendedor-flujo");
        var reportante = await RegistrarAsync(cliente, "reportante-flujo");
        var moderador = await RegistrarAsync(cliente, "moderador", RolesApp.Moderador);
        var avisoId = await CrearAvisoAsync(vendedor.Id);
        var claveFoto = await AgregarFotoAsync(avisoId);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/fotos/{claveFoto}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await ReportarAsync(cliente, avisoId, reportante.Token, "ProductoProhibido")).StatusCode);

        using var colaSinPermiso = Autorizada(HttpMethod.Get, "/api/admin/moderacion/avisos", reportante.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.SendAsync(colaSinPermiso)).StatusCode);

        using var cola = Autorizada(HttpMethod.Get, "/api/admin/moderacion/avisos", moderador.Token);
        var pagina = await (await cliente.SendAsync(cola)).Content.ReadFromJsonAsync<PaginaModeracion>();
        Assert.Contains(pagina!.Items, item => item.AvisoId == avisoId && item.CantidadReportes == 1);

        Assert.Equal(HttpStatusCode.NoContent,
            (await AccionAsync(cliente, $"/api/admin/moderacion/avisos/{avisoId}/ocultar", moderador.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/api/publico/avisos/{avisoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/api/fotos/{claveFoto}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await AccionAsync(cliente, $"/api/admin/moderacion/avisos/{avisoId}/restaurar", moderador.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/publico/avisos/{avisoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/fotos/{claveFoto}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await AccionAsync(cliente, $"/api/admin/moderacion/avisos/{avisoId}/eliminar", moderador.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/api/publico/avisos/{avisoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/api/fotos/{claveFoto}")).StatusCode);

        using var editar = Autorizada(HttpMethod.Put, $"/api/avisos/{avisoId}", vendedor.Token);
        editar.Content = JsonContent.Create(new EditarAvisoRequest(
            "No editable", "Eliminado por moderación", 10m, "Usado", _categoria, _ciudad));
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.SendAsync(editar)).StatusCode);
    }

    [Fact]
    public async Task Descartar_habilita_un_nuevo_reporte_del_mismo_usuario()
    {
        using var cliente = factory.CreateClient();
        var vendedor = await RegistrarAsync(cliente, "vendedor-descartar");
        var reportante = await RegistrarAsync(cliente, "reportante-descartar");
        var moderador = await RegistrarAsync(cliente, "moderador-descartar", RolesApp.Moderador);
        var avisoId = await CrearAvisoAsync(vendedor.Id);
        await ReportarAsync(cliente, avisoId, reportante.Token, "Otro");

        using var detalle = Autorizada(
            HttpMethod.Get, $"/api/admin/moderacion/avisos/{avisoId}", moderador.Token);
        var dto = await (await cliente.SendAsync(detalle)).Content.ReadFromJsonAsync<DetalleModeracion>();
        var reporteId = Assert.Single(dto!.Reportes).Id;

        Assert.Equal(HttpStatusCode.NoContent,
            (await AccionAsync(cliente, $"/api/admin/moderacion/reportes/{reporteId}/descartar", moderador.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await ReportarAsync(cliente, avisoId, reportante.Token, "ContenidoInapropiado")).StatusCode);
    }

    [Fact]
    public async Task Eliminar_por_el_dueno_atiende_los_reportes_pendientes()
    {
        using var cliente = factory.CreateClient();
        var vendedor = await RegistrarAsync(cliente, "vendedor-elimina");
        var reportante = await RegistrarAsync(cliente, "reportante-elimina");
        var moderador = await RegistrarAsync(cliente, "moderador-elimina", RolesApp.Moderador);
        var avisoId = await CrearAvisoAsync(vendedor.Id);
        await ReportarAsync(cliente, avisoId, reportante.Token, "Otro");

        using var eliminar = Autorizada(HttpMethod.Delete, $"/api/avisos/{avisoId}", vendedor.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(eliminar)).StatusCode);

        using var pendientes = Autorizada(
            HttpMethod.Get, "/api/admin/moderacion/avisos?estado=Pendiente", moderador.Token);
        var paginaPendiente = await (await cliente.SendAsync(pendientes)).Content.ReadFromJsonAsync<PaginaModeracion>();
        Assert.DoesNotContain(paginaPendiente!.Items, item => item.AvisoId == avisoId);

        using var atendidos = Autorizada(
            HttpMethod.Get, "/api/admin/moderacion/avisos?estado=Atendido", moderador.Token);
        var paginaAtendida = await (await cliente.SendAsync(atendidos)).Content.ReadFromJsonAsync<PaginaModeracion>();
        Assert.Contains(paginaAtendida!.Items, item => item.AvisoId == avisoId);
    }

    private async Task<(Guid Id, string Token)> RegistrarAsync(
        HttpClient cliente, string prefijo, string? rol = null)
    {
        var email = $"{prefijo}-{Guid.NewGuid():N}@caserito.test";
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register", new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = (await usuarios.FindByEmailAsync(email))!;
            id = usuario.Id;
            if (rol is not null)
            {
                Assert.True((await usuarios.AddToRoleAsync(usuario, rol)).Succeeded);
            }
        }

        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        var token = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        return (id, token);
    }

    private async Task<Guid> CrearAvisoAsync(Guid vendedorId)
    {
        var aviso = Aviso.Crear(
            vendedorId, "Aviso moderable", "Descripción pública",
            Dinero.Crear(100m, Moneda.BOB).Valor, _categoria, _ciudad,
            CondicionArticulo.Usado, DateTime.UtcNow);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        db.Avisos.Add(aviso);
        await db.SaveChangesAsync();
        return aviso.Id;
    }

    private async Task<string> AgregarFotoAsync(Guid avisoId)
    {
        using var scope = factory.Services.CreateScope();
        var almacen = scope.ServiceProvider.GetRequiredService<IAlmacenFotosAviso>();
        var clave = await almacen.GuardarAsync(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png", CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var aviso = (await db.Avisos.FindAsync(avisoId))!;
        Assert.True(aviso.AgregarFoto(clave, "image/png").EsExito);
        await db.SaveChangesAsync();
        return clave;
    }

    private static async Task<HttpResponseMessage> ReportarAsync(
        HttpClient cliente, Guid avisoId, string token, string motivo)
    {
        using var solicitud = Autorizada(HttpMethod.Post, $"/api/avisos/{avisoId}/reportes", token);
        solicitud.Content = JsonContent.Create(new ReportarAvisoRequest(motivo, "Detalle de prueba"));
        return await cliente.SendAsync(solicitud);
    }

    private static async Task<HttpResponseMessage> AccionAsync(HttpClient cliente, string url, string token)
    {
        using var solicitud = Autorizada(HttpMethod.Post, url, token);
        return await cliente.SendAsync(solicitud);
    }

    private static HttpRequestMessage Autorizada(HttpMethod metodo, string url, string token)
    {
        var solicitud = new HttpRequestMessage(metodo, url);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return solicitud;
    }
}

sealed file record ResumenModeracion(Guid AvisoId, int CantidadReportes);
sealed file record PaginaModeracion(ResumenModeracion[] Items, int Pagina, int Tamano, int Total);
sealed file record ReporteModeracion(Guid Id, string Estado);
sealed file record DetalleModeracion(Guid AvisoId, ReporteModeracion[] Reportes);
