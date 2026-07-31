import { describe, it, expect, vi, afterEach } from 'vitest';
import {
  enviarKyc,
  listarSolicitudesKyc,
  obtenerImagenKyc,
  rechazarKyc,
} from './kyc';
import { clearAccessToken } from '../auth/session';

afterEach(() => {
  vi.restoreAllMocks();
  clearAccessToken();
});

describe('api/kyc', () => {
  it('enviarKyc hace POST multipart a /api/kyc con documento y selfie', async () => {
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    const doc = new File(['a'], 'doc.png', { type: 'image/png' });
    const selfie = new File(['b'], 'selfie.jpg', { type: 'image/jpeg' });
    await enviarKyc(doc, selfie);
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.method).toBe('POST');
    expect(req.url).toContain('/api/kyc');
    // El body es FormData: el navegador fija el Content-Type con boundary, no lo hacemos nosotros.
    // Evitamos re-parsear con Request#formData() (choca entre el File de jsdom y el de
    // undici en este entorno de test); en cambio inspeccionamos el multipart ya serializado.
    expect(req.headers.get('content-type')).toContain('multipart/form-data');
    const crudo = await req.clone().text();
    expect(crudo).toContain('name="documento"');
    expect(crudo).toContain('Content-Type: image/png');
    expect(crudo).toContain('name="selfie"');
    expect(crudo).toContain('Content-Type: image/jpeg');
  });

  it('listarSolicitudesKyc arma la query con filtro y paginación', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(JSON.stringify({ items: [], pagina: 2, tamano: 10, total: 0 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    await listarSolicitudesKyc('Pendiente', 2, 10);
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.url).toContain('/api/admin/kyc');
    expect(req.url).toContain('estado=Pendiente');
    expect(req.url).toContain('pagina=2');
    expect(req.url).toContain('tamano=10');
  });

  it('obtenerImagenKyc convierte el Blob en objectURL', async () => {
    const contenido = new Uint8Array([0x89, 0x50, 0x4e, 0x47]);
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(contenido, {
        status: 200,
        headers: { 'Content-Type': 'image/png' },
      }),
    );
    const crear = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake');
    const url = await obtenerImagenKyc('abc', 'selfie');
    expect(crear).toHaveBeenCalledOnce();
    const blobRecibido = crear.mock.calls[0][0] as Blob;
    expect(blobRecibido.type).toBe('image/png');
    expect(blobRecibido.size).toBe(contenido.byteLength);
    expect(url).toBe('blob:fake');
  });

  it('rechazarKyc envía el motivo en el cuerpo JSON', async () => {
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    await rechazarKyc('abc', 'documento ilegible');
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.url).toContain('/api/admin/kyc/abc/rechazar');
    const body = await req.clone().text();
    expect(body).toContain('documento ilegible');
  });
});
