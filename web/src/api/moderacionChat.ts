import { api, desempaquetar } from './http';
import type { components, paths } from './schema';

export type ReporteChatCola = components['schemas']['ReporteChatColaDto'];
export type FiltrosColaChat =
  paths['/api/admin/moderacion/chat/reportes']['get']['parameters']['query'];
type EvidenciaReporteChatDto = components['schemas']['EvidenciaReporteChatDto'];
type MensajeEvidenciaChatDto = components['schemas']['MensajeEvidenciaChatDto'];
export type MensajeEvidenciaChat = Omit<MensajeEvidenciaChatDto, 'secuencia'> & {
  secuencia: number;
};
export type EvidenciaReporteChat = Omit<EvidenciaReporteChatDto, 'mensajes'> & {
  mensajes: MensajeEvidenciaChat[];
};
export type AtenderReporteChatRequest = components['schemas']['AtenderReporteChatRequest'];

export async function listarReportesChat(filtros: FiltrosColaChat): Promise<ReporteChatCola[]> {
  return desempaquetar(
    await api.GET('/api/admin/moderacion/chat/reportes', {
      params: { query: filtros },
    }),
  );
}

export async function obtenerEvidenciaReporteChat(
  reporteId: string,
): Promise<EvidenciaReporteChat> {
  const evidencia = desempaquetar(
    await api.GET('/api/admin/moderacion/chat/reportes/{id}/evidencia', {
      params: { path: { id: reporteId } },
    }),
  );
  return {
    ...evidencia,
    mensajes: evidencia.mensajes.map((mensaje) => ({
      ...mensaje,
      secuencia: Number(mensaje.secuencia),
    })),
  };
}

export async function tomarReporteChat(reporteId: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/moderacion/chat/reportes/{id}/tomar', {
      params: { path: { id: reporteId } },
    }),
  );
}

export async function liberarReporteChat(reporteId: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/moderacion/chat/reportes/{id}/liberar', {
      params: { path: { id: reporteId } },
    }),
  );
}

export async function atenderReporteChat(
  reporteId: string,
  request: AtenderReporteChatRequest,
): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/moderacion/chat/reportes/{id}/atender', {
      params: { path: { id: reporteId } },
      body: request,
    }),
  );
}

export async function descartarReporteChat(reporteId: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/moderacion/chat/reportes/{id}/descartar', {
      params: { path: { id: reporteId } },
    }),
  );
}

export async function cerrarConversacionModeracion(reporteId: string): Promise<void> {
  desempaquetar(
    await api.PUT('/api/admin/moderacion/chat/reportes/{id}/cierre-conversacion', {
      params: { path: { id: reporteId } },
    }),
  );
}

export async function reabrirConversacionModeracion(reporteId: string): Promise<void> {
  desempaquetar(
    await api.DELETE('/api/admin/moderacion/chat/reportes/{id}/cierre-conversacion', {
      params: { path: { id: reporteId } },
    }),
  );
}
