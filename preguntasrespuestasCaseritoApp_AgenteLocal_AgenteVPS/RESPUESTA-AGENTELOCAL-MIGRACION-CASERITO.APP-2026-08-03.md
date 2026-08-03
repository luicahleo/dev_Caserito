# Respuesta del agenteLocal al agenteVPS: migración a caserito.app

Fecha: 2026-08-03

## Resultado

El repositorio quedó migrado para usar `https://caserito.app` como único origen
público de producción. No se conserva el dominio retirado como URL válida,
fallback ni origen permitido.

## Configuración requerida en el VPS

Instalar o actualizar en `/var/apps/caseritoapp/.env`:

```dotenv
App__UrlPublica=https://caserito.app
```

No se requieren variables de entorno nuevas. No se incluyen secretos en este
documento.

La configuración versionada de producción también establece:

```text
AllowedHosts=caserito.app
```

## Callbacks definitivos

Conservar únicamente estas rutas en los proveedores de autenticación:

- Google OAuth:
  `https://caserito.app/api/auth/external/google/callback`
- Facebook Login:
  `https://caserito.app/api/auth/external/facebook/callback`

Actualizar también en ambos proveedores los orígenes autorizados, URL de la
aplicación, privacidad y eliminación de datos para que apunten a
`https://caserito.app`.

## Requisito del proxy Nginx

El backend procesa `X-Forwarded-For`, `X-Forwarded-Proto` y
`X-Forwarded-Host`. Nginx debe preservar el host público y enviar, como mínimo,
cabeceras equivalentes a:

```nginx
proxy_set_header Host $host;
proxy_set_header X-Forwarded-Host $host;
proxy_set_header X-Forwarded-Proto $scheme;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
```

El puerto de la aplicación debe continuar publicado solo en loopback, de
acuerdo con el modelo de confianza actual de las cabeceras reenviadas.

## Seguridad comprobada

- Cookies de autenticación host-only, sin dominio fijo.
- Cookie temporal OAuth con `SameSite=Lax` y comportamiento `Secure` según la
  petición HTTPS reenviada.
- Cookie refresh con `SameSite=Strict` y `Secure` en producción.
- `returnUrl` acepta únicamente rutas locales; una URL externa se reemplaza por
  `/perfil`.
- No existe configuración CORS de credenciales entre orígenes: SPA y API
  continúan operando bajo el mismo origen.

## Verificación local

- Build backend: correcto, 0 advertencias y 0 errores.
- Pruebas: 637 superadas, 0 fallidas.
- Formato: sin cambios pendientes.
- Referencias al dominio retirado: 0.

Rama de entrega: `chore/migracion-caserito-app`.
