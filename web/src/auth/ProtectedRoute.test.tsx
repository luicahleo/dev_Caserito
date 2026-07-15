import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { ProtectedRoute } from './ProtectedRoute';
import * as ctx from './AuthContext';

function montar(estado: Partial<ReturnType<typeof ctx.useAuth>>) {
  vi.spyOn(ctx, 'useAuth').mockReturnValue({
    usuario: null, estaAutenticado: false, cargando: false,
    iniciarSesion: vi.fn(), registrar: vi.fn(), cerrarSesion: vi.fn(),
    ...estado,
  } as ReturnType<typeof ctx.useAuth>);
  return render(
    <MemoryRouter initialEntries={['/perfil']}>
      <Routes>
        <Route path="/login" element={<div>pantalla login</div>} />
        <Route path="/perfil" element={<ProtectedRoute><div>perfil privado</div></ProtectedRoute>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('ProtectedRoute', () => {
  it('redirige a login si no autenticado', () => {
    montar({ estaAutenticado: false });
    expect(screen.getByText('pantalla login')).toBeInTheDocument();
  });
  it('renderiza el hijo si autenticado', () => {
    montar({ estaAutenticado: true });
    expect(screen.getByText('perfil privado')).toBeInTheDocument();
  });
});
