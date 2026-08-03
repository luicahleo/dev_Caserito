# Respuesta del agenteLocal: revisión del workflow de CaseritoApp

Fecha: 2026-08-03

## Resultado

Se corrigieron `.github/workflows/deploy.yml`, `docker-compose.yml` y el
contexto Docker. El workflow deja de reemplazar el directorio vivo y deja de
construir una imagen nueva directamente como `latest`.

## `.dockerignore`

Se versionó `deploy/.dockerignore` con este contenido:

```dockerignore
**
!web/**
!Dockerfile.web
!.dockerignore
```

El payload lo incorpora como `.dockerignore`; cada ejecución lo sincroniza al
release y lo instala en `/var/apps/caseritoapp/.dockerignore` antes de construir.
El contexto efectivo del build es únicamente:

```text
/var/apps/caseritoapp/releases/<GITHUB_SHA>
```

Por tanto `.env`, backups, claves, fotos, blobs KYC, Nginx y certificados no se
envían al builder. El agenteVPS debe confirmar la presencia del archivo después
del primer workflow con esta versión.

## Flujo y rollback exactos

1. CI publica la API, compila la PWA y valida DLL, `wwwroot/index.html`,
   Dockerfile, Compose y `.dockerignore`.
2. El payload se sincroniza con `--delete` exclusivamente dentro de
   `releases/<GITHUB_SHA>`.
3. El VPS vuelve a validar los archivos y captura `Config.Image` del contenedor
   existente.
4. Valida Compose y construye `caseritoapp:<GITHUB_SHA>` usando el release como
   contexto.
5. Recrea el servicio con `--no-build` y espera como máximo 30 intentos de 5
   segundos. Cada intento exige:
   - estado Docker `healthy`;
   - `http://127.0.0.1:8084/health` con timeout de 5 segundos;
   - `https://caserito.app/health` con timeout de 10 segundos.
6. Si falla la recreación o se agotan los intentos, restaura la etiqueta exacta
   de la imagen anterior, recrea el servicio y repite la misma validación. El
   job termina con error incluso si el rollback resulta correcto.
7. Solo después del éxito etiqueta la imagen validada como `caseritoapp:latest`
   y registra el SHA en `/var/apps/caseritoapp/current-release`.

No se ejecutan podas, borrados globales ni limpieza automática de releases.

## Política de ejecución

- Producción solo se ejecuta cuando `github.ref` es `refs/heads/master`, tanto
  para push como para `workflow_dispatch`.
- Environment: `production`.
- Permisos del token: `contents: read`.
- Concurrency: grupo `caseritoapp-production`, sin cancelar el despliegue activo.
- Timeout del job: 30 minutos.
- SSH inicial: 60 segundos; script remoto: 15 minutos; rsync tiene timeout de
  E/S de 60 segundos y SSH usa `ConnectTimeout=15`.
- Scripts sensibles usan `set -Eeuo pipefail`; no se usa `set -x` ni `|| true`.

El environment `production` debe configurarse en GitHub con protección de rama
`master` y los aprobadores que determine el propietario del repositorio.

## Verificación de host SSH

Se eliminó `ssh-keyscan` del job. Añadir en GitHub el secret
`VPS_SSH_KNOWN_HOSTS` con la línea de `known_hosts` obtenida y verificada por un
canal confiable. El workflow comprueba que el host configurado esté presente,
pero no imprime la clave ni el secret.

Este secret pertenece a GitHub Actions; no se añade a
`/var/apps/caseritoapp/.env`.

## Actions fijadas

| Action | Versión declarada | SHA fijado |
|---|---:|---|
| `actions/checkout` | `v4` | `11d5960a326750d5838078e36cf38b85af677262` |
| `actions/setup-dotnet` | `v4` | `67a3573c9a986a3f9c594539f4ab511d57bb3ce9` |
| `actions/setup-node` | `v4` | `49933ea5288caeca8642d1e84afbd3f7d6820020` |
| `webfactory/ssh-agent` | `v0.9.0` | `dc588b651fe13675774614f8e6a936a468676387` |

Los SHA se resolvieron directamente desde los tags de sus repositorios Git el
2026-08-03. El workflow usa los SHA completos; los comentarios conservan la
versión legible.

## Archivos relevantes

- `.github/workflows/deploy.yml`: workflow corregido completo.
- `deploy/.dockerignore`: contexto permitido.
- `docker-compose.yml`: imagen parametrizada por
  `CASERITOAPP_IMAGE_TAG`, sin build implícito.
- `deploy/deploy-contract.test.mjs`: cuatro pruebas estáticas del contrato.

## Verificación local

- 4 pruebas de contrato superadas.
- YAML analizado correctamente por Prettier.
- `docker compose config --quiet`: correcto.
- No se imprimieron secretos ni datos de producción.
