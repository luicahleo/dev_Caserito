import { describe, expect, it, vi } from 'vitest';
import { SincronizadorMensajes, type MensajeChat } from './sincronizacionMensajes';

const mensaje = (id: string, secuencia: number): MensajeChat => ({
  id,
  conversacionId: 'conversacion',
  remitenteId: 'usuario',
  secuencia,
  texto: `Mensaje ${secuencia}`,
  enviadoEn: '2026-07-20T12:00:00Z',
});

describe('sincronización de mensajes', () => {
  it('deduplica por id y ordena por secuencia', async () => {
    const sincronizador = new SincronizadorMensajes(async () => []);

    await sincronizador.aplicar([mensaje('dos', 2), mensaje('uno', 1), mensaje('uno', 1)]);

    expect(sincronizador.mensajes.map((x) => x.secuencia)).toEqual([1, 2]);
  });

  it('un hueco coordina una sola recuperación', async () => {
    let resolver: ((mensajes: MensajeChat[]) => void) | undefined;
    const recuperar = vi.fn(() => new Promise<MensajeChat[]>((resolve) => (resolver = resolve)));
    const sincronizador = new SincronizadorMensajes(recuperar);
    await sincronizador.aplicar([mensaje('uno', 1)]);

    const primera = sincronizador.aplicar([mensaje('tres', 3)]);
    const segunda = sincronizador.aplicar([mensaje('cuatro', 4)]);
    resolver?.([mensaje('dos', 2)]);
    await Promise.all([primera, segunda]);

    expect(recuperar).toHaveBeenCalledTimes(1);
    expect(recuperar).toHaveBeenCalledWith(1);
    expect(sincronizador.mensajes.map((x) => x.secuencia)).toEqual([1, 2, 3, 4]);
  });
});
