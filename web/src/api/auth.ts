import { clearAccessToken, setAccessToken } from '../auth/session';
import { api, desempaquetar } from './http';
import type { components } from './schema';

export interface RegistroDatos {
  email: string;
  password: string;
  nombres: string;
  apellidos: string;
  ciudadId: string;
}

export interface Credenciales {
  email: string;
  password: string;
}

export type ProveedorExterno = 'facebook' | 'google';
export type LoginExternoPendiente = components['schemas']['LoginExternoPendienteProyeccion'];
export type CompletarLoginExterno = components['schemas']['CompletarRegistroExternoRequest'];

export function normalizarRetorno(retorno: unknown): string {
  return typeof retorno === 'string' &&
    retorno.startsWith('/') &&
    !retorno.startsWith('//') &&
    !retorno.startsWith('/\\')
    ? retorno
    : '/perfil';
}

export async function obtenerProveedores(): Promise<ProveedorExterno[]> {
  const proveedores = desempaquetar(await api.GET('/api/auth/external/providers'));
  return proveedores.filter((p): p is ProveedorExterno => p === 'facebook' || p === 'google');
}

export async function obtenerLoginExternoPendiente(): Promise<LoginExternoPendiente> {
  return desempaquetar(await api.GET('/api/auth/external/pending'));
}

export async function completarLoginExterno(datos: CompletarLoginExterno): Promise<void> {
  const data = desempaquetar(await api.POST('/api/auth/external/complete', { body: datos }));
  setAccessToken(data.accessToken);
}

export async function vincularLoginExterno(): Promise<void> {
  desempaquetar(await api.POST('/api/auth/external/link'));
}

export async function registrar(datos: RegistroDatos): Promise<void> {
  desempaquetar(await api.POST('/api/auth/register', { body: datos }));
}

export async function iniciarSesion(cred: Credenciales): Promise<void> {
  const data = desempaquetar(await api.POST('/api/auth/login', { body: cred }));
  setAccessToken((data as { accessToken: string }).accessToken);
}

let renovacionEnCurso: Promise<boolean> | null = null;

async function ejecutarRenovacion(): Promise<boolean> {
  const r = await api.POST('/api/auth/refresh');
  if (r.error !== undefined || !r.response.ok) return false;
  setAccessToken((r.data as { accessToken: string }).accessToken);
  return true;
}

export function refrescar(): Promise<boolean> {
  renovacionEnCurso ??= ejecutarRenovacion().finally(() => {
    renovacionEnCurso = null;
  });
  return renovacionEnCurso;
}

export async function cerrarSesion(): Promise<void> {
  try {
    await api.POST('/api/auth/logout');
  } finally {
    clearAccessToken();
  }
}

export async function confirmarEmail(usuarioId: string, token: string): Promise<void> {
  desempaquetar(await api.POST('/api/auth/confirm-email', { body: { usuarioId, token } }));
}

export async function reenviarConfirmacionEmail(): Promise<void> {
  desempaquetar(await api.POST('/api/auth/resend-confirmation'));
}

export async function solicitarRestablecimientoPassword(email: string): Promise<void> {
  desempaquetar(await api.POST('/api/auth/forgot-password', { body: { email } }));
}

export async function restablecerPassword(
  usuarioId: string,
  token: string,
  password: string,
): Promise<void> {
  desempaquetar(
    await api.POST('/api/auth/reset-password', { body: { usuarioId, token, password } }),
  );
}
