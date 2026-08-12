import { describe, it, expect, vi, afterEach, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { FormAviso, type ValoresAviso } from './FormAviso';
import * as catalogo from '../api/catalogo';
import { esMovilConCamara } from '../kyc/captura/plataforma';
import { procesarFotoAviso } from '../avisos/fotos/procesarFotoAviso';

vi.mock('../kyc/captura/plataforma', () => ({
  esMovilConCamara: vi.fn(() => false),
}));

vi.mock('../avisos/fotos/procesarFotoAviso', () => ({
  procesarFotoAviso: vi.fn((archivo: File) => Promise.resolve(archivo)),
}));

afterEach(() => vi.restoreAllMocks());
beforeEach(() => {
  vi.mocked(esMovilConCamara).mockReturnValue(false);
  vi.mocked(procesarFotoAviso).mockReset();
  vi.mocked(procesarFotoAviso).mockImplementation((archivo) => Promise.resolve(archivo));
});

function montar(
  onSubmit = vi.fn(),
  inicial?: Partial<ValoresAviso>,
  onFotasLocalesChange?: (archivos: File[]) => void,
) {
  vi.spyOn(catalogo, 'listarCategorias').mockResolvedValue([{ id: 'c1', nombre: 'Muebles' }]);
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([{ id: 'u1', nombre: 'La Paz' }]);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <FormAviso
        inicial={inicial}
        enviando={false}
        textoBoton="Publicar"
        onSubmit={onSubmit}
        onFotasLocalesChange={onFotasLocalesChange}
      />
    </QueryClientProvider>,
  );
  return onSubmit;
}

describe('FormAviso', () => {
  it('no envía y muestra errores si faltan campos requeridos', async () => {
    const onSubmit = montar();
    await screen.findByRole('button', { name: /publicar/i }); // formulario listo
    await userEvent.click(screen.getByRole('button', { name: /publicar/i }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/el título es obligatorio/i)).toBeInTheDocument();
  });

  it('envía los valores cuando el formulario es válido', async () => {
    // Precargamos categoría/ciudad vía `inicial` para no depender de interactuar
    // con los <select> de MUI, que resulta frágil con userEvent.
    const onSubmit = montar(vi.fn(), { categoriaId: 'c1', ciudadId: 'u1' });
    await screen.findByRole('button', { name: /publicar/i }); // formulario listo

    fireEvent.change(screen.getByLabelText(/título/i), { target: { value: 'Mesa' } });
    fireEvent.change(screen.getByLabelText(/descripción/i), { target: { value: 'De madera' } });
    fireEvent.change(screen.getByLabelText(/precio/i), { target: { value: '300' } });
    fireEvent.click(screen.getByRole('button', { name: /publicar/i }));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ titulo: 'Mesa', descripcion: 'De madera', monto: '300' }),
    );
  });

  it('en escritorio solo ofrece subir fotos, sin opción de cámara', async () => {
    vi.mocked(esMovilConCamara).mockReturnValue(false);
    montar();
    await screen.findByRole('button', { name: /agregar fotos/i });
    expect(screen.getByRole('button', { name: /agregar fotos/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /tomar foto/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /subir de galería/i })).not.toBeInTheDocument();
    expect(screen.getByLabelText(/agregar fotos/i)).toHaveAttribute('accept', 'image/*');
    expect(screen.getByLabelText(/agregar fotos/i)).not.toHaveAttribute('capture');
  });

  it('en móvil con cámara ofrece tomar foto y subir de galería', async () => {
    vi.mocked(esMovilConCamara).mockReturnValue(true);
    montar();
    await screen.findByRole('button', { name: /tomar foto/i });
    expect(screen.getByRole('button', { name: /tomar foto/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /subir de galería/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/tomar foto/i)).toHaveAttribute('capture', 'environment');
    expect(screen.getByLabelText(/tomar foto/i)).toHaveAttribute('accept', 'image/*');
    expect(screen.getByLabelText(/subir de galería/i)).toHaveAttribute('accept', 'image/*');
    expect(screen.getByLabelText(/subir de galería/i)).not.toHaveAttribute('capture');
  });

  it('prepara secuencialmente varias fotos de galería antes del preview', async () => {
    const onFotos = vi.fn();
    montar(vi.fn(), undefined, onFotos);
    const archivos = [
      new File(['uno'], 'uno.png', { type: 'image/png' }),
      new File(['dos'], 'dos.jpg', { type: 'image/jpeg' }),
    ];

    fireEvent.change(await screen.findByLabelText(/agregar fotos/i), {
      target: { files: archivos },
    });

    await waitFor(() => expect(procesarFotoAviso).toHaveBeenCalledTimes(2));
    expect(onFotos).toHaveBeenLastCalledWith(archivos);
    expect(screen.getAllByAltText(/previsualización/i)).toHaveLength(2);
  });

  it('prepara también la foto tomada con la cámara', async () => {
    vi.mocked(esMovilConCamara).mockReturnValue(true);
    montar();
    const archivo = new File(['camara'], 'captura.jpg', { type: 'image/jpeg' });

    fireEvent.change(await screen.findByLabelText(/tomar foto/i), {
      target: { files: [archivo] },
    });

    await waitFor(() => expect(procesarFotoAviso).toHaveBeenCalledWith(archivo));
    expect(screen.getByAltText(/previsualización/i)).toBeInTheDocument();
  });

  it('muestra Preparando fotos y bloquea publicar mientras procesa', async () => {
    let terminar!: (archivo: File) => void;
    vi.mocked(procesarFotoAviso).mockImplementation(
      () => new Promise<File>((resolve) => (terminar = resolve)),
    );
    montar();
    const archivo = new File(['foto'], 'foto.jpg', { type: 'image/jpeg' });

    fireEvent.change(await screen.findByLabelText(/agregar fotos/i), {
      target: { files: [archivo] },
    });

    expect(await screen.findByText(/preparando fotos/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /publicar/i })).toBeDisabled();
    terminar(archivo);
    await waitFor(() => expect(screen.queryByText(/preparando fotos/i)).not.toBeInTheDocument());
  });

  it('conserva las fotos válidas e informa si otra no puede prepararse', async () => {
    vi.mocked(procesarFotoAviso)
      .mockResolvedValueOnce(new File(['ok'], 'foto-aviso.jpg', { type: 'image/jpeg' }))
      .mockRejectedValueOnce(new Error('privado'));
    const onFotos = vi.fn();
    montar(vi.fn(), undefined, onFotos);

    fireEvent.change(await screen.findByLabelText(/agregar fotos/i), {
      target: {
        files: [
          new File(['ok'], 'ok.jpg', { type: 'image/jpeg' }),
          new File(['mal'], 'mal.png', { type: 'image/png' }),
        ],
      },
    });

    expect(await screen.findByText(/una o más fotos no se pudieron preparar/i)).toBeInTheDocument();
    expect(screen.getAllByAltText(/previsualización/i)).toHaveLength(1);
    expect(onFotos).toHaveBeenLastCalledWith([expect.objectContaining({ name: 'foto-aviso.jpg' })]);
  });

  it('mantiene el máximo de cinco fotos antes de procesar', async () => {
    montar();
    const archivos = Array.from(
      { length: 6 },
      (_, indice) => new File(['foto'], `${indice}.jpg`, { type: 'image/jpeg' }),
    );

    fireEvent.change(await screen.findByLabelText(/agregar fotos/i), {
      target: { files: archivos },
    });

    expect(await screen.findByText(/máx\. 5/i)).toBeInTheDocument();
    expect(procesarFotoAviso).not.toHaveBeenCalled();
  });
});
