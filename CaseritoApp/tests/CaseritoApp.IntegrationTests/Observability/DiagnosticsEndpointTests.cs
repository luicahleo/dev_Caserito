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
    public async Task Rechaza_cuerpos_mayores_a_dieciseis_kibibytes()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(
            $"{{\"errorId\":\"ERR-0123456789AB\",\"padding\":\"{new string('a', 17000)}\"}}",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/diagnosticos/frontend", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Acepta_reporte_con_sesion_y_flujo_permitidos()
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();
        report["sessionId"] = "SES-0123456789AB";
        report["flowEvents"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["seq"] = 1,
                ["timestamp"] = "2026-08-06T10:00:00.000Z",
                ["eventName"] = "flow.navigation",
                ["detail"] = "/avisos/:id",
            },
            new Dictionary<string, object?>
            {
                ["seq"] = 2,
                ["timestamp"] = "2026-08-06T10:00:01.000Z",
                ["eventName"] = "flow.api_call",
                ["detail"] = "GET /api/avisos/:id",
                ["traceId"] = "0123456789abcdef0123456789abcdef",
                ["statusCode"] = 200,
                ["durationMs"] = 42.5,
            },
        };

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Acepta_el_maximo_de_eventos_de_flujo_superando_cuatro_kibibytes()
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();
        report["sessionId"] = "SES-0123456789AB";
        report["flowEvents"] = Enumerable.Range(1, 50)
            .Select(seq => new Dictionary<string, object?>
            {
                ["seq"] = seq,
                ["timestamp"] = "2026-08-06T10:00:00.000Z",
                ["eventName"] = "flow.api_call",
                ["detail"] = $"GET /api/recurso/{new string('a', 100)}",
            })
            .ToArray();

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Theory]
    [InlineData("ses-0123456789ab")]
    [InlineData("SES-no-valido")]
    public async Task Rechaza_sesion_fuera_del_patron(string sessionId)
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();
        report["sessionId"] = sessionId;

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rechaza_eventos_de_flujo_fuera_de_la_lista_permitida()
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();
        report["flowEvents"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["seq"] = 1,
                ["timestamp"] = "2026-08-06T10:00:00.000Z",
                ["eventName"] = "usuario.contenido",
                ["detail"] = "texto libre del usuario",
            },
        };

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(
            "texto libre del usuario",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
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
