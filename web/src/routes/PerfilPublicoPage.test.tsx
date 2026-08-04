import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { PerfilPublicoPage } from './PerfilPublicoPage';
import * as reputation from '../api/reputation';
import { HttpError } from '../api/http';

afterEach(() => vi.restoreAllMocks());

describe('PerfilPublicoPage', () => {
  it('muestra perfil, resumen y reseñas sin identificadores internos', async () => {
    vi.spyOn(reputation, 'obtenerPerfilPublico').mockResolvedValue({
      id: 'id-no-visible',
      nombreVisible: 'Ana Q.',
      ciudadId: '22222222-2222-2222-2222-000000000003',
      nombreCiudad: 'La Paz',
      verificado: true,
      promedio: 4.5,
      totalResenas: 2,
    });
    vi.spyOn(reputation, 'listarResenasPublicas').mockResolvedValue({
      items: [
        {
          puntuacion: 5,
          comentario: 'Cumplió con todo lo acordado.',
          creadaEn: '2026-07-25T10:00:00Z',
          rolAutor: 'comprador',
        },
      ],
      pagina: 1,
      tamano: 10,
      total: 1,
    });
    montar();

    expect(await screen.findByRole('heading', { name: 'Ana Q.' })).toBeInTheDocument();
    expect(screen.getByText('La Paz')).toBeInTheDocument();
    expect(screen.getByText('Usuario verificado')).toBeInTheDocument();
    expect(screen.getByText(/4,5 de 5/i)).toBeInTheDocument();
    expect(await screen.findByText('Cumplió con todo lo acordado.')).toBeInTheDocument();
    expect(screen.getByText('Comprador')).toBeInTheDocument();
    expect(screen.queryByText('id-no-visible')).not.toBeInTheDocument();
  });

  it('muestra estado no disponible para un perfil inexistente', async () => {
    vi.spyOn(reputation, 'obtenerPerfilPublico').mockRejectedValue(
      new HttpError(404, 'reputacion_usuario_no_disponible', 'Petición fallida'),
    );
    vi.spyOn(reputation, 'listarResenasPublicas').mockRejectedValue(new Error('No disponible'));
    montar();

    expect(await screen.findByText(/perfil no está disponible/i)).toBeInTheDocument();
  });
});

function montar() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={['/usuarios/u1']}>
        <Routes>
          <Route path="/usuarios/:id" element={<PerfilPublicoPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
