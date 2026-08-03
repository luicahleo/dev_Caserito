# Requerimiento para agenteLocal: migración definitiva a caserito.app

## Contexto

El VPS migró el despliegue al origen público definitivo
`https://caserito.app`. El subdominio anterior se retira y no debe conservarse
como URL válida ni como fallback.

## Revisiones requeridas en el código fuente

1. Buscar y reemplazar cualquier referencia fija al subdominio anterior
   en código, configuración por ambiente, pruebas, documentación, plantillas de
   correo, metadatos SEO, manifiesto PWA, enlaces compartidos y callbacks.
2. Usar `https://caserito.app` como URL pública/canónica de producción.
3. Confirmar que la aplicación respeta `X-Forwarded-Host` y
   `X-Forwarded-Proto` detrás de Nginx, especialmente al construir redirects y
   callbacks de autenticación externa.
4. Revisar cookies de autenticación: no fijar el dominio anterior; preferir
   cookies host-only para `caserito.app`, con `Secure` y política `SameSite`
   compatible con OAuth.
5. Revisar CORS, CSP (`connect-src`, `frame-src`, etc.), orígenes permitidos y
   validación de `returnUrl` para que acepten exclusivamente el dominio nuevo.
6. Confirmar enlaces absolutos generados en recuperación de contraseña,
   verificación de correo, avisos, chat y notificaciones.
7. Añadir o actualizar pruebas de producción para el host `caserito.app`.

## Configuración externa que debe coordinarse

- Google OAuth: registrar y conservar únicamente el callback de producción
  `https://caserito.app/api/auth/external/google/callback`. Esta ruta fue
  confirmada contra la respuesta real del servidor el 2026-08-03.
- Facebook Login: registrar y conservar únicamente
  `https://caserito.app/api/auth/external/facebook/callback`. Esta ruta fue
  confirmada contra la respuesta real del servidor el 2026-08-03.
- Actualizar en ambos proveedores los dominios autorizados/orígenes JavaScript,
  URL de aplicación, política de privacidad y eliminación de datos si todavía
  apuntan al subdominio anterior.

## Criterio de entrega al agente VPS

Informar las rutas finales de callback y cualquier variable de entorno nueva o
modificada que deba instalarse en `/var/apps/caseritoapp/.env`. No incluir
secretos en la respuesta.
