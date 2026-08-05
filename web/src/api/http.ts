// Cliente HTTP tipado generado del OpenAPI de la Web API. openapi-fetch resuelve URL,
// query y serialización a partir de los tipos de `schema.d.ts`; este módulo aporta el
// transporte transversal: inyección del Bearer, refresh-on-401 con reintento único y
// el mapeo de errores a HttpError sin PII.
//
// Nota sobre la firma del fetch custom: openapi-fetch (v0.14) NO invoca el fetch con
// `(input, init)`. Construye un `Request` completo (con URL, headers y body ya
// serializados) y llama `fetch(request, requestInitExt)`. Por eso el transporte
// reconstruye el `Request` para inyectar el header `Authorization` (no puede leer
// `init?.headers` porque no existe ese segundo parámetro con headers) y clona el
// `Request` antes del primer intento para poder reintentarlo tras un refresh (el body
// de un `Request`/`Response` es de un solo uso).
import createClient from 'openapi-fetch';
import type { paths } from './schema';
import { clearAccessToken, getAccessToken, setAccessToken } from '../auth/session';
import { crearErrorId, reportarDiagnostico } from '../lib/diagnosticos';

// Error tipado para ramificar por status (p. ej. 409). Solo status + code no-PII de ProblemDetails.
export class HttpError extends Error {
  readonly status: number;
  readonly code: string | null;
  readonly errorId: string | null;
  readonly traceId: string | null;

  constructor(
    status: number,
    code: string | null,
    mensaje: string,
    errorId: string | null = null,
    traceId: string | null = null,
  ) {
    super(mensaje);
    this.name = 'HttpError';
    this.status = status;
    this.code = code;
    this.errorId = errorId;
    this.traceId = traceId;
  }
}

async function refrescarToken(): Promise<boolean> {
  const r = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'include' });
  if (!r.ok) return false;
  try {
    const data = (await r.json()) as { accessToken: string };
    setAccessToken(data.accessToken);
    return true;
  } catch {
    return false;
  }
}

// Reconstruye un Request a partir de otro, agregando/actualizando el header Authorization
// según el token vigente en sesión. `new Request(request)` clona URL/método/headers/body.
function conAutorizacion(request: Request): Request {
  const headers = new Headers(request.headers);
  const token = getAccessToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  } else {
    headers.delete('Authorization');
  }
  return new Request(request, { headers, credentials: 'include' });
}

// fetch custom: openapi-fetch le entrega un Request ya armado. Clonamos antes de
// consumirlo (el body es de un solo uso) para poder reintentar tras un refresh.
// Ante 401 fuera de /api/auth/* refresca el access token una vez y reintenta.
async function transporte(request: Request): Promise<Response> {
  const esRutaAuth = new URL(request.url).pathname.startsWith('/api/auth/');

  const originalParaReintento = esRutaAuth ? null : request.clone();
  let respuesta = await fetchConDiagnostico(conAutorizacion(request));

  if (respuesta.status === 401 && !esRutaAuth && originalParaReintento) {
    if (await refrescarToken()) {
      respuesta = await fetchConDiagnostico(conAutorizacion(originalParaReintento));
    } else {
      clearAccessToken();
    }
  }
  return respuesta;
}

async function fetchConDiagnostico(request: Request): Promise<Response> {
  try {
    const response = await fetch(request);
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
    void reportarDiagnostico({
      errorId: crearErrorId(),
      eventName: 'http.network_failed',
      category: 'network',
      source: 'http',
    });
    throw error;
  }
}

// openapi-fetch arma la URL final concatenando baseUrl + pathname y construye un
// `Request` con ella; sin baseUrl absoluto, `new Request('/api/...')` falla (a
// diferencia de un fetch de navegador, el constructor de Request no resuelve contra
// la URL del documento). `window.location.origin` cubre dev, tests (jsdom) y prod,
// ya que la API se sirve tras el mismo origen (proxy de Vite / mismo host detrás de NGINX).
export const api = createClient<paths>({
  baseUrl: window.location.origin,
  fetch: transporte,
});

// Lee el código no-PII (title de ProblemDetails) de un error de openapi-fetch.
function leerCodigo(error: unknown): string | null {
  if (error && typeof error === 'object' && 'title' in error) {
    const title = (error as { title?: unknown }).title;
    return typeof title === 'string' ? title : null;
  }
  return null;
}

function leerErrorId(error: unknown): string | null {
  if (error && typeof error === 'object' && 'errorId' in error) {
    const value = (error as { errorId?: unknown }).errorId;
    return typeof value === 'string' && /^ERR-[0-9A-F]{12}$/.test(value) ? value : null;
  }
  return null;
}

function leerTraceId(value: unknown): string | null {
  return typeof value === 'string' && /^[0-9a-f]{32}$/.test(value) ? value : null;
}

// Desempaqueta un resultado de openapi-fetch: devuelve data si ok; si no, lanza HttpError.
export function desempaquetar<T>(resultado: { data?: T; error?: unknown; response: Response }): T {
  if (resultado.error !== undefined || !resultado.response.ok) {
    const code = leerCodigo(resultado.error);
    const errorId = leerErrorId(resultado.error);
    const errorTraceId =
      resultado.error && typeof resultado.error === 'object' && 'traceId' in resultado.error
        ? (resultado.error as { traceId?: unknown }).traceId
        : null;
    const traceId =
      leerTraceId(errorTraceId) ?? leerTraceId(resultado.response.headers.get('X-Trace-Id'));
    throw new HttpError(
      resultado.response.status,
      code,
      `Petición fallida (${resultado.response.status})`,
      errorId,
      traceId,
    );
  }
  return resultado.data as T;
}
