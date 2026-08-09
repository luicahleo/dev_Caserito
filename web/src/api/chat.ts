import type { MensajeChat } from '../chat/sincronizacionMensajes';
import { api, desempaquetar } from './http';
import type { components } from './schema';

export type CrearReporteChatRequest = components['schemas']['ReportarChatRequest'];
type ConversacionDto = components['schemas']['ConversacionDto'];
type PaginaMensajesDto = components['schemas']['PaginaChatResponseOfMensajeDto'];
type ConversacionResumenDto = components['schemas']['ConversacionResumenDto'];
type PaginaConversacionesDto = components['schemas']['PaginaChatResponseOfConversacionResumenDto'];
export type ConversacionResumen = Omit<
  ConversacionResumenDto,
  | 'ultimaSecuencia'
  | 'noLeidos'
  | 'ultimaSecuenciaEntregadaContraparte'
  | 'ultimaSecuenciaLeidaContraparte'
> & {
  ultimaSecuencia: number;
  noLeidos: number;
  ultimaSecuenciaEntregadaContraparte?: number;
  ultimaSecuenciaLeidaContraparte?: number;
};
export type PaginaConversaciones = Omit<PaginaConversacionesDto, 'items'> & {
  items: ConversacionResumen[];
};
export type Conversacion = Omit<
  ConversacionDto,
  'ultimaSecuencia' | 'ultimaSecuenciaEntregadaContraparte' | 'ultimaSecuenciaLeidaContraparte'
> & {
  ultimaSecuencia: number;
  ultimaSecuenciaEntregadaContraparte: number;
  ultimaSecuenciaLeidaContraparte: number;
};
export type PaginaMensajes = Omit<PaginaMensajesDto, 'items'> & {
  items: MensajeChat[];
};

function convertirConversacion(conversacion: ConversacionDto): Conversacion {
  return {
    ...conversacion,
    ultimaSecuencia: Number(conversacion.ultimaSecuencia),
    ultimaSecuenciaEntregadaContraparte: Number(conversacion.ultimaSecuenciaEntregadaContraparte),
    ultimaSecuenciaLeidaContraparte: Number(conversacion.ultimaSecuenciaLeidaContraparte),
  };
}

export async function iniciarConversacion(avisoId: string): Promise<Conversacion> {
  return convertirConversacion(
    desempaquetar(await api.POST('/api/chat/conversaciones', { body: { avisoId } })),
  );
}

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
      ultimaSecuenciaEntregadaContraparte: Number(conversacion.ultimaSecuenciaEntregadaContraparte),
      ultimaSecuenciaLeidaContraparte: Number(conversacion.ultimaSecuenciaLeidaContraparte),
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

export async function buscarConversacionPropia(
  conversacionId: string,
): Promise<ConversacionResumen | undefined> {
  let cursor: string | undefined;
  const cursoresVisitados = new Set<string>();
  do {
    const pagina = await listarConversaciones(cursor, 50);
    const encontrada = pagina.items.find((conversacion) => conversacion.id === conversacionId);
    if (encontrada) return encontrada;
    cursor = pagina.siguienteCursor ?? undefined;
    if (cursor && cursoresVisitados.has(cursor)) return undefined;
    if (cursor) cursoresVisitados.add(cursor);
  } while (cursor);
  return undefined;
}

export async function obtenerMensajes(
  conversacionId: string,
  cursor?: string,
  limite = 50,
): Promise<PaginaMensajes> {
  const pagina = desempaquetar(
    await api.GET('/api/chat/conversaciones/{id}/mensajes', {
      params: { path: { id: conversacionId }, query: { cursor, limite } },
    }),
  );
  return {
    ...pagina,
    items: pagina.items.map((mensaje) => ({ ...mensaje, secuencia: Number(mensaje.secuencia) })),
  };
}

export async function enviarMensaje(
  conversacionId: string,
  claveIdempotencia: string,
  texto: string,
): Promise<MensajeChat> {
  const mensaje = desempaquetar(
    await api.POST('/api/chat/conversaciones/{id}/mensajes', {
      params: { path: { id: conversacionId } },
      body: { claveIdempotencia, texto },
    }),
  );
  return { ...mensaje, secuencia: Number(mensaje.secuencia) };
}

export async function marcarLectura(conversacionId: string, hastaSecuencia: number): Promise<void> {
  desempaquetar(
    await api.PUT('/api/chat/conversaciones/{id}/lectura', {
      params: { path: { id: conversacionId } },
      body: { hastaSecuencia },
    }),
  );
}

export async function marcarEntrega(conversacionId: string, hastaSecuencia: number): Promise<void> {
  desempaquetar(
    await api.PUT('/api/chat/conversaciones/{id}/entrega', {
      params: { path: { id: conversacionId } },
      body: { hastaSecuencia },
    }),
  );
}

export async function contarMensajesNoLeidos(): Promise<number> {
  const resultado = desempaquetar(await api.GET('/api/chat/no-leidos'));
  return Number(resultado.cantidad);
}
