# Handoff agenteVPS → agenteLocal — OAuth queda en «Página no encontrada» hasta Ctrl+F5

**Fecha:** 2026-08-01  
**Aplicación:** CaseritoApp  
**Producción:** `https://caserito.app`  
**Estado:** causa raíz confirmada en el artefacto desplegado; requiere cambio en el código fuente local y nuevo deploy.

## Resumen

Google y Facebook Login están configurados y funcionan en producción, pero el primer clic en cualquiera
de los botones OAuth deja al usuario en una pantalla SPA «Página no encontrada», con una URL similar a:

```text
https://caserito.app/api/auth/external/facebook/start?returnUrl=%2Fperfil
```

Al presionar `Ctrl+F5`, el navegador vuelve a solicitar esa misma URL al servidor, CaseritoApp responde
`302` y la autenticación del proveedor comienza correctamente.

La causa raíz es el **service worker generado por Workbox**: su `NavigationRoute` usa el fallback de
`index.html` para todas las navegaciones y no excluye `/api`. Por eso captura la navegación OAuth,
devuelve el shell React y React Router interpreta `/api/auth/external/.../start` como una ruta SPA no
existente. `Ctrl+F5` evita esa intercepción y permite que la petición llegue al backend.

No es un problema de Nginx, credenciales OAuth ni callbacks de Google/Meta.

## Evidencia observada en producción

### 1. Botones desplegados

El bundle renderiza los botones como enlaces normales:

```jsx
<Button
  component="a"
  href={`/api/auth/external/${provider}/start?returnUrl=${encodeURIComponent(returnUrl)}`}
>
  Continuar con ...
</Button>
```

Por tanto, React Router no debería manejar directamente el clic. La captura ocurre en el service worker.

### 2. Service worker desplegado

El archivo `/var/apps/caseritoapp/web/wwwroot/sw.js` contiene, conceptualmente:

```js
registerRoute(
  new NavigationRoute(createHandlerBoundToURL("index.html"))
);
```

No existe una `denylist` para `/api`, de modo que Workbox acepta también navegaciones a endpoints del
backend.

### 3. Logs de Nginx

En el clic inicial:

- La URL visible cambia a `/api/auth/external/facebook/start?...`.
- React muestra «Página no encontrada».
- Nginx **no recibe** el `GET` del endpoint OAuth.
- La SPA continúa haciendo llamadas de notificaciones y refresh con esa URL como `Referer`.

Después de `Ctrl+F5`:

```text
GET /api/auth/external/facebook/start?returnUrl=%2Fperfil → 302
```

El mismo endpoint, solicitado directamente al backend o a través de Nginx, responde correctamente con
`302` hacia Google/Meta.

## Cambio obligatorio recomendado

En la configuración de `vite-plugin-pwa`/Workbox, excluir todas las rutas `/api` del fallback SPA.
Adaptar el fragmento al archivo real de configuración del frontend:

```ts
VitePWA({
  workbox: {
    navigateFallback: "/index.html",
    navigateFallbackDenylist: [
      /^\/api(?:\/|$)/,
      /^\/health(?:\/|$)/
    ]
  }
})
```

La exclusión mínima indispensable es:

```ts
navigateFallbackDenylist: [/^\/api(?:\/|$)/]
```

El objetivo es que una navegación a `/api/...` vaya siempre a la red/backend y nunca reciba
`index.html` desde el service worker.

Si el proyecto genera un service worker manual, configurar directamente:

```js
registerRoute(
  new NavigationRoute(createHandlerBoundToURL("index.html"), {
    denylist: [/^\/api(?:\/|$)/, /^\/health(?:\/|$)/]
  })
);
```

No editar `dist/sw.js` o el bundle minificado como solución permanente; deben regenerarse desde la
configuración fuente.

## Defensa adicional recomendada para OAuth

Aunque la corrección del service worker es obligatoria, conviene iniciar OAuth con una navegación de
documento explícita:

```tsx
const iniciarAccesoExterno = (provider: "google" | "facebook") => {
  const returnUrl = normalizarReturnUrl(location.state?.from);
  window.location.assign(
    `/api/auth/external/${provider}/start?returnUrl=${encodeURIComponent(returnUrl)}`
  );
};
```

Usar un botón con `onClick` o conservar `<a href>`; no usar `Link`, `NavLink` ni `navigate()` de React
Router para endpoints `/api`. Esta defensa no sustituye la `navigateFallbackDenylist`, porque el service
worker todavía puede interceptar una navegación de documento.

## Los errores 401 de consola son un problema separado

Mientras el usuario es anónimo, la SPA consulta repetidamente:

```text
GET /api/notificaciones... → 401
POST /api/auth/refresh → 401
```

Estos `401` no causan el fallo OAuth, pero generan ruido y reintentos innecesarios. Revisar:

1. No habilitar queries de notificaciones hasta que el estado de autenticación esté resuelto y exista
   una sesión autenticada.
2. Evitar que cada `401` dispare simultáneamente su propio refresh; usar un único refresh compartido
   (`single-flight`) para peticiones concurrentes.
3. No reintentar automáticamente queries protegidas cuando refresh devuelve `401`; tratarlo como sesión
   anónima y limpiar el estado una sola vez.
4. Desactivar retries de TanStack Query para `401/403`.

## Criterios de aceptación

Probar en Chrome/Edge con el service worker anterior instalado, no solo en una sesión limpia:

1. Desplegar la nueva versión y confirmar que el nuevo service worker se activa.
2. Abrir `/login` sin autenticar.
3. Pulsar Google: la primera pulsación debe salir inmediatamente hacia Google, sin 404 ni recarga.
4. Pulsar Facebook: la primera pulsación debe salir inmediatamente hacia Meta, sin 404 ni recarga.
5. Cancelar cada proveedor y verificar el retorno controlado a Caserito.
6. Completar ambos flujos y verificar el retorno a `/perfil`.
7. En DevTools → Network, el primer clic debe producir un `GET /api/auth/external/{provider}/start`
   con respuesta `302`; no debe devolver `index.html`.
8. Verificar que rutas SPA reales continúan funcionando offline/con fallback.
9. Verificar que una navegación directa a cualquier `/api/...` nunca es atendida por el service worker.
10. Confirmar que un usuario anónimo no provoca un bucle de consultas protegidas y refresh `401`.

## Consideración de actualización del PWA

Como el service worker actual usa `skipWaiting()` y `clientsClaim()`, el nuevo worker debería activarse
rápidamente, pero hay que probar explícitamente la actualización desde la versión defectuosa. Si una
pestaña antigua conserva el comportamiento, cerrar/reabrir la aplicación o recargar una vez; después de
que el worker corregido controle la página, OAuth debe funcionar siempre al primer clic.

