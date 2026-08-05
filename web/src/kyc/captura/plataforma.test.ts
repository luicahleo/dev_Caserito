import { describe, expect, it } from 'vitest';
import { esPwaMovilInstalada } from './plataforma';

const BASE = {
  displayStandalone: true,
  iosStandalone: false,
  userAgent: 'Mozilla/5.0 (Linux; Android 15)',
  platform: 'Linux armv8l',
  maxTouchPoints: 5,
  tieneCamaraWeb: true,
};

describe('esPwaMovilInstalada', () => {
  it.each([
    ['PWA Android', {}, true],
    ['PWA iPhone', { userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0)' }, true],
    [
      'PWA iPadOS con agente de escritorio',
      { userAgent: 'Mozilla/5.0 (Macintosh)', platform: 'MacIntel', maxTouchPoints: 5 },
      true,
    ],
    ['Safari iOS añadida a inicio', { displayStandalone: false, iosStandalone: true }, true],
    ['pestaña móvil', { displayStandalone: false }, false],
    [
      'PWA de escritorio',
      { userAgent: 'Mozilla/5.0 (Windows NT 10.0)', platform: 'Win32', maxTouchPoints: 0 },
      false,
    ],
    ['PWA móvil sin cámara web', { tieneCamaraWeb: false }, false],
  ])('%s', (_caso, cambios, esperado) => {
    expect(esPwaMovilInstalada({ ...BASE, ...cambios })).toBe(esperado);
  });
});
