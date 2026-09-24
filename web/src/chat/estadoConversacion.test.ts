import { describe, expect, it } from 'vitest';
import { EstadoConversacion, esRetenida, etiquetaEstado } from './estadoConversacion';

describe('EstadoConversacion', () => {
  // El contrato expone el estado como número. Estos valores replican
  // CaseritoApp.Chat.Domain.Conversaciones.EstadoConversacion: si alguien
  // reordena el enum del backend, este test falla en lugar de dejar que la UI
  // muestre el estado equivocado en silencio.
  it('fija los valores numéricos del enum del backend', () => {
    expect(EstadoConversacion.Activa).toBe(0);
    expect(EstadoConversacion.Cerrada).toBe(1);
    expect(EstadoConversacion.CerradaPorModeracion).toBe(2);
    expect(EstadoConversacion.RetenidaPorVerificacion).toBe(3);
  });

  it('reconoce solo el estado retenido', () => {
    expect(esRetenida(EstadoConversacion.RetenidaPorVerificacion)).toBe(true);
    expect(esRetenida(EstadoConversacion.Activa)).toBe(false);
    expect(esRetenida(EstadoConversacion.Cerrada)).toBe(false);
    expect(esRetenida(EstadoConversacion.CerradaPorModeracion)).toBe(false);
  });

  it('etiqueta cada estado', () => {
    expect(etiquetaEstado({ estado: 2, puedeEnviar: false })).toBe('Cerrada por moderación');
    expect(etiquetaEstado({ estado: 1, puedeEnviar: false })).toBe('Cerrada');
    expect(etiquetaEstado({ estado: 3, puedeEnviar: true })).toBe('En espera de verificación');
    expect(etiquetaEstado({ estado: 0, puedeEnviar: false })).toBe('Envío no disponible');
    expect(etiquetaEstado({ estado: 0, puedeEnviar: true })).toBe('Activa');
  });
});
