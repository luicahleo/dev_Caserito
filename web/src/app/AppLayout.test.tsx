import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import * as authCtx from '../auth/AuthContext';

afterEach(() => vi.restoreAllMocks());

function mockAuth(estaAutenticado: boolean) {
  vi.spyOn(authCtx, 'useAuth').mockReturnValue({
    estaAutenticado,
    verificado: false,
    cargando: false,
    usuario: null,
    permisos: [],
    tienePermiso: () => false,
    iniciarSesion: vi.fn(),
    registrar: vi.fn(),
    cerrarSesion: vi.fn(),
  } as never);
}

function montar() {
  render(
    <MemoryRouter>
      <AppLayout />
    </MemoryRouter>,
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
});
