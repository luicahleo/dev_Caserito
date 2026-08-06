import { obtenerEventosRecientes, obtenerSesionId } from './sesionDiagnostico';

export type DiagnosticEventName =
  | 'router.unexpected'
  | 'window.unexpected'
  | 'promise.unhandled'
  | 'http.network_failed'
  | 'http.server_failed'
  | 'chunk.load_failed'
  | 'flow.critical_failed';

export type DiagnosticCategory = 'unexpected' | 'network' | 'server' | 'chunk' | 'critical';
export type DiagnosticSource = 'router' | 'window' | 'promise' | 'http' | 'flow';

export interface DiagnosticReport {
  errorId: string;
  eventName: DiagnosticEventName;
  category: DiagnosticCategory;
  source: DiagnosticSource;
  traceId?: string;
  statusCode?: number;
  release?: string;
}

export function crearErrorId(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(6));
  const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('');
  return `ERR-${hex.toUpperCase()}`;
}

const MAX_EVENTOS_EN_REPORTE = 30;

export async function reportarDiagnostico(report: DiagnosticReport): Promise<void> {
  const eventosFlujo = obtenerEventosRecientes(MAX_EVENTOS_EN_REPORTE);
  const carga = {
    ...report,
    sessionId: obtenerSesionId(),
    ...(eventosFlujo.length > 0 ? { flowEvents: eventosFlujo } : {}),
  };
  try {
    await fetch('/api/diagnosticos/frontend', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(carga),
      keepalive: true,
    });
  } catch {
    // El diagnóstico nunca debe generar otro error ni alterar el flujo del usuario.
  }
}

type DiagnosticReporter = (report: DiagnosticReport) => Promise<void>;

let capturaGlobalInstalada = false;

export function instalarCapturaGlobal(
  reporter: DiagnosticReporter = reportarDiagnostico,
): () => void {
  if (capturaGlobalInstalada) return () => undefined;

  const reportarError = () => {
    void reporter({
      errorId: crearErrorId(),
      eventName: 'window.unexpected',
      category: 'unexpected',
      source: 'window',
    });
  };
  const reportarPromesa = () => {
    void reporter({
      errorId: crearErrorId(),
      eventName: 'promise.unhandled',
      category: 'unexpected',
      source: 'promise',
    });
  };

  window.addEventListener('error', reportarError);
  window.addEventListener('unhandledrejection', reportarPromesa);
  capturaGlobalInstalada = true;

  return () => {
    window.removeEventListener('error', reportarError);
    window.removeEventListener('unhandledrejection', reportarPromesa);
    capturaGlobalInstalada = false;
  };
}
