import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ErrorAplicacion } from './ErrorAplicacion';

const { obtenerError } = vi.hoisted(() => ({ obtenerError: vi.fn() }));

vi.mock('react-router-dom', async (importarOriginal) => ({
  ...(await importarOriginal<typeof import('react-router-dom')>()),
  useRouteError: obtenerError,
}));

describe('ErrorAplicacion', () => {
  beforeEach(() => {
    obtenerError.mockReturnValue(new Error('Error de aplicación'));
  });

  it('muestra acciones genéricas sin revelar el detalle interno', () => {
    render(<ErrorAplicacion />);

    expect(screen.getByRole('heading', { name: 'No pudimos mostrar esta página' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Intentar nuevamente' })).toBeVisible();
    expect(screen.getByRole('link', { name: 'Volver al inicio' })).toHaveAttribute('href', '/');
    expect(screen.queryByText('Error de aplicación')).not.toBeInTheDocument();
  });
});
