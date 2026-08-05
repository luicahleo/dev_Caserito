import { describe, expect, it } from 'vitest';
import { normalizarErrorCamara } from './erroresCamara';

describe('normalizarErrorCamara', () => {
  it.each([
    ['NotAllowedError', 'permisoDenegado'],
    ['NotFoundError', 'sinCamara'],
    ['NotReadableError', 'noDisponible'],
    ['otro', 'desconocido'],
  ] as const)('normaliza %s sin conservar mensajes', (nombre, esperado) => {
    const error = new DOMException('dato sensible del dispositivo', nombre);
    expect(normalizarErrorCamara(error)).toEqual({ tipo: esperado });
  });
});
