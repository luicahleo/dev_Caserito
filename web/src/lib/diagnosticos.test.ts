import { afterEach, describe, expect, it, vi } from 'vitest';
import { crearErrorId, reportarDiagnostico } from './diagnosticos';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('diagnósticos seguros', () => {
  it('crea códigos opacos con el formato compartido con backend', () => {
    const primero = crearErrorId();
    const segundo = crearErrorId();

    expect(primero).toMatch(/^ERR-[0-9A-F]{12}$/);
    expect(segundo).not.toBe(primero);
  });

  it('envía exclusivamente el contrato permitido', async () => {
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(null, { status: 202 }));

    await reportarDiagnostico({
      errorId: 'ERR-0123456789AB',
      eventName: 'http.server_failed',
      category: 'server',
      source: 'http',
      traceId: '0123456789abcdef0123456789abcdef',
      statusCode: 503,
    });

    expect(fetchMock).toHaveBeenCalledOnce();
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/diagnosticos/frontend');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(String(init?.body))).toEqual({
      errorId: 'ERR-0123456789AB',
      eventName: 'http.server_failed',
      category: 'server',
      source: 'http',
      traceId: '0123456789abcdef0123456789abcdef',
      statusCode: 503,
    });
  });

  it('absorbe fallos del endpoint para no generar recursión', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new Error('sin red'));

    await expect(
      reportarDiagnostico({
        errorId: 'ERR-0123456789AB',
        eventName: 'window.unexpected',
        category: 'unexpected',
        source: 'window',
      }),
    ).resolves.toBeUndefined();
  });
});
