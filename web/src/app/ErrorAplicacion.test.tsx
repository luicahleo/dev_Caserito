import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ErrorAplicacion } from './ErrorAplicacion';

const { obtenerError, reportarDiagnostico, crearErrorId } = vi.hoisted(() => ({
  obtenerError: vi.fn(),
  reportarDiagnostico: vi.fn(),
  crearErrorId: vi.fn(() => 'ERR-0123456789AB'),
}));

vi.mock('react-router-dom', async (importarOriginal) => ({
  ...(await importarOriginal<typeof import('react-router-dom')>()),
  useRouteError: obtenerError,
}));

vi.mock('../lib/diagnosticos', () => ({ crearErrorId, reportarDiagnostico }));

describe('ErrorAplicacion', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    obtenerError.mockReturnValue(new Error('Error de aplicación'));
    reportarDiagnostico.mockResolvedValue(undefined);
  });

  it('muestra acciones genéricas sin revelar el detalle interno', () => {
    render(<ErrorAplicacion />);

    expect(screen.getByRole('heading', { name: 'No pudimos mostrar esta página' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Intentar nuevamente' })).toBeVisible();
    expect(screen.getByRole('link', { name: 'Volver al inicio' })).toHaveAttribute('href', '/');
    expect(screen.getByText('ERR-0123456789AB')).toBeVisible();
    expect(screen.queryByText('Error de aplicación')).not.toBeInTheDocument();
    expect(reportarDiagnostico).toHaveBeenCalledWith(
      expect.objectContaining({
        errorId: 'ERR-0123456789AB',
        eventName: 'router.unexpected',
      }),
    );
  });

  it('copia únicamente el código de diagnóstico', async () => {
    const copiar = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: copiar },
    });

    render(<ErrorAplicacion />);
    fireEvent.click(screen.getByRole('button', { name: 'Copiar código de diagnóstico' }));

    await waitFor(() => expect(copiar).toHaveBeenCalledWith('ERR-0123456789AB'));
    expect(screen.getByText('Código copiado')).toBeVisible();
  });
});
