using CaseritoApp.Host.Observability;

namespace CaseritoApp.ArchitectureTests.Observability;

public sealed class DiagnosticContractTests
{
    [Fact]
    public void NuevoErrorId_genera_codigos_opacos_validos_y_distintos()
    {
        var primero = DiagnosticIds.NewErrorId();
        var segundo = DiagnosticIds.NewErrorId();

        Assert.Matches("^ERR-[0-9A-F]{12}$", primero);
        Assert.NotEqual(primero, segundo);
    }

    [Fact]
    public void Contrato_solo_expone_campos_seguros()
    {
        var propiedades = typeof(ClientDiagnosticReport)
            .GetProperties()
            .Select(propiedad => propiedad.Name)
            .Order()
            .ToArray();

        Assert.Equal(
            ["Category", "ErrorId", "EventName", "FlowEvents", "Release", "SessionId", "Source", "StatusCode", "TraceId"],
            propiedades);
    }

    [Fact]
    public void Validador_acepta_un_reporte_permitido()
    {
        var reporte = new ClientDiagnosticReport(
            "ERR-0123456789AB",
            "http.server_failed",
            "server",
            "http",
            "0123456789abcdef0123456789abcdef",
            503,
            "1.2.3",
            null,
            null);

        Assert.True(ClientDiagnosticReportValidator.IsValid(reporte));
    }

    [Theory]
    [MemberData(nameof(ReportesInvalidos))]
    public void Validador_rechaza_valores_fuera_de_la_lista_permitida(ClientDiagnosticReport reporte)
    {
        Assert.False(ClientDiagnosticReportValidator.IsValid(reporte));
    }

    public static TheoryData<ClientDiagnosticReport> ReportesInvalidos() =>
        new()
        {
            ReporteValido() with { ErrorId = "ERR-no-valido" },
            ReporteValido() with { EventName = "usuario.escribio.contenido" },
            ReporteValido() with { Category = "mensaje privado" },
            ReporteValido() with { Source = "formulario" },
            ReporteValido() with { TraceId = "trace-no-valido" },
            ReporteValido() with { StatusCode = 404 },
            ReporteValido() with { Release = "version con espacios" },
            ReporteValido() with { EventName = null! },
            ReporteValido() with { ErrorId = null! },
        };

    private static ClientDiagnosticReport ReporteValido() =>
        new(
            "ERR-0123456789AB",
            "router.unexpected",
            "unexpected",
            "router",
            null,
            null,
            null,
            null,
            null);

    [Fact]
    public void Validador_acepta_sesion_y_flujo_permitidos()
    {
        var reporte = ReporteValido() with
        {
            SessionId = "SES-0123456789AB",
            FlowEvents =
            [
                new ClientFlowEvent(
                    1,
                    new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.Zero),
                    "flow.navigation",
                    "/avisos/:id",
                    null,
                    null,
                    null),
                new ClientFlowEvent(
                    2,
                    new DateTimeOffset(2026, 8, 6, 10, 0, 1, TimeSpan.Zero),
                    "flow.api_call",
                    "GET /api/avisos/:id",
                    "0123456789abcdef0123456789abcdef",
                    200,
                    42.5),
            ],
        };

        Assert.True(ClientDiagnosticReportValidator.IsValid(reporte));
    }

    [Theory]
    [MemberData(nameof(FlujosInvalidos))]
    public void Validador_rechaza_flujos_fuera_de_la_lista_permitida(
        string? sessionId,
        IReadOnlyList<ClientFlowEvent>? flowEvents)
    {
        var reporte = ReporteValido() with { SessionId = sessionId, FlowEvents = flowEvents };

        Assert.False(ClientDiagnosticReportValidator.IsValid(reporte));
    }

    public static TheoryData<string?, IReadOnlyList<ClientFlowEvent>?> FlujosInvalidos() =>
        new()
        {
            { "SES-no-valido", null },
            { "ses-0123456789AB", null },
            {
                "SES-0123456789AB",
                Enumerable.Range(1, 51).Select(EventoValido).ToArray()
            },
            { "SES-0123456789AB", [EventoValido(1) with { EventName = "usuario.contenido" }] },
            { "SES-0123456789AB", [EventoValido(1) with { Detail = "" }] },
            { "SES-0123456789AB", [EventoValido(1) with { Detail = new string('a', 121) }] },
            { "SES-0123456789AB", [EventoValido(1) with { Seq = 0 }] },
            { "SES-0123456789AB", [EventoValido(1) with { StatusCode = 99 }] },
            { "SES-0123456789AB", [EventoValido(1) with { DurationMs = -1 }] },
            {
                "SES-0123456789AB",
                [EventoValido(1) with { Timestamp = new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.FromHours(2)) }]
            },
        };

    private static ClientFlowEvent EventoValido(int seq) =>
        new(
            seq,
            new DateTimeOffset(2026, 8, 6, 10, 0, 0, TimeSpan.Zero),
            "flow.navigation",
            "/explorar",
            null,
            null,
            null);

    [Theory]
    [InlineData("SES-0123456789AB", true)]
    [InlineData("SES-0123456789ab", false)]
    [InlineData("ERR-0123456789AB", false)]
    [InlineData("SES-012345678", false)]
    [InlineData(null, false)]
    public void IsSessionId_solo_acepta_el_patron_opaco(string? valor, bool esperado)
    {
        Assert.Equal(esperado, DiagnosticIds.IsSessionId(valor));
    }
}
