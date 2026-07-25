import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { DetalleAcuerdoPage } from './DetalleAcuerdoPage';
import * as orders from '../api/orders';
import * as avisos from '../api/avisos';

afterEach(() => vi.restoreAllMocks());

describe('DetalleAcuerdoPage', () => {
  it('permite al vendedor aceptar una solicitud y aclara que no hay pago', async () => {
    vi.spyOn(orders, 'obtenerOrden').mockResolvedValue({
      id: 'o1', avisoId: 'a1', estado: 'Requested', montoAcordado: 75,
      moneda: 'BOB', rol: 'vendedor', creadaEn: '2026-07-25T10:00:00Z',
      actualizadaEn: '2026-07-25T10:00:00Z',
    });
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockResolvedValue({
      id: 'a1', titulo: 'Bicicleta', descripcion: '', monto: 75, moneda: 'BOB',
      nombreCategoria: '', nombreCiudad: '', condicion: 'Usado',
      fechaCreacion: '', fotos: [],
    });
    const aceptar = vi.spyOn(orders, 'aceptarOrden').mockResolvedValue();
    const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={cliente}>
        <MemoryRouter initialEntries={['/acuerdos/o1']}>
          <Routes><Route path="/acuerdos/:id" element={<DetalleAcuerdoPage />} /></Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Aceptar acuerdo' }));
    await vi.waitFor(() => expect(aceptar).toHaveBeenCalledWith('o1'));
    expect(screen.getByText(/no realiza ningún pago/i)).toBeInTheDocument();
  });
});
