export type TipoErrorCamara =
  'permisoDenegado' | 'solicitarEnAjustes' | 'sinCamara' | 'noDisponible' | 'desconocido';

export interface ErrorCamara {
  tipo: TipoErrorCamara;
}

export function normalizarErrorCamara(error: unknown): ErrorCamara {
  if (error instanceof DOMException) {
    if (error.name === 'NotAllowedError') return { tipo: 'permisoDenegado' };
    if (error.name === 'NotFoundError') return { tipo: 'sinCamara' };
    if (error.name === 'NotReadableError') return { tipo: 'noDisponible' };
  }
  return { tipo: 'desconocido' };
}
