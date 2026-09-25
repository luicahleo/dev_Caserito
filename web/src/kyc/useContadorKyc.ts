import { useQuery } from '@tanstack/react-query';
import { contarSolicitudesKycPendientes } from '../api/kyc';

export const claveContadorKyc = ['kyc-pendientes'] as const;

/**
 * Contador de solicitudes en espera para el menú del revisor. Sin tiempo real: el aviso
 * inmediato es el correo y este badge es una referencia de estado.
 */
export function useContadorKyc(activo = true): number {
  const consulta = useQuery({
    queryKey: claveContadorKyc,
    queryFn: contarSolicitudesKycPendientes,
    enabled: activo,
    refetchInterval: 300_000,
    refetchOnWindowFocus: true,
  });

  return consulta.data ?? 0;
}
