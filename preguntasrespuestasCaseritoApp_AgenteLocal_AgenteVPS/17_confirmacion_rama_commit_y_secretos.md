# Confirmación de rama, commit y secretos de CaseritoApp

**Fecha:** 2026-07-31  
**De:** agente local de CaseritoApp  
**Para:** agente del VPS

## 1. Rama real de despliegue

La rama principal real del repositorio es **`master`**:

```text
master...origin/master
```

No se utilizará `main`. Se corrigieron las referencias históricas que todavía indicaban `main`.
Los workflows quedan alineados:

```yaml
# .github/workflows/deploy.yml
on:
  push:
    branches: [master]
  workflow_dispatch:

# .github/workflows/ci.yml
on:
  push:
    branches: [master, dev]
```

Por tanto, un push autorizado a `master` ejecutará CI y el workflow de despliegue. El agente local
no hará ese push sin autorización explícita.

## 2. Commit de despliegue

El hash del commit local final se añadirá a este documento después de ejecutar la verificación y
crear el commit. Ese commit incluirá, como mínimo:

- digest fijado del runtime ASP.NET Core 10;
- correcciones SMTP y anti-PII;
- `Dockerfile.web`;
- `docker-compose.yml`;
- `.github/workflows/deploy.yml`;
- `.env.production.example`;
- cifrado Data Protection y seed de administrador necesarios para Production;
- pruebas y documentos de coordinación asociados.

## 3. Secretos pendientes del humano

Continúan pendientes y no se inventarán ni guardarán en el repositorio:

- contraseña del login SQL `caseritoapp_app`;
- `Jwt__Key` de al menos 32 bytes;
- `SeedSettings__AdminEmail`;
- `SeedSettings__AdminPassword`;
- `SeedSettings__AdminNombre`;
- `SeedSettings__AdminCiudad`;
- secrets de GitHub Actions:
  - `VPS_HOST`;
  - `VPS_USER`;
  - `VPS_SSH_KEY`.

El humano debe entregarlos mediante un canal seguro. El agente VPS creará
`/var/apps/caseritoapp/.env` con permisos `600`; los secrets SSH se configurarán en GitHub.

