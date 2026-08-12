import {
  obtenerClavePublicaPush,
  registrarSuscripcionPush,
  revocarSuscripcionPush,
} from '../api/notificaciones';

const claveDispositivo = 'caserito-push-dispositivo';

export function soportaWebPush(): boolean {
  return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
}

function convertirClave(clave: string): Uint8Array<ArrayBuffer> {
  const base64 = clave.replace(/-/g, '+').replace(/_/g, '/');
  const rellena = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
  const bytes = Uint8Array.from(atob(rellena), (caracter) => caracter.charCodeAt(0));
  return new Uint8Array(bytes.buffer);
}

function obtenerDispositivoId(): string {
  const actual = localStorage.getItem(claveDispositivo);
  if (actual) return actual;
  const nuevo = crypto.randomUUID();
  localStorage.setItem(claveDispositivo, nuevo);
  return nuevo;
}

export async function activarNotificacionesPush(): Promise<'activada' | 'denegada'> {
  if (!soportaWebPush()) throw new Error('No fue posible activar las notificaciones.');
  const permiso = await Notification.requestPermission();
  if (permiso !== 'granted') return 'denegada';
  const clavePublica = await obtenerClavePublicaPush();
  if (!clavePublica) throw new Error('No fue posible activar las notificaciones.');
  const registro = await navigator.serviceWorker.ready;
  const existente = await registro.pushManager.getSubscription();
  const suscripcion =
    existente ??
    (await registro.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: convertirClave(clavePublica),
    }));
  const json = suscripcion.toJSON();
  if (!suscripcion.endpoint || !json.keys?.p256dh || !json.keys.auth) {
    throw new Error('No fue posible activar las notificaciones.');
  }
  await registrarSuscripcionPush({
    dispositivoId: obtenerDispositivoId(),
    endpoint: suscripcion.endpoint,
    p256dh: json.keys.p256dh,
    auth: json.keys.auth,
  });
  return 'activada';
}

export async function desactivarNotificacionesPush(): Promise<void> {
  const dispositivoId = localStorage.getItem(claveDispositivo);
  const registro = await navigator.serviceWorker.ready;
  const suscripcion = await registro.pushManager.getSubscription();
  if (dispositivoId) await revocarSuscripcionPush(dispositivoId);
  await suscripcion?.unsubscribe();
}
