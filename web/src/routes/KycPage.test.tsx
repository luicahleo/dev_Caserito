import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { KycPage } from './KycPage';
import * as api from '../api/kyc';
import { HttpError } from '../api/http';

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
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({
      estado: 'Aprobada',
      motivoRechazo: null,
    });
    montar();
    expect(await screen.findByText(/identidad verificada/i)).toBeInTheDocument();
  });

  it('estado Pendiente muestra "en revisión" y no muestra formulario', async () => {
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({
      estado: 'Pendiente',
      motivoRechazo: null,
    });
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
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({
      estado: 'NoIniciado',
      motivoRechazo: null,
    });
    const enviar = vi.spyOn(api, 'enviarKyc').mockResolvedValue(undefined);
    montar();
    await screen.findByRole('button', { name: /enviar/i });
    const u = userEvent.setup();
    const pdf = new File(['x'], 'doc.pdf', { type: 'application/pdf' });
    // fireEvent.change evita el filtro de `accept` del input (defensa-en-profundidad de UX
    // en producción), permitiendo verificar la validación JS independiente ante un tipo inválido.
    fireEvent.change(screen.getByLabelText(/documento/i), { target: { files: [pdf] } });
    expect(await screen.findByText(/formato no permitido/i)).toBeInTheDocument();
    await u.click(screen.getByRole('button', { name: /enviar/i }));
    expect(enviar).not.toHaveBeenCalled();
  });

  it('NoIniciado: con documento y selfie válidos, envía', async () => {
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({
      estado: 'NoIniciado',
      motivoRechazo: null,
    });
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

  it('ante un 409 en el envío, muestra un mensaje amable y refresca el estado', async () => {
    const obtenerEstado = vi
      .spyOn(api, 'obtenerEstadoKyc')
      .mockResolvedValue({ estado: 'NoIniciado', motivoRechazo: null });
    vi.spyOn(api, 'enviarKyc').mockRejectedValue(
      new HttpError(409, 'Kyc.YaVerificado', 'Petición fallida (409) a /api/kyc'),
    );
    montar();
    await screen.findByRole('button', { name: /enviar/i });
    const u = userEvent.setup();
    const doc = new File(['x'], 'doc.png', { type: 'image/png' });
    const selfie = new File(['y'], 'selfie.jpg', { type: 'image/jpeg' });
    await u.upload(screen.getByLabelText(/documento/i), doc);
    await u.upload(screen.getByLabelText(/selfie/i), selfie);
    const llamadasPrevias = obtenerEstado.mock.calls.length;
    await u.click(screen.getByRole('button', { name: /enviar/i }));
    expect(await screen.findByText(/ya no se puede enviar en este estado/i)).toBeInTheDocument();
    await waitFor(() => expect(obtenerEstado.mock.calls.length).toBeGreaterThan(llamadasPrevias));
  });

  it('si falla la carga del estado, muestra un alert de error', async () => {
    vi.spyOn(api, 'obtenerEstadoKyc').mockRejectedValue(new Error('falló la red'));
    montar();
    expect(
      await screen.findByText(/no se pudo cargar tu estado de verificación/i),
    ).toBeInTheDocument();
  });
});
