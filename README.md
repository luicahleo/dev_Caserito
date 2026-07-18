# CaseritoApp

Marketplace C2C para Bolivia (web + Android). Backend .NET (Clean Architecture,
bounded contexts) + frontend SPA React/TypeScript (PWA, envuelta con Capacitor
para tiendas).

- Alcance y visión: `CaseritoApp/Documentacion/marketplace-bolivia-mvp-brief.md`.
- Plan por fases: `CaseritoApp/Documentacion/plan-desarrollo-mvp-v1.md`.
- Convenciones y arquitectura: `CLAUDE.md`.
- Specs y planes de cada bloque: `docs/superpowers/{specs,plans}/`.

## Cómo levantar la app (desarrollo)

El entorno de desarrollo corre en contenedores: **SQL Server 2022 + API (.NET) +
Web (Vite/React)**.

### Requisitos (una sola vez)

- **Docker Desktop** corriendo.
- Un archivo **`.env`** en la raíz: copiá `.env.example` a `.env` y definí `SA_PASSWORD`.

### Levantar

Desde la raíz del repo (PowerShell):

```powershell
./rebuild.ps1            # construye y levanta los 3 contenedores en segundo plano
./rebuild.ps1 -Logs      # además sigue los logs
```

Equivalente directo (o `rebuild.sh` en Bash):

```bash
docker compose -f docker-compose.dev.yml up -d --build
```

El **primer** build tarda (compila la imagen .NET y la de la web); los siguientes
usan caché y son rápidos. Al arrancar en Development la base de datos se **migra
sola**.

### URLs

- Web → **http://localhost:5173**
- API → http://localhost:8080 (health: `http://localhost:8080/health`)
- SQL Server → `localhost:1433` (usuario `sa`, contraseña = `SA_PASSWORD` del `.env`)

### Probar el flujo end-to-end

1. Abrí http://localhost:5173 → pantalla **Explorar** (pública, sin login).
2. Registrate / iniciá sesión.
3. Para **publicar** hace falta identidad verificada: subí documento y selfie en
   `/kyc` y aprobá la solicitud desde `/admin/kyc` con un usuario que tenga el
   permiso `kyc.revisar`.
4. Ya verificado, publicá un aviso; aparecerá en **Explorar** y en **Mis avisos**
   (donde podés editar/pausar/reactivar/eliminar).

> Nota: los avisos se muestran **sin foto** (placeholder) hasta que se implemente
> el bloque 2C (fotos del aviso).

### Comandos útiles

```powershell
docker compose -f docker-compose.dev.yml ps            # estado de los contenedores
docker compose -f docker-compose.dev.yml logs -f web   # logs de la web (o api / sqlserver)
docker compose -f docker-compose.dev.yml down          # apagar (los datos de SQL persisten)
docker compose -f docker-compose.dev.yml down -v       # apagar y borrar datos (empezar de cero)
```

### Problemas frecuentes

- **La web da `504 (Outdated Optimize Dep)`**: es la caché de pre-bundle de Vite
  en el volumen de `node_modules`. Recreá solo la web con el volumen renovado:

  ```powershell
  docker compose -f docker-compose.dev.yml up -d --force-recreate --renew-anon-volumes web
  ```

- **`POST /api/auth/refresh` devuelve `401` en la consola al cargar**: es
  **esperado** cuando no hay sesión iniciada; la app lo maneja y continúa como
  usuario anónimo.

## Flujo híbrido (API en el host, SQL en contenedor)

Para depurar la API desde el IDE con hot-reload contra SQL en contenedor:

```bash
docker compose -f docker-compose.dev.yml up -d sqlserver
# cadena de conexión a localhost,1433 vía user-secrets del host
dotnet run --project CaseritoApp/src/Host/CaseritoApp.Host
cd web && npm run dev        # Vite proxya /api y /health al host
```

## Comandos de desarrollo

Backend (desde `CaseritoApp/`):

```bash
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

Frontend (desde `web/`):

```bash
npm run dev | build | lint | typecheck | test
```
