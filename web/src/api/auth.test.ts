import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from './http';
import { restablecerPassword, solicitarRestablecimientoPassword } from './auth';

describe('recuperación de contraseña', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

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
