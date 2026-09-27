import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { AvisoConversacionRetenida } from './AvisoConversacionRetenida';
import * as chat from '../api/chat';

afterEach(() => vi.restoreAllMocks());

// iniciarConversacion devuelve `Conversacion` (ConversacionDto): lleva
// compradorId y vendedorId, no contraparteId ni rol. No confundir con
// `ConversacionResumen`, que es lo que devuelve buscarConversacionPropia.
const conversacionLiberada: chat.Conversacion = {
  id: 'c1',
  avisoId: 'a1',
  compradorId: 'yo',
  vendedorId: 'otra-persona',
  creadaEn: '',
  ultimaActividadEn: '',
  ultimaSecuencia: 1,
  estado: 0,
  origenCierre: null,
  puedeEnviar: true,
  ultimaSecuenciaEntregadaContraparte: 0,
  ultimaSecuenciaLeidaContraparte: 0,
};

const conversacionRetenida: chat.Conversacion = { ...conversacionLiberada, estado: 3 };

function montar(alLiberar = vi.fn()) {
  render(
    <MemoryRouter>
      <AvisoConversacionRetenida avisoId="a1" alLiberar={alLiberar} />
    </MemoryRouter>,
  );
  return alLiberar;
}

describe('AvisoConversacionRetenida', () => {
  it('explica la retención y enlaza a la verificación', () => {
    montar();

    expect(
      screen.getByText(/todavía no se ha entregado/),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Verificar mi identidad' })).toHaveAttribute(
      'href',
      '/kyc',
    );
  });

  it('libera la conversación cuando la verificación ya está aprobada', async () => {
    const iniciar = vi
      .spyOn(chat, 'iniciarConversacion')
      .mockResolvedValue(conversacionLiberada);
    const alLiberar = montar();

    await userEvent.click(screen.getByRole('button', { name: 'Ya verifiqué mi identidad' }));

    expect(iniciar).toHaveBeenCalledWith('a1');
    expect(alLiberar).toHaveBeenCalledTimes(1);
  });

  it('informa sin error cuando la verificación sigue sin aprobarse', async () => {
    vi.spyOn(chat, 'iniciarConversacion').mockResolvedValue(conversacionRetenida);
    const alLiberar = montar();

    await userEvent.click(screen.getByRole('button', { name: 'Ya verifiqué mi identidad' }));

    expect(
      await screen.findByText('Tu verificación todavía no está aprobada.'),
    ).toBeInTheDocument();
    expect(alLiberar).not.toHaveBeenCalled();
  });

  it('muestra un error genérico cuando la comprobación falla', async () => {
    vi.spyOn(chat, 'iniciarConversacion').mockRejectedValue(new Error('fallo'));
    const alLiberar = montar();

    await userEvent.click(screen.getByRole('button', { name: 'Ya verifiqué mi identidad' }));

    expect(
      await screen.findByText('No se pudo comprobar tu verificación. Inténtalo nuevamente.'),
    ).toBeInTheDocument();
    expect(alLiberar).not.toHaveBeenCalled();
  });
});
