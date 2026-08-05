using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace CaseritoApp.Host.Observability;

public sealed partial class RequestObservabilityMiddleware(
    RequestDelegate next,
    ILogger<RequestObservabilityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        context.TraceIdentifier = traceId;
        DiagnosticContext.SetTraceId(context, traceId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
            return Task.CompletedTask;
        });
        var startedAt = Stopwatch.GetTimestamp();

        using (logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId }))
        {
            await next(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                var routePattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
                var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                var errorId = DiagnosticContext.GetErrorId(context);
                using (logger.BeginScope(new Dictionary<string, object?> { ["ErrorId"] = errorId }))
                {
                    LogRequestCompleted(
                        logger,
                        context.Request.Method,
                        routePattern,
                        context.Response.StatusCode,
                        durationMs);
                }
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{EventName}: {Method} {RoutePattern} respondió {StatusCode} en {DurationMs} ms")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        string method,
        string routePattern,
        int statusCode,
        double durationMs,
        string eventName = "http.request.completed");
}
