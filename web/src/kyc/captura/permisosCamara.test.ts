import { describe, expect, it, vi } from 'vitest';
import { solicitarPermisoCamara } from './permisosCamara';

describe('solicitarPermisoCamara', () => {
  it('no vuelve a solicitar un permiso concedido', async () => {
    const solicitar = vi.fn();
    const estado = await solicitarPermisoCamara({
      checkPermissions: vi.fn().mockResolvedValue({ camera: 'granted' }),
      requestPermissions: solicitar,
    });

    expect(estado).toBe('concedido');
    expect(solicitar).not.toHaveBeenCalled();
  });

  it.each([
    ['granted', 'concedido'],
    ['denied', 'denegado'],
    ['prompt-with-rationale', 'solicitarEnAjustes'],
  ] as const)('normaliza el resultado %s', async (permiso, esperado) => {
    const estado = await solicitarPermisoCamara({
      checkPermissions: vi.fn().mockResolvedValue({ camera: 'prompt' }),
      requestPermissions: vi.fn().mockResolvedValue({ camera: permiso }),
    });

    expect(estado).toBe(esperado);
  });
});
