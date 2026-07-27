using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.IntegrationTests.Infrastructure;
using CaseritoApp.Notifications.Application.Busquedas;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class BusquedasGuardadasEndpointsTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Crear_Listar_y_Eliminar_busqueda_guardada()
    {
        using var cliente = factory.CreateClient();
        var email = $"busquedas-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";

        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, password, "Usuario Alertas", "Cochabamba"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var token = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var crearRespuesta = await cliente.PostAsJsonAsync(
            "/api/busquedas-guardadas",
            new CrearBusquedaGuardadaRequest("iPhone", "Tecnología", "Cochabamba", 1000, 5000, "Usado"));
        Assert.Equal(HttpStatusCode.Created, crearRespuesta.StatusCode);

        var listarRespuesta = await cliente.GetAsync("/api/busquedas-guardadas");
        Assert.Equal(HttpStatusCode.OK, listarRespuesta.StatusCode);

        var busquedas = await listarRespuesta.Content.ReadFromJsonAsync<List<BusquedaGuardadaDto>>();
        Assert.NotNull(busquedas);
        Assert.Single(busquedas);
        Assert.Equal("iphone", busquedas![0].PalabraClave);
        Assert.Equal("tecnología", busquedas[0].Categoria);
        Assert.Equal("cochabamba", busquedas[0].Ciudad);
        Assert.Equal(1000, busquedas[0].PrecioMinimo);
        Assert.Equal(5000, busquedas[0].PrecioMaximo);
        Assert.Equal("usado", busquedas[0].EstadoProducto);

        var eliminarRespuesta = await cliente.DeleteAsync($"/api/busquedas-guardadas/{busquedas[0].Id}");
        Assert.Equal(HttpStatusCode.NoContent, eliminarRespuesta.StatusCode);

        var listarTrasEliminar = await cliente.GetAsync("/api/busquedas-guardadas");
        Assert.Equal(HttpStatusCode.OK, listarTrasEliminar.StatusCode);
        var vacio = await listarTrasEliminar.Content.ReadFromJsonAsync<List<BusquedaGuardadaDto>>();
        Assert.Empty(vacio!);
    }

    [Fact]
    public async Task Get_busquedas_guardadas_sin_token_devuelve_401()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/busquedas-guardadas");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Eliminar_busqueda_ajena_devuelve_404()
    {
        using var cliente = factory.CreateClient();
        var email = $"busquedas-ajena-{Guid.NewGuid():N}@caserito.test";
        const string password = "Password123!";

        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, password, "Usuario Ajeno", "Cochabamba"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var token = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await cliente.DeleteAsync($"/api/busquedas-guardadas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
