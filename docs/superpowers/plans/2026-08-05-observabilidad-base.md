# Plan de implementación — observabilidad base front-back

Spec: `docs/superpowers/specs/2026-08-05-observabilidad-base-design.md`

## Tarea 1: primitivas seguras de diagnóstico backend

**Entradas:** formato y política anti-PII definidos en el spec.

**Archivos:**

- Crear `CaseritoApp/src/Host/CaseritoApp.Host/Observability/DiagnosticIds.cs`.
- Crear `CaseritoApp/src/Host/CaseritoApp.Host/Observability/ClientDiagnosticReport.cs`.
- Crear `CaseritoApp/tests/CaseritoApp.ArchitectureTests/Observability/DiagnosticContractTests.cs`.

**Comportamiento:** `DiagnosticIds.NewErrorId()` devuelve
`ERR-` + 12 caracteres hexadecimales mayúsculos. El DTO del navegador solo
expone `ErrorId`, `EventName`, `Category`, `Source`, `TraceId`, `StatusCode` y
`Release`; un validador puro acepta exclusivamente formatos, longitudes y
valores enumerados. No existe ninguna propiedad de texto libre.

**Rojo esperado:** los tests no compilan porque todavía no existen los tipos.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj --filter DiagnosticContractTests
```

Resultado: generación válida/distinta, lista permitida válida y rechazo de cada
campo inválido; reflexión confirma la lista exacta de propiedades.

**Commit previsto:** `feat(observabilidad): define contrato seguro de diagnóstico`

## Tarea 2: correlación, errores globales y request logs

**Entradas:** `DiagnosticIds`, ASP.NET Core `Activity`, endpoint metadata y
`ProblemDetails`.

**Archivos:**

- Crear `CaseritoApp/src/Host/CaseritoApp.Host/Observability/DiagnosticContext.cs`.
- Crear `CaseritoApp/src/Host/CaseritoApp.Host/Observability/RequestObservabilityMiddleware.cs`.
- Crear `CaseritoApp/src/Host/CaseritoApp.Host/Observability/GlobalExceptionHandler.cs`.
- Modificar `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`.
- Crear `CaseritoApp/tests/CaseritoApp.IntegrationTests/Observability/ObservabilityPipelineTests.cs`.

**Interfaces producidas:** cabecera `X-Trace-Id`; en 500,
`application/problem+json` con título genérico y extensiones `errorId` y
`traceId`. `DiagnosticContext` conserva el `ErrorId` en `HttpContext.Items` para
el evento final.

**Comportamiento:** registrar una finalización con método, patrón del endpoint,
status, duración y correlación. Capturar excepciones no controladas antes de que
salgan del host, generar un único `ErrorId`, registrar tipo (no mensaje ni
objeto `Exception`) y responder 500 genérico. La ruta registrada se obtiene de
`RouteEndpoint.RoutePattern.RawText`; para endpoint desconocido se usa
`unmatched`, nunca `Request.Path`.

**Rojo esperado:** el endpoint de prueba que lanza devuelve la excepción o una
respuesta sin identificadores; una petición normal no trae `X-Trace-Id`.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter ObservabilityPipelineTests
```

Resultado: cabecera presente, 500 seguro y correlación común. Si Docker no está
disponible, complementar con build y documentar la integración pendiente.

**Commit previsto:** `feat(observabilidad): correlaciona peticiones y errores backend`

## Tarea 3: recepción segura de diagnósticos frontend

**Entradas:** `ClientDiagnosticReport`, rate limiter existente y logging
estructurado.

**Archivos:**

- Crear `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/DiagnosticsEndpoints.cs`.
- Modificar `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`.
- Crear `CaseritoApp/tests/CaseritoApp.IntegrationTests/Observability/DiagnosticsEndpointTests.cs`.

**Interfaz:** `POST /api/diagnosticos/frontend`, anónimo, máximo 4 KiB, política
`diagnosticos-frontend` de 20 peticiones/minuto por partición. Devuelve `202` sin
cuerpo al aceptar, `400` genérico al rechazar y `413` por exceso de tamaño.

**Comportamiento:** deserializar el DTO cerrado, validar todos los valores y
registrarlos como propiedades escalares con `EventName = frontend.error`. No
registrar el objeto completo ni el cuerpo. El endpoint debe aparecer en OpenAPI.

**Rojo esperado:** el POST devuelve fallback/404 y los casos inválidos no están
protegidos.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter DiagnosticsEndpointTests
```

Resultado: `202` para reporte permitido; `400` para valores inválidos y campos
desconocidos; límite de cuerpo verificado.

**Commit previsto:** `feat(observabilidad): recibe diagnósticos seguros del navegador`

## Tarea 4: servicio de diagnóstico y transporte frontend

**Entradas:** endpoint anterior, `fetch`, `crypto.getRandomValues`, cliente HTTP
central y contrato OpenAPI generado.

**Archivos:**

- Crear `web/src/lib/diagnosticos.ts`.
- Crear `web/src/lib/diagnosticos.test.ts`.
- Modificar `web/src/api/http.ts`.
- Modificar `web/src/api/http.test.ts`.

**Interfaces producidas:** `crearErrorId`, `reportarDiagnostico`,
`instalarCapturaGlobal` y tipos unión cerrados para evento/categoría/fuente.
`HttpError` añade `errorId` y `traceId` opcionales.

**Comportamiento:** crear el código en navegador, enviar solo campos
permitidos mediante un `fetch` dedicado marcado internamente para no reportarse
a sí mismo, y absorber fallos del envío. El transporte normal reporta errores de
red y respuestas 5xx una vez; no reporta 4xx. Lee `X-Trace-Id` y `errorId` de
`ProblemDetails` sin conservar detalles adicionales.

**Rojo esperado:** tests fallan por ausencia del servicio, ausencia de campos en
`HttpError` y falta de POST diagnóstico.

**Verificación:**

```powershell
npm run test -- src/lib/diagnosticos.test.ts src/api/http.test.ts
```

Resultado: formato, payload cerrado, 5xx/red reportados, 4xx ignorados y fallo
del reporter absorbido.

**Commit previsto:** `feat(web): reporta fallos técnicos con correlación`

## Tarea 5: captura global y código visible

**Entradas:** servicio de diagnóstico, router y recuperación de chunks actuales.

**Archivos:**

- Modificar `web/src/main.tsx`.
- Modificar `web/src/app/ErrorAplicacion.tsx`.
- Modificar `web/src/app/ErrorAplicacion.test.tsx`.
- Crear o ampliar tests de listeners en `web/src/lib/diagnosticos.test.ts`.

**Comportamiento:** instalar una sola vez listeners para `error` y
`unhandledrejection`; ambos reportan categorías cerradas sin mensaje, stack ni
objeto. `ErrorAplicacion` mantiene recuperación de chunks, crea/reutiliza un
`ErrorId`, reporta una sola vez y muestra/copía únicamente ese código. El botón
de copia ofrece confirmación accesible y tolera falta de Clipboard API.

**Rojo esperado:** la pantalla no muestra ni copia un código y los eventos
globales no generan reportes.

**Verificación:**

```powershell
npm run test -- src/app/ErrorAplicacion.test.tsx src/lib/diagnosticos.test.ts
```

Resultado: UI genérica, código visible/copiable, detalles internos ausentes y
listeners sin contenido libre.

**Commit previsto:** `feat(web): muestra códigos de diagnóstico seguros`

## Tarea 6: configuración, contrato e integración

**Entradas:** pipeline completo aprobado.

**Archivos:**

- Modificar `CaseritoApp/src/Host/CaseritoApp.Host/appsettings.json` para consola
  JSON y niveles.
- Modificar `docker-compose.yml` para `json-file`, `max-size: 10m` y
  `max-file: 5`.
- Regenerar `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` y
  `web/src/api/schema.d.ts`.
- Actualizar tests derivados únicamente si el contrato lo exige.

**Comportamiento:** producción emite JSON parseable y Docker rota sin afectar
volúmenes de negocio. OpenAPI describe el endpoint aditivo.

**Rojo esperado:** artefactos derivados no contienen el endpoint y la
configuración no selecciona consola JSON/rotación.

**Verificación backend (desde `CaseritoApp/`):**

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

**Verificación frontend (desde `web/`):**

```powershell
npm run generate:api
npm run typecheck
npm run lint
npm run test -- --run
npm run format:check
npm run build
```

**Verificación final (raíz):**

```powershell
git diff --check
docker compose config
```

Resultado: suites completas verdes, artefactos sincronizados, JSON/compose
válidos y ausencia de mojibake o campos PII en el diff.

**Commit previsto:** `chore(observabilidad): integra contrato y rotación de logs`

## Revisión final

Comparar el diff completo con el spec y comprobar específicamente:

- que ninguna plantilla usa rutas concretas, query, headers o cuerpos;
- que ninguna llamada de log recibe `Exception` ni DTOs completos;
- que el reporter no entra en recursión;
- que `errorId` y `traceId` coinciden entre respuesta, frontend y logs;
- que el endpoint anónimo tiene validación, límite de cuerpo y rate limit;
- que no se añadieron sinks, persistencia de negocio ni telemetría diferida.
