import { describe, it, expect, vi, afterEach } from 'vitest';
import { getHealth } from './health';

afterEach(() => vi.restoreAllMocks());

describe('getHealth', () => {
  it('devuelve el estado del backend', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ estado: 'ok' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    const resultado = await getHealth();
    expect(resultado.estado).toBe('ok');
  });

  it('lanza si la respuesta no es exitosa', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('', { status: 500 }));
    await expect(getHealth()).rejects.toThrow();
  });
});
