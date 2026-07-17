import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { PerfilPage } from './PerfilPage';
import * as ctx from '../auth/AuthContext';

afterEach(() => vi.restoreAllMocks());

function montar(over: Partial<ReturnType<typeof ctx.useAuth>>) {
  vi.spyOn(ctx, 'useAuth').mockReturnValue({
    usuario: { id: '1', email: 'a@b.co', nombre: 'Ana', ciudad: 'La Paz' },
    estaAutenticado: true,
    cargando: false,
    permisos: [],
    verificado: false,
    tienePermiso: () => false,
    iniciarSesion: vi.fn(),
    registrar: vi.fn(),
    cerrarSesion: vi.fn(),
    ...over,
  } as ReturnType<typeof ctx.useAuth>);
  render(
    <MemoryRouter>
      <PerfilPage />
    </MemoryRouter>,
  );
}

describe('PerfilPage — KYC', () => {
  it('muestra chip "Verificado" cuando verificado=true', () => {
    montar({ verificado: true });
    expect(screen.getByText(/verificado/i)).toBeInTheDocument();
  });

  it('muestra enlace de admin solo con permiso kyc.revisar', () => {
    montar({ tienePermiso: (p: string) => p === 'kyc.revisar' });
    expect(screen.getByRole('link', { name: /revisar verificaciones/i })).toBeInTheDocument();
  });

  it('oculta enlace de admin sin permiso', () => {
    montar({});
    expect(screen.queryByRole('link', { name: /revisar verificaciones/i })).not.toBeInTheDocument();
  });
});
