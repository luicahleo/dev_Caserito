import { describe, it, expect, vi, afterEach } from 'vitest';
import * as client from './client';
import { enviarKyc, listarSolicitudesKyc, obtenerImagenKyc, rechazarKyc } from './kyc';

afterEach(() => vi.restoreAllMocks());

describe('api/kyc', () => {
  it('enviarKyc arma FormData con documento y selfie', async () => {
    const spy = vi.spyOn(client, 'postForm').mockResolvedValue(undefined);
    const doc = new File(['a'], 'doc.png', { type: 'image/png' });
    const selfie = new File(['b'], 'selfie.jpg', { type: 'image/jpeg' });
    await enviarKyc(doc, selfie);
    expect(spy).toHaveBeenCalledWith('/api/kyc', expect.any(FormData));
    const form = spy.mock.calls[0][1] as FormData;
    expect(form.get('documento')).toBe(doc);
    expect(form.get('selfie')).toBe(selfie);
  });

  it('listarSolicitudesKyc construye la query con filtro y paginación', async () => {
    const spy = vi
      .spyOn(client, 'getJson')
      .mockResolvedValue({ items: [], pagina: 1, tamano: 20, total: 0 });
    await listarSolicitudesKyc('Pendiente', 2, 10);
    expect(spy).toHaveBeenCalledWith('/api/admin/kyc?estado=Pendiente&pagina=2&tamano=10');
  });

  it('obtenerImagenKyc convierte el Blob en objectURL', async () => {
    vi.spyOn(client, 'getBlob').mockResolvedValue(new Blob(['x'], { type: 'image/png' }));
    const crear = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake');
    const url = await obtenerImagenKyc('abc', 'selfie');
    expect(crear).toHaveBeenCalled();
    expect(url).toBe('blob:fake');
  });

  it('rechazarKyc envía el motivo en el cuerpo', async () => {
    const spy = vi.spyOn(client, 'postJson').mockResolvedValue(undefined);
    await rechazarKyc('abc', 'documento ilegible');
    expect(spy).toHaveBeenCalledWith('/api/admin/kyc/abc/rechazar', { motivo: 'documento ilegible' });
  });
});
