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
}
