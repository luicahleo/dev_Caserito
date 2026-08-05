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
            ["Category", "ErrorId", "EventName", "Release", "Source", "StatusCode", "TraceId"],
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
            "1.2.3");

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
            null);
}
