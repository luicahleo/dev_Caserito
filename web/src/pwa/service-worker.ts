/// <reference lib="webworker" />
import {
  cleanupOutdatedCaches,
  createHandlerBoundToURL,
  precacheAndRoute,
} from 'workbox-precaching';
import { NavigationRoute, registerRoute } from 'workbox-routing';
import { rutasExcluidasFallbackPwa } from './navigation.js';

declare let self: ServiceWorkerGlobalScope;

precacheAndRoute(self.__WB_MANIFEST);
cleanupOutdatedCaches();
registerRoute(
  new NavigationRoute(createHandlerBoundToURL('/index.html'), {
    denylist: rutasExcluidasFallbackPwa,
  }),
);

interface DatosPush {
  titulo: string;
  mensaje: string;
  ruta: string;
  comprobante: string;
}

let conversacionVisible: string | null = null;

self.addEventListener('message', (evento) => {
  const datos = evento.data as { tipo?: string; conversacionId?: string } | undefined;
  if (datos?.tipo === 'conversacion-visible') conversacionVisible = datos.conversacionId ?? null;
});

self.addEventListener('push', (evento) => {
  evento.waitUntil(
    (async () => {
      let datos: DatosPush;
      try {
        datos = evento.data?.json() as DatosPush;
      } catch {
        return;
      }
      if (
        datos.titulo !== 'Nuevo mensaje' ||
        datos.mensaje !== 'Tienes un nuevo mensaje' ||
        !datos.ruta.startsWith('/mensajes/') ||
        !datos.comprobante
      )
        return;
      await fetch('/api/notificaciones/push/confirmar-entrega', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ comprobante: datos.comprobante }),
      }).catch(() => undefined);
      if (conversacionVisible && datos.ruta === `/mensajes/${conversacionVisible}`) return;
      await self.registration.showNotification(datos.titulo, {
        body: datos.mensaje,
        tag: datos.ruta,
        data: { ruta: datos.ruta },
      });
    })(),
  );
});

self.addEventListener('notificationclick', (evento) => {
  evento.notification.close();
  const ruta = (evento.notification.data as { ruta?: string } | undefined)?.ruta;
  if (!ruta?.startsWith('/mensajes/')) return;
  evento.waitUntil(
    (async () => {
      const ventanas = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
      const existente = ventanas[0] as WindowClient | undefined;
      if (existente) {
        await existente.navigate(ruta);
        await existente.focus();
        return;
      }
      await self.clients.openWindow(ruta);
    })(),
  );
});
