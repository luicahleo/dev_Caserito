import type { components } from './schema';
import { api, desempaquetar } from './http';

export type BusquedaGuardada = components['schemas']['BusquedaGuardadaDto'];
export type CrearBusquedaGuardadaRequest = components['schemas']['CrearBusquedaGuardadaRequest'];

export async function listarBusquedasGuardadas(): Promise<BusquedaGuardada[]> {
  return desempaquetar(await api.GET('/api/busquedas-guardadas'));
}

export async function crearBusquedaGuardada(
  request: CrearBusquedaGuardadaRequest,
): Promise<string> {
  return desempaquetar(await api.POST('/api/busquedas-guardadas', { body: request }));
}

export async function eliminarBusquedaGuardada(id: string): Promise<void> {
  desempaquetar(await api.DELETE('/api/busquedas-guardadas/{id}', { params: { path: { id } } }));
}
