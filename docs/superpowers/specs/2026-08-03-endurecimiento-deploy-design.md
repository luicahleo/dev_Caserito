# Endurecimiento del despliegue de CaseritoApp

## Objetivo

Convertir el despliegue a producción en un proceso aislado, serializado y
recuperable, sin enviar secretos ni datos persistentes al builder de Docker.

## Diseño

- Cada ejecución prepara un payload y lo sincroniza a
  `/var/apps/caseritoapp/releases/<GITHUB_SHA>`.
- El contexto Docker será exclusivamente ese release y tendrá una
  `.dockerignore` de denegación por defecto.
- La imagen se construirá como `caseritoapp:<GITHUB_SHA>`. La etiqueta `latest`
  se actualizará solo después de superar todos los controles de salud.
- Compose consumirá `CASERITOAPP_IMAGE_TAG`, con `latest` como valor operativo
  predeterminado, y no construirá durante la recreación.
- Antes de desplegar se capturará la imagen del contenedor actual. Si falla la
  recreación o cualquier health check, se restaurará esa imagen y se verificará
  el rollback antes de devolver error.
- Se comprobarán, en orden, el estado `healthy` del contenedor, el endpoint de
  loopback y `https://caserito.app/health`, todos con límites explícitos.
- GitHub serializará producción mediante `concurrency` sin cancelar una
  ejecución activa. El job tendrá `environment: production`, permisos mínimos
  y solo aceptará `master`, incluso bajo `workflow_dispatch`.
- Todas las actions externas quedarán fijadas a SHA completo y documentadas con
  su versión legible.

## Seguridad y PII

El workflow nunca leerá ni sincronizará `.env`, claves, certificados, backups,
fotos, blobs KYC ni claves de Data Protection. No usará trazas expandidas ni
imprimirá respuestas, variables o logs que puedan contener datos sensibles.
Los errores serán genéricos y mostrarán únicamente estado operativo.

## Operación externa

El agenteVPS mantiene `.env`, Nginx, certificados, bind mounts y backups.
GitHub debe configurar el environment `production` con las protecciones y
aprobadores requeridos. La clave pública del host SSH debe comprobarse antes de
actualizar el secret o variable usado para `known_hosts`.

## Criterios de aceptación

- El payload contiene DLL, SPA, Dockerfile, Compose y `.dockerignore`.
- El builder nunca recibe la raíz operativa del VPS.
- Dos despliegues de producción no se ejecutan simultáneamente.
- Un fallo de salud restaura la imagen anterior y hace fallar el job.
- `latest` solo identifica una entrega validada.
- No existen tags flotantes en `uses:`.
- El workflow no administra ningún elemento reservado al agenteVPS.

## Fuera de alcance

No se eliminan releases ni imágenes automáticamente y no se ejecutan limpiezas
globales de Docker. La retención se coordina por separado con el agenteVPS.
