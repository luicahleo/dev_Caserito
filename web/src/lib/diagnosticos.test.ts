import { afterEach, describe, expect, it, vi } from 'vitest';
import { crearErrorId, instalarCapturaGlobal, reportarDiagnostico } from './diagnosticos';
import { registrarEventoFlujo } from './sesionDiagnostico';

afterEach(() => {
  vi.restoreAllMocks();
  sessionStorage.clear();
  vi.resetModules();
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
      sessionId: expect.stringMatching(/^SES-[0-9A-F]{12}$/),
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

  it('adjunta los últimos 30 eventos de flujo cuando existen', async () => {
    sessionStorage.setItem('caserito.debug', '1');
    for (let i = 0; i < 35; i++) {
      registrarEventoFlujo({ eventName: 'flow.navigation', detail: `/ruta-${i}` });
    }
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(null, { status: 202 }));

    await reportarDiagnostico({
      errorId: 'ERR-0123456789AB',
      eventName: 'window.unexpected',
      category: 'unexpected',
      source: 'window',
    });

    const cuerpo = JSON.parse(String(fetchMock.mock.calls[0][1]?.body));
    expect(cuerpo.sessionId).toMatch(/^SES-[0-9A-F]{12}$/);
    expect(cuerpo.flowEvents).toHaveLength(30);
    expect(cuerpo.flowEvents[0].detail).toBe('/ruta-5');
    expect(cuerpo.flowEvents[29].detail).toBe('/ruta-34');
  });

  it('captura errores globales sin propagar su contenido', () => {
    const reportar = vi.fn().mockResolvedValue(undefined);
    const desinstalar = instalarCapturaGlobal(reportar);

    window.dispatchEvent(new ErrorEvent('error', { message: 'documento privado' }));
    window.dispatchEvent(new Event('unhandledrejection'));

    expect(reportar).toHaveBeenCalledTimes(2);
    expect(reportar).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({
        eventName: 'window.unexpected',
        category: 'unexpected',
        source: 'window',
      }),
    );
    expect(reportar).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({
        eventName: 'promise.unhandled',
        category: 'unexpected',
        source: 'promise',
      }),
    );
    expect(JSON.stringify(reportar.mock.calls)).not.toContain('documento privado');

    desinstalar();
  });
});
