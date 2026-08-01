import { describe, expect, it } from 'vitest';
import { rutasExcluidasFallbackPwa } from './navigation';

const quedaExcluida = (ruta: string) => rutasExcluidasFallbackPwa.some((patron) => patron.test(ruta));

describe('fallback de navegación PWA', () => {
  it.each([
    '/api',
    '/api/auth/external/google/start?returnUrl=%2Fperfil',
    '/api/auth/external/facebook/start?returnUrl=%2Fperfil',
    '/health',
  ])('no atiende %s con el shell de la SPA', (ruta) => {
    expect(quedaExcluida(ruta)).toBe(true);
  });

  it.each(['/', '/login', '/perfil'])('mantiene el fallback para la ruta SPA %s', (ruta) => {
    expect(quedaExcluida(ruta)).toBe(false);
  });
});
