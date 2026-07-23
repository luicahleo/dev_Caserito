import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from './http';
import {
  atenderReporteChat,
  cerrarConversacionModeracion,
  descartarReporteChat,
  liberarReporteChat,
  listarReportesChat,
  obtenerEvidenciaReporteChat,
  reabrirConversacionModeracion,
  tomarReporteChat,
} from './moderacionChat';

describe('api/moderacionChat', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('lista la cola con filtros tipados y sin contenido de evidencia', async () => {
    const cola = [
      {
        id: 'reporte-1',
        conversacionId: 'conversacion-1',
        tipoObjetivo: 1,
        mensajeId: null,
        categoria: 2,
        estado: 0,
        creadoEn: '2026-07-23T10:00:00Z',
        tomadoEn: null,
        resueltoEn: null,
      },
    ];
    const spy = vi.spyOn(api, 'GET').mockResolvedValue({
      data: cola,
      response: new Response(null, { status: 200 }),
    } as never);

    const resultado = await listarReportesChat({ estado: 0, cursor: 'cursor', limite: 20 });

    expect(spy).toHaveBeenCalledWith('/api/admin/moderacion/chat/reportes', {
      params: { query: { estado: 0, cursor: 'cursor', limite: 20 } },
    });
    expect(resultado).toEqual(cola);
    expect(resultado[0]).not.toHaveProperty('mensajes');
    expect(resultado[0]).not.toHaveProperty('detalle');
  });

  it('obtiene evidencia y convierte cada secuencia explícitamente', async () => {
    vi.spyOn(api, 'GET').mockResolvedValue({
      data: {
        reporteId: 'reporte-1',
        tipoObjetivo: 1,
        categoria: 2,
        detalle: null,
        rolReportante: 'Comprador',
        rolObjetivo: 'Vendedor',
        mensajes: [
          {
            id: 'mensaje-1',
            secuencia: '42',
            autorRol: 'Comprador',
            texto: 'contenido autorizado',
            enviadoEn: '2026-07-23T10:00:00Z',
            esObjetivo: true,
          },
        ],
      },
      response: new Response(null, { status: 200 }),
    } as never);

    const evidencia = await obtenerEvidenciaReporteChat('reporte-1');

    expect(evidencia.mensajes[0].secuencia).toBe(Number('42'));
  });

  it('serializa toma, liberación y resolución con contratos generados', async () => {
    const spy = vi.spyOn(api, 'POST').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    await tomarReporteChat('reporte-1');
    await liberarReporteChat('reporte-1');
    await atenderReporteChat('reporte-1', { cerrarConversacion: true });
    await descartarReporteChat('reporte-1');

    expect(spy.mock.calls).toEqual([
      ['/api/admin/moderacion/chat/reportes/{id}/tomar', { params: { path: { id: 'reporte-1' } } }],
      [
        '/api/admin/moderacion/chat/reportes/{id}/liberar',
        { params: { path: { id: 'reporte-1' } } },
      ],
      [
        '/api/admin/moderacion/chat/reportes/{id}/atender',
        {
          params: { path: { id: 'reporte-1' } },
          body: { cerrarConversacion: true },
        },
      ],
      [
        '/api/admin/moderacion/chat/reportes/{id}/descartar',
        { params: { path: { id: 'reporte-1' } } },
      ],
    ]);
  });

  it('serializa cierre y reapertura desde el expediente', async () => {
    const put = vi.spyOn(api, 'PUT').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);
    const eliminar = vi.spyOn(api, 'DELETE').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    await cerrarConversacionModeracion('reporte-1');
    await reabrirConversacionModeracion('reporte-1');

    const parametros = { params: { path: { id: 'reporte-1' } } };
    expect(put).toHaveBeenCalledWith(
      '/api/admin/moderacion/chat/reportes/{id}/cierre-conversacion',
      parametros,
    );
    expect(eliminar).toHaveBeenCalledWith(
      '/api/admin/moderacion/chat/reportes/{id}/cierre-conversacion',
      parametros,
    );
  });
});
