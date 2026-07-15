// Cliente HTTP mínimo hacia la Web API. Envía credenciales (cookie de refresh
// httpOnly) y el access token en memoria; ante 401 intenta refrescar una vez
// y reintenta la petición original. En Fase 1, cuando existan endpoints de
// negocio, se reemplaza/complementa con un cliente tipado generado del OpenAPI.
import { clearAccessToken, getAccessToken, setAccessToken } from '../auth/session';

async function refrescarToken(): Promise<boolean> {
  const r = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'include' });
  if (!r.ok) return false;
  const data = (await r.json()) as { accessToken: string };
  setAccessToken(data.accessToken);
  return true;
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
