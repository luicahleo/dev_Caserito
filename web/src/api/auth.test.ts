import { beforeEach, describe, expect, it, vi } from 'vitest';
import { clearAccessToken, getAccessToken } from '../auth/session';
import { api } from './http';
import { refrescar, restablecerPassword, solicitarRestablecimientoPassword } from './auth';

beforeEach(() => {
  vi.restoreAllMocks();
  clearAccessToken();
});

describe('refrescar', () => {
  it('comparte una única renovación cuando hay llamadas concurrentes', async () => {
    let completar!: (respuesta: Response) => void;
    const respuestaPendiente = new Promise<Response>((resolve) => {
      completar = resolve;
    });
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockReturnValue(respuestaPendiente);

    const primera = refrescar();
    const segunda = refrescar();

    expect(fetchMock).toHaveBeenCalledTimes(1);

    completar(
      new Response(JSON.stringify({ accessToken: 'nuevo' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await expect(Promise.all([primera, segunda])).resolves.toEqual([true, true]);
    expect(getAccessToken()).toBe('nuevo');
  });

  it('permite volver a renovar después de finalizar la petición anterior', async () => {
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response('', { status: 401 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ accessToken: 'recuperado' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      );

    await expect(refrescar()).resolves.toBe(false);
    await expect(refrescar()).resolves.toBe(true);

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(getAccessToken()).toBe('recuperado');
  });
});

describe('recuperación de contraseña', () => {
  it('solicita el enlace con el correo indicado', async () => {
    const post = vi.spyOn(api, 'POST').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    });

    await solicitarRestablecimientoPassword('usuario@caserito.test');

    expect(post).toHaveBeenCalledWith('/api/auth/forgot-password', {
      body: { email: 'usuario@caserito.test' },
    });
  });

  it('envía solo usuario, token y contraseña nueva', async () => {
    const post = vi.spyOn(api, 'POST').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    });

    await restablecerPassword('usuario-id', 'token-sintetico', 'Password456!');

    expect(post).toHaveBeenCalledWith('/api/auth/reset-password', {
      body: {
        usuarioId: 'usuario-id',
        token: 'token-sintetico',
        password: 'Password456!',
      },
    });
  });
});
