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
