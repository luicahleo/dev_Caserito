import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { KycPage } from './KycPage';
import * as api from '../api/kyc';
import { HttpError } from '../api/http';
import { esMovilConCamara } from '../kyc/captura/plataforma';

vi.mock('../kyc/captura/plataforma', () => ({ esMovilConCamara: vi.fn(() => true) }));
vi.mock('../kyc/captura/FlujoCapturaKyc', () => ({
  FlujoCapturaKyc: ({
    onDocumento,
    onSelfie,
  }: {
    onDocumento(archivo: File): void;
    onSelfie(archivo: File): void;
  }) => (
    <>
      <button
        onClick={() => onDocumento(new File(['x'], 'documento-ci.jpg', { type: 'image/jpeg' }))}
      >
        Capturar documento
      </button>
      <button onClick={() => onSelfie(new File(['y'], 'selfie.jpg', { type: 'image/jpeg' }))}>
        Capturar selfie
      </button>
    </>
  ),
}));

afterEach(() => {
  vi.restoreAllMocks();
  vi.mocked(esMovilConCamara).mockReturnValue(true);
});

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

  it('estado Pendiente muestra revisión humana y no muestra formulario', async () => {
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({
      estado: 'Pendiente',
      motivoRechazo: null,
    });
    montar();
    expect(await screen.findByText(/pendiente de revisión humana/i)).toBeInTheDocument();
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

  it('NoIniciado: en navegador bloquea la captura y no ofrece archivos', async () => {
    vi.mocked(esMovilConCamara).mockReturnValue(false);
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({
      estado: 'NoIniciado',
      motivoRechazo: null,
    });
    montar();
    expect(await screen.findByText(/debe realizarse desde un teléfono/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /enviar/i })).not.toBeInTheDocument();
    expect(document.querySelector('input[type="file"]')).not.toBeInTheDocument();
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
    await u.type(screen.getByLabelText(/número de ci/i), '1234567');
    await u.click(screen.getByRole('combobox', { name: /departamento de expedición/i }));
    await u.click(await screen.findByRole('option', { name: 'La Paz' }));
    await u.click(screen.getByRole('button', { name: /capturar documento/i }));
    await u.click(screen.getByRole('button', { name: /capturar selfie/i }));
    await u.click(screen.getByRole('button', { name: /enviar/i }));
    await waitFor(() =>
      expect(enviar).toHaveBeenCalledWith(
        expect.objectContaining({
          numeroCi: '1234567',
          departamentoExpedicion: 'LaPaz',
          documento: expect.objectContaining({ name: 'documento-ci.jpg' }),
          selfie: expect.objectContaining({ name: 'selfie.jpg' }),
        }),
      ),
    );
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
    await u.type(screen.getByLabelText(/número de ci/i), '1234567');
    await u.click(screen.getByRole('combobox', { name: /departamento de expedición/i }));
    await u.click(await screen.findByRole('option', { name: 'La Paz' }));
    await u.click(screen.getByRole('button', { name: /capturar documento/i }));
    await u.click(screen.getByRole('button', { name: /capturar selfie/i }));
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

  it('ante un 422 por rostro no detectado, muestra un mensaje accionable', async () => {
    vi.spyOn(api, 'obtenerEstadoKyc').mockResolvedValue({ estado: 'NoIniciado', motivoRechazo: null });
    vi.spyOn(api, 'enviarKyc').mockRejectedValue(
      new HttpError(422, 'Kyc.RostroNoDetectado', 'Petición fallida (422) a /api/kyc'),
    );
    montar();
    await screen.findByRole('button', { name: /enviar/i });
    const u = userEvent.setup();
    await u.type(screen.getByLabelText(/número de ci/i), '1234567');
    await u.click(screen.getByRole('combobox', { name: /departamento de expedición/i }));
    await u.click(await screen.findByRole('option', { name: 'La Paz' }));
    await u.click(screen.getByRole('button', { name: /capturar documento/i }));
    await u.click(screen.getByRole('button', { name: /capturar selfie/i }));
    await u.click(screen.getByRole('button', { name: /enviar/i }));
    expect(await screen.findByText(/no detectamos un rostro/i)).toBeInTheDocument();
  });
});
