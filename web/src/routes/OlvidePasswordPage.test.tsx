import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { HttpError } from '../api/http';
import * as auth from '../api/auth';
import { OlvidePasswordPage } from './OlvidePasswordPage';

afterEach(() => {
  vi.restoreAllMocks();
});

function montar() {
  render(
    <MemoryRouter>
      <OlvidePasswordPage />
    </MemoryRouter>,
  );
}

describe('OlvidePasswordPage', () => {
  it('valida el correo antes de solicitar el enlace', async () => {
    const usuario = userEvent.setup();
    const solicitar = vi.spyOn(auth, 'solicitarRestablecimientoPassword');
    montar();

    await usuario.type(screen.getByLabelText('Email'), 'correo-invalido');
    await usuario.click(screen.getByRole('button', { name: 'Enviar enlace' }));

    expect(await screen.findByText('Email inválido')).toBeInTheDocument();
    expect(solicitar).not.toHaveBeenCalled();
  });

  it('envía el correo y muestra el mensaje uniforme', async () => {
    const usuario = userEvent.setup();
    const solicitar = vi
      .spyOn(auth, 'solicitarRestablecimientoPassword')
      .mockResolvedValue(undefined);
    montar();

    await usuario.type(screen.getByLabelText('Email'), 'usuario@caserito.test');
    await usuario.click(screen.getByRole('button', { name: 'Enviar enlace' }));

    expect(solicitar).toHaveBeenCalledWith('usuario@caserito.test');
    expect(
      await screen.findByText(
        'Si existe una cuenta asociada a ese correo, recibirás un enlace para restablecer tu contraseña.',
      ),
    ).toBeInTheDocument();
  });

  it('muestra espera al superar el límite', async () => {
    const usuario = userEvent.setup();
    vi.spyOn(auth, 'solicitarRestablecimientoPassword').mockRejectedValue(
      new HttpError(429, null, 'Petición fallida (429)'),
    );
    montar();

    await usuario.type(screen.getByLabelText('Email'), 'usuario@caserito.test');
    await usuario.click(screen.getByRole('button', { name: 'Enviar enlace' }));

    expect(await screen.findByText('Has realizado demasiados intentos. Espera antes de volver a intentarlo.')).toBeInTheDocument();
  });
});
