import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RegistroPage } from './RegistroPage';
import * as ctx from '../auth/AuthContext';
import * as catalogo from '../api/catalogo';

afterEach(() => {
  vi.restoreAllMocks();
});

function montar(registrar = vi.fn()) {
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([
    { id: '22222222-2222-2222-2222-000000000003', nombre: 'La Paz' },
  ]);
  vi.spyOn(ctx, 'useAuth').mockReturnValue({
    usuario: null,
    estaAutenticado: false,
    cargando: false,
    permisos: [],
    verificado: false,
    identidadHabilitada: false,
    tienePermiso: () => false,
    iniciarSesion: vi.fn(),
    registrar,
    cerrarSesion: vi.fn(),
    restaurarSesion: vi.fn(),
  } as ReturnType<typeof ctx.useAuth>);
  render(<QueryClientProvider client={new QueryClient()}>
    <MemoryRouter>
      <RegistroPage />
    </MemoryRouter></QueryClientProvider>);
  return { registrar };
}

describe('RegistroPage', () => {
  it('permite mostrar y ocultar cada contraseña de forma independiente', async () => {
    const usuario = userEvent.setup();
    montar();
    const password = screen.getByLabelText(/^contraseña$/i);
    const confirmacion = screen.getByLabelText(/^confirmar contraseña$/i);

    expect(password).toHaveAttribute('type', 'password');
    expect(confirmacion).toHaveAttribute('type', 'password');

    await usuario.click(screen.getByRole('button', { name: 'Mostrar contraseña' }));
    expect(password).toHaveAttribute('type', 'text');
    expect(confirmacion).toHaveAttribute('type', 'password');

    await usuario.click(screen.getByRole('button', { name: 'Mostrar confirmación de contraseña' }));
    expect(confirmacion).toHaveAttribute('type', 'text');
  });

  it('avisa y bloquea el registro cuando las contraseñas no coinciden', async () => {
    const usuario = userEvent.setup();
    montar();

    await usuario.type(screen.getByLabelText(/^contraseña$/i), 'secreta123');
    await usuario.type(screen.getByLabelText(/^confirmar contraseña$/i), 'distinta123');

    expect(await screen.findByText('Las contraseñas no coinciden')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /registrarme/i })).toBeDisabled();
  });

  it('envía el registro sin el campo de confirmación cuando coinciden', async () => {
    const usuario = userEvent.setup();
    const { registrar } = montar(vi.fn().mockResolvedValue(undefined));

    await usuario.type(screen.getByLabelText(/email/i), 'ana@example.com');
    await usuario.type(screen.getByLabelText(/^contraseña$/i), 'secreta123');
    await usuario.type(screen.getByLabelText(/^confirmar contraseña$/i), 'secreta123');
    await usuario.type(screen.getByLabelText(/nombres/i), 'Ana');
    await usuario.type(screen.getByLabelText(/apellidos/i), 'Quispe');
    await usuario.click(screen.getByRole('combobox', { name: /ciudad/i }));
    await usuario.click(await screen.findByRole('option', { name: 'La Paz' }));
    await usuario.click(screen.getByRole('button', { name: /registrarme/i }));

    expect(registrar).toHaveBeenCalledWith({
      email: 'ana@example.com',
      password: 'secreta123',
      nombres: 'Ana',
      apellidos: 'Quispe',
      ciudadId: '22222222-2222-2222-2222-000000000003',
    });
  });
});
