import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { MisAvisosPage } from './MisAvisosPage';
import * as avisos from '../api/avisos';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

const activo: avisos.AvisoResumen = {
  id: 'a1',
  titulo: 'Mesa',
  monto: 300,
  moneda: 'BOB',
  categoriaId: 'c1',
  ciudadId: 'u1',
  condicion: 'Usado',
  estado: 'Activo',
  estadoModeracion: 'Visible',
  fechaCreacion: '2026-07-18T10:00:00Z',
  fotos: [],
};

function montar({ movil = false }: { movil?: boolean } = {}) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn().mockImplementation((consulta: string) => ({
      matches: movil && consulta.includes('max-width'),
      media: consulta,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  );
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
    expect(screen.getByRole('table', { name: 'Mis avisos' })).toBeInTheDocument();
  });

  it('muestra tarjetas sin tabla en móvil', async () => {
    vi.spyOn(avisos, 'listarMisAvisos').mockResolvedValue({
      items: [
        {
          ...activo,
          titulo: 'Mesa extensible de madera para comedor familiar',
          estadoModeracion: 'Oculto',
          fotos: [{ id: 'f1', url: '/mesa.jpg', orden: 0 }],
        },
      ],
      pagina: 1,
      tamano: 20,
      total: 1,
    });

    montar({ movil: true });

    expect(await screen.findByRole('list', { name: 'Mis avisos' })).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByRole('img', { name: /mesa extensible/i })).toHaveAttribute(
      'src',
      '/mesa.jpg',
    );
    expect(screen.getByText('Oculto por moderación')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pausar' })).toBeInTheDocument();
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

  it('muestra vendido sin acciones de mutación', async () => {
    vi.spyOn(avisos, 'listarMisAvisos').mockResolvedValue({
      items: [{ ...activo, estado: 'Vendido' }],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    montar();

    expect(await screen.findByText('Vendido')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: /editar|pausar|reactivar|eliminar/i }),
    ).not.toBeInTheDocument();
  });
});
