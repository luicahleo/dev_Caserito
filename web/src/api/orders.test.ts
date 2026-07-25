import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from './http';
import {
  aceptarOrden,
  cancelarOrden,
  confirmarCierreOrden,
  listarOrdenes,
  marcarOrdenVendida,
  obtenerOrden,
  solicitarOrden,
} from './orders';

describe('api/orders', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('solicita usando únicamente el aviso', async () => {
    const spy = vi.spyOn(api, 'POST').mockResolvedValue({
      data: { id: 'o1', avisoId: 'a1', estado: 'Requested' },
      response: new Response(null, { status: 201 }),
    } as never);

    await solicitarOrden('a1');

    expect(spy).toHaveBeenCalledWith('/api/orders', { body: { avisoId: 'a1' } });
  });

  it('lista con rol, estado y paginación sin aceptar un usuario arbitrario', async () => {
    const spy = vi.spyOn(api, 'GET').mockResolvedValue({
      data: { items: [], pagina: 2, tamano: 10, total: 0 },
      response: new Response(null, { status: 200 }),
    } as never);

    await listarOrdenes('vendedor', 'Requested', 2, 10);

    expect(spy).toHaveBeenCalledWith('/api/orders', {
      params: { query: { rol: 'vendedor', estado: 'Requested', pagina: 2, tamano: 10 } },
    });
  });

  it('obtiene y acepta por identificador de orden', async () => {
    const get = vi.spyOn(api, 'GET').mockResolvedValue({
      data: { id: 'o1' },
      response: new Response(null, { status: 200 }),
    } as never);
    const post = vi.spyOn(api, 'POST').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    await obtenerOrden('o1');
    await aceptarOrden('o1');

    expect(get).toHaveBeenCalledWith('/api/orders/{id}', {
      params: { path: { id: 'o1' } },
    });
    expect(post).toHaveBeenCalledWith('/api/orders/{id}/aceptar', {
      params: { path: { id: 'o1' } },
    });
  });

  it('cancela una orden por identificador', async () => {
    const post = vi.spyOn(api, 'POST').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    await cancelarOrden('o1');

    expect(post).toHaveBeenCalledWith('/api/orders/{id}/cancelar', {
      params: { path: { id: 'o1' } },
    });
  });

  it('marca vendido y confirma cierre mediante POST sin body', async () => {
    const post = vi.spyOn(api, 'POST').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    await marcarOrdenVendida('o1');
    await confirmarCierreOrden('o1');

    expect(post).toHaveBeenNthCalledWith(1, '/api/orders/{id}/marcar-vendido', {
      params: { path: { id: 'o1' } },
    });
    expect(post).toHaveBeenNthCalledWith(2, '/api/orders/{id}/confirmar-completado', {
      params: { path: { id: 'o1' } },
    });
  });
});
