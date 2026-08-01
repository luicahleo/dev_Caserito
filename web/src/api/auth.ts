import { clearAccessToken, setAccessToken } from '../auth/session';
import { api, desempaquetar } from './http';

export interface RegistroDatos {
  email: string;
  password: string;
  nombre: string;
  ciudad: string;
}

export interface Credenciales {
  email: string;
  password: string;
}

export type ProveedorExterno = 'facebook' | 'google';
export interface LoginExternoPendiente {
  requiereEmail: boolean;
  requiereNombre: boolean;
  requiereCiudad: boolean;
  requiereVinculacion: boolean;
  nombreVisible: string | null;
}

export interface CompletarLoginExterno {
  email?: string;
  nombre?: string;
  ciudad?: string;
}

export function normalizarRetorno(retorno: unknown): string {
  return typeof retorno === 'string' && retorno.startsWith('/') &&
    !retorno.startsWith('//') && !retorno.startsWith('/\\') ? retorno : '/perfil';
}

export async function obtenerProveedores(): Promise<ProveedorExterno[]> {
  const respuesta = await fetch('/api/auth/external/providers', { credentials: 'include' });
  if (!respuesta.ok) throw new Error('No se pudieron consultar los proveedores.');
  return (await respuesta.json()) as ProveedorExterno[];
}

export async function obtenerLoginExternoPendiente(): Promise<LoginExternoPendiente> {
  const respuesta = await fetch('/api/auth/external/pending', { credentials: 'include' });
  if (!respuesta.ok) throw new Error('No se pudo consultar el acceso pendiente.');
  return (await respuesta.json()) as LoginExternoPendiente;
}

export async function completarLoginExterno(datos: CompletarLoginExterno): Promise<void> {
  const respuesta = await fetch('/api/auth/external/complete', {
    method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(datos),
  });
  if (!respuesta.ok) throw new Error('No se pudo completar el acceso externo.');
  const data = (await respuesta.json()) as { accessToken: string };
  setAccessToken(data.accessToken);
}

export async function vincularLoginExterno(): Promise<void> {
  const respuesta = await fetch('/api/auth/external/link', { method: 'POST', credentials: 'include' });
  if (!respuesta.ok) throw new Error('No se pudo vincular el acceso externo.');
}

export async function registrar(datos: RegistroDatos): Promise<void> {
  desempaquetar(await api.POST('/api/auth/register', { body: datos }));
}

export async function iniciarSesion(cred: Credenciales): Promise<void> {
  const data = desempaquetar(await api.POST('/api/auth/login', { body: cred }));
  setAccessToken((data as { accessToken: string }).accessToken);
}

export async function refrescar(): Promise<boolean> {
  const r = await api.POST('/api/auth/refresh');
  if (r.error !== undefined || !r.response.ok) return false;
  setAccessToken((r.data as { accessToken: string }).accessToken);
  return true;
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
