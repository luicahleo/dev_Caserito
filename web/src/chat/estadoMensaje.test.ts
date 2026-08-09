import { describe, expect, it } from 'vitest';
import { obtenerEstadoMensaje } from './estadoMensaje';

describe('obtenerEstadoMensaje', () => {
  it('deriva enviado, entregado y leído con prioridad monotónica', () => {
    expect(obtenerEstadoMensaje(3, 2, 1)).toBe('enviado');
    expect(obtenerEstadoMensaje(3, 3, 1)).toBe('entregado');
    expect(obtenerEstadoMensaje(3, 4, 3)).toBe('leido');
  });
});
