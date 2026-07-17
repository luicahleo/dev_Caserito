import { describe, it, expect, vi, afterEach } from 'vitest';
import { api, desempaquetar, HttpError } from './http';
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

describe('http refresh-on-401', () => {
  it('ante 401 refresca una vez y reintenta con el nuevo token', async () => {
    setAccessToken('viejo');
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(respuesta(401)) // GET original
      .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' })) // refresh
      .mockResolvedValueOnce(
        respuesta(200, { id: '1', email: 'a@b.c', nombre: 'A', ciudad: 'LP' }),
      ); // reintento

    const r = await api.GET('/api/perfil');
    const data = desempaquetar(r);
    expect((data as { email: string }).email).toBe('a@b.c');
    expect(getAccessToken()).toBe('nuevo');
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it('si el refresh falla, limpia la sesión', async () => {
    setAccessToken('viejo');
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(respuesta(401))
      .mockResolvedValueOnce(respuesta(401)); // refresh falla
    await api.GET('/api/perfil');
    expect(getAccessToken()).toBeNull();
  });

  it('no intenta refrescar en rutas /api/auth/*', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(respuesta(401));
    await api.POST('/api/auth/login', { body: { email: 'x', password: 'y' } });
    expect(fetchMock).toHaveBeenCalledTimes(1); // sin reintento
  });
});

describe('desempaquetar / HttpError', () => {
  it('lanza HttpError con status y code de ProblemDetails ante un 409', () => {
    const r = {
      error: { title: 'Kyc.YaVerificado' },
      response: new Response(null, { status: 409 }),
    };
    try {
      desempaquetar(r as never);
      expect.unreachable();
    } catch (e) {
      expect(e).toBeInstanceOf(HttpError);
      expect((e as HttpError).status).toBe(409);
      expect((e as HttpError).code).toBe('Kyc.YaVerificado');
    }
  });

  it('inyecta Authorization: Bearer desde la sesión', async () => {
    setAccessToken('tok');
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(respuesta(200, {}));
    await api.GET('/api/perfil');
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.headers.get('Authorization')).toBe('Bearer tok');
  });
});
