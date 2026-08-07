import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { FormAviso, type ValoresAviso } from './FormAviso';
import * as catalogo from '../api/catalogo';
import { esMovilConCamara } from '../kyc/captura/plataforma';

vi.mock('../kyc/captura/plataforma', () => ({
  esMovilConCamara: vi.fn(() => false),
}));

afterEach(() => vi.restoreAllMocks());

function montar(onSubmit = vi.fn(), inicial?: Partial<ValoresAviso>) {
  vi.spyOn(catalogo, 'listarCategorias').mockResolvedValue([{ id: 'c1', nombre: 'Muebles' }]);
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([{ id: 'u1', nombre: 'La Paz' }]);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <FormAviso inicial={inicial} enviando={false} textoBoton="Publicar" onSubmit={onSubmit} />
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

    await userEvent.type(screen.getByLabelText(/título/i), 'Mesa');
    await userEvent.type(screen.getByLabelText(/descripción/i), 'De madera');
    await userEvent.type(screen.getByLabelText(/precio/i), '300');
    await userEvent.click(screen.getByRole('button', { name: /publicar/i }));

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
});
