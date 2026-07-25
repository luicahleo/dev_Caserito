import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { MisAcuerdosPage } from './MisAcuerdosPage';
import * as orders from '../api/orders';

afterEach(() => vi.restoreAllMocks());

describe('MisAcuerdosPage', () => {
  it('muestra compras y cambia al listado de ventas', async () => {
    const listar = vi.spyOn(orders, 'listarOrdenes').mockResolvedValue({
      items: [{
        id: 'o1', avisoId: 'a1', estado: 'Requested', montoAcordado: 75,
        moneda: 'BOB', rol: 'comprador', actualizadaEn: '2026-07-25T10:00:00Z',
      }],
      pagina: 1, tamano: 20, total: 1,
    });
    const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={cliente}>
        <MemoryRouter><MisAcuerdosPage /></MemoryRouter>
      </QueryClientProvider>,
    );

    expect(await screen.findByText('Solicitado')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('tab', { name: 'Ventas' }));
    await vi.waitFor(() => expect(listar).toHaveBeenCalledWith('vendedor'));
  });

  it('el comprador cancela una solicitud tras confirmar', async () => {
    vi.spyOn(orders, 'listarOrdenes').mockResolvedValue({
      items: [{
        id: 'o1', avisoId: 'a1', estado: 'Requested', montoAcordado: 100,
        moneda: 'BOB', rol: 'comprador', actualizadaEn: '2026-07-25T00:00:00Z',
      }],
      pagina: 1, tamano: 20, total: 1,
    });
    const cancelar = vi.spyOn(orders, 'cancelarOrden').mockResolvedValue();
    const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={cliente}>
        <MemoryRouter><MisAcuerdosPage /></MemoryRouter>
      </QueryClientProvider>,
    );

    fireEvent.click(await screen.findByRole('button', { name: /cancelar solicitud/i }));
    fireEvent.click(await screen.findByRole('button', { name: /confirmar/i }));

    await vi.waitFor(() => expect(cancelar).toHaveBeenCalledWith('o1'));
  });

  it('el vendedor ve "Rechazar solicitud" y una orden cancelada no tiene acciones', async () => {
    vi.spyOn(orders, 'listarOrdenes').mockResolvedValue({
      items: [{
        id: 'o1', avisoId: 'a1', estado: 'Requested', montoAcordado: 100,
        moneda: 'BOB', rol: 'vendedor', actualizadaEn: '2026-07-25T00:00:00Z',
      }, {
        id: 'o2', avisoId: 'a2', estado: 'Cancelled', montoAcordado: 50,
        moneda: 'BOB', rol: 'vendedor', actualizadaEn: '2026-07-25T00:00:00Z',
      }],
      pagina: 1, tamano: 20, total: 2,
    });
    const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={cliente}>
        <MemoryRouter><MisAcuerdosPage /></MemoryRouter>
      </QueryClientProvider>,
    );
    fireEvent.click(screen.getByRole('tab', { name: 'Ventas' }));

    expect(await screen.findByRole('button', { name: /rechazar solicitud/i })).toBeInTheDocument();
    expect(await screen.findByText('Cancelado')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancelar acuerdo/i })).not.toBeInTheDocument();
  });

  it('el comprador cancela un acuerdo tras confirmar', async () => {
    vi.spyOn(orders, 'listarOrdenes').mockResolvedValue({
      items: [{
        id: 'o1', avisoId: 'a1', estado: 'Agreed', montoAcordado: 150,
        moneda: 'BOB', rol: 'comprador', actualizadaEn: '2026-07-25T10:30:00Z',
      }],
      pagina: 1, tamano: 20, total: 1,
    });
    const cancelar = vi.spyOn(orders, 'cancelarOrden').mockResolvedValue();
    const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={cliente}>
        <MemoryRouter><MisAcuerdosPage /></MemoryRouter>
      </QueryClientProvider>,
    );

    fireEvent.click(await screen.findByRole('button', { name: /cancelar acuerdo/i }));
    fireEvent.click(await screen.findByRole('button', { name: /confirmar/i }));

    await vi.waitFor(() => expect(cancelar).toHaveBeenCalledWith('o1'));
  });

  it('el vendedor cancela un acuerdo tras confirmar', async () => {
    vi.spyOn(orders, 'listarOrdenes').mockResolvedValue({
      items: [{
        id: 'o1', avisoId: 'a1', estado: 'Agreed', montoAcordado: 200,
        moneda: 'BOB', rol: 'vendedor', actualizadaEn: '2026-07-25T11:00:00Z',
      }],
      pagina: 1, tamano: 20, total: 1,
    });
    const cancelar = vi.spyOn(orders, 'cancelarOrden').mockResolvedValue();
    const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={cliente}>
        <MemoryRouter><MisAcuerdosPage /></MemoryRouter>
      </QueryClientProvider>,
    );
    fireEvent.click(screen.getByRole('tab', { name: 'Ventas' }));

    fireEvent.click(await screen.findByRole('button', { name: /cancelar acuerdo/i }));
    fireEvent.click(await screen.findByRole('button', { name: /confirmar/i }));

    await vi.waitFor(() => expect(cancelar).toHaveBeenCalledWith('o1'));
  });

  it('mantiene abierto el diálogo mientras la cancelación está pendiente', async () => {
    vi.spyOn(orders, 'listarOrdenes').mockResolvedValue({
      items: [{
        id: 'o1', avisoId: 'a1', estado: 'Requested', montoAcordado: 100,
        moneda: 'BOB', rol: 'comprador', actualizadaEn: '2026-07-25T00:00:00Z',
      }],
      pagina: 1, tamano: 20, total: 1,
    });
    vi.spyOn(orders, 'cancelarOrden').mockImplementation(() => new Promise(() => {}));
    const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={cliente}>
        <MemoryRouter><MisAcuerdosPage /></MemoryRouter>
      </QueryClientProvider>,
    );

    fireEvent.click(await screen.findByRole('button', { name: /cancelar solicitud/i }));
    fireEvent.click(screen.getByRole('button', { name: /confirmar/i }));

    const cerrar = screen.getByRole('button', { name: /^cancelar$/i });
    await vi.waitFor(() => expect(cerrar).toBeDisabled());
    fireEvent.keyDown(screen.getByRole('dialog'), { key: 'Escape' });

    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});
