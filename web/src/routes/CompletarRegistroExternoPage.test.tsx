import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as authApi from '../api/auth';
import * as ctx from '../auth/AuthContext';
import { CompletarRegistroExternoPage } from './CompletarRegistroExternoPage';

beforeEach(() => {
  vi.restoreAllMocks();
  vi.spyOn(ctx, 'useAuth').mockReturnValue({ restaurarSesion: vi.fn() } as unknown as ReturnType<typeof ctx.useAuth>);
});

describe('CompletarRegistroExternoPage', () => {
  it('muestra únicamente los datos pendientes', async () => {
    vi.spyOn(authApi, 'obtenerLoginExternoPendiente').mockResolvedValue({
      requiereEmail: true, requiereNombre: false, requiereCiudad: true,
      requiereVinculacion: false, nombreVisible: 'Ana',
    });
    render(<MemoryRouter><CompletarRegistroExternoPage /></MemoryRouter>);

    expect(await screen.findByLabelText(/^Email/)).toBeInTheDocument();
    expect(screen.getByLabelText(/^Ciudad/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/^Nombre/)).not.toBeInTheDocument();
  });

  it('explica genéricamente la vinculación y ofrece acceso y recuperación', async () => {
    vi.spyOn(authApi, 'obtenerLoginExternoPendiente').mockResolvedValue({
      requiereEmail: false, requiereNombre: false, requiereCiudad: true,
      requiereVinculacion: true, nombreVisible: null,
    });
    render(<MemoryRouter><CompletarRegistroExternoPage /></MemoryRouter>);

    expect(await screen.findByText(/verifica tu cuenta existente/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /iniciar sesión/i })).toHaveAttribute('href', '/login');
    expect(screen.getByRole('link', { name: /recuperar acceso/i })).toHaveAttribute('href', '/olvide-password');
  });

  it('muestra un error genérico sin reproducir detalles privados', async () => {
    vi.spyOn(authApi, 'obtenerLoginExternoPendiente').mockRejectedValue(new Error('ana@example.com'));
    render(<MemoryRouter><CompletarRegistroExternoPage /></MemoryRouter>);

    expect(await screen.findByText('No pudimos continuar el acceso externo. Inténtalo de nuevo.')).toBeInTheDocument();
    expect(screen.queryByText('ana@example.com')).not.toBeInTheDocument();
  });
});
