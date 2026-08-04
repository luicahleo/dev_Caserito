import type { components } from './schema';
import { api, desempaquetar } from './http';

export type Perfil = components['schemas']['PerfilDto'];

export async function obtenerPerfil(): Promise<Perfil> {
  return desempaquetar(await api.GET('/api/perfil'));
}

export async function actualizarPerfil(datos: {
  nombres: string; apellidos: string; ciudadId: string;
}): Promise<void> {
  desempaquetar(await api.PUT('/api/perfil', { body: datos }));
}
