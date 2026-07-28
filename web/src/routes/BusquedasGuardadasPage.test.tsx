import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BusquedasGuardadasPage } from './BusquedasGuardadasPage';
import * as busquedas from '../api/busquedasGuardadas';

afterEach(() => vi.restoreAllMocks());

const busqueda: busquedas.BusquedaGuardada = {
  id: 'b1',
  palabraClave: 'silla',
  categoria: null,
  ciudad: 'Cochabamba',
  precioMinimo: null,
  precioMaximo: null,
  estadoProducto: null,
  creadaEn: '2026-07-28T10:00:00Z',
};

function montar() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <BusquedasGuardadasPage />
    </QueryClientProvider>,
  );
}

describe('BusquedasGuardadasPage', () => {
  it('muestra las búsquedas guardadas', async () => {
    vi.spyOn(busquedas, 'listarBusquedasGuardadas').mockResolvedValue([busqueda]);
    montar();

    expect(await screen.findByText('silla · Cochabamba')).toBeInTheDocument();
  });

  it('muestra mensaje vacío cuando no hay búsquedas', async () => {
    vi.spyOn(busquedas, 'listarBusquedasGuardadas').mockResolvedValue([]);
    montar();

    expect(await screen.findByText(/no tienes alertas/i)).toBeInTheDocument();
  });

  it('crea una nueva búsqueda al enviar el formulario', async () => {
    vi.spyOn(busquedas, 'listarBusquedasGuardadas').mockResolvedValue([]);
    const spy = vi.spyOn(busquedas, 'crearBusquedaGuardada').mockResolvedValue('b2');
    montar();

    await screen.findByText(/no tienes alertas/i);

    await userEvent.type(screen.getByLabelText(/palabra clave/i), 'mesa');
    await userEvent.type(screen.getByLabelText(/ciudad/i), 'Cochabamba');
    await userEvent.click(screen.getByRole('button', { name: /guardar alerta/i }));

    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(
        expect.objectContaining({ palabraClave: 'mesa', ciudad: 'Cochabamba' }),
        expect.anything(),
      ),
    );
  });

  it('elimina una búsqueda guardada', async () => {
    vi.spyOn(busquedas, 'listarBusquedasGuardadas').mockResolvedValue([busqueda]);
    const spy = vi.spyOn(busquedas, 'eliminarBusquedaGuardada').mockResolvedValue();
    montar();

    await screen.findByText('silla · Cochabamba');
    await userEvent.click(screen.getByRole('button', { name: /eliminar/i }));

    await waitFor(() => expect(spy).toHaveBeenCalledWith('b1', expect.anything()));
  });
});
