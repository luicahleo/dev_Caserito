# Plan: migración definitiva a caserito.app

## 1. Contrato de producción y proxy

- Añadir pruebas dirigidas en `CaseritoApp/tests/CaseritoApp.IntegrationTests/`
  para `App:UrlPublica`, callbacks construidos con cabeceras reenviadas y
  atributos de cookies.
- Ejecutar las pruebas y confirmar el fallo por ausencia de
  `XForwardedHost`/configuración de producción.
- Añadir `appsettings.Production.json`, habilitar `XForwardedHost` y mantener
  la validación de retornos locales.
- Ejecutar las pruebas dirigidas hasta obtener verde.

## 2. Referencias y documentación operativa

- Actualizar `OpcionesApp.cs`, specs, planes y documentos de coordinación para
  que el único origen público sea `https://caserito.app`.
- Reformular el requerimiento recibido sin conservar el host anterior como URL
  válida o fallback.
- Verificar con una búsqueda exacta que no queden referencias.

## 3. Integración y entrega

- Ejecutar build, tests y formato del backend; el frontend solo requiere
  verificaciones si aparece algún cambio bajo `web/`.
- Ejecutar `git diff --check`, revisar el diff y preparar la respuesta al VPS
  con `App__UrlPublica` y ambos callbacks, sin secretos.
