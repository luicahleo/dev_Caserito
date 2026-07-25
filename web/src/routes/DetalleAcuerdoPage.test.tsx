import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { DetalleAcuerdoPage } from './DetalleAcuerdoPage';
import * as orders from '../api/orders';
import * as avisos from '../api/avisos';
import * as reputation from '../api/reputation';

afterEach(() => vi.restoreAllMocks());

describe('DetalleAcuerdoPage', () => {
  it('no consulta reputación antes de completar el acuerdo', async () => {
    const estado = vi.spyOn(reputation, 'obtenerEstadoResena');
    vi.spyOn(orders, 'obtenerOrden').mockResolvedValue({
      id: 'o1',
      avisoId: 'a1',
      estado: 'Agreed',
      montoAcordado: 75,
      moneda: 'BOB',
      rol: 'comprador',
      creadaEn: '2026-07-25T10:00:00Z',
      actualizadaEn: '2026-07-25T10:00:00Z',
      marcadaVendidaEn: null,
      compradorConfirmoEn: null,
      completadaEn: null,
    });
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockRejectedValue(new Error('No disponible'));
    montar();

    await screen.findByText('Acordado');
    expect(estado).not.toHaveBeenCalled();
    expect(screen.queryByLabelText('Puntuación')).not.toBeInTheDocument();
  });

  it('permite calificar un acuerdo completado tras confirmar la irreversibilidad', async () => {
    vi.spyOn(orders, 'obtenerOrden').mockResolvedValue({
      id: 'o1',
      avisoId: 'a1',
      estado: 'Completed',
      montoAcordado: 75,
      moneda: 'BOB',
      rol: 'comprador',
      creadaEn: '2026-07-25T10:00:00Z',
      actualizadaEn: '2026-07-25T12:00:00Z',
      marcadaVendidaEn: '2026-07-25T11:00:00Z',
      compradorConfirmoEn: '2026-07-25T12:00:00Z',
      completadaEn: '2026-07-25T12:00:00Z',
    });
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockRejectedValue(new Error('No disponible'));
    vi.spyOn(reputation, 'obtenerEstadoResena').mockResolvedValue({
      puedeCalificar: true,
      autorYaCalifico: false,
      contraparteYaCalifico: false,
      reveladas: false,
      enviadaEn: null,
      contraparteId: 'u2',
    });
    const crear = vi.spyOn(reputation, 'crearResena').mockResolvedValue({
      id: 'r1',
      creadaEn: '2026-07-25T13:00:00Z',
    });
    montar();

    const usuario = userEvent.setup();
    await usuario.click(await screen.findByLabelText('Puntuación'));
    await usuario.click(screen.getByRole('option', { name: '5 de 5' }));
    await usuario.type(screen.getByLabelText('Comentario'), 'Cumplió con todo lo acordado.');
    await usuario.click(screen.getByRole('button', { name: 'Enviar reseña' }));
    expect(await screen.findByText(/no puede editarse/i)).toBeInTheDocument();
    expect(screen.getByText(/permanece oculta/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar envío' }));

    await vi.waitFor(() =>
      expect(crear).toHaveBeenCalledWith('o1', 5, 'Cumplió con todo lo acordado.'),
    );
  });

  it('permite al vendedor aceptar una solicitud y aclara que no hay pago', async () => {
    vi.spyOn(orders, 'obtenerOrden').mockResolvedValue({
      id: 'o1',
      avisoId: 'a1',
      estado: 'Requested',
      montoAcordado: 75,
      moneda: 'BOB',
      rol: 'vendedor',
      creadaEn: '2026-07-25T10:00:00Z',
      actualizadaEn: '2026-07-25T10:00:00Z',
      marcadaVendidaEn: null,
      compradorConfirmoEn: null,
      completadaEn: null,
    });
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockResolvedValue({
      id: 'a1',
      vendedorId: 'vendedor',
      titulo: 'Bicicleta',
      descripcion: '',
      monto: 75,
      moneda: 'BOB',
      nombreCategoria: '',
      nombreCiudad: '',
      condicion: 'Usado',
      fechaCreacion: '',
      fotos: [],
    });
    const aceptar = vi.spyOn(orders, 'aceptarOrden').mockResolvedValue();
    const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={cliente}>
        <MemoryRouter initialEntries={['/acuerdos/o1']}>
          <Routes>
            <Route path="/acuerdos/:id" element={<DetalleAcuerdoPage />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Aceptar acuerdo' }));
    await vi.waitFor(() => expect(aceptar).toHaveBeenCalledWith('o1'));
    expect(screen.getByText(/Caserito no verifica el pago ni la entrega/i)).toBeInTheDocument();
  });

  it('permite al comprador confirmar una orden marcada como vendida', async () => {
    vi.spyOn(orders, 'obtenerOrden').mockResolvedValue({
      id: 'o1',
      avisoId: 'a1',
      estado: 'MarkedAsSold',
      montoAcordado: 75,
      moneda: 'BOB',
      rol: 'comprador',
      creadaEn: '2026-07-25T10:00:00Z',
      actualizadaEn: '2026-07-25T11:00:00Z',
      marcadaVendidaEn: '2026-07-25T11:00:00Z',
      compradorConfirmoEn: null,
      completadaEn: null,
    });
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockRejectedValue(new Error('No disponible'));
    const confirmar = vi.spyOn(orders, 'confirmarCierreOrden').mockResolvedValue();
    const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={cliente}>
        <MemoryRouter initialEntries={['/acuerdos/o1']}>
          <Routes>
            <Route path="/acuerdos/:id" element={<DetalleAcuerdoPage />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Confirmar cierre' }));
    expect(screen.getByText(/acción es irreversible/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar' }));
    await vi.waitFor(() => expect(confirmar).toHaveBeenCalledWith('o1'));
  });
});

function montar() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={['/acuerdos/o1']}>
        <Routes>
          <Route path="/acuerdos/:id" element={<DetalleAcuerdoPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
