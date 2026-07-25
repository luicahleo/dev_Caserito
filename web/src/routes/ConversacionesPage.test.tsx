import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { ConversacionesPage } from './ConversacionesPage';
import * as chat from '../api/chat';
import * as avisos from '../api/avisos';

afterEach(() => vi.restoreAllMocks());

function montar() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter>
        <ConversacionesPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ConversacionesPage', () => {
  it('muestra título, rol relativo y no leídos sin IDs técnicos', async () => {
    vi.spyOn(chat, 'listarConversaciones').mockResolvedValue({
      siguienteCursor: null,
      items: [
        {
          id: 'id-tecnico-conversacion',
          avisoId: 'id-tecnico-aviso',
          contraparteId: 'id-tecnico-persona',
          rol: 'Comprador',
          creadaEn: '2026-07-23T10:00:00Z',
          ultimaActividadEn: '2026-07-23T10:01:00Z',
          ultimaSecuencia: 3,
          noLeidos: 2,
          estado: 0,
          origenCierre: null,
          puedeEnviar: true,
        },
      ],
    });
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockResolvedValue({
      id: 'id-tecnico-aviso',
      vendedorId: 'id-vendedor',
      titulo: 'Mesa de madera',
      descripcion: '',
      monto: 10,
      moneda: 'BOB',
      nombreCategoria: '',
      nombreCiudad: '',
      condicion: 'Usado',
      fechaCreacion: '',
      fotos: [],
    });

    montar();

    expect(await screen.findByText('Mesa de madera')).toBeInTheDocument();
    expect(screen.getByText(/hablas con el vendedor/i)).toBeInTheDocument();
    expect(screen.getByText('2 sin leer')).toBeInTheDocument();
    expect(screen.queryByText('id-tecnico-persona')).not.toBeInTheDocument();
  });

  it('muestra el estado vacío', async () => {
    vi.spyOn(chat, 'listarConversaciones').mockResolvedValue({ items: [], siguienteCursor: null });
    montar();
    expect(await screen.findByText(/no tienes conversaciones/i)).toBeInTheDocument();
  });
});
