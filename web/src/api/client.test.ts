import { describe, it, expect, vi, afterEach } from 'vitest';
import { getJson, postForm, getBlob } from './client';
import { setAccessToken, getAccessToken, clearAccessToken } from '../auth/session';

afterEach(() => {
  vi.restoreAllMocks();
  clearAccessToken();
});

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? '' : JSON.stringify(cuerpo), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('client refresh-on-401', () => {
  it('ante 401 refresca una vez y reintenta con el nuevo token', async () => {
    setAccessToken('viejo');
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(respuesta(401)) // GET original
      .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' })) // refresh
      .mockResolvedValueOnce(respuesta(200, { ok: true })); // reintento

    const data = await getJson<{ ok: boolean }>('/api/perfil');
    expect(data.ok).toBe(true);
    expect(getAccessToken()).toBe('nuevo');
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it('si el refresh falla, limpia la sesión y propaga el error', async () => {
    setAccessToken('viejo');
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(respuesta(401))
      .mockResolvedValueOnce(respuesta(401)); // refresh falla
    await expect(getJson('/api/perfil')).rejects.toThrow();
    expect(getAccessToken()).toBeNull();
  });
});

describe('postForm', () => {
  it('envía FormData sin fijar Content-Type y resuelve en 204', async () => {
    setAccessToken('tok');
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(new Response(null, { status: 204 }));
    const form = new FormData();
    form.append('documento', new Blob(['x'], { type: 'image/png' }), 'doc.png');
    await expect(postForm('/api/kyc', form)).resolves.toBeUndefined();
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    const headers = new Headers(init.headers);
    expect(headers.has('Content-Type')).toBe(false);
    expect(headers.get('Authorization')).toBe('Bearer tok');
  });

  it('lanza error si la respuesta no es ok', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(new Response('', { status: 400 }));
    await expect(postForm('/api/kyc', new FormData())).rejects.toThrow();
  });
});

describe('getBlob', () => {
  it('devuelve el Blob de una respuesta ok', async () => {
    setAccessToken('tok');
    const blob = new Blob(['imagen'], { type: 'image/jpeg' });
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(blob, { status: 200, headers: { 'Content-Type': 'image/jpeg' } }),
    );
    const r = await getBlob('/api/admin/kyc/1/documento');
    expect(r.type).toBe('image/jpeg');
  });

  it('lanza error si la respuesta no es ok', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(new Response('', { status: 404 }));
    await expect(getBlob('/api/admin/kyc/1/documento')).rejects.toThrow();
  });
});
