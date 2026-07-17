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

  it('revoca el objectURL si el panel se desmonta antes de que resuelva la imagen (evita fuga de PII)', async () => {
    vi.spyOn(api, 'listarSolicitudesKyc').mockResolvedValue({
      items: [solicitud],
      pagina: 1,
      tamano: 20,
      total: 1,
    });

    let resolverDoc: (url: string) => void = () => {};
    const promesaDoc = new Promise<string>((resolve) => {
      resolverDoc = resolve;
    });
    vi.spyOn(api, 'obtenerImagenKyc').mockImplementation((_id, tipo) =>
      tipo === 'documento' ? promesaDoc : new Promise<string>(() => {}),
    );
    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});

    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { unmount } = render(
      <QueryClientProvider client={qc}>
        <AdminKycPage />
      </QueryClientProvider>,
    );
    const u = userEvent.setup();
    await u.click(await screen.findByRole('button', { name: /revisar/i }));

    // Se desmonta el árbol (equivalente a cerrar el diálogo/cambiar de página)
    // ANTES de que resuelva el fetch de la imagen del documento.
    unmount();

    resolverDoc('blob:fake-doc');

    await waitFor(() => expect(revokeSpy).toHaveBeenCalledWith('blob:fake-doc'));
  });

  it('una solicitud ya resuelta (Aprobada) no muestra los botones Aprobar/Rechazar', async () => {
    const solicitudResuelta: api.SolicitudKycResumen = {
      ...solicitud,
      estado: 'Aprobada',
      resueltaEn: '2026-07-16T12:00:00Z',
    };
    vi.spyOn(api, 'listarSolicitudesKyc').mockResolvedValue({
      items: [solicitudResuelta],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    vi.spyOn(api, 'obtenerImagenKyc').mockResolvedValue('blob:fake');
    montar();
    const u = userEvent.setup();
    await u.click(await screen.findByRole('button', { name: /revisar/i }));
    expect(await screen.findByText(/esta solicitud ya fue resuelta/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^aprobar$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^rechazar$/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cerrar/i })).toBeInTheDocument();
  });
});
