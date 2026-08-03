import { describe, it, expect, vi, afterEach } from 'vitest';
import { act, cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import * as authCtx from '../auth/AuthContext';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as chat from '../api/chat';
import * as notificaciones from '../api/notificaciones';

afterEach(() => vi.restoreAllMocks());

function mockAuth(estaAutenticado: boolean, permisos: string[] = []) {
  vi.spyOn(authCtx, 'useAuth').mockReturnValue({
    estaAutenticado,
    verificado: false,
    identidadHabilitada: false,
    cargando: false,
    usuario: null,
    permisos,
    tienePermiso: (permiso: string) => permisos.includes(permiso),
    iniciarSesion: vi.fn(),
    registrar: vi.fn(),
    cerrarSesion: vi.fn(),
  } as never);
}

function montar() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AppLayout />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('AppLayout', () => {
  it('muestra enlaces legales públicos en el pie de página', () => {
    mockAuth(false);
    montar();

    expect(screen.getByRole('link', { name: 'Privacidad' })).toHaveAttribute('href', '/privacidad');
    expect(screen.getByRole('link', { name: 'Términos' })).toHaveAttribute('href', '/terminos');
    expect(screen.getByRole('link', { name: 'Cookies' })).toHaveAttribute('href', '/cookies');
    expect(screen.getByRole('link', { name: 'Contacto' })).toHaveAttribute('href', '/contacto');
    expect(screen.getByRole('link', { name: 'Eliminar mis datos' })).toHaveAttribute(
      'href',
      '/eliminacion-de-datos',
    );
  });

  it('muestra "Entrar" cuando no hay sesión', () => {
    mockAuth(false);
    montar();
    expect(screen.getByRole('link', { name: /explorar/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /entrar/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /mis avisos/i })).not.toBeInTheDocument();
  });

  it('no consulta notificaciones cuando no hay sesión', async () => {
    mockAuth(false);
    const listar = vi.spyOn(notificaciones, 'listarNotificaciones');

    montar();

    await act(async () => Promise.resolve());
    expect(listar).not.toHaveBeenCalled();
  });

  it('muestra enlaces del dueño cuando hay sesión', () => {
    mockAuth(true);
    montar();
    expect(screen.getByRole('link', { name: /publicar/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /mis avisos/i })).toBeInTheDocument();
  });

  it('muestra Moderación de chat únicamente con chat.moderar', () => {
    mockAuth(true, ['chat.moderar']);
    montar();
    expect(
      screen.getByRole('link', { name: /^moderación de chat$/i }),
    ).toHaveAttribute('href', '/admin/moderacion-chat');

    cleanup();
    vi.restoreAllMocks();
    mockAuth(true);
    montar();
    expect(
      screen.queryByRole('link', { name: /^moderación de chat$/i }),
    ).not.toBeInTheDocument();
  });

  it('muestra el contador global básico de mensajes no leídos', async () => {
    mockAuth(true);
    vi.spyOn(chat, 'listarConversaciones').mockResolvedValue({
      siguienteCursor: null,
      items: [
        {
          id: 'c1', avisoId: 'a1', contraparteId: 'u2', rol: 'Comprador',
          creadaEn: '', ultimaActividadEn: '', ultimaSecuencia: 3, noLeidos: 3,
          estado: 0, origenCierre: null, puedeEnviar: true,
        },
      ],
    });
    montar();
    expect(await screen.findByRole('link', { name: 'Mensajes, 3 no leídos' })).toBeInTheDocument();
  });

  it('muestra el badge de notificaciones con el conteo', async () => {
    mockAuth(true);
    vi.spyOn(notificaciones, 'contarNoLeidas').mockResolvedValue(5);
    montar();

    expect(await screen.findByLabelText('Notificaciones')).toBeInTheDocument();
    expect(await screen.findByText('5')).toBeInTheDocument();
  });
});
