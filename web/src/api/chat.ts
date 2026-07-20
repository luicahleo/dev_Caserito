import type { MensajeChat } from '../chat/sincronizacionMensajes';
import { api, desempaquetar } from './http';

export async function recuperarMensajes(
  conversacionId: string,
  despuesDeSecuencia: number,
  limite = 50,
): Promise<MensajeChat[]> {
  const pagina = desempaquetar(
    await api.GET('/api/chat/conversaciones/{id}/mensajes', {
      params: {
        path: { id: conversacionId },
        query: { despuesDeSecuencia, limite },
      },
    }),
  );
  return pagina.items.map((mensaje) => ({
    ...mensaje,
    secuencia: Number(mensaje.secuencia),
  }));
}
