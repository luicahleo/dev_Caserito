import { clearAccessToken, setAccessToken } from '../auth/session';
import { postJson } from './client';

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
  await postJson('/api/auth/register', datos);
}

export async function iniciarSesion(cred: Credenciales): Promise<void> {
  const { accessToken } = await postJson<{ accessToken: string }>('/api/auth/login', cred);
  setAccessToken(accessToken);
}

export async function refrescar(): Promise<boolean> {
  try {
    const { accessToken } = await postJson<{ accessToken: string }>('/api/auth/refresh');
    setAccessToken(accessToken);
    return true;
  } catch {
    return false;
  }
}

export async function cerrarSesion(): Promise<void> {
  try {
    await postJson('/api/auth/logout');
  } finally {
    clearAccessToken();
  }
}
