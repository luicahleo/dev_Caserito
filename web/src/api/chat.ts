import type { MensajeChat } from '../chat/sincronizacionMensajes';
import { api, desempaquetar } from './http';
import type { components } from './schema';

export type CrearReporteChatRequest = components['schemas']['ReportarChatRequest'];
type ConversacionResumenDto = components['schemas']['ConversacionResumenDto'];
type PaginaConversacionesDto = components['schemas']['PaginaChatResponseOfConversacionResumenDto'];
export type ConversacionResumen = Omit<ConversacionResumenDto, 'ultimaSecuencia' | 'noLeidos'> & {
  ultimaSecuencia: number;
  noLeidos: number;
};
export type PaginaConversaciones = Omit<PaginaConversacionesDto, 'items'> & {
  items: ConversacionResumen[];
};

export async function listarConversaciones(
  cursor?: string,
  limite = 20,
): Promise<PaginaConversaciones> {
  const pagina = desempaquetar(
    await api.GET('/api/chat/conversaciones', {
      params: { query: { cursor, limite } },
    }),
  );
  return {
    ...pagina,
    items: pagina.items.map((conversacion) => ({
      ...conversacion,
      ultimaSecuencia: Number(conversacion.ultimaSecuencia),
      noLeidos: Number(conversacion.noLeidos),
    })),
  };
}

export async function cerrarConversacion(conversacionId: string): Promise<void> {
  desempaquetar(
    await api.PUT('/api/chat/conversaciones/{id}/cierre', {
      params: { path: { id: conversacionId } },
    }),
  );
}

export async function reabrirConversacion(conversacionId: string): Promise<void> {
  desempaquetar(
    await api.DELETE('/api/chat/conversaciones/{id}/cierre', {
      params: { path: { id: conversacionId } },
    }),
  );
}

export async function bloquearParticipante(conversacionId: string): Promise<void> {
  desempaquetar(
    await api.PUT('/api/chat/conversaciones/{id}/bloqueo', {
      params: { path: { id: conversacionId } },
    }),
  );
}

export async function desbloquearParticipante(conversacionId: string): Promise<void> {
  desempaquetar(
    await api.DELETE('/api/chat/conversaciones/{id}/bloqueo', {
      params: { path: { id: conversacionId } },
    }),
  );
}

export async function crearReporteChat(
  conversacionId: string,
  reporte: CrearReporteChatRequest,
): Promise<void> {
  desempaquetar(
    await api.POST('/api/chat/conversaciones/{id}/reportes', {
      params: { path: { id: conversacionId } },
      body: reporte,
    }),
  );
}

export async function recuperarMensajes(
  conversacionId: string,
  despuesDeSecuencia: number,
  limite = 50,
): Promise<MensajeChat[]> {
  const pagina = desempaquetar(
    await api.GET('/api/chat/conversaciones/{id}/mensajes', {
      params: {
        path: { id: conversacionId },
        query: { despuesDeSecuencia, limite },
      },
    }),
  );
  return pagina.items.map((mensaje) => ({
    ...mensaje,
    secuencia: Number(mensaje.secuencia),
  }));
}
