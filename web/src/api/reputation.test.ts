import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from './http';
import {
  crearResena,
  listarResenasPublicas,
  obtenerEstadoResena,
  obtenerPerfilPublico,
} from './reputation';

describe('api/reputation', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('obtiene estado y crea la reseña con body tipado', async () => {
    const get = vi.spyOn(api, 'GET').mockResolvedValue({
      data: { puedeCalificar: true, contraparteId: 'u2' },
      response: new Response(null, { status: 200 }),
    } as never);
    const post = vi.spyOn(api, 'POST').mockResolvedValue({
      data: { id: 'r1', creadaEn: '2026-07-25T00:00:00Z' },
      response: new Response(null, { status: 201 }),
    } as never);

    await obtenerEstadoResena('o1');
    await crearResena('o1', 5, 'Cumplió con todo lo acordado.');

    expect(get).toHaveBeenCalledWith('/api/reputacion/ordenes/{orderId}', {
      params: { path: { orderId: 'o1' } },
    });
    expect(post).toHaveBeenCalledWith('/api/reputacion/ordenes/{orderId}/resenas', {
      params: { path: { orderId: 'o1' } },
      body: { puntuacion: 5, comentario: 'Cumplió con todo lo acordado.' },
    });
  });

  it('normaliza números del perfil y las reseñas públicas', async () => {
    vi.spyOn(api, 'GET')
      .mockResolvedValueOnce({
        data: {
          id: 'u1',
          nombre: 'Ana',
          ciudad: 'La Paz',
          verificado: true,
          promedio: '4.5',
          totalResenas: '2',
        },
        response: new Response(null, { status: 200 }),
      } as never)
      .mockResolvedValueOnce({
        data: {
          items: [
            {
              puntuacion: '5',
              comentario: 'Excelente contraparte.',
              creadaEn: '2026-07-25T00:00:00Z',
              rolAutor: 'comprador',
            },
          ],
          pagina: '1',
          tamano: '10',
          total: '1',
        },
        response: new Response(null, { status: 200 }),
      } as never);

    const perfil = await obtenerPerfilPublico('u1');
    const resenas = await listarResenasPublicas('u1', 1, 10);

    expect(perfil.promedio).toBe(4.5);
    expect(perfil.totalResenas).toBe(2);
    expect(resenas.items[0].puntuacion).toBe(5);
    expect(resenas.total).toBe(1);
  });
});
