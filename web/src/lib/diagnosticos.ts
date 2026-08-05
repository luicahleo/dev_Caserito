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

export async function reportarDiagnostico(report: DiagnosticReport): Promise<void> {
  try {
    await fetch('/api/diagnosticos/frontend', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(report),
      keepalive: true,
    });
  } catch {
    // El diagnóstico nunca debe generar otro error ni alterar el flujo del usuario.
  }
}
