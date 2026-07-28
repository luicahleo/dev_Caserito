import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { NotificacionesPage } from './NotificacionesPage';
import * as notificaciones from '../api/notificaciones';

afterEach(() => vi.restoreAllMocks());

const notificacion: notificaciones.Notificacion = {
  id: 'n1',
  tipo: 'NuevoMensaje',
  titulo: 'Nuevo mensaje',
  mensaje: 'Tienes un nuevo mensaje de chat.',
  entidadRelacionadaId: 'c1',
  leida: false,
  creadaEn: '2026-07-28T10:00:00Z',
};

function montar() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <NotificacionesPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('NotificacionesPage', () => {
  it('muestra la lista de notificaciones', async () => {
    vi.spyOn(notificaciones, 'listarNotificaciones').mockResolvedValue({
      items: [notificacion],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    montar();

    expect(await screen.findByText('Nuevo mensaje')).toBeInTheDocument();
  });

  it('muestra mensaje vacío cuando no hay notificaciones', async () => {
    vi.spyOn(notificaciones, 'listarNotificaciones').mockResolvedValue({
      items: [],
      pagina: 1,
      tamano: 20,
      total: 0,
    });
    montar();

    expect(await screen.findByText(/no tienes notificaciones/i)).toBeInTheDocument();
  });

  it('marca todas como leídas al hacer clic', async () => {
    vi.spyOn(notificaciones, 'listarNotificaciones').mockResolvedValue({
      items: [notificacion],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    const spy = vi.spyOn(notificaciones, 'marcarTodasLeidas').mockResolvedValue(1);
    montar();

    await screen.findByText('Nuevo mensaje');
    await userEvent.click(screen.getByRole('button', { name: /marcar todas/i }));

    await waitFor(() => expect(spy).toHaveBeenCalledTimes(1));
  });
});
