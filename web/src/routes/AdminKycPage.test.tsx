import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AdminKycPage } from './AdminKycPage';
import * as api from '../api/kyc';

afterEach(() => vi.restoreAllMocks());

const solicitud: api.SolicitudKycResumen = {
  solicitudId: 'abc',
  usuarioId: 'u1',
  estado: 'Pendiente',
  tipoDocumento: 'CI',
  enviadaEn: '2026-07-16T10:00:00Z',
  resueltaEn: null,
};

function montar() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <AdminKycPage />
    </QueryClientProvider>,
  );
}

describe('AdminKycPage', () => {
  it('lista las solicitudes pendientes', async () => {
    vi.spyOn(api, 'listarSolicitudesKyc').mockResolvedValue({
      items: [solicitud],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    montar();
    expect(await screen.findByText('u1')).toBeInTheDocument();
  });

  it('aprobar llama al endpoint y refresca', async () => {
    vi.spyOn(api, 'listarSolicitudesKyc').mockResolvedValue({
      items: [solicitud],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    vi.spyOn(api, 'obtenerImagenKyc').mockResolvedValue('blob:fake');
    const aprobar = vi.spyOn(api, 'aprobarKyc').mockResolvedValue(undefined);
    montar();
    const u = userEvent.setup();
    await u.click(await screen.findByRole('button', { name: /revisar/i }));
    await u.click(await screen.findByRole('button', { name: /aprobar/i }));
    await waitFor(() => expect(aprobar).toHaveBeenCalledWith('abc'));
  });

  it('rechazar exige motivo antes de llamar al endpoint', async () => {
    vi.spyOn(api, 'listarSolicitudesKyc').mockResolvedValue({
      items: [solicitud],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    vi.spyOn(api, 'obtenerImagenKyc').mockResolvedValue('blob:fake');
    const rechazar = vi.spyOn(api, 'rechazarKyc').mockResolvedValue(undefined);
    montar();
    const u = userEvent.setup();
    await u.click(await screen.findByRole('button', { name: /revisar/i }));
    await u.click(await screen.findByRole('button', { name: /rechazar/i }));
    // Sin motivo no debe llamar
    expect(rechazar).not.toHaveBeenCalled();
    await u.type(screen.getByLabelText(/motivo/i), 'Documento ilegible');
    await u.click(screen.getByRole('button', { name: /confirmar rechazo/i }));
    await waitFor(() => expect(rechazar).toHaveBeenCalledWith('abc', 'Documento ilegible'));
  });
});
