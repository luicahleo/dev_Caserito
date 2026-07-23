import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AdminModeracionChatPage } from './AdminModeracionChatPage';
import * as api from '../api/moderacionChat';

afterEach(() => vi.restoreAllMocks());

function montar() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  render(
    <QueryClientProvider client={queryClient}>
      <AdminModeracionChatPage />
    </QueryClientProvider>,
  );
  return queryClient;
}

describe('AdminModeracionChatPage', () => {
  it('muestra la cola sin contenido sensible ni identificadores técnicos', async () => {
    vi.spyOn(api, 'listarReportesChat').mockResolvedValue([
      {
        id: 'reporte-tecnico-1',
        conversacionId: 'conversacion-tecnica-1',
        tipoObjetivo: 2,
        mensajeId: 'mensaje-tecnico-1',
        categoria: 2,
        estado: 1,
        creadoEn: '2026-07-23T10:00:00Z',
        tomadoEn: null,
        resueltoEn: null,
      },
    ]);

    montar();

    expect(await screen.findByText(/estafa/i)).toBeInTheDocument();
    expect(screen.getByText(/pendiente/i)).toBeInTheDocument();
    expect(screen.queryByText(/reporte-tecnico-1/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/conversacion-tecnica-1/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/mensaje-tecnico-1/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/contenido del mensaje/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/detalle del reporte/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/comprador|vendedor/i)).not.toBeInTheDocument();
  });

  it('carga la evidencia únicamente al abrir explícitamente el expediente', async () => {
    vi.spyOn(api, 'listarReportesChat').mockResolvedValue([
      {
        id: 'reporte-1',
        conversacionId: 'conversacion-1',
        tipoObjetivo: 2,
        mensajeId: 'mensaje-1',
        categoria: 1,
        estado: 1,
        creadoEn: '2026-07-23T10:00:00Z',
        tomadoEn: null,
        resueltoEn: null,
      },
    ]);
    const obtenerEvidencia = vi.spyOn(api, 'obtenerEvidenciaReporteChat').mockResolvedValue({
      reporteId: 'reporte-1',
      tipoObjetivo: 2,
      categoria: 1,
      detalle: 'Detalle visible sólo en el expediente',
      rolReportante: 'Comprador',
      rolObjetivo: 'Vendedor',
      mensajes: [],
    });

    montar();

    expect(await screen.findByRole('button', { name: /revisar expediente/i })).toBeInTheDocument();
    expect(obtenerEvidencia).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: /revisar expediente/i }));

    await waitFor(() => expect(obtenerEvidencia).toHaveBeenCalledWith('reporte-1'));
    expect(await screen.findByText(/detalle visible sólo en el expediente/i)).toBeInTheDocument();
  });

  it('elimina inmediatamente la evidencia del estado y del caché al cerrar', async () => {
    vi.spyOn(api, 'listarReportesChat').mockResolvedValue([
      {
        id: 'reporte-1',
        conversacionId: 'conversacion-1',
        tipoObjetivo: 1,
        mensajeId: null,
        categoria: 4,
        estado: 1,
        creadoEn: '2026-07-23T10:00:00Z',
        tomadoEn: null,
        resueltoEn: null,
      },
    ]);
    vi.spyOn(api, 'obtenerEvidenciaReporteChat').mockResolvedValue({
      reporteId: 'reporte-1',
      tipoObjetivo: 1,
      categoria: 4,
      detalle: 'Evidencia temporal',
      rolReportante: 'Comprador',
      rolObjetivo: 'Vendedor',
      mensajes: [],
    });
    const queryClient = montar();

    await userEvent.click(
      await screen.findByRole('button', { name: /revisar expediente/i }),
    );
    expect(await screen.findByText('Evidencia temporal')).toBeInTheDocument();
    expect(
      queryClient.getQueryData(['admin', 'moderacion-chat', 'evidencia', 'reporte-1']),
    ).toBeDefined();

    await userEvent.click(screen.getByRole('button', { name: /^cerrar$/i }));

    expect(screen.queryByText('Evidencia temporal')).not.toBeInTheDocument();
    expect(
      queryClient.getQueryData(['admin', 'moderacion-chat', 'evidencia', 'reporte-1']),
    ).toBeUndefined();
  });

  it('permite tomar un reporte pendiente', async () => {
    vi.spyOn(api, 'listarReportesChat').mockResolvedValue([
      {
        id: 'reporte-1',
        conversacionId: 'conversacion-1',
        tipoObjetivo: 1,
        mensajeId: null,
        categoria: 1,
        estado: 1,
        creadoEn: '2026-07-23T10:00:00Z',
        tomadoEn: null,
        resueltoEn: null,
      },
    ]);
    vi.spyOn(api, 'obtenerEvidenciaReporteChat').mockResolvedValue({
      reporteId: 'reporte-1',
      tipoObjetivo: 1,
      categoria: 1,
      detalle: null,
      rolReportante: 'Comprador',
      rolObjetivo: 'Vendedor',
      mensajes: [],
    });
    const tomar = vi.spyOn(api, 'tomarReporteChat').mockResolvedValue(undefined);
    montar();

    await userEvent.click(
      await screen.findByRole('button', { name: /revisar expediente/i }),
    );
    await userEvent.click(await screen.findByRole('button', { name: /^tomar reporte$/i }));

    await waitFor(() => expect(tomar).toHaveBeenCalledWith('reporte-1'));
  });

  it('permite liberar un reporte en revisión', async () => {
    vi.spyOn(api, 'listarReportesChat').mockResolvedValue([
      {
        id: 'reporte-1',
        conversacionId: 'conversacion-1',
        tipoObjetivo: 1,
        mensajeId: null,
        categoria: 1,
        estado: 2,
        creadoEn: '2026-07-23T10:00:00Z',
        tomadoEn: '2026-07-23T10:05:00Z',
        resueltoEn: null,
      },
    ]);
    vi.spyOn(api, 'obtenerEvidenciaReporteChat').mockResolvedValue({
      reporteId: 'reporte-1',
      tipoObjetivo: 1,
      categoria: 1,
      detalle: null,
      rolReportante: 'Comprador',
      rolObjetivo: 'Vendedor',
      mensajes: [],
    });
    const liberar = vi.spyOn(api, 'liberarReporteChat').mockResolvedValue(undefined);
    montar();

    await userEvent.click(
      await screen.findByRole('button', { name: /revisar expediente/i }),
    );
    await userEvent.click(await screen.findByRole('button', { name: /^liberar reporte$/i }));

    await waitFor(() => expect(liberar).toHaveBeenCalledWith('reporte-1'));
  });

  it('permite atender con la opción de cerrar la conversación', async () => {
    vi.spyOn(api, 'listarReportesChat').mockResolvedValue([
      {
        id: 'reporte-1',
        conversacionId: 'conversacion-1',
        tipoObjetivo: 1,
        mensajeId: null,
        categoria: 1,
        estado: 2,
        creadoEn: '2026-07-23T10:00:00Z',
        tomadoEn: '2026-07-23T10:05:00Z',
        resueltoEn: null,
      },
    ]);
    vi.spyOn(api, 'obtenerEvidenciaReporteChat').mockResolvedValue({
      reporteId: 'reporte-1',
      tipoObjetivo: 1,
      categoria: 1,
      detalle: null,
      rolReportante: 'Comprador',
      rolObjetivo: 'Vendedor',
      mensajes: [],
    });
    const atender = vi.spyOn(api, 'atenderReporteChat').mockResolvedValue(undefined);
    montar();

    await userEvent.click(
      await screen.findByRole('button', { name: /revisar expediente/i }),
    );
    await userEvent.click(
      await screen.findByRole('checkbox', { name: /cerrar la conversación/i }),
    );
    await userEvent.click(screen.getByRole('button', { name: /^atender reporte$/i }));

    await waitFor(() =>
      expect(atender).toHaveBeenCalledWith('reporte-1', { cerrarConversacion: true }),
    );
  });

  it('permite descartar un reporte en revisión', async () => {
    vi.spyOn(api, 'listarReportesChat').mockResolvedValue([
      {
        id: 'reporte-1',
        conversacionId: 'conversacion-1',
        tipoObjetivo: 1,
        mensajeId: null,
        categoria: 1,
        estado: 2,
        creadoEn: '2026-07-23T10:00:00Z',
        tomadoEn: '2026-07-23T10:05:00Z',
        resueltoEn: null,
      },
    ]);
    vi.spyOn(api, 'obtenerEvidenciaReporteChat').mockResolvedValue({
      reporteId: 'reporte-1',
      tipoObjetivo: 1,
      categoria: 1,
      detalle: null,
      rolReportante: 'Comprador',
      rolObjetivo: 'Vendedor',
      mensajes: [],
    });
    const descartar = vi.spyOn(api, 'descartarReporteChat').mockResolvedValue(undefined);
    montar();

    await userEvent.click(
      await screen.findByRole('button', { name: /revisar expediente/i }),
    );
    await userEvent.click(await screen.findByRole('button', { name: /^descartar reporte$/i }));

    await waitFor(() => expect(descartar).toHaveBeenCalledWith('reporte-1'));
  });

  it('permite cerrar y reabrir la conversación desde el expediente', async () => {
    vi.spyOn(api, 'listarReportesChat').mockResolvedValue([
      {
        id: 'reporte-1',
        conversacionId: 'conversacion-1',
        tipoObjetivo: 1,
        mensajeId: null,
        categoria: 1,
        estado: 2,
        creadoEn: '2026-07-23T10:00:00Z',
        tomadoEn: '2026-07-23T10:05:00Z',
        resueltoEn: null,
      },
    ]);
    vi.spyOn(api, 'obtenerEvidenciaReporteChat').mockResolvedValue({
      reporteId: 'reporte-1',
      tipoObjetivo: 1,
      categoria: 1,
      detalle: null,
      rolReportante: 'Comprador',
      rolObjetivo: 'Vendedor',
      mensajes: [],
    });
    const cerrar = vi
      .spyOn(api, 'cerrarConversacionModeracion')
      .mockResolvedValue(undefined);
    const reabrir = vi
      .spyOn(api, 'reabrirConversacionModeracion')
      .mockResolvedValue(undefined);
    montar();

    await userEvent.click(
      await screen.findByRole('button', { name: /revisar expediente/i }),
    );
    await userEvent.click(
      await screen.findByRole('button', { name: /^cerrar conversación$/i }),
    );
    await userEvent.click(
      screen.getByRole('button', { name: /^reabrir conversación$/i }),
    );

    await waitFor(() => expect(cerrar).toHaveBeenCalledWith('reporte-1'));
    expect(reabrir).toHaveBeenCalledWith('reporte-1');
  });

  it('muestra evidencia con roles relativos sin exponer IDs técnicos', async () => {
    vi.spyOn(api, 'listarReportesChat').mockResolvedValue([
      {
        id: 'reporte-1',
        conversacionId: 'conversacion-1',
        tipoObjetivo: 2,
        mensajeId: 'mensaje-1',
        categoria: 1,
        estado: 1,
        creadoEn: '2026-07-23T10:00:00Z',
        tomadoEn: null,
        resueltoEn: null,
      },
    ]);
    vi.spyOn(api, 'obtenerEvidenciaReporteChat').mockResolvedValue({
      reporteId: 'reporte-1',
      tipoObjetivo: 2,
      categoria: 1,
      detalle: null,
      rolReportante: 'Comprador',
      rolObjetivo: 'Vendedor',
      mensajes: [
        {
          id: 'mensaje-tecnico-1',
          secuencia: 42,
          autorRol: 'Vendedor',
          texto: 'Mensaje autorizado para moderación',
          enviadoEn: '2026-07-23T10:01:00Z',
          esObjetivo: true,
        },
      ],
    });
    montar();

    await userEvent.click(
      await screen.findByRole('button', { name: /revisar expediente/i }),
    );

    expect(await screen.findByText(/reportante: comprador/i)).toBeInTheDocument();
    expect(screen.getByText(/objetivo: vendedor/i)).toBeInTheDocument();
    expect(screen.getByText('Mensaje autorizado para moderación')).toBeInTheDocument();
    expect(screen.queryByText(/mensaje-tecnico-1/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/reporte-1|conversacion-1/i)).not.toBeInTheDocument();
  });
});
