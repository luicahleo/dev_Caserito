import type { components } from './schema';
import { api, desempaquetar } from './http';

export type EstadoResenaOrden = components['schemas']['EstadoResenaOrdenDto'];
export type ResenaCreada = components['schemas']['ResenaCreadaDto'];
type PerfilGenerado = components['schemas']['PerfilPublicoConReputacionDto'];
type ResenaGenerada = components['schemas']['ResenaPublicaDto'];

export type PerfilPublico = Omit<PerfilGenerado, 'promedio' | 'totalResenas'> & {
  promedio: number | null;
  totalResenas: number;
};

export type ResenaPublica = Omit<ResenaGenerada, 'puntuacion'> & {
  puntuacion: number;
};

export interface PaginaResenasPublicas {
  items: ResenaPublica[];
  pagina: number;
  tamano: number;
  total: number;
}

export async function obtenerEstadoResena(orderId: string): Promise<EstadoResenaOrden> {
  return desempaquetar(
    await api.GET('/api/reputacion/ordenes/{orderId}', {
      params: { path: { orderId } },
    }),
  );
}

export async function crearResena(
  orderId: string,
  puntuacion: number,
  comentario: string,
): Promise<ResenaCreada> {
  return desempaquetar(
    await api.POST('/api/reputacion/ordenes/{orderId}/resenas', {
      params: { path: { orderId } },
      body: { puntuacion, comentario },
    }),
  );
}

export async function obtenerPerfilPublico(id: string): Promise<PerfilPublico> {
  const perfil = desempaquetar(
    await api.GET('/api/publico/usuarios/{id}', {
      params: { path: { id } },
    }),
  );
  return {
    ...perfil,
    promedio: perfil.promedio === null ? null : Number(perfil.promedio),
    totalResenas: Number(perfil.totalResenas),
  };
}

export async function listarResenasPublicas(
  id: string,
  pagina = 1,
  tamano = 10,
): Promise<PaginaResenasPublicas> {
  const resultado = desempaquetar(
    await api.GET('/api/publico/usuarios/{id}/resenas', {
      params: { path: { id }, query: { pagina, tamano } },
    }),
  );
  return {
    items: resultado.items.map((resena) => ({
      ...resena,
      puntuacion: Number(resena.puntuacion),
    })),
    pagina: Number(resultado.pagina),
    tamano: Number(resultado.tamano),
    total: Number(resultado.total),
  };
}
