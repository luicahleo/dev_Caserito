import { afterEach, describe, expect, it, vi } from 'vitest';
import { LIMITE_ENTRADA_FOTO, LIMITE_SALIDA_FOTO, procesarFotoAviso } from './procesarFotoAviso';

afterEach(() => vi.restoreAllMocks());

function prepararCanvas(tamanos: number[]) {
  const contexto = { fillStyle: '', fillRect: vi.fn(), drawImage: vi.fn() };
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
    contexto as unknown as CanvasRenderingContext2D,
  );
  vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation((callback, tipo) => {
    const tamano = tamanos.shift() ?? 100;
    callback(new Blob([new Uint8Array(tamano)], { type: tipo ?? 'image/jpeg' }));
  });
  return contexto;
}

function simularImagen(ancho: number, alto: number) {
  const close = vi.fn();
  const createImageBitmap = vi.fn().mockResolvedValue({ width: ancho, height: alto, close });
  vi.stubGlobal('createImageBitmap', createImageBitmap);
  return { createImageBitmap, close };
}

describe('procesarFotoAviso', () => {
  it.each(['image/jpeg', 'image/png'])('convierte %s a JPEG sin superar 1600 px', async (tipo) => {
    const contexto = prepararCanvas([500]);
    const imagen = simularImagen(3200, 2400);

    const salida = await procesarFotoAviso(new File(['foto'], 'privado.png', { type: tipo }));

    expect(salida.type).toBe('image/jpeg');
    expect(salida.name).toBe('foto-aviso.jpg');
    expect(contexto.fillStyle).toBe('#ffffff');
    expect(contexto.fillRect).toHaveBeenCalledWith(0, 0, 1600, 1200);
    expect(contexto.drawImage).toHaveBeenCalled();
    expect(imagen.createImageBitmap).toHaveBeenCalledWith(
      expect.any(File),
      expect.objectContaining({ imageOrientation: 'from-image' }),
    );
    expect(imagen.close).toHaveBeenCalled();
  });

  it('reduce calidad y dimensiones hasta el objetivo de 1 MiB', async () => {
    prepararCanvas([
      LIMITE_SALIDA_FOTO + 1,
      LIMITE_SALIDA_FOTO + 1,
      LIMITE_SALIDA_FOTO + 1,
      LIMITE_SALIDA_FOTO + 1,
      LIMITE_SALIDA_FOTO + 1,
      LIMITE_SALIDA_FOTO + 1,
      900_000,
    ]);
    simularImagen(2000, 1000);

    const salida = await procesarFotoAviso(new File(['foto'], 'foto.jpg', { type: 'image/jpeg' }));

    expect(salida.size).toBe(900_000);
    expect(salida.size).toBeLessThanOrEqual(LIMITE_SALIDA_FOTO);
  });

  it('rechaza formatos no soportados sin intentar decodificarlos', async () => {
    const createImageBitmap = vi.fn();
    vi.stubGlobal('createImageBitmap', createImageBitmap);

    await expect(
      procesarFotoAviso(new File(['gif'], 'foto.gif', { type: 'image/gif' })),
    ).rejects.toThrow('No se pudo preparar la foto.');
    expect(createImageBitmap).not.toHaveBeenCalled();
  });

  it('rechaza entradas que superan el límite absoluto', async () => {
    const archivo = new File([new Uint8Array(LIMITE_ENTRADA_FOTO + 1)], 'foto.jpg', {
      type: 'image/jpeg',
    });

    await expect(procesarFotoAviso(archivo)).rejects.toThrow('No se pudo preparar la foto.');
  });

  it('convierte los errores de decodificación en un error genérico', async () => {
    vi.stubGlobal('createImageBitmap', vi.fn().mockRejectedValue(new Error('metadata privada')));

    await expect(
      procesarFotoAviso(new File(['corrupta'], 'foto.jpg', { type: 'image/jpeg' })),
    ).rejects.toThrow('No se pudo preparar la foto.');
  });
});
