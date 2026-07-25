import { api, desempaquetar } from './http';
import type { components } from './schema';

export type OrdenCreada = components['schemas']['OrdenCreadaDto'];
export type OrdenResumen = components['schemas']['OrdenResumenDto'];
export type OrdenDetalle = components['schemas']['OrdenDetalleDto'];
export type PaginaOrdenes = components['schemas']['ResultadoPaginadoOrdenes'];
export type RolOrden = 'comprador' | 'vendedor';
export type EstadoOrden = 'Requested' | 'Agreed' | 'Cancelled';

export async function solicitarOrden(avisoId: string): Promise<OrdenCreada> {
  return desempaquetar(await api.POST('/api/orders', { body: { avisoId } }));
}

export async function listarOrdenes(
  rol: RolOrden,
  estado?: EstadoOrden,
  pagina = 1,
  tamano = 20,
): Promise<PaginaOrdenes> {
  return desempaquetar(
    await api.GET('/api/orders', {
      params: { query: { rol, estado, pagina, tamano } },
    }),
  );
}

export async function obtenerOrden(ordenId: string): Promise<OrdenDetalle> {
  return desempaquetar(
    await api.GET('/api/orders/{id}', {
      params: { path: { id: ordenId } },
    }),
  );
}

export async function aceptarOrden(ordenId: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/orders/{id}/aceptar', {
      params: { path: { id: ordenId } },
    }),
  );
}

export async function cancelarOrden(ordenId: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/orders/{id}/cancelar', {
      params: { path: { id: ordenId } },
    }),
  );
}
