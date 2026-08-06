import { afterEach, describe, expect, it, vi } from 'vitest';

async function cargarModulo() {
  return import('./sesionDiagnostico');
}

afterEach(() => {
  sessionStorage.clear();
  vi.resetModules();
  vi.restoreAllMocks();
});

describe('sesión de diagnóstico', () => {
  it('genera un sessionId opaco y lo mantiene estable en la pestaña', async () => {
    const modulo = await cargarModulo();

    const primero = modulo.obtenerSesionId();
    const segundo = modulo.obtenerSesionId();

    expect(primero).toMatch(/^SES-[0-9A-F]{12}$/);
    expect(segundo).toBe(primero);
  });

  it('regenera el sessionId si el almacenado no cumple el patrón', async () => {
    sessionStorage.setItem('caserito.sesion', 'manipulado');
    const modulo = await cargarModulo();

    expect(modulo.obtenerSesionId()).toMatch(/^SES-[0-9A-F]{12}$/);
  });

  it('activa el modo diagnóstico con ?debug=1 y lo conserva', async () => {
    const modulo = await cargarModulo();
    expect(modulo.modoDiagnosticoActivo()).toBe(false);

    window.history.replaceState(null, '', '/?debug=1');
    expect(modulo.modoDiagnosticoActivo()).toBe(true);

    window.history.replaceState(null, '', '/');
    expect(modulo.modoDiagnosticoActivo()).toBe(true);
  });

  it('sanitiza segmentos numéricos y UUID sin tocar el resto', async () => {
    const modulo = await cargarModulo();

    expect(modulo.sanitizarRuta('/avisos/123')).toBe('/avisos/:id');
    expect(modulo.sanitizarRuta('/avisos/3fa85f64-5717-4562-b3fc-2c963f66afa6')).toBe(
      '/avisos/:id',
    );
    expect(modulo.sanitizarRuta('/mis-avisos')).toBe('/mis-avisos');
    expect(modulo.sanitizarRuta('/')).toBe('/');
  });

  it('no registra eventos sin modo diagnóstico', async () => {
    const modulo = await cargarModulo();

    modulo.registrarEventoFlujo({ eventName: 'flow.navigation', detail: '/explorar' });

    expect(modulo.obtenerEventosRecientes(10)).toEqual([]);
  });

  it('registra eventos con seq incremental y trunca el detalle a 120', async () => {
    sessionStorage.setItem('caserito.debug', '1');
    const modulo = await cargarModulo();

    modulo.registrarEventoFlujo({ eventName: 'flow.navigation', detail: '/explorar' });
    modulo.registrarEventoFlujo({
      eventName: 'flow.api_call',
      detail: `GET /api/${'a'.repeat(200)}`,
      statusCode: 200,
      durationMs: 42,
    });

    const eventos = modulo.obtenerEventosRecientes(10);
    expect(eventos).toHaveLength(2);
    expect(eventos[0]).toMatchObject({ seq: 1, eventName: 'flow.navigation', detail: '/explorar' });
    expect(eventos[1].seq).toBe(2);
    expect(eventos[1].detail).toHaveLength(120);
    expect(eventos[0].timestamp).toMatch(/Z$/);
  });

  it('descarta los eventos más antiguos al superar el máximo de 100', async () => {
    sessionStorage.setItem('caserito.debug', '1');
    const modulo = await cargarModulo();

    for (let i = 0; i < 105; i++) {
      modulo.registrarEventoFlujo({ eventName: 'flow.navigation', detail: `/ruta-${i}` });
    }

    const eventos = modulo.obtenerEventosRecientes(200);
    expect(eventos).toHaveLength(100);
    expect(eventos[0].detail).toBe('/ruta-5');
    expect(eventos[0].seq).toBe(6);
  });

  it('exporta el diagnóstico como descarga JSON', async () => {
    sessionStorage.setItem('caserito.debug', '1');
    const modulo = await cargarModulo();
    modulo.registrarEventoFlujo({ eventName: 'flow.navigation', detail: '/explorar' });
    const crearUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:mock');
    const revocar = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);

    modulo.exportarDiagnostico();

    expect(crearUrl).toHaveBeenCalledOnce();
    const blob = crearUrl.mock.calls[0][0] as Blob;
    const contenido = JSON.parse(await blob.text());
    expect(contenido.sessionId).toMatch(/^SES-[0-9A-F]{12}$/);
    expect(contenido.eventos).toHaveLength(1);
    expect(click).toHaveBeenCalledOnce();
    expect(revocar).toHaveBeenCalledWith('blob:mock');
  });
});
