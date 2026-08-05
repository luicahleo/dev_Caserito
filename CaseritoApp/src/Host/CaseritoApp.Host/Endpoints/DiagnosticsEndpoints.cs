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

        LogFrontendDiagnostic(
            logger,
            report.ErrorId,
            report.EventName,
            report.Category,
            report.Source,
            report.TraceId,
            report.StatusCode,
            report.Release);

        return Results.Accepted();
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{LogEventName}: evento {ClientEventName}, categoría {Category}, origen {Source}. "
            + "ErrorId={ErrorId} TraceId={TraceId} StatusCode={StatusCode} Release={Release}")]
    private static partial void LogFrontendDiagnostic(
        ILogger logger,
        string errorId,
        string clientEventName,
        string category,
        string source,
        string? traceId,
        int? statusCode,
        string? release,
        string logEventName = "frontend.error");
}
