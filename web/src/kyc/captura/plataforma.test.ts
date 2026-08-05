import { describe, expect, it } from 'vitest';
import { esAndroidNativo } from './plataforma';

describe('esAndroidNativo', () => {
  it.each([
    ['navegador Android', true, 'web', false],
    ['aplicación iOS', true, 'ios', false],
    ['navegador de escritorio', false, 'web', false],
    ['aplicación Android', true, 'android', true],
  ])('%s', (_caso, esNativo, plataforma, esperado) => {
    expect(
      esAndroidNativo({
        isNativePlatform: () => esNativo,
        getPlatform: () => plataforma,
      }),
    ).toBe(esperado);
  });
});
