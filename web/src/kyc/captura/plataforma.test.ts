import { describe, expect, it } from 'vitest';
import { esMovilConCamara } from './plataforma';

const BASE = {
  userAgent: 'Mozilla/5.0 (Linux; Android 15)',
  platform: 'Linux armv8l',
  maxTouchPoints: 5,
  tieneCamaraWeb: true,
};

describe('esMovilConCamara', () => {
  it.each([
    ['Chrome Android', {}, true],
    ['PWA Android', {}, true],
    ['Safari iPhone', { userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0)' }, true],
    [
      'Safari iPadOS con agente de escritorio',
      { userAgent: 'Mozilla/5.0 (Macintosh)', platform: 'MacIntel', maxTouchPoints: 5 },
      true,
    ],
    [
      'navegador de escritorio',
      { userAgent: 'Mozilla/5.0 (Windows NT 10.0)', platform: 'Win32', maxTouchPoints: 0 },
      false,
    ],
    ['móvil sin cámara web', { tieneCamaraWeb: false }, false],
  ])('%s', (_caso, cambios, esperado) => {
    expect(esMovilConCamara({ ...BASE, ...cambios })).toBe(esperado);
  });
});
