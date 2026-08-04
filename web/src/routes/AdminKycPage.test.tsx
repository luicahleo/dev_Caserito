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
  scoreSimilitud: null,
  resueltaPor: null,
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

  it('una solicitud pendiente muestra detalle y acciones manuales', async () => {
    vi.spyOn(api, 'listarSolicitudesKyc').mockResolvedValue({
      items: [solicitud],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    vi.spyOn(api, 'obtenerImagenKyc').mockResolvedValue('blob:fake');
    vi.spyOn(api, 'obtenerDetalleKyc').mockResolvedValue({ solicitudId: 'abc', nombres: 'Ana',
      apellidos: 'Quispe', numeroCi: '1234567', complementoCi: null,
      departamentoExpedicion: 'LaPaz', estado: 'Pendiente', scoreSimilitud: 95 });
    montar();
    const u = userEvent.setup();
    await u.click(await screen.findByRole('button', { name: /revisar/i }));
    expect(await screen.findByText(/Ana/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^aprobar$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^rechazar$/i })).toBeInTheDocument();
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
