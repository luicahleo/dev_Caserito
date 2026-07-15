import { describe, it, expect, vi, afterEach } from 'vitest';
import { getJson } from './client';
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
