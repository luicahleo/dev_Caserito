import type { components } from './schema';
import { api, desempaquetar, HttpError } from './http';

// El estado se expone como string en el contrato; el union preserva el uso en UI.
export type EstadoKyc = 'NoIniciado' | 'Pendiente' | 'Aprobada' | 'Rechazada';

export type EstadoKycDto = components['schemas']['EstadoKycDto'];
export type SolicitudKycResumen = components['schemas']['SolicitudKycResumenDto'];
export type PaginaSolicitudes = components['schemas']['ResultadoPaginadoOfSolicitudKycResumenDto'];

export async function obtenerEstadoKyc(): Promise<EstadoKycDto> {
  return desempaquetar(await api.GET('/api/kyc/estado'));
}

export async function enviarKyc(documento: File, selfie: File): Promise<void> {
  const form = new FormData();
  form.append('documento', documento);
  form.append('selfie', selfie);
  // El body real que viaja es el FormData: el bodySerializer por defecto de
  // openapi-fetch detecta instancias de FormData y las deja pasar tal cual (sin
  // tocarlas), dejando que el navegador fije el Content-Type con el boundary. El
  // tipo generado del contrato modela el multipart como un objeto { documento, selfie }
  // (IFormFile), por eso el cast: en tiempo de ejecución lo que se envía es el FormData.
  desempaquetar(
    await api.POST('/api/kyc', {
      body: form as unknown as { documento: string; selfie: string },
    }),
  );
}

export function listarSolicitudesKyc(
  estado?: EstadoKyc,
  pagina = 1,
  tamano = 20,
): Promise<PaginaSolicitudes> {
  return api
    .GET('/api/admin/kyc', { params: { query: { estado, pagina, tamano } } })
    .then((r) => desempaquetar(r));
}

export async function obtenerImagenKyc(
  solicitudId: string,
  tipo: 'documento' | 'selfie',
): Promise<string> {
  const ruta =
    tipo === 'documento'
      ? '/api/admin/kyc/{solicitudId}/documento'
      : '/api/admin/kyc/{solicitudId}/selfie';
  const r = await api.GET(ruta, {
    params: { path: { solicitudId } },
    parseAs: 'blob',
  });
  // No-PII: el mensaje de error no incluye la ruta con el id ni el contenido del blob.
  if (r.error !== undefined || !r.response.ok) {
    throw new HttpError(r.response.status, null, `Petición fallida (${r.response.status})`);
  }
  return URL.createObjectURL(r.data);
}

export async function aprobarKyc(solicitudId: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/kyc/{solicitudId}/aprobar', {
      params: { path: { solicitudId } },
    }),
  );
}

export async function rechazarKyc(solicitudId: string, motivo: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/kyc/{solicitudId}/rechazar', {
      params: { path: { solicitudId } },
      body: { motivo },
    }),
  );
}
