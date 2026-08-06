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
            .WithMetadata(new RequestSizeLimitAttribute(16384));

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
            ["SessionId"] = report.SessionId,
        }))
        {
            LogFrontendDiagnostic(logger);
        }

        if (report.FlowEvents is { } flowEvents)
        {
            foreach (var flowEvent in flowEvents)
            {
                using (logger.BeginScope(new Dictionary<string, object?>
                {
                    ["SessionId"] = report.SessionId,
                    ["Seq"] = flowEvent.Seq,
                    ["EventName"] = flowEvent.EventName,
                    ["Detail"] = flowEvent.Detail,
                    ["TraceId"] = flowEvent.TraceId,
                    ["StatusCode"] = flowEvent.StatusCode,
                    ["DurationMs"] = flowEvent.DurationMs,
                }))
                {
                    LogFrontendFlow(logger);
                }
            }
        }

        return Results.Accepted();
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{LogEventName}: evento técnico del navegador")]
    private static partial void LogFrontendDiagnostic(
        ILogger logger,
        string logEventName = "frontend.error");

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{LogEventName}: evento de flujo del navegador")]
    private static partial void LogFrontendFlow(
        ILogger logger,
        string logEventName = "frontend.flow");
}
