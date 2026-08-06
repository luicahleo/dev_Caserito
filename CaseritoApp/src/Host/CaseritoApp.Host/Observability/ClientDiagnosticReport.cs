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
    string? Release,
    string? SessionId,
    IReadOnlyList<ClientFlowEvent>? FlowEvents);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClientFlowEvent(
    int Seq,
    DateTimeOffset Timestamp,
    string EventName,
    string Detail,
    string? TraceId,
    int? StatusCode,
    double? DurationMs);

public static class ClientDiagnosticReportValidator
{
    public const int MaximoEventosFlujo = 50;

    public static bool IsValid(ClientDiagnosticReport report) =>
        IsErrorId(report.ErrorId)
        && IsEventName(report.EventName)
        && IsCategory(report.Category)
        && IsSource(report.Source)
        && IsTraceId(report.TraceId)
        && IsStatusCode(report.StatusCode)
        && IsRelease(report.Release)
        && IsSessionIdValido(report.SessionId)
        && IsFlowEvents(report.FlowEvents);

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

    private static bool IsSource(string value) =>
        value is "router" or "window" or "promise" or "http" or "flow";

    private static bool IsTraceId(string? value) =>
        value is null
        || value.Length == 32
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsStatusCode(int? value) => value is null or >= 500 and <= 599;

    private static bool IsRelease(string? value) =>
        value is null
        || value.Length is >= 1 and <= 40
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    private static bool IsSessionIdValido(string? value) =>
        value is null || DiagnosticIds.IsSessionId(value);

    private static bool IsFlowEvents(IReadOnlyList<ClientFlowEvent>? events) =>
        events is null
        || events.Count <= MaximoEventosFlujo && events.All(IsFlowEvent);

    private static bool IsFlowEvent(ClientFlowEvent evento) =>
        evento.Seq is >= 1 and <= 10000
        && evento.Timestamp.Offset == TimeSpan.Zero
        && IsFlowEventName(evento.EventName)
        && IsDetail(evento.Detail)
        && IsTraceId(evento.TraceId)
        && IsFlowStatusCode(evento.StatusCode)
        && IsDurationMs(evento.DurationMs);

    private static bool IsFlowEventName(string? value) =>
        value is "flow.navigation" or "flow.api_call";

    private static bool IsDetail(string? value) =>
        value is not null && value.Length is >= 1 and <= 120;

    private static bool IsFlowStatusCode(int? value) => value is null or >= 100 and <= 599;

    private static bool IsDurationMs(double? value) => value is null or >= 0 and <= 600000;
}
