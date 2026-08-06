# Diagnóstico de flujo frontend — diseño

Fecha: 2026-08-06
Rama: `feat/diagnostico-flujo-frontend`
Estado: aprobado por el usuario (enfoque C, sin preguntas adicionales)

## Objetivo

Permitir reconstruir el flujo de un usuario (navegación, llamadas API y errores)
para diagnosticar comportamientos incorrectos del frontend, de dos maneras:

1. Exportando un JSON desde el navegador (modo diagnóstico) que se puede pegar
   a un agente o adjuntar a un reporte.
2. Grepeando los logs del backend por `SessionId`, donde quedan intercalados
   los eventos de flujo del navegador y las peticiones HTTP del servidor.

## Enfoque elegido

Enfoque C (híbrido), aprobado por el usuario:

- Los **errores** se reportan siempre, como hoy, pero enriquecidos con
  `sessionId` y los últimos eventos de flujo capturados.
- El **flujo detallado** (navegación y llamadas API) solo se captura y envía
  cuando el modo diagnóstico está activo en esa pestaña.

## Enmienda 2026-08-06: breadcrumbs siempre capturados

Decisión posterior aprobada por el usuario (motivo: en producción no habrá
contacto con el cliente más que por formulario, así que el error debe llegar
con su contexto incluido):

- El buffer de flujo se captura **siempre en memoria**, con o sin modo
  diagnóstico. No sale del navegador por sí solo.
- El envío sigue ocurriendo **solo adjunto a un reporte de error** (los
  últimos 30 eventos), exactamente como antes.
- El modo diagnóstico (`?debug=1`) se conserva únicamente como herramienta
  manual: muestra el botón "Descargar diagnóstico" para exportar el JSON.
- El perfil anti-PII no cambia: los breadcrumbs son la misma metadata
  sanitizada (rutas parametrizadas, métodos, status, duraciones) validada por
  la whitelist del backend.

## No objetivos

- No se instrumentan acciones de UI concretas (`flow.action`) en esta
  iteración; navegación + llamadas API + errores cubren el caso de uso.
- No se propaga `traceparent` desde el navegador (W3C Trace Context); el
  backend sigue generando el `TraceId` y el front lo asocia tras la respuesta.
- No hay almacenamiento persistente nuevo en backend (ni tabla ni fichero);
  la persistencia sigue siendo la rotación JSON de Docker.
- No se envían eventos de flujo de usuarios sin modo diagnóstico.

## Frontend (`web/`)

### `src/lib/sesionDiagnostico.ts` (nuevo)

Módulo puro y transversal, sin dependencias de React:

- `obtenerSesionId(): string` — devuelve `SES-<12 hex mayúsculas>` estable por
  pestaña, generado con `crypto.getRandomValues` y persistido en
  `sessionStorage` (clave `caserito.sesion`). Si el valor almacenado no cumple
  el patrón, se regenera.
- `modoDiagnosticoActivo(): boolean` — verdadero si la URL actual contiene
  `?debug=1` (al detectarlo persiste `caserito.debug=1` en `sessionStorage`)
  o si ya existe esa marca en `sessionStorage`.
- `registrarEventoFlujo(evento)` — añade al buffer circular en memoria
  (máximo 100 eventos) asignando `seq` incremental (empieza en 1 por pestaña)
  y `timestamp` ISO UTC. Captura siempre (ver enmienda); el buffer solo sale
  del navegador adjunto a un error o por exportación manual en modo debug.
- `obtenerEventosRecientes(n): EventoFlujo[]` — copia de los últimos `n`.
- `sanitizarRuta(pathname): string` — sustituye por `:id` cualquier segmento
  puramente numérico o con formato UUID; nunca incluye query string ni hash.
- `exportarDiagnostico(): void` — descarga un fichero
  `diagnostico-<sessionId>.json` con `{ sessionId, generadoEn, eventos }`.

Tipo `EventoFlujo`:

```ts
interface EventoFlujo {
  seq: number;
  timestamp: string;          // ISO UTC
  eventName: 'flow.navigation' | 'flow.api_call';
  detail: string;             // ruta sanitizada o "GET /api/pedidos/:id"
  traceId?: string;
  statusCode?: number;
  durationMs?: number;
}
```

### `src/lib/diagnosticos.ts` (extensión)

- `DiagnosticReport` gana `sessionId: string` (siempre) y
  `eventosFlujo?: EventoFlujo[]` (solo si hay eventos capturados; se adjuntan
  los últimos 30 al reportar un error).
- El resto del contrato actual no cambia.

### `src/api/http.ts` (extensión mínima)

- `conAutorizacion` añade el header `X-Session-Id` con el `sessionId` en todas
  las llamadas API (ID opaco, no PII).
- `fetchConDiagnostico`, solo con modo diagnóstico activo, registra
  `flow.api_call` con método, ruta sanitizada (sin query), `statusCode`,
  `durationMs` y `traceId` (de `X-Trace-Id` cuando exista).

### Navegación

Componente `src/app/CapturaFlujoNavegacion.tsx` montado dentro del árbol de
rutas: usa `useLocation` y registra `flow.navigation` con la ruta sanitizada
cuando cambia `location.pathname` (incluido el montaje inicial). Inerte sin
modo diagnóstico.

### Exportación visual

Componente `src/app/BotonDiagnostico.tsx`: `Fab` de MUI con nombre accesible
"Descargar diagnóstico", fijado en la esquina inferior derecha, visible solo
con modo diagnóstico activo; al pulsar llama `exportarDiagnostico()`.

## Backend (`CaseritoApp/`)

### Contrato (`ClientDiagnosticReport`)

Campos nuevos, ambos opcionales para compatibilidad:

- `sessionId`: string con patrón `^SES-[0-9A-F]{12}$`.
- `flowEvents`: array de máximo 50 `ClientFlowEvent` con:
  - `seq` entero entre 1 y 10000;
  - `timestamp` UTC;
  - `eventName` ∈ {`flow.navigation`, `flow.api_call`};
  - `detail` string ≤ 120 chars;
  - `traceId` opcional (patrón de 32 hex minúsculas);
  - `statusCode` opcional (100–599);
  - `durationMs` opcional (0–600000).

`ClientDiagnosticReportValidator` valida lo anterior y rechaza lo demás. El
`RequestSizeLimit` del endpoint y el middleware de límite de body suben de
4096 a 16384 bytes.

### Endpoint `/api/diagnosticos/frontend`

- Si el reporte incluye `flowEvents`, emite un log `frontend.flow` en nivel
  Information por cada evento, con scope `SessionId`, `Seq`, `EventName`,
  `Detail`, `TraceId` (cuando exista).
- El log `frontend.error` actual añade `SessionId` a su scope cuando llega.
- Sigue respondiendo 202/400/413 y usando el rate limit `diagnosticos-frontend`.

### Middleware de observabilidad

`RequestObservabilityMiddleware` lee el header `X-Session-Id`; si cumple el
patrón `SES-…`, añade `SessionId` al scope del log `http.request.completed`.
Headers con formato inválido se ignoran en silencio.

### Artefactos derivados

- Regenerar `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`.
- Regenerar `web/src/api/schema.d.ts` con `npm run generate:api`.

## Seguridad y anti-PII

- `sessionId` es opaco, generado en el navegador, sin datos personales.
- `detail` solo contiene método + ruta sanitizada (IDs parametrizados, sin
  query strings) y está acotado a 120 caracteres.
- Whitelist cerrada de `eventName`; todo lo demás se rechaza con 400.
- Sin bodies, formularios, tokens ni contenido de usuario en ningún log.
- Rate limit y tamaño de request acotados ya existentes se mantienen.

## Pruebas

Frontend (TDD, Vitest + Testing Library):

- `sesionDiagnostico.test.ts`: sessionId estable y con patrón; regeneración
  ante valor inválido; activación por `?debug=1` y persistencia; buffer
  circular con seq incremental y descarte del más antiguo; sanitización de
  rutas numéricas/UUID; inactividad sin modo diagnóstico.
- `diagnosticos.test.ts` extendido: el reporte incluye `sessionId` y los
  últimos 30 eventos de flujo cuando existen.
- `http.test.ts` extendido: header `X-Session-Id` presente; `flow.api_call`
  registrado solo en modo diagnóstico, con ruta sanitizada.
- `BotonDiagnostico.test.tsx`: visible solo en modo diagnóstico; al pulsar se
  genera la descarga.
- `CapturaFlujoNavegacion.test.tsx`: registra `flow.navigation` al cambiar de
  ruta en modo diagnóstico; no registra sin él.

Backend:

- `DiagnosticContractTests` / validador: acepta `sessionId` y `flowEvents`
  válidos; rechaza patrones, tamaños y eventNames inválidos.
- `DiagnosticsEndpointTests`: 202 con flujo válido y emisión de
  `frontend.flow` por evento; 400 con flujo inválido; 413 sobre el nuevo
  límite.
- `ObservabilityPipelineTests`: `SessionId` aparece en el scope cuando llega
  `X-Session-Id` válido y se ignora uno inválido.

## Criterios de aceptación

1. Con `?debug=1`, navegar y usar la app genera eventos `flow.navigation` y
   `flow.api_call` en el buffer y en los logs del backend bajo el mismo
   `SessionId`.
2. El botón "Descargar diagnóstico" produce un JSON legible con la secuencia
   completa de la pestaña.
3. Un error de front reportado incluye `sessionId` y los últimos 30 eventos.
4. Los eventos de flujo se capturan en memoria con o sin modo diagnóstico
   (enmienda 2026-08-06), pero solo se envían adjuntos a un reporte de error;
   sin errores no sale nada del navegador.
5. `docker logs caseritoapp | grep SES-XXXX` muestra intercalados
   `http.request.completed`, `frontend.flow` y `frontend.error`.
6. Ningún log contiene query strings, IDs de recursos, bodies ni datos
   personales.
