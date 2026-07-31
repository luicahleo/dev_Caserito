using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_responde_200()
    {
        var cliente = factory
            .WithWebHostBuilder(b => b.UseEnvironment("Testing"))
            .CreateClient();
        var respuesta = await cliente.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Ruta_api_inexistente_devuelve_404_no_spa()
    {
        var cliente = factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"))
            .CreateClient();

        var respuesta = await cliente.GetAsync("/api/ruta-inexistente");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
