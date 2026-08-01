import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { LoginPage } from './LoginPage';
import * as ctx from '../auth/AuthContext';
import * as authApi from '../api/auth';

afterEach(() => {
  vi.restoreAllMocks();
});

function montar(iniciarSesion = vi.fn(), proveedores: Promise<authApi.ProveedorExterno[]> = Promise.resolve(['facebook', 'google'])) {
  vi.spyOn(authApi, 'obtenerProveedores').mockReturnValue(proveedores);
  vi.spyOn(ctx, 'useAuth').mockReturnValue({
    usuario: null,
    estaAutenticado: false,
    cargando: false,
    permisos: [],
    verificado: false,
    identidadHabilitada: false,
    tienePermiso: () => false,
    iniciarSesion,
    registrar: vi.fn(),
    cerrarSesion: vi.fn(),
    restaurarSesion: vi.fn(),
  } as ReturnType<typeof ctx.useAuth>);
  render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>,
  );
  return { iniciarSesion };
}

describe('LoginPage', () => {
  it('muestra Facebook y Google, en ese orden, antes del formulario tradicional', async () => {
    montar();

    const facebook = await screen.findByRole('link', { name: 'Continuar con Facebook' });
    const google = screen.getByRole('link', { name: 'Continuar con Google' });
    const entrar = screen.getByRole('button', { name: 'Entrar' });
    expect(facebook.compareDocumentPosition(google) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(google.compareDocumentPosition(entrar) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('oculta proveedores deshabilitados y conserva el login si falla la consulta', async () => {
    montar(vi.fn(), Promise.reject(new Error('fallo privado')));

    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument();
    expect(screen.queryByText(/Continuar con/)).not.toBeInTheDocument();
  });

  it('incluye únicamente un retorno local validado en el enlace externo', async () => {
    vi.spyOn(authApi, 'obtenerProveedores').mockResolvedValue(['facebook']);
    vi.spyOn(ctx, 'useAuth').mockReturnValue({
      usuario: null, estaAutenticado: false, cargando: false, permisos: [], verificado: false,
      identidadHabilitada: false, tienePermiso: () => false, iniciarSesion: vi.fn(),
      registrar: vi.fn(), cerrarSesion: vi.fn(), restaurarSesion: vi.fn(),
    });
    render(<MemoryRouter initialEntries={[{ pathname: '/login', state: { from: '//malicioso.test' } }]}><LoginPage /></MemoryRouter>);

    expect(await screen.findByRole('link', { name: 'Continuar con Facebook' })).toHaveAttribute(
      'href', '/api/auth/external/facebook/start?returnUrl=%2Fperfil',
    );
  });
  it('enlaza a la recuperación de contraseña', () => {
    montar();

    expect(screen.getByRole('link', { name: '¿Olvidaste tu contraseña?' })).toHaveAttribute(
      'href',
      '/olvide-password',
    );
  });

  it('llama a iniciarSesion con credenciales válidas', async () => {
    const usuarioEvento = userEvent.setup();
    const { iniciarSesion } = montar(vi.fn().mockResolvedValue(undefined));

    await usuarioEvento.type(screen.getByLabelText(/email/i), 'ana@example.com');
    await usuarioEvento.type(screen.getByLabelText(/contraseña/i), 'secreta123');
    await usuarioEvento.click(screen.getByRole('button', { name: /entrar/i }));

    expect(iniciarSesion).toHaveBeenCalledWith({
      email: 'ana@example.com',
      password: 'secreta123',
    });
  });

  it('muestra "Credenciales inválidas" si iniciarSesion rechaza', async () => {
    const usuarioEvento = userEvent.setup();
    montar(vi.fn().mockRejectedValue(new Error('401')));

    await usuarioEvento.type(screen.getByLabelText(/email/i), 'ana@example.com');
    await usuarioEvento.type(screen.getByLabelText(/contraseña/i), 'secreta123');
    await usuarioEvento.click(screen.getByRole('button', { name: /entrar/i }));

    expect(await screen.findByText('Credenciales inválidas')).toBeInTheDocument();
  });

  it('muestra error de validación con email inválido y no llama a iniciarSesion', async () => {
    const usuarioEvento = userEvent.setup();
    const { iniciarSesion } = montar();

    await usuarioEvento.type(screen.getByLabelText(/email/i), 'no-es-un-email');
    await usuarioEvento.type(screen.getByLabelText(/contraseña/i), 'secreta123');
    await usuarioEvento.click(screen.getByRole('button', { name: /entrar/i }));

    expect(await screen.findByText('Email inválido')).toBeInTheDocument();
    expect(iniciarSesion).not.toHaveBeenCalled();
  });
});
