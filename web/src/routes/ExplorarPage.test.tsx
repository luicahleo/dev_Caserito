import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { ExplorarPage } from './ExplorarPage';
import * as avisos from '../api/avisos';
import * as catalogo from '../api/catalogo';

afterEach(() => vi.restoreAllMocks());

const resumen: avisos.AvisoPublicoResumen = {
  id: 'a1',
  titulo: 'Silla de madera',
  monto: 150,
  moneda: 'BOB',
  nombreCategoria: 'Muebles',
  nombreCiudad: 'La Paz',
  condicion: 'Usado',
  fechaCreacion: '2026-07-18T10:00:00Z',
};

function montar() {
  vi.spyOn(catalogo, 'listarCategorias').mockResolvedValue([{ id: 'c1', nombre: 'Muebles' }]);
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([{ id: 'u1', nombre: 'La Paz' }]);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <ExplorarPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ExplorarPage', () => {
  it('muestra los avisos devueltos por la búsqueda', async () => {
    vi.spyOn(avisos, 'buscarAvisos').mockResolvedValue({
      items: [resumen],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    montar();
    expect(await screen.findByText('Silla de madera')).toBeInTheDocument();
  });

  it('muestra el estado vacío cuando no hay resultados', async () => {
    vi.spyOn(avisos, 'buscarAvisos').mockResolvedValue({
      items: [],
      pagina: 1,
      tamano: 20,
      total: 0,
    });
    montar();
    expect(await screen.findByText(/no se encontraron avisos/i)).toBeInTheDocument();
  });

  it('al buscar por texto re-consulta con el filtro q', async () => {
    const spy = vi.spyOn(avisos, 'buscarAvisos').mockResolvedValue({
      items: [resumen],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    montar();
    await screen.findByText('Silla de madera');

    await userEvent.type(screen.getByLabelText(/buscar/i), 'silla');
    await userEvent.click(screen.getByRole('button', { name: /buscar/i }));

    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(
        expect.objectContaining({ q: 'silla' }),
        1,
        expect.any(Number),
      ),
    );
  });
});
