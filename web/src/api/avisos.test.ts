import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from './http';
import { buscarAvisos, borrarFotoAviso, crearAviso, eliminarAviso, subirFotoAviso } from './avisos';

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

describe('subirFotoAviso', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('llama a fetch con POST multipart y retorna el id', async () => {
    const fetchSpy = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'foto-uuid' }), { status: 201 }));

    const archivo = new File([new Uint8Array(10)], 'foto.png', { type: 'image/png' });
    const resultado = await subirFotoAviso('aviso-id', archivo);

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/avisos/aviso-id/fotos',
      expect.objectContaining({ method: 'POST' }),
    );
    expect(resultado.id).toBe('foto-uuid');
  });

  it('lanza error si la respuesta no es ok', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response('bad request', { status: 400 }),
    );

    const archivo = new File([new Uint8Array(10)], 'foto.png', { type: 'image/png' });
    await expect(subirFotoAviso('aviso-id', archivo)).rejects.toThrow('400');
  });
});

describe('borrarFotoAviso', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('llama a fetch con DELETE y resuelve si la respuesta es ok', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(new Response(null, { status: 204 }));
    await expect(borrarFotoAviso('aviso-id', 'foto-id')).resolves.toBeUndefined();
  });

  it('lanza error si la respuesta no es ok', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(new Response(null, { status: 404 }));
    await expect(borrarFotoAviso('aviso-id', 'foto-id')).rejects.toThrow('404');
  });
});
