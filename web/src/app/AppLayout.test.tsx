import { describe, it, expect, vi, afterEach } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import * as authCtx from '../auth/AuthContext';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

afterEach(() => vi.restoreAllMocks());

function mockAuth(estaAutenticado: boolean, permisos: string[] = []) {
  vi.spyOn(authCtx, 'useAuth').mockReturnValue({
    estaAutenticado,
    verificado: false,
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
  it('muestra "Entrar" cuando no hay sesión', () => {
    mockAuth(false);
    montar();
    expect(screen.getByRole('link', { name: /explorar/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /entrar/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /mis avisos/i })).not.toBeInTheDocument();
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
});
