import {
  HubConnectionBuilder,
  HubConnectionState,
  type IRetryPolicy,
  type RetryContext,
} from '@microsoft/signalr';
import type { MensajeChat } from './sincronizacionMensajes';

export interface ConexionSignalR {
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(metodo: string, id: string): Promise<void>;
  on(evento: string, handler: (payload: unknown) => void): void;
  off(evento: string): void;
  onreconnected(handler: () => Promise<void>): void;
}

interface OpcionesClienteTiempoReal {
  getAccessToken: () => string | null;
  alReconectar?: (conversacionId: string) => Promise<void>;
  crearConexion?: () => ConexionSignalR;
}

const errorGenerico = 'No fue posible completar la operación.';

class PoliticaReconexion implements IRetryPolicy {
  nextRetryDelayInMilliseconds(contexto: RetryContext): number | null {
    const base = [0, 1_000, 3_000, 8_000, 15_000][contexto.previousRetryCount];
    if (base === undefined) return null;
    return base === 0 ? 0 : Math.round(base * (0.8 + Math.random() * 0.4));
  }
}

function conexionOficial(getAccessToken: () => string | null): ConexionSignalR {
  const conexion = new HubConnectionBuilder()
    .withUrl('/hubs/chat', { accessTokenFactory: () => getAccessToken() ?? '' })
    .withAutomaticReconnect(new PoliticaReconexion())
    .build();
  return {
    start: () => conexion.start(),
    stop: () => conexion.stop(),
    invoke: (metodo, id) => conexion.invoke(metodo, id),
    on: (evento, handler) => conexion.on(evento, handler),
    off: (evento) => conexion.off(evento),
    onreconnected: (handler) => conexion.onreconnected(() => void handler()),
  };
}

export function crearClienteTiempoReal(opciones: OpcionesClienteTiempoReal) {
  const conexion = opciones.crearConexion?.() ?? conexionOficial(opciones.getAccessToken);
  const suscripciones = new Set<string>();
  let conectado = false;

  conexion.onreconnected(async () => {
    for (const id of suscripciones) {
      await conexion.invoke('SuscribirConversacion', id);
      await opciones.alReconectar?.(id);
    }
  });

  async function ejecutar(accion: () => Promise<void>): Promise<void> {
    try {
      await accion();
    } catch {
      throw new Error(errorGenerico);
    }
  }

  async function conectar(): Promise<void> {
    if (conectado) return;
    opciones.getAccessToken();
    await ejecutar(() => conexion.start());
    conectado = true;
  }

  return {
    conectar,
    async detener(): Promise<void> {
      if (conectado) await ejecutar(() => conexion.stop());
      conectado = false;
      suscripciones.clear();
      conexion.off('MensajeCreado');
    },
    async suscribir(conversacionId: string): Promise<void> {
      await conectar();
      await ejecutar(() => conexion.invoke('SuscribirConversacion', conversacionId));
      suscripciones.add(conversacionId);
    },
    async desuscribir(conversacionId: string): Promise<void> {
      if (conectado) {
        await ejecutar(() => conexion.invoke('DesuscribirConversacion', conversacionId));
      }
      suscripciones.delete(conversacionId);
    },
    revocar(conversacionId: string): void {
      suscripciones.delete(conversacionId);
    },
    alRecibirMensaje(handler: (mensaje: MensajeChat) => void): void {
      conexion.off('MensajeCreado');
      conexion.on('MensajeCreado', (payload) => handler(payload as MensajeChat));
    },
    estado: () => (conectado ? HubConnectionState.Connected : HubConnectionState.Disconnected),
  };
}
