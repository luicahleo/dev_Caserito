import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { StrictMode } from 'react';
import { HttpError } from '../api/http';
import * as auth from '../api/auth';
import { RestablecerPasswordPage } from './RestablecerPasswordPage';

afterEach(() => {
  vi.restoreAllMocks();
  window.history.replaceState({}, '', '/');
});

function montar(hash = '#usuarioId=usuario-id&token=token-sintetico') {
  window.history.replaceState({}, '', `/restablecer-password${hash}`);
  const replaceState = vi.spyOn(window.history, 'replaceState');
  render(
    <StrictMode>
      <MemoryRouter>
        <RestablecerPasswordPage />
      </MemoryRouter>
    </StrictMode>,
  );
  return { replaceState };
}

describe('RestablecerPasswordPage', () => {
  it('lee el fragmento, limpia la URL y no muestra el token', () => {
    const { replaceState } = montar();

    expect(replaceState).toHaveBeenCalledWith(null, '', '/restablecer-password');
    expect(window.location.hash).toBe('');
    expect(screen.queryByText('token-sintetico')).not.toBeInTheDocument();
  });

  it('muestra el estado genérico si el fragmento está incompleto', () => {
    montar('#usuarioId=usuario-id');

    expect(screen.getByText('El enlace no es válido o ha caducado.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Restablecer contraseña' })).not.toBeInTheDocument();
  });

  it('valida mínimo y coincidencia después de interactuar con la confirmación', async () => {
    const usuario = userEvent.setup();
    montar();
    const boton = screen.getByRole('button', { name: 'Restablecer contraseña' });

    await usuario.type(screen.getByLabelText('Nueva contraseña'), 'corta');
    expect(await screen.findByText('Mínimo 8 caracteres')).toBeInTheDocument();
    expect(screen.queryByText('Las contraseñas no coinciden')).not.toBeInTheDocument();

    await usuario.type(screen.getByLabelText('Confirmar contraseña'), 'distinta');
    expect(await screen.findByText('Las contraseñas no coinciden')).toBeInTheDocument();
    expect(boton).toBeDisabled();
  });

  it('controla la visibilidad de ambos campos de forma independiente', async () => {
    const usuario = userEvent.setup();
    montar();
    const password = screen.getByLabelText('Nueva contraseña');
    const confirmacion = screen.getByLabelText('Confirmar contraseña');

    await usuario.click(screen.getByRole('button', { name: 'Mostrar nueva contraseña' }));
    expect(password).toHaveAttribute('type', 'text');
    expect(confirmacion).toHaveAttribute('type', 'password');

    await usuario.click(screen.getByRole('button', { name: 'Mostrar confirmación de contraseña' }));
    expect(password).toHaveAttribute('type', 'text');
    expect(confirmacion).toHaveAttribute('type', 'text');
  });

  it('envía solo usuario, token y contraseña y muestra éxito', async () => {
    const usuario = userEvent.setup();
    const restablecer = vi.spyOn(auth, 'restablecerPassword').mockResolvedValue(undefined);
    montar();

    await usuario.type(screen.getByLabelText('Nueva contraseña'), 'Password456!');
    await usuario.type(screen.getByLabelText('Confirmar contraseña'), 'Password456!');
    await usuario.click(screen.getByRole('button', { name: 'Restablecer contraseña' }));

    expect(restablecer).toHaveBeenCalledWith('usuario-id', 'token-sintetico', 'Password456!');
    expect(
      await screen.findByText('Tu contraseña se restableció correctamente. Ya puedes iniciar sesión.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Iniciar sesión' })).toHaveAttribute('href', '/login');
  });

  it.each([
    [400, 'El enlace no es válido o ha caducado.'],
    [429, 'Has realizado demasiados intentos. Espera antes de volver a intentarlo.'],
  ])('muestra el estado seguro para HTTP %i', async (status, mensaje) => {
    const usuario = userEvent.setup();
    vi.spyOn(auth, 'restablecerPassword').mockRejectedValue(
      new HttpError(status, null, `Petición fallida (${status})`),
    );
    montar();

    await usuario.type(screen.getByLabelText('Nueva contraseña'), 'Password456!');
    await usuario.type(screen.getByLabelText('Confirmar contraseña'), 'Password456!');
    await usuario.click(screen.getByRole('button', { name: 'Restablecer contraseña' }));

    expect(await screen.findByText(mensaje)).toBeInTheDocument();
  });
});
