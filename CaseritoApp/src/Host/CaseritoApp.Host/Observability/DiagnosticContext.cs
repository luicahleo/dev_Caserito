namespace CaseritoApp.Host.Observability;

public static class DiagnosticContext
{
    private const string ErrorIdKey = "CaseritoApp.Observability.ErrorId";
    private const string TraceIdKey = "CaseritoApp.Observability.TraceId";

    public static void SetErrorId(HttpContext context, string errorId) =>
        context.Items[ErrorIdKey] = errorId;

    public static string? GetErrorId(HttpContext context) =>
        context.Items.TryGetValue(ErrorIdKey, out var value) ? value as string : null;

    public static void SetTraceId(HttpContext context, string traceId) =>
        context.Items[TraceIdKey] = traceId;

    public static string? GetTraceId(HttpContext context) =>
        context.Items.TryGetValue(TraceIdKey, out var value) ? value as string : null;
}
