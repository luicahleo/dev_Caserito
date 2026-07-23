import { describe, expect, it, vi } from 'vitest';
import { crearClienteTiempoReal, type ConexionSignalR } from './tiempoReal';

class ConexionFake implements ConexionSignalR {
  invocaciones: Array<[string, string]> = [];
  reconectado: (() => Promise<void>) | null = null;
  handlers = new Map<string, (payload: unknown) => void>();

  async start() {}
  async stop() {}
  async invoke(metodo: string, id: string) {
    this.invocaciones.push([metodo, id]);
  }
  on(evento: string, handler: (payload: unknown) => void) {
    this.handlers.set(evento, handler);
  }
  off(evento: string) {
    this.handlers.delete(evento);
  }
  onreconnected(handler: () => Promise<void>) {
    this.reconectado = handler;
  }
}

describe('cliente de tiempo real de chat', () => {
  it('obtiene el token bajo demanda sin persistirlo', async () => {
    const getAccessToken = vi.fn(() => 'token-vigente');
    const conexion = new ConexionFake();
    const cliente = crearClienteTiempoReal({ getAccessToken, crearConexion: () => conexion });

    await cliente.conectar();

    expect(getAccessToken).toHaveBeenCalled();
    expect(localStorage.length).toBe(0);
  });

  it('registra suscripciones, desuscribe y al reconectar resuscribe y recupera', async () => {
    const conexion = new ConexionFake();
    const recuperar = vi.fn(async () => undefined);
    const cliente = crearClienteTiempoReal({
      getAccessToken: () => 'token',
      crearConexion: () => conexion,
      alReconectar: recuperar,
    });
    await cliente.suscribir('conversacion-1');
    await cliente.suscribir('conversacion-2');
    await cliente.desuscribir('conversacion-2');

    await conexion.reconectado?.();

    expect(conexion.invocaciones).toContainEqual(['SuscribirConversacion', 'conversacion-1']);
    expect(conexion.invocaciones).toContainEqual(['DesuscribirConversacion', 'conversacion-2']);
    expect(recuperar).toHaveBeenCalledWith('conversacion-1');
    expect(recuperar).not.toHaveBeenCalledWith('conversacion-2');
  });

  it('exige una suscripción explícita nueva después de una revocación', async () => {
    const conexion = new ConexionFake();
    const cliente = crearClienteTiempoReal({
      getAccessToken: () => 'token',
      crearConexion: () => conexion,
    });
    await cliente.suscribir('conversacion-1');

    cliente.revocar('conversacion-1');
    conexion.invocaciones = [];
    await conexion.reconectado?.();

    expect(conexion.invocaciones).toEqual([]);

    await cliente.suscribir('conversacion-1');

    expect(conexion.invocaciones).toEqual([['SuscribirConversacion', 'conversacion-1']]);
  });
});
