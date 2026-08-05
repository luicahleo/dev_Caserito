using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CaseritoApp.Host.Observability;

public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is BadHttpRequestException badRequest)
        {
            httpContext.Response.StatusCode = badRequest.StatusCode;
            return true;
        }

        var errorId = DiagnosticIds.NewErrorId();
        var traceId = DiagnosticContext.GetTraceId(httpContext) ?? httpContext.TraceIdentifier;
        DiagnosticContext.SetErrorId(httpContext, errorId);

        LogUnexpectedError(logger, exception.GetType().FullName ?? exception.GetType().Name, traceId, errorId);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "No pudimos completar la solicitud.",
        };
        problem.Extensions["errorId"] = errorId;
        problem.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problem,
            JsonSerializerOptions.Web,
            cancellationToken);

        return true;
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{EventName}: error inesperado de tipo {ExceptionType}. TraceId={TraceId} ErrorId={ErrorId}")]
    private static partial void LogUnexpectedError(
        ILogger logger,
        string exceptionType,
        string traceId,
        string errorId,
        string eventName = "http.request.failed");
}
