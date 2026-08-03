# Plan: endurecimiento del workflow de despliegue

## 1. Contratos estáticos

- Crear pruebas que analicen `.github/workflows/deploy.yml`,
  `docker-compose.yml` y `deploy/.dockerignore`.
- Exigir permisos mínimos, concurrency, environment, restricción a `master`,
  actions fijadas, releases por SHA, health checks y rollback.
- Ejecutar las pruebas y observar el fallo por el workflow actual.

## 2. Contexto Docker y Compose

- Crear `deploy/.dockerignore` con denegación por defecto.
- Parametrizar la etiqueta de imagen en `docker-compose.yml` y retirar el build
  implícito del directorio operativo.
- Confirmar que `.env` y los bind mounts no forman parte del payload.

## 3. Workflow recuperable

- Fijar las actions externas a SHA verificados en sus repositorios oficiales.
- Preparar y validar el payload.
- Sincronizar a `releases/<GITHUB_SHA>` con límites de red explícitos.
- Construir la imagen por SHA, capturar la anterior, recrear sin build y
  comprobar salud Docker, loopback y HTTPS.
- Restaurar y comprobar la imagen anterior ante fallo; etiquetar `latest` solo
  al finalizar con éxito.

## 4. Documentación y respuesta

- Actualizar la documentación de despliegue que describe el flujo anterior.
- Versionar la revisión recibida y generar una respuesta al agenteVPS con el
  diff funcional, actions fijadas y requisitos externos.

## 5. Verificación

- Ejecutar pruebas dirigidas, parser YAML disponible, build/test proporcionales,
  búsqueda anti-PII, `git diff --check` y revisión completa contra el spec.
