import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { CapturadorGuiado } from './CapturadorGuiado';

const abrir = vi.fn();
vi.mock('./usarCamara', () => ({
  useCamara: () => ({
    estado: 'inactiva',
    stream: null,
    error: null,
    abrir,
    cerrar: vi.fn(),
  }),
}));

describe('CapturadorGuiado', () => {
  it('abre la lente trasera solo después de una acción explícita', () => {
    render(<CapturadorGuiado tipo="documento" onConfirmar={vi.fn()} onCancelar={vi.fn()} />);
    expect(abrir).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: /abrir cámara/i }));
    expect(abrir).toHaveBeenCalledWith('trasera');
    expect(screen.getByText(/frontal completo del CI/i)).toBeInTheDocument();
  });
});
