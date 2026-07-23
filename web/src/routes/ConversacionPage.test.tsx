import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ConversacionPage } from './ConversacionPage';
import * as chat from '../api/chat';
import * as authCtx from '../auth/AuthContext';

vi.mock('../chat/tiempoReal', () => ({
  crearClienteTiempoReal: () => ({
    alRecibirMensaje: vi.fn(), suscribir: vi.fn().mockResolvedValue(undefined),
    desuscribir: vi.fn().mockResolvedValue(undefined), detener: vi.fn().mockResolvedValue(undefined),
    revocar: vi.fn(),
  }),
}));

afterEach(() => vi.restoreAllMocks());

function montar() {
  vi.spyOn(authCtx, 'useAuth').mockReturnValue({ usuario: { id: 'yo' } } as never);
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={['/mensajes/c1']}>
        <Routes><Route path="/mensajes/:id" element={<ConversacionPage />} /></Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ConversacionPage', () => {
  it('muestra historial y deshabilita el compositor cuando no puede enviar', async () => {
    vi.spyOn(chat, 'listarConversaciones').mockResolvedValue({
      siguienteCursor: null,
      items: [{
        id: 'c1', avisoId: 'a1', contraparteId: 'otra-persona', rol: 'Comprador',
        creadaEn: '', ultimaActividadEn: '', ultimaSecuencia: 1, noLeidos: 1,
        estado: 0, origenCierre: null, puedeEnviar: false,
      }],
    });
    vi.spyOn(chat, 'obtenerMensajes').mockResolvedValue({
      siguienteCursor: null,
      items: [{ id: 'm1', conversacionId: 'c1', remitenteId: 'otra-persona', secuencia: 1, texto: '¿Sigue disponible?', enviadoEn: '' }],
    });
    vi.spyOn(chat, 'marcarLectura').mockResolvedValue();

    montar();

    expect(await screen.findByText('¿Sigue disponible?')).toBeInTheDocument();
    expect(screen.getByText(/no admite nuevos mensajes/i)).toBeInTheDocument();
    expect(screen.getByLabelText('Mensaje')).toBeDisabled();
    expect(screen.queryByText('otra-persona')).not.toBeInTheDocument();
  });
});
