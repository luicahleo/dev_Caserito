import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { EditarAvisoPage } from './EditarAvisoPage';
import * as avisos from '../api/avisos';
import * as catalogo from '../api/catalogo';
import { HttpError } from '../api/http';

afterEach(() => vi.restoreAllMocks());

function montar(id: string) {
  vi.spyOn(catalogo, 'listarCategorias').mockResolvedValue([{ id: 'c1', nombre: 'Muebles' }]);
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([{ id: 'u1', nombre: 'La Paz' }]);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[`/mis-avisos/${id}/editar`]}>
        <Routes>
          <Route path="/mis-avisos/:id/editar" element={<EditarAvisoPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('EditarAvisoPage', () => {
  it('precarga el formulario con los valores del aviso', async () => {
    vi.spyOn(avisos, 'obtenerMiAviso').mockResolvedValue({
      id: 'a1',
      vendedorId: 'v1',
      titulo: 'Mesa antigua',
      descripcion: 'De roble',
      monto: 500,
      moneda: 'BOB',
      categoriaId: 'c1',
      ciudadId: 'u1',
      condicion: 'Usado',
      estado: 'Activo',
      estadoModeracion: 'Visible',
      fechaCreacion: '2026-07-18T10:00:00Z',
      fechaActualizacion: '2026-07-18T10:00:00Z',
      fotos: [],
    });
    montar('a1');
    expect(await screen.findByDisplayValue('Mesa antigua')).toBeInTheDocument();
  });

  it('muestra un mensaje si el aviso no existe o no es propio (404/403)', async () => {
    vi.spyOn(avisos, 'obtenerMiAviso').mockRejectedValue(
      new HttpError(404, null, 'Petición fallida (404)'),
    );
    montar('x');
    expect(await screen.findByText(/no se encontró/i)).toBeInTheDocument();
  });
});
