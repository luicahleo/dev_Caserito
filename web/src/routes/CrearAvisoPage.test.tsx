import { describe, it, expect, vi, afterEach } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { CrearAvisoPage } from './CrearAvisoPage';
import * as authCtx from '../auth/AuthContext';
import * as catalogo from '../api/catalogo';
import * as avisos from '../api/avisos';

vi.mock('./FormAviso', () => ({
  FormAviso: ({
    onSubmit,
    onFotasLocalesChange,
  }: {
    onSubmit: (valores: Record<string, string>) => void;
    onFotasLocalesChange?: (archivos: File[]) => void;
  }) => (
    <>
      <input aria-label="Título" />
      <button
        type="button"
        onClick={() =>
          onFotasLocalesChange?.([new File(['foto'], 'foto-aviso.jpg', { type: 'image/jpeg' })])
        }
      >
        Elegir foto
      </button>
      <button
        type="button"
        onClick={() =>
          onSubmit({
            titulo: 'Mesa',
            descripcion: 'Mesa de madera',
            monto: '300',
            condicion: 'Usado',
            categoriaId: 'c1',
            ciudadId: 'u1',
          })
        }
      >
        Publicar
      </button>
    </>
  ),
}));

afterEach(() => vi.restoreAllMocks());

function mockAuth(verificado: boolean, identidadHabilitada = verificado) {
  vi.spyOn(authCtx, 'useAuth').mockReturnValue({
    verificado,
    identidadHabilitada,
    estaAutenticado: true,
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
  vi.spyOn(catalogo, 'listarCategorias').mockResolvedValue([{ id: 'c1', nombre: 'Muebles' }]);
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([{ id: 'u1', nombre: 'La Paz' }]);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <CrearAvisoPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('CrearAvisoPage', () => {
  it('muestra el gate KYC si el usuario no está verificado', () => {
    mockAuth(false);
    montar();
    expect(screen.getByText(/verificar tu identidad/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/título/i)).not.toBeInTheDocument();
  });

  it('muestra el formulario si el usuario está verificado', () => {
    mockAuth(true);
    montar();
    expect(screen.getByLabelText(/título/i)).toBeInTheDocument();
  });

  it('muestra el formulario al administrador sin KYC', () => {
    mockAuth(false, true);
    montar();
    expect(screen.getByLabelText(/t.tulo/i)).toBeInTheDocument();
  });

  it('informa si falla la subida de una foto y no oculta el error', async () => {
    mockAuth(true);
    vi.spyOn(avisos, 'crearAviso').mockResolvedValue({ id: 'aviso-1' });
    vi.spyOn(avisos, 'subirFotoAviso').mockRejectedValue(new Error('detalle privado'));
    montar();

    fireEvent.click(screen.getByRole('button', { name: /elegir foto/i }));
    fireEvent.click(screen.getByRole('button', { name: /publicar/i }));

    expect(
      await screen.findByText(/el aviso se creó, pero no se pudieron subir todas las fotos/i),
    ).toBeInTheDocument();
    expect(avisos.subirFotoAviso).toHaveBeenCalledWith(
      'aviso-1',
      expect.objectContaining({ type: 'image/jpeg' }),
    );
  });
});
