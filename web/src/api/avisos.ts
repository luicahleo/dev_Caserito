import type { components } from './schema';
import { api, desempaquetar } from './http';
import { getAccessToken } from '../auth/session';

export type FotoAvisoDto = components['schemas']['FotoAvisoDto'];
export type AvisoPublicoResumen = components['schemas']['AvisoPublicoResumenDto'];
export type AvisoPublico = components['schemas']['AvisoPublicoDto'];
export type AvisoResumen = components['schemas']['AvisoResumenDto'];
export type Aviso = components['schemas']['AvisoDto'];
export type CrearAvisoRequest = components['schemas']['CrearAvisoRequest'];
export type EditarAvisoRequest = components['schemas']['EditarAvisoRequest'];
export type PaginaAvisosPublicos =
  components['schemas']['ResultadoPaginadoOfAvisoPublicoResumenDto'];
export type PaginaMisAvisos = components['schemas']['ResultadoPaginadoOfAvisoResumenDto'];

// Criterios de búsqueda pública; todos opcionales. Se pasan como query a /api/publico/avisos.
export interface FiltroBusqueda {
  q?: string;
  categoriaId?: string;
  ciudadId?: string;
  precioMin?: number;
  precioMax?: number;
  condicion?: string;
}

export async function buscarAvisos(
  filtro: FiltroBusqueda,
  pagina: number,
  tamano: number,
): Promise<PaginaAvisosPublicos> {
  return desempaquetar(
    await api.GET('/api/publico/avisos', {
      params: { query: { ...filtro, pagina, tamano } },
    }),
  );
}

export async function obtenerAvisoPublico(id: string): Promise<AvisoPublico> {
  return desempaquetar(await api.GET('/api/publico/avisos/{id}', { params: { path: { id } } }));
}

export async function listarMisAvisos(pagina: number, tamano: number): Promise<PaginaMisAvisos> {
  return desempaquetar(
    await api.GET('/api/avisos/mios', { params: { query: { pagina, tamano } } }),
  );
}

export async function obtenerMiAviso(id: string): Promise<Aviso> {
  return desempaquetar(await api.GET('/api/avisos/mios/{id}', { params: { path: { id } } }));
}

export async function crearAviso(req: CrearAvisoRequest): Promise<{ id: string }> {
  return desempaquetar(await api.POST('/api/avisos', { body: req }));
}

export async function editarAviso(id: string, req: EditarAvisoRequest): Promise<void> {
  desempaquetar(await api.PUT('/api/avisos/{id}', { params: { path: { id } }, body: req }));
}

export async function pausarAviso(id: string): Promise<void> {
  desempaquetar(await api.POST('/api/avisos/{id}/pausar', { params: { path: { id } } }));
}

export async function reactivarAviso(id: string): Promise<void> {
  desempaquetar(await api.POST('/api/avisos/{id}/reactivar', { params: { path: { id } } }));
}

export async function eliminarAviso(id: string): Promise<void> {
  desempaquetar(await api.DELETE('/api/avisos/{id}', { params: { path: { id } } }));
}

/**
 * Sube una foto al aviso. Devuelve el id de la FotoAviso creada.
 * Usa fetch con FormData (multipart); no pasa por el wrapper openapi-fetch.
 */
export async function subirFotoAviso(avisoId: string, archivo: File): Promise<{ id: string }> {
  const form = new FormData();
  form.append('file', archivo);

  const resp = await fetch(`/api/avisos/${avisoId}/fotos`, {
    method: 'POST',
    body: form,
    headers: {
      Authorization: `Bearer ${getAccessToken() ?? ''}`,
    },
  });

  if (!resp.ok) {
    const texto = await resp.text().catch(() => '');
    throw new Error(`Error al subir foto: ${resp.status} ${texto}`);
  }

  return resp.json() as Promise<{ id: string }>;
}

/**
 * Borra una foto del aviso.
 */
export async function borrarFotoAviso(avisoId: string, fotoId: string): Promise<void> {
  const resp = await fetch(`/api/avisos/${avisoId}/fotos/${fotoId}`, {
    method: 'DELETE',
    headers: {
      Authorization: `Bearer ${getAccessToken() ?? ''}`,
    },
  });

  if (!resp.ok) {
    throw new Error(`Error al borrar foto: ${resp.status}`);
  }
}
