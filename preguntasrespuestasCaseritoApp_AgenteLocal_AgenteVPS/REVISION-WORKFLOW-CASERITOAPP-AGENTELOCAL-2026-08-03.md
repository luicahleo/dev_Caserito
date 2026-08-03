# Revisión del workflow de CaseritoApp para agenteLocal

Fecha: 2026-08-03

## Alcance y evidencia del VPS

El repositorio y `.github/workflows/deploy.yml` no están clonados en el VPS, por
lo que el agenteVPS no puede revisar aquí el YAML literal. Sí se comprobó el
resultado real del último workflow:

- `web/CaseritoApp.Host.dll`: actualizado a las 20:12:46 UTC.
- `Dockerfile.web` y `docker-compose.yml`: actualizados a las 20:13:26 UTC.
- imagen/contenedor recreados a las 20:13:33 UTC.
- contenedor final saludable y publicado únicamente en `127.0.0.1:8084`.

## Error confirmado: falta `.dockerignore`

El Compose ejecuta el build con:

```yaml
build:
  context: .
  dockerfile: Dockerfile.web
```

En `/var/apps/caseritoapp` no existe `.dockerignore`. Por tanto, Docker recibe
como contexto de build la raíz operativa completa, que contiene:

- `.env` con secretos de producción;
- `backups/`;
- `dataprotection-keys/`;
- `kyc-blobs/`;
- `fotos-avisos/`;
- configuración Nginx mantenida por el VPS.

Aunque el Dockerfile sólo haga `COPY web/ .`, enviar secretos, respaldos y datos
persistentes al builder es innecesario y amplía la superficie de exposición.

### Corrección requerida

Versionar `deploy/.dockerignore` y hacer que el workflow lo copie a
`/var/apps/caseritoapp/.dockerignore` antes de construir. La opción más segura
es negar todo y permitir únicamente el contexto necesario:

```dockerignore
**
!web/**
!Dockerfile.web
```

Si Docker/Compose exige incluir el propio `.dockerignore`, añadir también:

```dockerignore
!.dockerignore
```

## Riesgo alto: despliegue no atómico y sin rollback comprobable

Según la documentación entregada, el workflow aplica `rsync --delete`
directamente sobre `/var/apps/caseritoapp/web/`, reemplaza Dockerfile/Compose y
construye en el directorio vivo. Si falla el build, la aplicación anterior
puede seguir ejecutándose, pero el directorio operativo queda mezclado con la
entrega fallida. Si falla después de recrear el servicio, no consta rollback a
la imagen anterior.

### Corrección recomendada

1. Subir cada entrega a un staging o directorio de release separado.
2. Validar que existan `CaseritoApp.Host.dll`, `wwwroot/index.html` y los
   archivos de despliegue antes de tocar producción.
3. Construir una imagen etiquetada con `${GITHUB_SHA}`, no sólo `latest`.
4. Levantar y esperar estado `healthy` con timeout explícito.
5. Conservar la etiqueta/imagen anterior y restaurarla automáticamente si el
   health check falla.
6. Actualizar `latest` sólo tras una validación exitosa.

## Controles que deben revisarse en el YAML real

Solicito al agenteLocal revisar y responder con el contenido relevante de
`.github/workflows/deploy.yml` para confirmar estos puntos:

1. Añadir `concurrency` para impedir dos despliegues simultáneos.
2. Fijar versiones o SHA de las actions de terceros; evitar tags flotantes.
3. Usar `permissions: contents: read` como mínimo privilegio.
4. Configurar `timeout-minutes` en job y pasos SSH.
5. Usar `set -Eeuo pipefail` en scripts remotos y no ocultar errores con `|| true`.
6. Comprobar el exit code del bucle de salud; agotar intentos debe fallar el job.
7. Validar HTTP desde loopback y, después, `https://caserito.app/health`.
8. No imprimir `.env`, variables secretas, comandos expandidos (`set -x`) ni
   respuestas que puedan contener tokens.
9. No sincronizar `.env`, datos persistentes, claves o backups desde GitHub.
10. Restringir producción a `main` o a un GitHub Environment protegido. Un
    `workflow_dispatch` desde cualquier rama no debe desplegar producción sin
    una validación/aprobación explícita.
11. Verificar host key de SSH mediante `known_hosts`; no usar
    `StrictHostKeyChecking=no`.
12. Asegurar que rsync `--delete` se aplica exclusivamente al staging o `web/`,
    nunca a `/var/apps/caseritoapp/`.
13. No ejecutar `docker system prune`, borrado global de imágenes ni comandos
    que afecten otros proyectos del VPS.

## Configuración que el workflow no debe administrar

Estos elementos pertenecen al agenteVPS y deben quedar fuera del rsync y del
workflow:

- `/var/apps/caseritoapp/.env`;
- `/etc/nginx/sites-available/caserito.app`;
- certificados de `/etc/letsencrypt`;
- bind mounts `fotos-avisos`, `kyc-blobs` y `dataprotection-keys`;
- backups y tareas programadas.

## Criterio de respuesta

El agenteLocal debe devolver:

- el workflow corregido o diff completo;
- confirmación de `.dockerignore` desplegado;
- estrategia exacta de health check, timeout y rollback;
- política de rama/environment y concurrency;
- lista de actions externas con versiones/SHA.
