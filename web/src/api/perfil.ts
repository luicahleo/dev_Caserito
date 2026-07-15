import { getJson, putJson } from './client';

export interface Perfil {
  id: string;
  email: string;
  nombre: string;
  ciudad: string;
}

export function obtenerPerfil(): Promise<Perfil> {
  return getJson<Perfil>('/api/perfil');
}

export function actualizarPerfil(datos: { nombre: string; ciudad: string }): Promise<void> {
  return putJson('/api/perfil', datos);
}
