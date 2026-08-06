# Diagnóstico de flujo frontend — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capturar el flujo de una pestaña del navegador (navegación, llamadas API y errores) bajo un `sessionId` opaco, exportable a JSON y grep-able en los logs del backend, solo en modo diagnóstico (`?debug=1`).

**Architecture:** El frontend mantiene un buffer circular de eventos de flujo en memoria (máx. 100) activo solo con modo diagnóstico; los reportes de error existentes se enriquecen con `sessionId` + últimos 30 eventos; el backend extiende el contrato `/api/diagnosticos/frontend` con `sessionId` y `flowEvents` (whitelist estricta) y registra cada evento como `frontend.flow`, y el middleware de observabilidad añade `SessionId` al scope cuando llega el header `X-Session-Id`.

**Tech Stack:** .NET 10 (minimal APIs, LoggerMessage), React 19 + TypeScript estricto, Vitest + Testing Library, xUnit + Testcontainers.

**Spec:** `docs/superpowers/specs/2026-08-06-diagnostico-flujo-frontend-design.md`
**Rama:** `feat/diagnostico-flujo-frontend` (ya creada desde `master`)

## Global Constraints

- Textos de UI y comentarios en español, UTF-8 con acentos; nunca mojibake.
- Anti-PII: solo IDs opacos, rutas sanitizadas y metadatos; nunca query strings, bodies, tokens ni contenido de usuario.
- Patrón `sessionId`: `^SES-[0-9A-F]{12}$` (16 chars). Patrón `traceId`: 32 hex minúsculas.
- Límites exactos: buffer front 100 eventos; eventos adjuntos a un error 30; `flowEvents` máx. 50 por request; `detail` 1–120 chars; `seq` 1–10000; `statusCode` flujo 100–599; `durationMs` 0–600000; body del endpoint ≤ 16384 bytes.
- `eventName` de flujo permitidos: `flow.navigation`, `flow.api_call` (whitelist cerrada).
- TypeScript estricto: sin `any` ni aserciones inseguras; MUI 9; iconos desde `@mui/icons-material`.
- Backend: warnings-as-errors, `.editorconfig`, comandos desde `CaseritoApp/`; frontend desde `web/`.
- Commits pequeños por tarea, estilo conventional en español como los existentes (`feat(web): ...`, `feat(observabilidad): ...`).
- No hacer push ni merge sin autorización explícita del usuario.

---

### Task 1: Backend — extender contrato de diagnóstico

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Observability/ClientDiagnosticReport.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Observability/DiagnosticIds.cs`
- Test: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/Observability/DiagnosticContractTests.cs`

**Interfaces:**
- Consumes: nada nuevo.
- Produces:
  - `DiagnosticIds.IsSessionId(string? value): bool` — patrón `SES-<12 hex mayúsculas>`; `null` → `false`.
  - `ClientFlowEvent(int Seq, DateTimeOffset Timestamp, string EventName, string Detail, string? TraceId, int? StatusCode, double? DurationMs)`.
  - `ClientDiagnosticReport` con dos campos nuevos al final: `string? SessionId, IReadOnlyList<ClientFlowEvent>? FlowEvents`.
  - `ClientDiagnosticReportValidator.MaximoEventosFlujo = 50`.

- [ ] **Step 1: Escribir los tests que fallan**

En `DiagnosticContractTests.cs`:

1. Actualizar `Contrato_solo_expone_campos_seguros` (la lista ordenada crece):

```csharp
        Assert.Equal(
            ["Category", "ErrorId", "EventName", "FlowEvents", "Release", "SessionId", "Source", "StatusCode", "TraceId"],
            propiedades);
```

2. Actualizar `ReporteValido()` (record posicional: añadir `null, null` al final):

```csharp
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
```

3. Añadir tests nuevos al final de la clase:

```csharp
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
```

Y un test para el patrón de sesión:

```csharp
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
```

- [ ] **Step 2: Ejecutar y observar el fallo**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.ArchitectureTests --filter "FullyQualifiedName~DiagnosticContractTests"`
Expected: FAIL de compilación (`ClientFlowEvent` no existe, `IsSessionId` no existe, argumentos de sobra en `ReporteValido`).

- [ ] **Step 3: Implementar el contrato extendido**

En `DiagnosticIds.cs`:

```csharp
using System.Security.Cryptography;

namespace CaseritoApp.Host.Observability;

public static class DiagnosticIds
{
    public static string NewErrorId() => $"ERR-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}";

    public static bool IsSessionId(string? value) =>
        value is not null
        && value.Length == 16
        && value.StartsWith("SES-", StringComparison.Ordinal)
        && value.AsSpan(4).ToString().All(character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F');
}
```

En `ClientDiagnosticReport.cs` (contenido completo resultante):

```csharp
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
```

- [ ] **Step 4: Ejecutar y observar verde**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.ArchitectureTests --filter "FullyQualifiedName~DiagnosticContractTests"`
Expected: PASS (todos los tests de la clase).

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Observability/ClientDiagnosticReport.cs CaseritoApp/src/Host/CaseritoApp.Host/Observability/DiagnosticIds.cs CaseritoApp/tests/CaseritoApp.ArchitectureTests/Observability/DiagnosticContractTests.cs
git commit -m "feat(observabilidad): extiende contrato con sesión y flujo"
```

---

### Task 2: Backend — endpoint registra `frontend.flow` y límite de 16 KB

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/DiagnosticsEndpoints.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Observability/ClientDiagnosticsBodyLimitMiddleware.cs`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/Observability/DiagnosticsEndpointTests.cs`

**Interfaces:**
- Consumes: `ClientDiagnosticReport`, `ClientFlowEvent`, `ClientDiagnosticReportValidator.MaximoEventosFlujo` (Task 1).
- Produces: comportamiento del endpoint — acepta `sessionId`/`flowEvents` válidos (202), rechaza inválidos (400), límite de body 16384 bytes (413 por encima).

- [ ] **Step 1: Escribir/actualizar los tests que fallan**

En `DiagnosticsEndpointTests.cs`:

1. Renombrar y actualizar el test de tamaño (de 4 KiB a 16 KiB):

```csharp
    [Fact]
    public async Task Rechaza_cuerpos_mayores_a_dieciseis_kibibytes()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(
            $"{{\"errorId\":\"ERR-0123456789AB\",\"padding\":\"{new string('a', 17000)}\"}}",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/diagnosticos/frontend", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
```

2. Añadir tests nuevos:

```csharp
    [Fact]
    public async Task Acepta_reporte_con_sesion_y_flujo_permitidos()
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();
        report["sessionId"] = "SES-0123456789AB";
        report["flowEvents"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["seq"] = 1,
                ["timestamp"] = "2026-08-06T10:00:00.000Z",
                ["eventName"] = "flow.navigation",
                ["detail"] = "/avisos/:id",
            },
            new Dictionary<string, object?>
            {
                ["seq"] = 2,
                ["timestamp"] = "2026-08-06T10:00:01.000Z",
                ["eventName"] = "flow.api_call",
                ["detail"] = "GET /api/avisos/:id",
                ["traceId"] = "0123456789abcdef0123456789abcdef",
                ["statusCode"] = 200,
                ["durationMs"] = 42.5,
            },
        };

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Acepta_el_maximo_de_eventos_de_flujo_superando_cuatro_kibibytes()
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();
        report["sessionId"] = "SES-0123456789AB";
        report["flowEvents"] = Enumerable.Range(1, 50)
            .Select(seq => new Dictionary<string, object?>
            {
                ["seq"] = seq,
                ["timestamp"] = "2026-08-06T10:00:00.000Z",
                ["eventName"] = "flow.api_call",
                ["detail"] = $"GET /api/recurso/{new string('a', 100)}",
            })
            .ToArray();

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Theory]
    [InlineData("ses-0123456789ab")]
    [InlineData("SES-no-valido")]
    public async Task Rechaza_sesion_fuera_del_patron(string sessionId)
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();
        report["sessionId"] = sessionId;

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rechaza_eventos_de_flujo_fuera_de_la_lista_permitida()
    {
        using var client = factory.CreateClient();
        var report = ReporteValido();
        report["flowEvents"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["seq"] = 1,
                ["timestamp"] = "2026-08-06T10:00:00.000Z",
                ["eventName"] = "usuario.contenido",
                ["detail"] = "texto libre del usuario",
            },
        };

        using var response = await client.PostAsJsonAsync("/api/diagnosticos/frontend", report);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(
            "texto libre del usuario",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Ejecutar y observar el fallo**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~DiagnosticsEndpointTests"`
Expected: FAIL — los tests nuevos reciben 400 (propiedades desconocidas por `JsonUnmappedMemberHandling.Disallow`... ya no, el contrato cambió en Task 1) o el validador no contempla flujo; el test de 16 KiB recibe 413 con 17000 bytes de padding porque el límite sigue en 4096. En cualquier caso: rojo.

- [ ] **Step 3: Implementar**

En `ClientDiagnosticsBodyLimitMiddleware.cs`: cambiar la constante:

```csharp
    private const long MaxBodySize = 16384;
```

En `DiagnosticsEndpoints.cs` (contenido completo resultante):

```csharp
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
```

- [ ] **Step 4: Ejecutar y observar verde**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~DiagnosticsEndpointTests"`
Expected: PASS (todos, incluidos los previos).

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/DiagnosticsEndpoints.cs CaseritoApp/src/Host/CaseritoApp.Host/Observability/ClientDiagnosticsBodyLimitMiddleware.cs CaseritoApp/tests/CaseritoApp.IntegrationTests/Observability/DiagnosticsEndpointTests.cs
git commit -m "feat(observabilidad): registra flujo del navegador por sesión"
```

---

### Task 3: Backend — `SessionId` en el scope del middleware

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Observability/RequestObservabilityMiddleware.cs`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/Observability/ObservabilityPipelineTests.cs`

**Interfaces:**
- Consumes: `DiagnosticIds.IsSessionId` (Task 1).
- Produces: header de entrada `X-Session-Id`; cuando cumple el patrón, `SessionId` queda en el scope de `http.request.completed`. Headers inválidos se ignoran en silencio.

Nota de verificación: el scope de logging no es observable vía HTTP; los tests cubren que el header (válido o inválido) nunca rompe la petición, y el patrón queda cubierto por los tests puros de `IsSessionId` (Task 1). La emisión en el scope se valida por revisión de código.

- [ ] **Step 1: Escribir el test que falla**

En `ObservabilityPipelineTests.cs`, añadir:

```csharp
    [Theory]
    [InlineData("SES-0123456789AB")]
    [InlineData("sesion-no-valida")]
    public async Task Peticion_con_header_de_sesion_responde_con_normalidad(string sessionId)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Session-Id", sessionId);

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Single(response.Headers.GetValues("X-Trace-Id"));
    }
```

- [ ] **Step 2: Ejecutar y observar el fallo**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~ObservabilityPipelineTests"`
Expected: PASS incluso antes del cambio (el header extra no rompe nada) — este test es de caracterización, no rojo/verde estricto. Ejecutarlo igualmente para fijar el comportamiento antes de tocar el middleware.

- [ ] **Step 3: Implementar**

En `RequestObservabilityMiddleware.cs` (contenido completo resultante):

```csharp
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

        var scope = new Dictionary<string, object?> { ["TraceId"] = traceId };
        var sessionId = context.Request.Headers["X-Session-Id"].FirstOrDefault();
        if (DiagnosticIds.IsSessionId(sessionId))
        {
            scope["SessionId"] = sessionId;
        }

        using (logger.BeginScope(scope))
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
```

- [ ] **Step 4: Ejecutar y observar verde**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~Observability"`
Expected: PASS (`ObservabilityPipelineTests` + `DiagnosticsEndpointTests`).

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Observability/RequestObservabilityMiddleware.cs CaseritoApp/tests/CaseritoApp.IntegrationTests/Observability/ObservabilityPipelineTests.cs
git commit -m "feat(observabilidad): propaga sesión del navegador al scope de logs"
```

---

### Task 4: Frontend — módulo `sesionDiagnostico`

**Files:**
- Create: `web/src/lib/sesionDiagnostico.ts`
- Test: `web/src/lib/sesionDiagnostico.test.ts`

**Interfaces:**
- Consumes: nada (módulo base; no importa de `diagnosticos.ts` para evitar ciclos).
- Produces (las usan Tasks 5, 6 y 7):

```ts
export interface EventoFlujo {
  seq: number;
  timestamp: string;
  eventName: 'flow.navigation' | 'flow.api_call';
  detail: string;
  traceId?: string;
  statusCode?: number;
  durationMs?: number;
}
export function obtenerSesionId(): string;
export function modoDiagnosticoActivo(): boolean;
export function sanitizarRuta(pathname: string): string;
export function registrarEventoFlujo(evento: Omit<EventoFlujo, 'seq' | 'timestamp'>): void;
export function obtenerEventosRecientes(n: number): EventoFlujo[];
export function exportarDiagnostico(): void;
```

- [ ] **Step 1: Escribir el test que falla**

Crear `web/src/lib/sesionDiagnostico.test.ts`. El estado vive a nivel de módulo, así que cada test usa `vi.resetModules()` + import dinámico para aislarlo:

```ts
import { afterEach, describe, expect, it, vi } from 'vitest';

async function cargarModulo() {
  return import('./sesionDiagnostico');
}

afterEach(() => {
  sessionStorage.clear();
  vi.resetModules();
  vi.restoreAllMocks();
});

describe('sesión de diagnóstico', () => {
  it('genera un sessionId opaco y lo mantiene estable en la pestaña', async () => {
    const modulo = await cargarModulo();

    const primero = modulo.obtenerSesionId();
    const segundo = modulo.obtenerSesionId();

    expect(primero).toMatch(/^SES-[0-9A-F]{12}$/);
    expect(segundo).toBe(primero);
  });

  it('regenera el sessionId si el almacenado no cumple el patrón', async () => {
    sessionStorage.setItem('caserito.sesion', 'manipulado');
    const modulo = await cargarModulo();

    expect(modulo.obtenerSesionId()).toMatch(/^SES-[0-9A-F]{12}$/);
  });

  it('activa el modo diagnóstico con ?debug=1 y lo conserva', async () => {
    const modulo = await cargarModulo();
    expect(modulo.modoDiagnosticoActivo()).toBe(false);

    window.history.replaceState(null, '', '/?debug=1');
    expect(modulo.modoDiagnosticoActivo()).toBe(true);

    window.history.replaceState(null, '', '/');
    expect(modulo.modoDiagnosticoActivo()).toBe(true);
  });

  it('sanitiza segmentos numéricos y UUID sin tocar el resto', async () => {
    const modulo = await cargarModulo();

    expect(modulo.sanitizarRuta('/avisos/123')).toBe('/avisos/:id');
    expect(modulo.sanitizarRuta('/avisos/3fa85f64-5717-4562-b3fc-2c963f66afa6')).toBe(
      '/avisos/:id',
    );
    expect(modulo.sanitizarRuta('/mis-avisos')).toBe('/mis-avisos');
    expect(modulo.sanitizarRuta('/')).toBe('/');
  });

  it('no registra eventos sin modo diagnóstico', async () => {
    const modulo = await cargarModulo();

    modulo.registrarEventoFlujo({ eventName: 'flow.navigation', detail: '/explorar' });

    expect(modulo.obtenerEventosRecientes(10)).toEqual([]);
  });

  it('registra eventos con seq incremental y trunca el detalle a 120', async () => {
    sessionStorage.setItem('caserito.debug', '1');
    const modulo = await cargarModulo();

    modulo.registrarEventoFlujo({ eventName: 'flow.navigation', detail: '/explorar' });
    modulo.registrarEventoFlujo({
      eventName: 'flow.api_call',
      detail: `GET /api/${'a'.repeat(200)}`,
      statusCode: 200,
      durationMs: 42,
    });

    const eventos = modulo.obtenerEventosRecientes(10);
    expect(eventos).toHaveLength(2);
    expect(eventos[0]).toMatchObject({ seq: 1, eventName: 'flow.navigation', detail: '/explorar' });
    expect(eventos[1].seq).toBe(2);
    expect(eventos[1].detail).toHaveLength(120);
    expect(eventos[0].timestamp).toMatch(/Z$/);
  });

  it('descarta los eventos más antiguos al superar el máximo de 100', async () => {
    sessionStorage.setItem('caserito.debug', '1');
    const modulo = await cargarModulo();

    for (let i = 0; i < 105; i++) {
      modulo.registrarEventoFlujo({ eventName: 'flow.navigation', detail: `/ruta-${i}` });
    }

    const eventos = modulo.obtenerEventosRecientes(200);
    expect(eventos).toHaveLength(100);
    expect(eventos[0].detail).toBe('/ruta-5');
    expect(eventos[0].seq).toBe(6);
  });

  it('exporta el diagnóstico como descarga JSON', async () => {
    sessionStorage.setItem('caserito.debug', '1');
    const modulo = await cargarModulo();
    modulo.registrarEventoFlujo({ eventName: 'flow.navigation', detail: '/explorar' });
    const crearUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:mock');
    const revocar = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);

    modulo.exportarDiagnostico();

    expect(crearUrl).toHaveBeenCalledOnce();
    const blob = crearUrl.mock.calls[0][0] as Blob;
    const contenido = JSON.parse(await blob.text());
    expect(contenido.sessionId).toMatch(/^SES-[0-9A-F]{12}$/);
    expect(contenido.eventos).toHaveLength(1);
    expect(click).toHaveBeenCalledOnce();
    expect(revocar).toHaveBeenCalledWith('blob:mock');
  });
});
```

- [ ] **Step 2: Ejecutar y observar el fallo**

Run: `cd web && npx vitest run src/lib/sesionDiagnostico.test.ts`
Expected: FAIL — `Cannot find module './sesionDiagnostico'`.

- [ ] **Step 3: Implementar**

Crear `web/src/lib/sesionDiagnostico.ts`:

```ts
// Sesión de diagnóstico por pestaña: identificador opaco, modo diagnóstico
// activable con ?debug=1 y buffer circular de eventos de flujo. Solo registra
// metadatos seguros (rutas sanitizadas, status, duraciones); nunca query
// strings, bodies ni contenido de usuario.

export interface EventoFlujo {
  seq: number;
  timestamp: string;
  eventName: 'flow.navigation' | 'flow.api_call';
  detail: string;
  traceId?: string;
  statusCode?: number;
  durationMs?: number;
}

const SESION_CLAVE = 'caserito.sesion';
const DEBUG_CLAVE = 'caserito.debug';
const MAX_EVENTOS = 100;
const MAX_DETALLE = 120;
const PATRON_SESION = /^SES-[0-9A-F]{12}$/;
const PATRON_UUID = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

let eventos: EventoFlujo[] = [];
let siguienteSeq = 1;

export function obtenerSesionId(): string {
  const almacenada = sessionStorage.getItem(SESION_CLAVE);
  if (almacenada !== null && PATRON_SESION.test(almacenada)) {
    return almacenada;
  }
  const bytes = crypto.getRandomValues(new Uint8Array(6));
  const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('');
  const id = `SES-${hex.toUpperCase()}`;
  sessionStorage.setItem(SESION_CLAVE, id);
  return id;
}

export function modoDiagnosticoActivo(): boolean {
  if (new URLSearchParams(window.location.search).get('debug') === '1') {
    sessionStorage.setItem(DEBUG_CLAVE, '1');
  }
  return sessionStorage.getItem(DEBUG_CLAVE) === '1';
}

export function sanitizarRuta(pathname: string): string {
  const sanitizada = pathname
    .split('/')
    .map((segmento) => (/^\d+$/.test(segmento) || PATRON_UUID.test(segmento) ? ':id' : segmento))
    .join('/');
  return sanitizada === '' ? '/' : sanitizada;
}

export function registrarEventoFlujo(evento: Omit<EventoFlujo, 'seq' | 'timestamp'>): void {
  if (!modoDiagnosticoActivo()) {
    return;
  }
  eventos.push({
    ...evento,
    detail: evento.detail.slice(0, MAX_DETALLE),
    seq: siguienteSeq,
    timestamp: new Date().toISOString(),
  });
  siguienteSeq += 1;
  if (eventos.length > MAX_EVENTOS) {
    eventos.splice(0, eventos.length - MAX_EVENTOS);
  }
}

export function obtenerEventosRecientes(n: number): EventoFlujo[] {
  return eventos.slice(-n);
}

export function exportarDiagnostico(): void {
  const carga = JSON.stringify(
    {
      sessionId: obtenerSesionId(),
      generadoEn: new Date().toISOString(),
      eventos,
    },
    null,
    2,
  );
  const url = URL.createObjectURL(new Blob([carga], { type: 'application/json' }));
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = `diagnostico-${obtenerSesionId()}.json`;
  enlace.click();
  URL.revokeObjectURL(url);
}
```

- [ ] **Step 4: Ejecutar y observar verde**

Run: `cd web && npx vitest run src/lib/sesionDiagnostico.test.ts`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add web/src/lib/sesionDiagnostico.ts web/src/lib/sesionDiagnostico.test.ts
git commit -m "feat(web): añade sesión y buffer de diagnóstico de flujo"
```

---

### Task 5: Frontend — reportes de error enriquecidos

**Files:**
- Modify: `web/src/lib/diagnosticos.ts`
- Test: `web/src/lib/diagnosticos.test.ts`

**Interfaces:**
- Consumes: `obtenerSesionId`, `obtenerEventosRecientes` de `./sesionDiagnostico` (Task 4).
- Produces: el POST a `/api/diagnosticos/frontend` incluye siempre `sessionId` y, cuando hay eventos capturados, `flowEvents` con los últimos 30. La firma pública `reportarDiagnostico(report: DiagnosticReport): Promise<void>` no cambia.

- [ ] **Step 1: Escribir los tests que fallan**

En `diagnosticos.test.ts`:

1. El test `envía exclusivamente el contrato permitido` ahora espera también `sessionId`:

```ts
    expect(JSON.parse(String(init?.body))).toEqual({
      errorId: 'ERR-0123456789AB',
      eventName: 'http.server_failed',
      category: 'server',
      source: 'http',
      traceId: '0123456789abcdef0123456789abcdef',
      statusCode: 503,
      sessionId: expect.stringMatching(/^SES-[0-9A-F]{12}$/),
    });
```

2. Añadir test nuevo al final del `describe`:

```ts
  it('adjunta los últimos 30 eventos de flujo cuando existen', async () => {
    sessionStorage.setItem('caserito.debug', '1');
    const sesion = await import('./sesionDiagnostico');
    for (let i = 0; i < 35; i++) {
      sesion.registrarEventoFlujo({ eventName: 'flow.navigation', detail: `/ruta-${i}` });
    }
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(null, { status: 202 }));

    await reportarDiagnostico({
      errorId: 'ERR-0123456789AB',
      eventName: 'window.unexpected',
      category: 'unexpected',
      source: 'window',
    });

    const cuerpo = JSON.parse(String(fetchMock.mock.calls[0][1]?.body));
    expect(cuerpo.sessionId).toMatch(/^SES-[0-9A-F]{12}$/);
    expect(cuerpo.flowEvents).toHaveLength(30);
    expect(cuerpo.flowEvents[0].detail).toBe('/ruta-5');
    expect(cuerpo.flowEvents[29].detail).toBe('/ruta-34');
  });
```

Nota: el `afterEach` existente hace `vi.restoreAllMocks()`; añadir `sessionStorage.clear()` y `vi.resetModules()` al `afterEach` del fichero para aislar el estado del módulo entre tests:

```ts
afterEach(() => {
  vi.restoreAllMocks();
  sessionStorage.clear();
  vi.resetModules();
});
```

Ojo: `vi.resetModules()` no afecta a los imports estáticos ya cargados (`diagnosticos` y su dependencia `sesionDiagnostico` quedan en la instancia original), así que el test de 35 eventos debe limpiar lo que ensucia: como el buffer es compartido en este fichero, basta con que este test sea el último o registrar y luego verificar con datos relativos. Solución simple: este test usa `sesion.registrarEventoFlujo` sobre el módulo ya cargado (el import dinámico devuelve la misma instancia cacheada) y se coloca al final del `describe`; los tests anteriores no registran eventos de flujo, así que no hay contaminación.

- [ ] **Step 2: Ejecutar y observar el fallo**

Run: `cd web && npx vitest run src/lib/diagnosticos.test.ts`
Expected: FAIL — el cuerpo no contiene `sessionId` ni `flowEvents`.

- [ ] **Step 3: Implementar**

En `web/src/lib/diagnosticos.ts`, modificar imports y `reportarDiagnostico`:

```ts
import { obtenerEventosRecientes, obtenerSesionId } from './sesionDiagnostico';
```

```ts
const MAX_EVENTOS_EN_REPORTE = 30;

export async function reportarDiagnostico(report: DiagnosticReport): Promise<void> {
  const eventosFlujo = obtenerEventosRecientes(MAX_EVENTOS_EN_REPORTE);
  const carga = {
    ...report,
    sessionId: obtenerSesionId(),
    ...(eventosFlujo.length > 0 ? { flowEvents: eventosFlujo } : {}),
  };
  try {
    await fetch('/api/diagnosticos/frontend', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(carga),
      keepalive: true,
    });
  } catch {
    // El diagnóstico nunca debe generar otro error ni alterar el flujo del usuario.
  }
}
```

- [ ] **Step 4: Ejecutar y observar verde**

Run: `cd web && npx vitest run src/lib/diagnosticos.test.ts`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add web/src/lib/diagnosticos.ts web/src/lib/diagnosticos.test.ts
git commit -m "feat(web): enriquece reportes de error con sesión y flujo"
```

---

### Task 6: Frontend — transporte HTTP registra `flow.api_call` y envía `X-Session-Id`

**Files:**
- Modify: `web/src/api/http.ts`
- Test: `web/src/api/http.test.ts`

**Interfaces:**
- Consumes: `obtenerSesionId`, `modoDiagnosticoActivo`, `registrarEventoFlujo`, `sanitizarRuta` de `../lib/sesionDiagnostico` (Task 4); `leerTraceId` ya existe en el propio fichero.
- Produces: todas las llamadas API llevan header `X-Session-Id`; en modo diagnóstico cada llamada registra `flow.api_call` (`"<MÉTODO> <ruta sanitizada>"`, `statusCode`, `durationMs`, `traceId` cuando exista), incluidas las que fallan por red (sin `statusCode`).

- [ ] **Step 1: Escribir los tests que fallan**

En `http.test.ts`:

1. Dentro de `describe('desempaquetar / HttpError')`, junto al test de Authorization, añadir:

```ts
  it('inyecta X-Session-Id opaco en todas las llamadas', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(respuesta(200, {}));
    await api.GET('/api/perfil');
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.headers.get('X-Session-Id')).toMatch(/^SES-[0-9A-F]{12}$/);
  });
```

2. Nuevo `describe` al final del fichero:

```ts
describe('flujo de llamadas API', () => {
  afterEach(() => {
    sessionStorage.removeItem('caserito.debug');
  });

  it('registra flow.api_call con ruta sanitizada en modo diagnóstico', async () => {
    sessionStorage.setItem('caserito.debug', '1');
    const sesion = await import('../lib/sesionDiagnostico');
    const antes = sesion.obtenerEventosRecientes(100).length;
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(JSON.stringify({}), {
        status: 200,
        headers: {
          'Content-Type': 'application/json',
          'X-Trace-Id': '0123456789abcdef0123456789abcdef',
        },
      }),
    );

    await api.GET('/api/perfil');

    const eventos = sesion.obtenerEventosRecientes(100);
    expect(eventos.length).toBe(antes + 1);
    expect(eventos[eventos.length - 1]).toMatchObject({
      eventName: 'flow.api_call',
      detail: 'GET /api/perfil',
      statusCode: 200,
      traceId: '0123456789abcdef0123456789abcdef',
    });
  });

  it('no registra llamadas API sin modo diagnóstico', async () => {
    sessionStorage.removeItem('caserito.debug');
    const sesion = await import('../lib/sesionDiagnostico');
    const antes = sesion.obtenerEventosRecientes(100).length;
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(respuesta(200, {}));

    await api.GET('/api/perfil');

    expect(sesion.obtenerEventosRecientes(100).length).toBe(antes);
  });
});
```

- [ ] **Step 2: Ejecutar y observar el fallo**

Run: `cd web && npx vitest run src/api/http.test.ts`
Expected: FAIL — `X-Session-Id` ausente y no se registra `flow.api_call`.

- [ ] **Step 3: Implementar**

En `web/src/api/http.ts`:

1. Import nuevo junto al de `diagnosticos`:

```ts
import {
  modoDiagnosticoActivo,
  obtenerSesionId,
  registrarEventoFlujo,
  sanitizarRuta,
} from '../lib/sesionDiagnostico';
```

2. En `conAutorizacion`, fijar el header de sesión:

```ts
function conAutorizacion(request: Request): Request {
  const headers = new Headers(request.headers);
  headers.set('X-Session-Id', obtenerSesionId());
  const token = getAccessToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  } else {
    headers.delete('Authorization');
  }
  return new Request(request, { headers, credentials: 'include' });
}
```

3. En `fetchConDiagnostico`, registrar la llamada (éxito o error de red):

```ts
async function fetchConDiagnostico(request: Request): Promise<Response> {
  const inicio = performance.now();
  try {
    const response = await fetch(request);
    registrarLlamadaApi(request, inicio, response.status, response.headers.get('X-Trace-Id'));
    if (response.status >= 500) {
      void reportarDiagnostico({
        errorId: crearErrorId(),
        eventName: 'http.server_failed',
        category: 'server',
        source: 'http',
        traceId: leerTraceId(response.headers.get('X-Trace-Id')) ?? undefined,
        statusCode: response.status,
      });
    }
    return response;
  } catch (error) {
    registrarLlamadaApi(request, inicio, undefined, undefined);
    void reportarDiagnostico({
      errorId: crearErrorId(),
      eventName: 'http.network_failed',
      category: 'network',
      source: 'http',
    });
    throw error;
  }
}

// Registra la llamada en el buffer de flujo solo con modo diagnóstico activo.
// El detalle es método + ruta sanitizada (sin query); nunca bodies ni tokens.
function registrarLlamadaApi(
  request: Request,
  inicio: number,
  statusCode: number | undefined,
  traceIdCrudo: string | null,
): void {
  if (!modoDiagnosticoActivo()) {
    return;
  }
  registrarEventoFlujo({
    eventName: 'flow.api_call',
    detail: `${request.method} ${sanitizarRuta(new URL(request.url).pathname)}`,
    statusCode,
    durationMs: Math.round(performance.now() - inicio),
    traceId: leerTraceId(traceIdCrudo) ?? undefined,
  });
}
```

- [ ] **Step 4: Ejecutar y observar verde**

Run: `cd web && npx vitest run src/api/http.test.ts`
Expected: PASS (todos los tests del fichero).

- [ ] **Step 5: Commit**

```bash
git add web/src/api/http.ts web/src/api/http.test.ts
git commit -m "feat(web): registra llamadas API en el flujo de diagnóstico"
```

---

### Task 7: Frontend — captura de navegación y botón de exportación

**Files:**
- Create: `web/src/app/CapturaFlujoNavegacion.tsx`
- Create: `web/src/app/BotonDiagnostico.tsx`
- Modify: `web/src/app/AppLayout.tsx`
- Test: `web/src/app/CapturaFlujoNavegacion.test.tsx`
- Test: `web/src/app/BotonDiagnostico.test.tsx`

**Interfaces:**
- Consumes: `registrarEventoFlujo`, `sanitizarRuta`, `modoDiagnosticoActivo`, `exportarDiagnostico` de `../lib/sesionDiagnostico` (Task 4).
- Produces: `<CapturaFlujoNavegacion />` (sin salida visual; registra `flow.navigation` al montarse y ante cada cambio de `location.pathname`) y `<BotonDiagnostico />` (Fab fijo visible solo en modo diagnóstico). Ambos se montan en `AppLayout`.

- [ ] **Step 1: Escribir los tests que fallan**

Crear `web/src/app/CapturaFlujoNavegacion.test.tsx`:

```tsx
import { afterEach, describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { Link, MemoryRouter, Route, Routes } from 'react-router-dom';
import { CapturaFlujoNavegacion } from './CapturaFlujoNavegacion';
import { obtenerEventosRecientes } from '../lib/sesionDiagnostico';

afterEach(() => {
  sessionStorage.clear();
});

function BancoDePruebas() {
  return (
    <MemoryRouter initialEntries={['/']}>
      <CapturaFlujoNavegacion />
      <Link to="/avisos/123">ir al aviso</Link>
      <Routes>
        <Route path="/" element={<p>inicio</p>} />
        <Route path="/avisos/:id" element={<p>aviso</p>} />
      </Routes>
    </MemoryRouter>
  );
}

describe('captura de navegación', () => {
  it('registra flow.navigation con rutas sanitizadas en modo diagnóstico', () => {
    sessionStorage.setItem('caserito.debug', '1');
    const antes = obtenerEventosRecientes(100).length;

    render(<BancoDePruebas />);
    fireEvent.click(screen.getByRole('link', { name: 'ir al aviso' }));

    const eventos = obtenerEventosRecientes(100).slice(antes);
    expect(eventos.map((evento) => evento.detail)).toEqual(['/', '/avisos/:id']);
    expect(eventos.every((evento) => evento.eventName === 'flow.navigation')).toBe(true);
  });

  it('no registra navegación sin modo diagnóstico', () => {
    const antes = obtenerEventosRecientes(100).length;

    render(<BancoDePruebas />);
    fireEvent.click(screen.getByRole('link', { name: 'ir al aviso' }));

    expect(obtenerEventosRecientes(100).length).toBe(antes);
  });
});
```

Crear `web/src/app/BotonDiagnostico.test.tsx`:

```tsx
import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { BotonDiagnostico } from './BotonDiagnostico';

afterEach(() => {
  sessionStorage.clear();
  vi.restoreAllMocks();
});

describe('botón de diagnóstico', () => {
  it('no se muestra sin modo diagnóstico', () => {
    render(<BotonDiagnostico />);

    expect(screen.queryByRole('button', { name: 'Descargar diagnóstico' })).toBeNull();
  });

  it('descarga el diagnóstico al pulsarlo en modo diagnóstico', () => {
    sessionStorage.setItem('caserito.debug', '1');
    const crearUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:mock');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);

    render(<BotonDiagnostico />);
    fireEvent.click(screen.getByRole('button', { name: 'Descargar diagnóstico' }));

    expect(crearUrl).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Ejecutar y observar el fallo**

Run: `cd web && npx vitest run src/app/CapturaFlujoNavegacion.test.tsx src/app/BotonDiagnostico.test.tsx`
Expected: FAIL — módulos inexistentes.

- [ ] **Step 3: Implementar**

Crear `web/src/app/CapturaFlujoNavegacion.tsx`:

```tsx
import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { registrarEventoFlujo, sanitizarRuta } from '../lib/sesionDiagnostico';

// Registra cada cambio de ruta en el buffer de flujo (solo con modo
// diagnóstico activo; registrarEventoFlujo ya filtra). No renderiza nada.
export function CapturaFlujoNavegacion() {
  const location = useLocation();

  useEffect(() => {
    registrarEventoFlujo({
      eventName: 'flow.navigation',
      detail: sanitizarRuta(location.pathname),
    });
  }, [location.pathname]);

  return null;
}
```

Crear `web/src/app/BotonDiagnostico.tsx`:

```tsx
import { Fab } from '@mui/material';
import DownloadRoundedIcon from '@mui/icons-material/DownloadRounded';
import { exportarDiagnostico, modoDiagnosticoActivo } from '../lib/sesionDiagnostico';

// Acceso discreto a la exportación del flujo de diagnóstico; solo existe con
// el modo diagnóstico activo (?debug=1), nunca para usuarios normales.
export function BotonDiagnostico() {
  if (!modoDiagnosticoActivo()) {
    return null;
  }

  return (
    <Fab
      aria-label="Descargar diagnóstico"
      color="secondary"
      size="small"
      onClick={() => exportarDiagnostico()}
      sx={{ position: 'fixed', right: 16, bottom: 16, zIndex: (tema) => tema.zIndex.snackbar }}
    >
      <DownloadRoundedIcon />
    </Fab>
  );
}
```

En `web/src/app/AppLayout.tsx`:

1. Añadir imports junto a los de `./MarcaCaserito`:

```tsx
import { BotonDiagnostico } from './BotonDiagnostico';
import { CapturaFlujoNavegacion } from './CapturaFlujoNavegacion';
```

2. Dentro del `Box` raíz del `return`, como primeros hijos:

```tsx
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100dvh' }}>
      <CapturaFlujoNavegacion />
      <BotonDiagnostico />
      <AppBar
```

- [ ] **Step 4: Ejecutar y observar verde**

Run: `cd web && npx vitest run src/app/CapturaFlujoNavegacion.test.tsx src/app/BotonDiagnostico.test.tsx`
Expected: PASS (4 tests). Ejecutar también los tests vecinos del layout/router: `npx vitest run src/app`.

- [ ] **Step 5: Commit**

```bash
git add web/src/app/CapturaFlujoNavegacion.tsx web/src/app/CapturaFlujoNavegacion.test.tsx web/src/app/BotonDiagnostico.tsx web/src/app/BotonDiagnostico.test.tsx web/src/app/AppLayout.tsx
git commit -m "feat(web): captura navegación y permite exportar el diagnóstico"
```

---

### Task 8: Integración — artefactos derivados y verificación completa

**Files:**
- Modify (generado): `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`
- Modify (generado): `web/src/api/schema.d.ts`

**Interfaces:**
- Consumes: todo lo anterior.
- Produces: contrato OpenAPI y tipos regenerados; CI valida que no haya diff pendiente (`git diff --exit-code` sobre ambos ficheros).

- [ ] **Step 1: Regenerar OpenAPI**

El artefacto se genera al compilar el Host (`OpenApiDocumentsDirectory` en `CaseritoApp.Host.csproj`):

Run: `cd CaseritoApp && dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj`
Expected: build sin errores; `git status` muestra `artifacts/openapi/CaseritoApp.Host.json` modificado (nuevos campos `sessionId`/`flowEvents` en el schema del request).

- [ ] **Step 2: Regenerar tipos del frontend**

Run: `cd web && npm run generate:api`
Expected: `src/api/schema.d.ts` actualizado sin edición manual.

- [ ] **Step 3: Suite completa backend + formato**

Run: `cd CaseritoApp && dotnet test CaseritoApp.sln`
Expected: PASS — 375 unitarios + 69 arquitectura + 229 integración + los nuevos (Tasks 1–3).

Run: `cd CaseritoApp && dotnet format CaseritoApp.sln --verify-no-changes`
Expected: sin cambios reportados.

- [ ] **Step 4: Verificación completa frontend**

Run: `cd web && npm run typecheck && npm run lint && npm test && npm run build`
Expected: todo en verde; build de Vite correcto.

- [ ] **Step 5: Commit de artefactos**

```bash
git add CaseritoApp/artifacts/openapi/CaseritoApp.Host.json web/src/api/schema.d.ts
git commit -m "chore(observabilidad): regenera contrato OpenAPI y tipos del cliente"
```

- [ ] **Step 6: Revisión final del diff**

Run: `git diff master...HEAD --stat && git diff --check`
Expected: solo los ficheros de este plan; sin whitespace errors. Verificar contra el spec: comportamiento, anti-PII, límites, cobertura. No mergear ni pushear sin autorización del usuario.

---

## Self-Review

- **Spec coverage:** sessionId (T1, T4, T5), modo debug (T4), buffer circular (T4), sanitización (T4, T6, T7), enriquecimiento de errores (T5), header X-Session-Id (T6, T3), `frontend.flow` (T2), límites 16 KB (T2), botón exportación (T7), captura navegación (T7), artefactos (T8), criterios de aceptación 1–6 (T2–T8). Sin huecos.
- **Placeholders:** ninguno; todo el código y tests están completos.
- **Type consistency:** `EventoFlujo` (front) ↔ `ClientFlowEvent` (back) con claves camelCase idénticas (`seq`, `timestamp`, `eventName`, `detail`, `traceId`, `statusCode`, `durationMs`); `flowEvents`/`sessionId` coinciden con el record C# tras la serialización camelCase de ASP.NET; `DiagnosticIds.IsSessionId` usado en T1 (validador) y T3 (middleware); `leerTraceId` reutilizado en T6 ya existe en `http.ts`.
- **Limitación conocida:** la emisión efectiva del scope `SessionId`/`frontend.flow` en los logs no se observa vía tests HTTP (requeriría un logger de prueba); se cubre con tests puros del patrón, tests de aceptación/rechazo del endpoint y revisión de código.
