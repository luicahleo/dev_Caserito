import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { RequierePermiso } from './RequierePermiso';
import * as ctx from './AuthContext';

afterEach(() => vi.restoreAllMocks());

function montarCon(permisos: string[]) {
  vi.spyOn(ctx, 'useAuth').mockReturnValue({
    usuario: { id: '1', email: 'a@b.co', nombre: 'Ana', ciudad: 'La Paz', verificado: false },
    estaAutenticado: true,
    cargando: false,
    permisos,
    verificado: false,
    identidadHabilitada: false,
    tienePermiso: (p: string) => permisos.includes(p),
    iniciarSesion: vi.fn(),
    registrar: vi.fn(),
    cerrarSesion: vi.fn(),
  } as ReturnType<typeof ctx.useAuth>);
  render(
    <MemoryRouter initialEntries={['/admin/kyc']}>
      <Routes>
        <Route
          path="/admin/kyc"
          element={
            <RequierePermiso permiso="kyc.revisar">
              <div>Panel admin</div>
            </RequierePermiso>
          }
        />
        <Route path="/perfil" element={<div>Mi perfil</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('RequierePermiso', () => {
  it('muestra el contenido si tiene el permiso', () => {
    montarCon(['kyc.revisar']);
    expect(screen.getByText('Panel admin')).toBeInTheDocument();
  });

  it('redirige a /perfil si falta el permiso', () => {
    montarCon([]);
    expect(screen.getByText('Mi perfil')).toBeInTheDocument();
    expect(screen.queryByText('Panel admin')).not.toBeInTheDocument();
  });
});
