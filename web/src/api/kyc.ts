import { getBlob, getJson, postForm, postJson } from './client';

export type EstadoKyc = 'NoIniciado' | 'Pendiente' | 'Aprobada' | 'Rechazada';

export interface EstadoKycDto {
  estado: EstadoKyc;
  motivoRechazo: string | null;
}

export interface SolicitudKycResumen {
  solicitudId: string;
  usuarioId: string;
  estado: EstadoKyc;
  tipoDocumento: string;
  enviadaEn: string;
  resueltaEn: string | null;
}

export interface PaginaSolicitudes {
  items: SolicitudKycResumen[];
  pagina: number;
  tamano: number;
  total: number;
}

export function obtenerEstadoKyc(): Promise<EstadoKycDto> {
  return getJson<EstadoKycDto>('/api/kyc/estado');
}

export function enviarKyc(documento: File, selfie: File): Promise<void> {
  const form = new FormData();
  form.append('documento', documento);
  form.append('selfie', selfie);
  return postForm('/api/kyc', form);
}

export function listarSolicitudesKyc(
  estado?: EstadoKyc,
  pagina = 1,
  tamano = 20,
): Promise<PaginaSolicitudes> {
  const params = new URLSearchParams();
  if (estado) params.set('estado', estado);
  params.set('pagina', String(pagina));
  params.set('tamano', String(tamano));
  return getJson<PaginaSolicitudes>(`/api/admin/kyc?${params.toString()}`);
}

export async function obtenerImagenKyc(
  solicitudId: string,
  tipo: 'documento' | 'selfie',
): Promise<string> {
  const blob = await getBlob(`/api/admin/kyc/${solicitudId}/${tipo}`);
  return URL.createObjectURL(blob);
}

export function aprobarKyc(solicitudId: string): Promise<void> {
  return postJson<void>(`/api/admin/kyc/${solicitudId}/aprobar`);
}

export function rechazarKyc(solicitudId: string, motivo: string): Promise<void> {
  return postJson<void>(`/api/admin/kyc/${solicitudId}/rechazar`, { motivo });
}
