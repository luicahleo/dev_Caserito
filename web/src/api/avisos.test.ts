import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from './http';
import { buscarAvisos, crearAviso, eliminarAviso } from './avisos';

describe('capa de API de avisos', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('buscarAvisos pasa filtros y paginación como query', async () => {
    const spy = vi.spyOn(api, 'GET').mockResolvedValue({
      data: { items: [], pagina: 1, tamano: 20, total: 0 },
      response: new Response(null, { status: 200 }),
    } as never);

    await buscarAvisos({ q: 'silla', categoriaId: 'c1' }, 2, 20);

    expect(spy).toHaveBeenCalledWith('/api/publico/avisos', {
      params: { query: { q: 'silla', categoriaId: 'c1', pagina: 2, tamano: 20 } },
    });
  });

  it('crearAviso hace POST con el body del request', async () => {
    const spy = vi.spyOn(api, 'POST').mockResolvedValue({
      data: { id: 'nuevo' },
      response: new Response(null, { status: 201 }),
    } as never);

    const req = {
      titulo: 'Mesa',
      descripcion: 'De madera',
      monto: 300,
      condicion: 'Usado',
      categoriaId: 'c1',
      ciudadId: 'u1',
    };
    const res = await crearAviso(req);

    expect(spy).toHaveBeenCalledWith('/api/avisos', { body: req });
    expect(res).toEqual({ id: 'nuevo' });
  });

  it('eliminarAviso hace DELETE con el id en el path', async () => {
    const spy = vi.spyOn(api, 'DELETE').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    await eliminarAviso('a1');

    expect(spy).toHaveBeenCalledWith('/api/avisos/{id}', { params: { path: { id: 'a1' } } });
  });
});
