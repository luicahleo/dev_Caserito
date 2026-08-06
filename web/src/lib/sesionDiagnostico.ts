// Sesión de diagnóstico por pestaña: identificador opaco, modo diagnóstico
// activable con ?debug=1 y buffer circular de eventos de flujo. Solo registra
// metadatos seguros (rutas sanitizadas, status, duraciones); nunca query
// strings, bodies ni contenido de usuario.

export interface EventoFlujo {
  seq: number;
  timestamp: string;
  eventName: 'flow.navigation' | 'flow.api_call';
  detail: string;
  traceId?: string;
  statusCode?: number;
  durationMs?: number;
}

const SESION_CLAVE = 'caserito.sesion';
const DEBUG_CLAVE = 'caserito.debug';
const MAX_EVENTOS = 100;
const MAX_DETALLE = 120;
const PATRON_SESION = /^SES-[0-9A-F]{12}$/;
const PATRON_UUID = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

const eventos: EventoFlujo[] = [];
let siguienteSeq = 1;

export function obtenerSesionId(): string {
  const almacenada = sessionStorage.getItem(SESION_CLAVE);
  if (almacenada !== null && PATRON_SESION.test(almacenada)) {
    return almacenada;
  }
  const bytes = crypto.getRandomValues(new Uint8Array(6));
  const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('');
  const id = `SES-${hex.toUpperCase()}`;
  sessionStorage.setItem(SESION_CLAVE, id);
  return id;
}

export function modoDiagnosticoActivo(): boolean {
  if (new URLSearchParams(window.location.search).get('debug') === '1') {
    sessionStorage.setItem(DEBUG_CLAVE, '1');
  }
  return sessionStorage.getItem(DEBUG_CLAVE) === '1';
}

export function sanitizarRuta(pathname: string): string {
  const sanitizada = pathname
    .split('/')
    .map((segmento) => (/^\d+$/.test(segmento) || PATRON_UUID.test(segmento) ? ':id' : segmento))
    .join('/');
  return sanitizada === '' ? '/' : sanitizada;
}

export function registrarEventoFlujo(evento: Omit<EventoFlujo, 'seq' | 'timestamp'>): void {
  if (!modoDiagnosticoActivo()) {
    return;
  }
  eventos.push({
    ...evento,
    detail: evento.detail.slice(0, MAX_DETALLE),
    seq: siguienteSeq,
    timestamp: new Date().toISOString(),
  });
  siguienteSeq += 1;
  if (eventos.length > MAX_EVENTOS) {
    eventos.splice(0, eventos.length - MAX_EVENTOS);
  }
}

export function obtenerEventosRecientes(n: number): EventoFlujo[] {
  return eventos.slice(-n);
}

export function exportarDiagnostico(): void {
  const carga = JSON.stringify(
    {
      sessionId: obtenerSesionId(),
      generadoEn: new Date().toISOString(),
      eventos,
    },
    null,
    2,
  );
  const url = URL.createObjectURL(new Blob([carga], { type: 'application/json' }));
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = `diagnostico-${obtenerSesionId()}.json`;
  enlace.click();
  URL.revokeObjectURL(url);
}
