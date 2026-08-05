import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { FlujoCapturaKyc } from './FlujoCapturaKyc';

vi.mock('./CapturadorGuiado', () => ({
  CapturadorGuiado: ({ tipo, onConfirmar }: { tipo: string; onConfirmar(f: File): void }) => (
    <button onClick={() => onConfirmar(new File(['x'], `${tipo}.jpg`, { type: 'image/jpeg' }))}>
      Confirmar {tipo}
    </button>
  ),
}));

describe('FlujoCapturaKyc', () => {
  it('exige confirmar el CI antes de habilitar la selfie', () => {
    const onDocumento = vi.fn();
    const onSelfie = vi.fn();
    const { rerender } = render(
      <FlujoCapturaKyc
        documento={null}
        selfie={null}
        onDocumento={onDocumento}
        onSelfie={onSelfie}
      />,
    );

    expect(screen.getByRole('button', { name: /tomar selfie/i })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: /tomar foto del ci/i }));
    fireEvent.click(screen.getByRole('button', { name: /confirmar documento/i }));
    expect(onDocumento).toHaveBeenCalledWith(expect.objectContaining({ type: 'image/jpeg' }));
    expect(onSelfie).toHaveBeenCalledWith(null);

    rerender(
      <FlujoCapturaKyc
        documento={onDocumento.mock.calls[0][0]}
        selfie={null}
        onDocumento={onDocumento}
        onSelfie={onSelfie}
      />,
    );
    expect(screen.getByRole('button', { name: /tomar selfie/i })).toBeEnabled();
  });
});
