import { getAccessToken } from '../auth/session';
import type { MensajeChat } from '../chat/sincronizacionMensajes';
import { HttpError } from './http';

export async function recuperarMensajes(
  conversacionId: string,
  despuesDeSecuencia: number,
  limite = 50,
): Promise<MensajeChat[]> {
  const parametros = new URLSearchParams({
    despuesDeSecuencia: String(despuesDeSecuencia),
    limite: String(limite),
  });
  const token = getAccessToken();
  const respuesta = await fetch(
    `/api/chat/conversaciones/${encodeURIComponent(conversacionId)}/mensajes?${parametros}`,
    { headers: token ? { Authorization: `Bearer ${token}` } : undefined },
  );
  if (!respuesta.ok)
    throw new HttpError(respuesta.status, null, 'No fue posible recuperar mensajes.');
  const cuerpo = (await respuesta.json()) as { items: MensajeChat[] };
  return cuerpo.items;
}
