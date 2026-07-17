import { describe, it, expect } from 'vitest';
import { decodificarClaims } from './jwt';

// Construye un JWT de prueba (solo el payload importa; firma irrelevante).
function tokenCon(payload: Record<string, unknown>): string {
  const b64 = (o: unknown) =>
    btoa(JSON.stringify(o)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${b64({ alg: 'HS256' })}.${b64(payload)}.firma`;
}

describe('decodificarClaims', () => {
  it('devuelve claims vacíos si el token es null', () => {
    expect(decodificarClaims(null)).toEqual({ permisos: [], verificado: false });
  });

  it('devuelve claims vacíos si el token está malformado', () => {
    expect(decodificarClaims('no-es-jwt')).toEqual({ permisos: [], verificado: false });
  });

  it('lee un permiso único (string) y verificado=true', () => {
    const t = tokenCon({ perm: 'kyc.revisar', verificado: 'true' });
    expect(decodificarClaims(t)).toEqual({ permisos: ['kyc.revisar'], verificado: true });
  });

  it('lee múltiples permisos (array) y verificado=false', () => {
    const t = tokenCon({ perm: ['kyc.revisar', 'usuarios.gestionar'], verificado: 'false' });
    expect(decodificarClaims(t)).toEqual({
      permisos: ['kyc.revisar', 'usuarios.gestionar'],
      verificado: false,
    });
  });

  it('sin claim perm devuelve lista vacía', () => {
    const t = tokenCon({ verificado: 'true' });
    expect(decodificarClaims(t)).toEqual({ permisos: [], verificado: true });
  });
});
