import type { components } from './schema';
import { api, desempaquetar } from './http';

export type Notificacion = components['schemas']['NotificacionDto'];
export type PaginaNotificaciones = components['schemas']['PaginaNotificacionesDto'];

export async function listarNotificaciones(
  soloNoLeidas = false,
  pagina = 1,
  tamano = 20,
): Promise<PaginaNotificaciones> {
  return desempaquetar(
    await api.GET('/api/notificaciones', {
      params: { query: { soloNoLeidas, pagina, tamano } },
    }),
  );
}

export async function contarNoLeidas(): Promise<number> {
  const resultado = desempaquetar(await api.GET('/api/notificaciones/no-leidas'));
  return typeof resultado === 'string' ? Number(resultado) : resultado;
}

export async function marcarLeida(id: string): Promise<void> {
  desempaquetar(
    await api.PATCH('/api/notificaciones/{id}/leida', { params: { path: { id } } }),
  );
}

export async function marcarTodasLeidas(): Promise<number> {
  const resultado = desempaquetar(await api.PATCH('/api/notificaciones/marcar-todas-leidas'));
  return typeof resultado === 'string' ? Number(resultado) : resultado;
}
