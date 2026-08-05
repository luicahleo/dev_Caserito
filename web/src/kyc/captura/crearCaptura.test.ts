import { afterEach, describe, expect, it, vi } from 'vitest';
import { crearCaptura } from './crearCaptura';

afterEach(() => vi.restoreAllMocks());

describe('crearCaptura', () => {
  it('genera un JPEG con nombre neutro y limita el lado mayor', async () => {
    const contexto = { drawImage: vi.fn() };
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
      contexto as unknown as CanvasRenderingContext2D,
    );
    vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation((callback) =>
      callback(new Blob(['imagen'], { type: 'image/jpeg' })),
    );

    const archivo = await crearCaptura(
      document.createElement('video'),
      { x: 0, y: 0, ancho: 2400, alto: 1200 },
      'documento-ci.jpg',
    );

    expect(archivo.name).toBe('documento-ci.jpg');
    expect(archivo.type).toBe('image/jpeg');
    expect(contexto.drawImage).toHaveBeenCalled();
  });
});
