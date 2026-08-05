import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { inicializarInstalacionPwa, useInstalacionPwa } from './usarInstalacionPwa';

describe('useInstalacionPwa', () => {
  it('expone el prompt del navegador y lo consume al aceptar', async () => {
    inicializarInstalacionPwa();
    const prompt = vi.fn().mockResolvedValue(undefined);
    const evento = new Event('beforeinstallprompt', { cancelable: true });
    Object.assign(evento, {
      prompt,
      userChoice: Promise.resolve({ outcome: 'accepted' }),
    });
    window.dispatchEvent(evento);
    expect(evento.defaultPrevented).toBe(false);

    const { result } = renderHook(() => useInstalacionPwa());
    expect(result.current.puedeInstalar).toBe(true);
    await act(() => result.current.instalar());
    expect(prompt).toHaveBeenCalledOnce();
    expect(result.current.puedeInstalar).toBe(false);
  });
});
