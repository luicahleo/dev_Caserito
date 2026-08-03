# Migración definitiva a caserito.app

## Objetivo

Establecer `https://caserito.app` como único origen público de producción y
eliminar las referencias operativas al dominio retirado.

## Alcance y decisiones

- `App:UrlPublica` usará `https://caserito.app` en producción y seguirá siendo
  la fuente de enlaces absolutos de confirmación y recuperación.
- El host procesará `X-Forwarded-For`, `X-Forwarded-Proto` y
  `X-Forwarded-Host` antes de autenticación.
- Los callbacks finales serán
  `/api/auth/external/google/callback` y
  `/api/auth/external/facebook/callback` bajo el origen público.
- Los retornos de OAuth seguirán aceptando solo rutas locales.
- Las cookies seguirán siendo host-only. En producción serán `Secure`; la
  cookie temporal OAuth conservará `SameSite=Lax` y la de refresh
  `SameSite=Strict`, compatibles con el flujo actual de mismo origen.
- Se actualizará la documentación que aún presenta el dominio retirado como
  utilizable. No se añaden fallbacks ni redirecciones de compatibilidad.

## Seguridad y PII

No se registrarán cabeceras, tokens, cookies, documentos ni contenido privado.
Las pruebas inspeccionarán solo metadatos de redirección y atributos de cookie.

## Pruebas y aceptación

- Una petición de producción con host y esquema reenviados genera el callback
  OAuth absoluto bajo `https://caserito.app`.
- La configuración de producción fija `App:UrlPublica` al origen nuevo.
- Las cookies no contienen atributo `Domain` y mantienen los atributos seguros.
- Los retornos externos continúan normalizándose a una ruta local.
- No queda ninguna referencia fija al dominio retirado en el repositorio.

## Fuera de alcance

La modificación de Google OAuth, Facebook Login, DNS y Nginx se coordina con el
agente VPS; este cambio solo entrega los valores finales sin secretos.
