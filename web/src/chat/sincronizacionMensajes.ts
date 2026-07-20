import type { components } from '../api/schema';

type MensajeApi = components['schemas']['MensajeDto'];

export interface MensajeChat extends Omit<MensajeApi, 'secuencia'> {
  secuencia: number;
}

export class SincronizadorMensajes {
  readonly #porId = new Map<string, MensajeChat>();
  readonly #recuperar: (despuesDeSecuencia: number) => Promise<MensajeChat[]>;
  #recuperacion: Promise<void> | null = null;

  constructor(recuperar: (despuesDeSecuencia: number) => Promise<MensajeChat[]>) {
    this.#recuperar = recuperar;
  }

  get mensajes(): readonly MensajeChat[] {
    return [...this.#porId.values()].sort((a, b) => a.secuencia - b.secuencia);
  }

  async aplicar(nuevos: readonly MensajeChat[]): Promise<void> {
    this.#combinar(nuevos);
    const ultimaContigua = this.#ultimaSecuenciaContigua();
    const hayHueco = this.mensajes.some((mensaje) => mensaje.secuencia > ultimaContigua + 1);
    if (!hayHueco) return;

    if (!this.#recuperacion) {
      this.#recuperacion = this.#recuperar(ultimaContigua)
        .then((recuperados) => this.#combinar(recuperados))
        .finally(() => {
          this.#recuperacion = null;
        });
    }
    await this.#recuperacion;
  }

  #combinar(nuevos: readonly MensajeChat[]): void {
    for (const mensaje of nuevos) {
      if (!this.#porId.has(mensaje.id)) this.#porId.set(mensaje.id, mensaje);
    }
  }

  #ultimaSecuenciaContigua(): number {
    let ultima = 0;
    for (const mensaje of this.mensajes) {
      if (mensaje.secuencia === ultima + 1) ultima = mensaje.secuencia;
      else if (mensaje.secuencia > ultima + 1) break;
    }
    return ultima;
  }
}
