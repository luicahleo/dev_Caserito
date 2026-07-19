import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { MisAvisosPage } from './MisAvisosPage';
import * as avisos from '../api/avisos';

afterEach(() => vi.restoreAllMocks());

const activo: avisos.AvisoResumen = {
  id: 'a1',
  titulo: 'Mesa',
  monto: 300,
  moneda: 'BOB',
  categoriaId: 'c1',
  ciudadId: 'u1',
  condicion: 'Usado',
  estado: 'Activo',
  fechaCreacion: '2026-07-18T10:00:00Z',
  fotos: [],
};

function montar() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <MisAvisosPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('MisAvisosPage', () => {
  it('lista mis avisos', async () => {
    vi.spyOn(avisos, 'listarMisAvisos').mockResolvedValue({
      items: [activo],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    montar();
    expect(await screen.findByText('Mesa')).toBeInTheDocument();
  });

  it('pausar un aviso activo llama al endpoint', async () => {
    vi.spyOn(avisos, 'listarMisAvisos').mockResolvedValue({
      items: [activo],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    const pausar = vi.spyOn(avisos, 'pausarAviso').mockResolvedValue(undefined);
    montar();
    await screen.findByText('Mesa');
    await userEvent.click(screen.getByRole('button', { name: /pausar/i }));
    await waitFor(() => expect(pausar).toHaveBeenCalledWith('a1'));
  });

  it('eliminar pide confirmación antes de llamar al endpoint', async () => {
    vi.spyOn(avisos, 'listarMisAvisos').mockResolvedValue({
      items: [activo],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    const eliminar = vi.spyOn(avisos, 'eliminarAviso').mockResolvedValue(undefined);
    montar();
    await screen.findByText('Mesa');
    await userEvent.click(screen.getByRole('button', { name: /eliminar/i }));
    // El diálogo de confirmación aparece; recién al confirmar se llama al endpoint.
    expect(eliminar).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: /confirmar/i }));
    await waitFor(() => expect(eliminar).toHaveBeenCalledWith('a1'));
  });
});
