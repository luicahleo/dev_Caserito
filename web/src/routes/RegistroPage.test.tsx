import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { RegistroPage } from './RegistroPage';
import * as ctx from '../auth/AuthContext';

afterEach(() => {
  vi.restoreAllMocks();
});

function montar(registrar = vi.fn()) {
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
  } as ReturnType<typeof ctx.useAuth>);
  render(
    <MemoryRouter>
      <RegistroPage />
    </MemoryRouter>,
  );
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
    await usuario.type(screen.getByLabelText(/nombre/i), 'Ana');
    await usuario.type(screen.getByLabelText(/ciudad/i), 'Madrid');
    await usuario.click(screen.getByRole('button', { name: /registrarme/i }));

    expect(registrar).toHaveBeenCalledWith({
      email: 'ana@example.com',
      password: 'secreta123',
      nombre: 'Ana',
      ciudad: 'Madrid',
    });
  });
});
