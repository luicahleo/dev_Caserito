// Decodifica los claims propios de la app desde el payload del JWT en memoria.
// NO valida la firma (eso es del backend): solo lee claims para gatear la UI.
export interface ClaimsJwt {
  permisos: string[];
  verificado: boolean;
  identidadHabilitada: boolean;
}

const VACIO: ClaimsJwt = { permisos: [], verificado: false, identidadHabilitada: false };

export function decodificarClaims(token: string | null): ClaimsJwt {
  if (!token) return VACIO;
  const partes = token.split('.');
  if (partes.length !== 3) return VACIO;
  try {
    const base64 = partes[1].replace(/-/g, '+').replace(/_/g, '/');
    const json = atob(base64);
    const payload = JSON.parse(json) as Record<string, unknown>;
    const perm = payload['perm'];
    const permisos = Array.isArray(perm)
      ? perm.map(String)
      : typeof perm === 'string'
        ? [perm]
        : [];
    return {
      permisos,
      verificado: payload['verificado'] === 'true',
      identidadHabilitada: payload['identidadHabilitada'] === 'true',
    };
  } catch {
    return VACIO;
  }
}
