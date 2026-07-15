import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { LoginPage } from './LoginPage';
import * as ctx from '../auth/AuthContext';

afterEach(() => {
  vi.restoreAllMocks();
});

function montar(iniciarSesion = vi.fn()) {
  vi.spyOn(ctx, 'useAuth').mockReturnValue({
    usuario: null,
    estaAutenticado: false,
    cargando: false,
    iniciarSesion,
    registrar: vi.fn(),
    cerrarSesion: vi.fn(),
  } as ReturnType<typeof ctx.useAuth>);
  render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>,
  );
  return { iniciarSesion };
}

describe('LoginPage', () => {
  it('llama a iniciarSesion con credenciales válidas', async () => {
    const usuarioEvento = userEvent.setup();
    const { iniciarSesion } = montar(vi.fn().mockResolvedValue(undefined));

    await usuarioEvento.type(screen.getByLabelText(/email/i), 'ana@example.com');
    await usuarioEvento.type(screen.getByLabelText(/contraseña/i), 'secreta123');
    await usuarioEvento.click(screen.getByRole('button', { name: /entrar/i }));

    expect(iniciarSesion).toHaveBeenCalledWith({
      email: 'ana@example.com',
      password: 'secreta123',
    });
  });

  it('muestra "Credenciales inválidas" si iniciarSesion rechaza', async () => {
    const usuarioEvento = userEvent.setup();
    montar(vi.fn().mockRejectedValue(new Error('401')));

    await usuarioEvento.type(screen.getByLabelText(/email/i), 'ana@example.com');
    await usuarioEvento.type(screen.getByLabelText(/contraseña/i), 'secreta123');
    await usuarioEvento.click(screen.getByRole('button', { name: /entrar/i }));

    expect(await screen.findByText('Credenciales inválidas')).toBeInTheDocument();
  });

  it('muestra error de validación con email inválido y no llama a iniciarSesion', async () => {
    const usuarioEvento = userEvent.setup();
    const { iniciarSesion } = montar();

    await usuarioEvento.type(screen.getByLabelText(/email/i), 'no-es-un-email');
    await usuarioEvento.type(screen.getByLabelText(/contraseña/i), 'secreta123');
    await usuarioEvento.click(screen.getByRole('button', { name: /entrar/i }));

    expect(await screen.findByText('Email inválido')).toBeInTheDocument();
    expect(iniciarSesion).not.toHaveBeenCalled();
  });
});
