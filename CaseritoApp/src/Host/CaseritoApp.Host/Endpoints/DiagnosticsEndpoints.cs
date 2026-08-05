using CaseritoApp.Host.Observability;
using Microsoft.AspNetCore.Mvc;

namespace CaseritoApp.Host.Endpoints;

public static partial class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/diagnosticos/frontend", ReceiveAsync)
            .Accepts<ClientDiagnosticReport>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .RequireRateLimiting("diagnosticos-frontend")
            .WithMetadata(new RequestSizeLimitAttribute(4096));

        return app;
    }

    private static IResult ReceiveAsync(
        ClientDiagnosticReport report,
        ILogger<ClientDiagnosticReport> logger)
    {
        if (!ClientDiagnosticReportValidator.IsValid(report))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "El diagnóstico no es válido.");
        }

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["ErrorId"] = report.ErrorId,
            ["ClientEventName"] = report.EventName,
            ["Category"] = report.Category,
            ["Source"] = report.Source,
            ["TraceId"] = report.TraceId,
            ["StatusCode"] = report.StatusCode,
            ["Release"] = report.Release,
        }))
        {
            LogFrontendDiagnostic(logger);
        }

        return Results.Accepted();
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{LogEventName}: evento técnico del navegador")]
    private static partial void LogFrontendDiagnostic(
        ILogger logger,
        string logEventName = "frontend.error");
}
