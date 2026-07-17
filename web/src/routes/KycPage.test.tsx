import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { KycPage } from './KycPage';
import * as api from '../api/kyc';

afterEach(() => vi.restoreAllMocks());

function montar() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <KycPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('KycPage', () => {
  it('estado Aprobada muestra el chip de verificado', async () => {
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({ estado: 'Aprobada', motivoRechazo: null });
    montar();
    expect(await screen.findByText(/identidad verificada/i)).toBeInTheDocument();
  });

  it('estado Pendiente muestra "en revisión" y no muestra formulario', async () => {
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({ estado: 'Pendiente', motivoRechazo: null });
    montar();
    expect(await screen.findByText(/en revisión/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /enviar/i })).not.toBeInTheDocument();
  });

  it('estado Rechazada muestra el motivo y permite reenviar', async () => {
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({
      estado: 'Rechazada',
      motivoRechazo: 'Documento ilegible',
    });
    montar();
    expect(await screen.findByText(/documento ilegible/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /enviar/i })).toBeInTheDocument();
  });

  it('NoIniciado: rechaza archivo con tipo inválido y no envía', async () => {
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({ estado: 'NoIniciado', motivoRechazo: null });
    const enviar = vi.spyOn(api, 'enviarKyc').mockResolvedValue(undefined);
    montar();
    await screen.findByRole('button', { name: /enviar/i });
    const u = userEvent.setup();
    const pdf = new File(['x'], 'doc.pdf', { type: 'application/pdf' });
    await u.upload(screen.getByLabelText(/documento/i), pdf);
    expect(await screen.findByText(/formato no permitido/i)).toBeInTheDocument();
    await u.click(screen.getByRole('button', { name: /enviar/i }));
    expect(enviar).not.toHaveBeenCalled();
  });

  it('NoIniciado: con documento y selfie válidos, envía', async () => {
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({ estado: 'NoIniciado', motivoRechazo: null });
    const enviar = vi.spyOn(api, 'enviarKyc').mockResolvedValue(undefined);
    montar();
    await screen.findByRole('button', { name: /enviar/i });
    const u = userEvent.setup();
    const doc = new File(['x'], 'doc.png', { type: 'image/png' });
    const selfie = new File(['y'], 'selfie.jpg', { type: 'image/jpeg' });
    await u.upload(screen.getByLabelText(/documento/i), doc);
    await u.upload(screen.getByLabelText(/selfie/i), selfie);
    await u.click(screen.getByRole('button', { name: /enviar/i }));
    await waitFor(() => expect(enviar).toHaveBeenCalledWith(doc, selfie));
  });
});
