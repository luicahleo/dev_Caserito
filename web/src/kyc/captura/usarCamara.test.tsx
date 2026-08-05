import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useCamara } from './usarCamara';

function streamFalso() {
  const stop = vi.fn();
  return {
    stream: { getTracks: () => [{ stop }] } as unknown as MediaStream,
    stop,
  };
}

describe('useCamara', () => {
  it('abre vídeo sin audio tras obtener permiso y lo detiene al cerrar', async () => {
    const falso = streamFalso();
    const obtenerMedios = vi.fn().mockResolvedValue(falso.stream);
    const { result } = renderHook(() =>
      useCamara({
        obtenerMedios,
      }),
    );

    await act(() => result.current.abrir('trasera'));
    expect(obtenerMedios).toHaveBeenCalledWith(
      expect.objectContaining({ audio: false, video: expect.any(Object) }),
    );
    expect(result.current.estado).toBe('activa');

    act(() => result.current.cerrar());
    expect(falso.stop).toHaveBeenCalledOnce();
    expect(result.current.estado).toBe('inactiva');
  });

  it('normaliza la denegación producida por getUserMedia', async () => {
    const obtenerMedios = vi
      .fn()
      .mockRejectedValue(new DOMException('detalle del dispositivo', 'NotAllowedError'));
    const { result } = renderHook(() =>
      useCamara({
        obtenerMedios,
      }),
    );

    await act(() => result.current.abrir('frontal'));
    expect(obtenerMedios).toHaveBeenCalledOnce();
    expect(result.current.error).toEqual({ tipo: 'permisoDenegado' });
  });
});
