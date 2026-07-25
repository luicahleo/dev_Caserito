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
});
