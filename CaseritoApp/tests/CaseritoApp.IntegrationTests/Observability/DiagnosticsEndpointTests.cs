using System.Net;
using System.Net.Http.Json;
using System.Text;
using CaseritoApp.IntegrationTests.Infrastructure;

namespace CaseritoApp.IntegrationTests.Observability;

public sealed class DiagnosticsEndpointTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Acepta_reporte_permitido_sin_autenticacion()
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength);
    }

    [Theory]
    [InlineData("eventName", "usuario.contenido")]
    [InlineData("category", "mensaje privado")]
    [InlineData("source", "formulario")]
    [InlineData("errorId", "ERR-no-valido")]
    [InlineData("traceId", "trace-no-valido")]
    public async Task Rechaza_valores_fuera_de_la_lista_permitida(string property, string value)
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();
        report[property] = value;

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rechaza_propiedades_desconocidas()
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();
        report["mensaje"] = "contenido-que-no-debe-aceptarse";

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(
            "contenido-que-no-debe-aceptarse",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rechaza_cuerpos_mayores_a_cuatro_kibibytes()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(
            $"{{\"errorId\":\"ERR-0123456789AB\",\"padding\":\"{new string('a', 5000)}\"}}",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/diagnosticos/frontend", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private static Dictionary<string, object?> ReporteValido() =>
        new()
        {
            ["errorId"] = "ERR-0123456789AB",
            ["eventName"] = "router.unexpected",
            ["category"] = "unexpected",
            ["source"] = "router",
            ["traceId"] = null,
            ["statusCode"] = null,
            ["release"] = "1.2.3",
        };
}
