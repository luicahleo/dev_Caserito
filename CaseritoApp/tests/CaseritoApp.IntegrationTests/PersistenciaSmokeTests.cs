using System.Net;
using CaseritoApp.IntegrationTests.Infrastructure;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class PersistenciaSmokeTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task App_arranca_contra_sql_real_y_health_responde_200()
    {
        var cliente = factory.CreateClient();
        var respuesta = await cliente.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
