import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { DetalleAvisoPage } from './DetalleAvisoPage';
import * as avisos from '../api/avisos';
import { HttpError } from '../api/http';

afterEach(() => vi.restoreAllMocks());

function montar(id: string) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[`/avisos/${id}`]}>
        <Routes>
          <Route path="/avisos/:id" element={<DetalleAvisoPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('DetalleAvisoPage', () => {
  it('muestra el detalle de un aviso', async () => {
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockResolvedValue({
      id: 'a1',
      titulo: 'Bicicleta',
      descripcion: 'Rodado 26, poco uso',
      monto: 800,
      moneda: 'BOB',
      nombreCategoria: 'Deportes',
      nombreCiudad: 'Cochabamba',
      condicion: 'Usado',
      fechaCreacion: '2026-07-18T10:00:00Z',
      fotos: [],
    });
    montar('a1');
    expect(await screen.findByText('Bicicleta')).toBeInTheDocument();
    expect(screen.getByText('Rodado 26, poco uso')).toBeInTheDocument();
  });

  it('muestra "no disponible" ante un 404', async () => {
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockRejectedValue(
      new HttpError(404, null, 'Petición fallida (404)'),
    );
    montar('inexistente');
    expect(await screen.findByText(/no está disponible/i)).toBeInTheDocument();
  });
});
