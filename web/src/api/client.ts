// Cliente HTTP mínimo hacia la Web API. Envía credenciales (cookie de refresh
// httpOnly) y el access token en memoria; ante 401 intenta refrescar una vez
// y reintenta la petición original. En Fase 1, cuando existan endpoints de
// negocio, se reemplaza/complementa con un cliente tipado generado del OpenAPI.
import { clearAccessToken, getAccessToken, setAccessToken } from '../auth/session';

// Error tipado para respuestas no-ok que necesitan ramificar por status (p.ej. 409).
// El mensaje solo lleva status/ruta/código no-PII de ProblemDetails, nunca contenido sensible.
export class HttpError extends Error {
  readonly status: number;
  readonly code: string | null;

  constructor(status: number, code: string | null, mensaje: string) {
    super(mensaje);
    this.name = 'HttpError';
    this.status = status;
    this.code = code;
  }
}

// Intenta leer el código no-PII de un ProblemDetails; null si no se puede.
async function leerCodigoProblema(r: Response): Promise<string | null> {
  try {
    const cuerpo = (await r.clone().json()) as { title?: string };
    return cuerpo.title ?? null;
  } catch {
    return null;
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

async function ejecutar(ruta: string, init: RequestInit, reintentar = true): Promise<Response> {
  const headers = new Headers(init.headers);
  const token = getAccessToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const respuesta = await fetch(ruta, { ...init, headers, credentials: 'include' });

  const esRutaAuth = ruta.startsWith('/api/auth/');
  if (respuesta.status === 401 && reintentar && !esRutaAuth) {
    if (await refrescarToken()) {
      return ejecutar(ruta, init, false);
    }
    clearAccessToken();
  }
  return respuesta;
}

async function leer<T>(respuesta: Response, ruta: string): Promise<T> {
  if (!respuesta.ok) {
    throw new Error(`Petición fallida (${respuesta.status}) a ${ruta}`);
  }
  const texto = await respuesta.text();
  return texto ? (JSON.parse(texto) as T) : (undefined as T);
}

export async function getJson<T>(ruta: string): Promise<T> {
  const r = await ejecutar(ruta, { method: 'GET', headers: { Accept: 'application/json' } });
  return leer<T>(r, ruta);
}

export async function postJson<T>(ruta: string, cuerpo?: unknown): Promise<T> {
  const r = await ejecutar(ruta, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  });
  return leer<T>(r, ruta);
}

export async function putJson<T>(ruta: string, cuerpo?: unknown): Promise<T> {
  const r = await ejecutar(ruta, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
  });
  return leer<T>(r, ruta);
}

export async function postForm(ruta: string, form: FormData): Promise<void> {
  const r = await ejecutar(ruta, { method: 'POST', body: form });
  if (!r.ok) {
    const code = await leerCodigoProblema(r);
    throw new HttpError(r.status, code, `Petición fallida (${r.status}) a ${ruta}`);
  }
}

export async function getBlob(ruta: string): Promise<Blob> {
  const r = await ejecutar(ruta, { method: 'GET' });
  if (!r.ok) {
    throw new Error(`Petición fallida (${r.status}) a ${ruta}`);
  }
  return r.blob();
}
