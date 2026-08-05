using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CaseritoApp.IntegrationTests.Infrastructure;

namespace CaseritoApp.IntegrationTests.Observability;

public sealed class ObservabilityPipelineTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Peticion_normal_incluye_trace_id_w3c()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var traceId = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        Assert.Matches("^[0-9a-f]{32}$", traceId);
    }

    [Fact]
    public async Task Excepcion_inesperada_devuelve_problem_details_generico_y_correlacionado()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/testing/observability/error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var traceId = Assert.Single(response.Headers.GetValues("X-Trace-Id"));
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = Assert.IsType<JsonElement>(body?.RootElement);
        Assert.Equal("No pudimos completar la solicitud.", root.GetProperty("title").GetString());
        Assert.Matches("^ERR-[0-9A-F]{12}$", root.GetProperty("errorId").GetString()!);
        Assert.Equal(traceId, root.GetProperty("traceId").GetString());
        Assert.DoesNotContain("detalle-sensible", root.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }
}
