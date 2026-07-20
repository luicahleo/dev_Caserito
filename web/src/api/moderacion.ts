import type { components } from './schema';
import { api, desempaquetar } from './http';

export type EstadoReporte = 'Pendiente' | 'Atendido' | 'Descartado';
export type AvisoReportado = components['schemas']['AvisoReportadoDto'];
export type PaginaAvisosReportados =
  components['schemas']['ResultadoPaginadoOfAvisoReportadoResumenDto'];

export async function listarAvisosReportados(
  estado: EstadoReporte,
): Promise<PaginaAvisosReportados> {
  return desempaquetar(
    await api.GET('/api/admin/moderacion/avisos', {
      params: { query: { estado, pagina: 1, tamano: 50 } },
    }),
  );
}

export async function obtenerAvisoReportado(
  id: string,
  estado: EstadoReporte,
): Promise<AvisoReportado> {
  return desempaquetar(
    await api.GET('/api/admin/moderacion/avisos/{id}', {
      params: { path: { id }, query: { estado } },
    }),
  );
}

export async function ocultarAviso(id: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/moderacion/avisos/{id}/ocultar', { params: { path: { id } } }),
  );
}
export async function restaurarAviso(id: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/moderacion/avisos/{id}/restaurar', { params: { path: { id } } }),
  );
}
export async function eliminarAvisoModeracion(id: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/moderacion/avisos/{id}/eliminar', { params: { path: { id } } }),
  );
}
export async function descartarReporte(id: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/moderacion/reportes/{id}/descartar', { params: { path: { id } } }),
  );
}
