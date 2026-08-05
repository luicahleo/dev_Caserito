import { describe, it, expect, vi, afterEach } from 'vitest';
import { api, desempaquetar, HttpError } from './http';
import { setAccessToken, getAccessToken, clearAccessToken } from '../auth/session';
import * as diagnosticos from '../lib/diagnosticos';

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

  it('conserva errorId y traceId seguros de ProblemDetails', () => {
    const r = {
      error: {
        title: 'Error inesperado',
        errorId: 'ERR-0123456789AB',
        traceId: '0123456789abcdef0123456789abcdef',
      },
      response: new Response(null, { status: 500 }),
    };

    expect(() => desempaquetar(r as never)).toThrowError(
      expect.objectContaining({
        errorId: 'ERR-0123456789AB',
        traceId: '0123456789abcdef0123456789abcdef',
      }),
    );
  });

  it('inyecta Authorization: Bearer desde la sesión', async () => {
    setAccessToken('tok');
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(respuesta(200, {}));
    await api.GET('/api/perfil');
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.headers.get('Authorization')).toBe('Bearer tok');
  });
});

describe('diagnóstico del transporte', () => {
  it('reporta respuestas 5xx con el traceId del backend', async () => {
    const reportar = vi.spyOn(diagnosticos, 'reportarDiagnostico').mockResolvedValue();
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ title: 'Error inesperado' }), {
        status: 503,
        headers: {
          'Content-Type': 'application/problem+json',
          'X-Trace-Id': '0123456789abcdef0123456789abcdef',
        },
      }),
    );

    await api.GET('/api/catalogo/categorias');

    expect(reportar).toHaveBeenCalledWith(
      expect.objectContaining({
        eventName: 'http.server_failed',
        traceId: '0123456789abcdef0123456789abcdef',
        statusCode: 503,
      }),
    );
  });

  it('reporta errores de red y vuelve a lanzarlos', async () => {
    const reportar = vi.spyOn(diagnosticos, 'reportarDiagnostico').mockResolvedValue();
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'));

    await expect(api.GET('/api/catalogo/categorias')).rejects.toBeInstanceOf(TypeError);
    expect(reportar).toHaveBeenCalledWith(
      expect.objectContaining({ eventName: 'http.network_failed' }),
    );
  });

  it('no reporta respuestas 4xx esperadas', async () => {
    const reportar = vi.spyOn(diagnosticos, 'reportarDiagnostico').mockResolvedValue();
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(respuesta(404, { title: 'No encontrado' }));

    await api.GET('/api/catalogo/categorias');

    expect(reportar).not.toHaveBeenCalled();
  });
});
