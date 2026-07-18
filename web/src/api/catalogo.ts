import type { components } from './schema';
import { api, desempaquetar } from './http';

export type Categoria = components['schemas']['CategoriaDto'];
export type Ciudad = components['schemas']['CiudadDto'];

export async function listarCategorias(): Promise<Categoria[]> {
  return desempaquetar(await api.GET('/api/catalogo/categorias'));
}

export async function listarCiudades(): Promise<Ciudad[]> {
  return desempaquetar(await api.GET('/api/catalogo/ciudades'));
}
