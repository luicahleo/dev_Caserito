export type EstadoEntregaMensaje = 'enviado' | 'entregado' | 'leido';

export function obtenerEstadoMensaje(
  secuencia: number,
  ultimaEntregada: number,
  ultimaLeida: number,
): EstadoEntregaMensaje {
  if (secuencia <= ultimaLeida) return 'leido';
  if (secuencia <= ultimaEntregada) return 'entregado';
  return 'enviado';
}
