import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ConfirmarEmailPage } from './ConfirmarEmailPage';
import * as auth from '../api/auth';

vi.mock('../api/auth', () => ({
  confirmarEmail: vi.fn(),
}));

const confirmarEmailMock = vi.mocked(auth.confirmarEmail);

afterEach(() => {
  vi.clearAllMocks();
});

function montar(url: string) {
  render(
    <MemoryRouter initialEntries={[url]}>
      <ConfirmarEmailPage />
    </MemoryRouter>,
  );
}

describe('ConfirmarEmailPage', () => {
  it('muestra éxito cuando el token es válido', async () => {
    confirmarEmailMock.mockResolvedValue(undefined);
    montar('/confirmar-email?userId=123&token=abc');

    expect(await screen.findByText(/email ha sido confirmado/i)).toBeInTheDocument();
    expect(confirmarEmailMock).toHaveBeenCalledWith('123', 'abc');
  });

  it('muestra error cuando el token es inválido o expiró', async () => {
    confirmarEmailMock.mockRejectedValue(new Error('400'));
    montar('/confirmar-email?userId=123&token=abc');

    expect(await screen.findByText(/no es válido o ha expirado/i)).toBeInTheDocument();
  });

  it('muestra error y no llama a la API si faltan parámetros', async () => {
    montar('/confirmar-email');

    expect(await screen.findByText(/no es válido o ha expirado/i)).toBeInTheDocument();
    expect(confirmarEmailMock).not.toHaveBeenCalled();
  });
});
