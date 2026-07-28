import type { components } from './schema';
import { api, desempaquetar } from './http';

export type PuntoEncuentroSeguro = components['schemas']['PuntoEncuentroSeguroDto'];

export async function listarPuntosEncuentro(ciudad: string): Promise<PuntoEncuentroSeguro[]> {
  return desempaquetar(
    await api.GET('/api/publico/puntos-encuentro', {
      params: { query: { ciudad } },
    }),
  );
}
