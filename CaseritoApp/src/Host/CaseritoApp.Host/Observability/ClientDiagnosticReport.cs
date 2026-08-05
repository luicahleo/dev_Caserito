using System.Text.Json.Serialization;

namespace CaseritoApp.Host.Observability;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClientDiagnosticReport(
    string ErrorId,
    string EventName,
    string Category,
    string Source,
    string? TraceId,
    int? StatusCode,
    string? Release);

public static class ClientDiagnosticReportValidator
{
    public static bool IsValid(ClientDiagnosticReport report) =>
        IsErrorId(report.ErrorId)
        && IsEventName(report.EventName)
        && IsCategory(report.Category)
        && IsSource(report.Source)
        && IsTraceId(report.TraceId)
        && IsStatusCode(report.StatusCode)
        && IsRelease(report.Release);

    private static bool IsErrorId(string? value) =>
        value is not null
        && value.Length == 16
        && value.StartsWith("ERR-", StringComparison.Ordinal)
        && value.AsSpan(4).ToString().All(character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static bool IsEventName(string value) => value is
        "router.unexpected"
        or "window.unexpected"
        or "promise.unhandled"
        or "http.network_failed"
        or "http.server_failed"
        or "chunk.load_failed"
        or "flow.critical_failed";

    private static bool IsCategory(string value) => value is
        "unexpected" or "network" or "server" or "chunk" or "critical";

    private static bool IsSource(string value) => value is
        "router" or "window" or "promise" or "http" or "flow";

    private static bool IsTraceId(string? value) =>
        value is null
        || value.Length == 32
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsStatusCode(int? value) => value is null or >= 500 and <= 599;

    private static bool IsRelease(string? value) =>
        value is null
        || value.Length is >= 1 and <= 40
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
}
