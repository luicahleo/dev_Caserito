import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PerfilPage } from './PerfilPage';
import * as ctx from '../auth/AuthContext';
import * as catalogo from '../api/catalogo';

afterEach(() => vi.restoreAllMocks());

function montar(over: Partial<ReturnType<typeof ctx.useAuth>>) {
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([
    { id: '22222222-2222-2222-2222-000000000003', nombre: 'La Paz' },
  ]);
  vi.spyOn(ctx, 'useAuth').mockReturnValue({
    usuario: { id: '1', email: 'a@b.co', nombres: 'Ana', apellidos: 'Quispe',
      ciudadId: '22222222-2222-2222-2222-000000000003', nombreCiudad: 'La Paz', verificado: false },
    estaAutenticado: true,
    cargando: false,
    permisos: [],
    verificado: false,
    identidadHabilitada: false,
    tienePermiso: () => false,
    iniciarSesion: vi.fn(),
    registrar: vi.fn(),
    cerrarSesion: vi.fn(),
    restaurarSesion: vi.fn(),
    ...over,
  } as ReturnType<typeof ctx.useAuth>);
  render(<QueryClientProvider client={new QueryClient()}>
    <MemoryRouter>
      <PerfilPage />
    </MemoryRouter></QueryClientProvider>);
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
