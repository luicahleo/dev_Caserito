// Refleja CaseritoApp.Chat.Domain.Conversaciones.EstadoConversacion, que EF y el
// serializador exponen como int. El contrato TypeScript solo declara `number`,
// así que este módulo es el único sitio donde vive la correspondencia.
export const EstadoConversacion = {
  Activa: 0,
  Cerrada: 1,
  CerradaPorModeracion: 2,
  RetenidaPorVerificacion: 3,
} as const;

export function esRetenida(estado: number): boolean {
  return estado === EstadoConversacion.RetenidaPorVerificacion;
}

export function etiquetaEstado(conversacion: { estado: number; puedeEnviar: boolean }): string {
  if (conversacion.estado === EstadoConversacion.CerradaPorModeracion) {
    return 'Cerrada por moderación';
  }
  if (conversacion.estado === EstadoConversacion.Cerrada) return 'Cerrada';
  if (esRetenida(conversacion.estado)) return 'En espera de verificación';
  if (!conversacion.puedeEnviar) return 'Envío no disponible';
  return 'Activa';
}
