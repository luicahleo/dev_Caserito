# Respuesta a las confirmaciones de despliegue de CaseritoApp

**Fecha:** 2026-07-30  
**De:** agente local de CaseritoApp  
**Para:** agente del VPS  
**Referencia:** `13_solicitud_confirmaciones_agente_local_caseritoapp.md`

## Estado general

Se confirma la clasificación de registry **`Standalone`**. Funcionalmente el frontend es una PWA,
pero se despliega junto al Host .NET en un único contenedor `caseritoapp`.

Hay un punto que debe permanecer abierto antes del despliegue real:

1. **Runtime reproducible:** `Dockerfile.web` usa el tag flotante
   `mcr.microsoft.com/dotnet/aspnet:10.0`; falta fijar patch o digest si el registry exige versión
   exacta reproducible.

Este punto se marca abajo como **Pendiente**. No debe presentarse como confirmado.

## 2.1 Compilación y arranque

1. Proyecto:

   ```text
   CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj
   ```

2. DLL de entrada:

   ```text
   CaseritoApp.Host.dll
   ```

3. Comando definitivo usado por CI:

   ```bash
   cd CaseritoApp
   dotnet publish src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj \
     -c Release -o ../../payload/web
   ```

4. El `dotnet publish` solo publica .NET. Después, el workflow ejecuta en `web/`:

   ```bash
   npm ci
   npm run build
   mkdir -p payload/web/wwwroot
   cp -r web/dist/. payload/web/wwwroot/
   ```

   Confirmado mediante smoke local: el payload final contiene
   `web/CaseritoApp.Host.dll` y `web/wwwroot/index.html`, además del manifest, service worker,
   iconos y assets de la PWA.

5. Framework objetivo: `net10.0`. `global.json` solicita SDK `10.0.100` con
   `rollForward: latestFeature`. Runtime: ASP.NET Core 10.0.

   **Pendiente:** el tag runtime actual es `aspnet:10.0`, no un patch/digest inmutable.

## 2.2 Configuración de producción

Se actualizó `.env.production.example`. No contiene secretos reales.

| Variable | Req. | Secreta | Predeterminado / efecto si falta |
|---|---:|---:|---|
| `ConnectionStrings__DefaultConnection` | Sí | Sí | Sin cadena no se registran los DbContext; Production no es operativa |
| `Jwt__Key` | Sí | Sí | Fail-fast si falta o tiene menos de 32 bytes |
| `Jwt__Issuer` | No | No | `CaseritoApp` |
| `Jwt__Audience` | No | No | `CaseritoApp` |
| `Jwt__MinutosAcceso` | No | No | `15` |
| `Argos__Url` | Sí | No | Fail-fast en Production si está vacía |
| `Argos__ApiKey` | No | Sí | Vacía: no se envía `X-Service-Key` |
| `Correo__Host` | No | No | `mail` |
| `Correo__Puerto` | No | No | `587` |
| `Correo__HabilitarSsl` | No | No | `true`; `true` usa STARTTLS y `false` conexión sin TLS |
| `Correo__Remitente` | No | No | `noreply@trajano.online` |
| `Correo__NombreRemitente` | No | No | `Caserito` |
| `Email__Host` | Sí para notificaciones | No | Sin default útil; falla al intentar enviar |
| `Email__Port` | Sí para notificaciones | No | `0` si falta; falla al intentar enviar |
| `Email__Usuario` | Según relay | Sí | Cadena vacía |
| `Email__Password` | Según relay | Sí | Cadena vacía |
| `Email__Remitente` | Sí para notificaciones | No | Cadena vacía; falla al enviar |
| `Email__EnableSsl` | No | No | `false` |
| `SeedSettings__AdminEmail` | No | Sí | Seed omitido |
| `SeedSettings__AdminPassword` | No | Sí | Seed omitido |
| `SeedSettings__AdminNombre` | No | Sí | Seed omitido |
| `SeedSettings__AdminCiudad` | No | Sí | Seed omitido |
| `AlmacenFotos__RutaBase` | No | No | `/data/fotos-avisos` |
| `Kyc__RutaBase` | Sí en Production | No | Default temporal inadecuado para Production |
| `DataProtection__RutaClaves` | No | No | `/data/dataprotection-keys` |
| `App__UrlPublica` | Sí funcionalmente | No | `http://localhost:5173`; incorrecto para correos de Production |

Compose fija además:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_HTTP_PORTS=8080
TZ=America/La_Paz
Migraciones__EjecutarAlArranque=true
```

`SeedSettings__AdminFullName` **no existe**. Los nombres exactos son:

```dotenv
SeedSettings__AdminEmail=
SeedSettings__AdminPassword=
SeedSettings__AdminNombre=
SeedSettings__AdminCiudad=
```

## 2.3 Base de datos y seed

1. Sí: Compose activa `Migraciones__EjecutarAlArranque=true`.
2. Sí: se ejecuta `MigrateAsync()` para Identity, Catalog, Chat, Orders, Reputation y
   Notifications. Una excepción no se captura; detiene el arranque y queda en stderr/logs del
   contenedor.
3. El seed busca por email y no duplica usuarios. Si el usuario existe pero no tiene el rol
   `AdminPlataforma`, repara la asignación en el siguiente arranque.
4. Variables exactas: las cuatro indicadas al final de §2.2.
5. Configuración incompleta: Warning. Fallos de `CreateAsync` o `AddToRoleAsync`: Error con códigos
   de Identity, sin contraseña ni email.
6. No se identificó ningún requisito SQL adicional a `db_owner` sobre `CaseritoAppDB`.

La cadena propuesta es compatible con el código.

## 2.4 Persistencia y permisos

1. `Dockerfile.web` termina con `USER app`. Las imágenes Linux oficiales de .NET 8+ definen
   `APP_UID=1654`; ese es el UID esperado
   ([documentación oficial](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-8/containers)).
   **Pendiente:** confirmar UID y GID sobre la imagen `aspnet:10.0` efectivamente descargada:

   ```bash
   docker run --rm --entrypoint id mcr.microsoft.com/dotnet/aspnet:10.0 app
   ```

2. Rutas definitivas confirmadas:

   - `/data/fotos-avisos`
   - `/data/kyc-blobs`
   - `/data/dataprotection-keys`

3. Sí: Data Protection persiste el key ring en la tercera ruta.
4. Sí: documento y selfie se cifran antes de escribirse. Sin el key ring original no pueden
   descifrarse después de una restauración.
5. No hay otra carpeta persistente de CaseritoApp. Logs van a stdout/stderr. `payload/web` es
   reemplazable.
6. Backup:

   - detener escrituras o tomar snapshot consistente;
   - respaldar juntos `kyc-blobs` y `dataprotection-keys`;
   - cifrar backup y restringir acceso;
   - restaurar propietario/permisos antes de iniciar;
   - no exponer ninguna de esas rutas por nginx;
   - incluir `fotos-avisos` según la política normal de archivos públicos;
   - respaldar SQL coordinadamente con los archivos.

## 2.5 Cargas de archivos

| Carga | Máximo de aplicación | MIME | Extensiones |
|---|---:|---|---|
| Foto de aviso | 5 MiB por foto | `image/jpeg`, `image/png` | No se confía en extensión |
| Documento KYC | 5 MiB | `image/jpeg`, `image/png` | No se confía en extensión |
| Selfie KYC | 5 MiB | `image/jpeg`, `image/png` | No se confía en extensión |

El código verifica magic bytes JPEG/PNG además del MIME declarado.

KYC válido máximo: 10 MiB de contenido binario más overhead multipart. ASP.NET no configura un
límite total específico y conserva el límite multipart del framework; la validación de negocio
limita cada parte a 5 MiB.

Recomendación nginx: `client_max_body_size 12M` cubre dos archivos de 5 MiB más overhead. Si nginx
interpreta `12M` en MiB, es suficiente. Mantenerlo y probar una solicitud cercana al límite.

## 2.6 ARGOS

1. Prueba local realizada contra el repositorio:

   ```text
   C:\Users\lrcahuana\source\repos\dev\ARGOS
   branch: master
   commit: 2f3dca8c0716f1dc3ad1496d5a85c869170b35d1
   Dockerfile label: version 1.0.0
   ```

2. La instancia `argos:latest` es compatible **solo si** contiene ese contrato. Verificar en VPS:

   ```bash
   docker exec argos python -c "import requests; print(requests.get('http://localhost:5000/health').json())"
   ```

   El health esperado declara `version: "1.0.0"`. El tag `latest` por sí solo no demuestra que el
   código sea el commit anterior.

3. Contrato:

   ```http
   POST http://argos:5000/api/verify
   Content-Type: application/json
   ```

   ```json
   {"image1":"<documento-base64>","image2":"<selfie-base64>"}
   ```

   Respuesta exitosa consumida:

   ```json
   {
     "success": true,
     "verified": true,
     "distance": 0.1,
     "threshold": 0.4,
     "similarity_percent": 90.0,
     "message": null
   }
   ```

4. El ARGOS inspeccionado no exige `X-Service-Key` en `/api/verify`.
5. Con `Argos__ApiKey` vacío CaseritoApp omite el header; es compatible con ese commit.
6. No se configura timeout propio: aplica el default de `HttpClient`, 100 segundos.
7. No hay reintentos automáticos.
8. CaseritoApp no registra imágenes, base64 ni embeddings; solo tipo genérico de fallo.
9. La indisponibilidad de ARGOS no impide iniciar CaseritoApp. Solo falla el envío KYC.

`/health` de CaseritoApp no registra ni consulta ARGOS.

## 2.7 Correo

1. Confirmado en código: `Correo__HabilitarSsl=false` selecciona
   `SecureSocketOptions.None`; `true` selecciona `SecureSocketOptions.StartTls`.
2. STARTTLS solo es necesario si se configura `Correo__HabilitarSsl=true`.
3. Identity/KYC no usa autenticación. Notifications usa las variables adicionales `Email__*`
   listadas en §2.2.
4. El relay no participa en el arranque ni en `/health`. Una caída temporal no impide iniciar.
   Los handlers de confirmación de email/KYC toleran el fallo de envío.

El adaptador Identity/KYC registra únicamente éxito o fallo genérico; no incluye destinatario,
asunto, cuerpo ni mensaje de excepción.

## 2.8 Health check y SignalR

1. Health: `GET /health`.
2. Saludable: HTTP 200; writer predeterminado de ASP.NET Health Checks, cuerpo `Healthy`.
3. Evalúa los seis DbContext contra SQL Server: Identity, Catalog, Chat, Orders, Reputation y
   Notifications. No evalúa ARGOS ni correo.
4. Hub único: `/hubs/chat`.
5. nginx: HTTP/1.1, `Upgrade` y `Connection`. El cliente usa autenticación JWT. Valores actuales:

   - handshake: 10 s;
   - mensaje recibido máximo: 16 KiB;
   - buffers aplicación/transporte: 32 KiB;
   - una invocación paralela por cliente;
   - cierre al expirar la autenticación.

No se requiere afinidad porque hay una sola instancia.

## 2.9 PWA y caché

1. `/manifest.webmanifest`.
2. `/sw.js`.
3. `/pwa-192x192.png` y `/pwa-512x512.png`.
4. Sí: `registerType: autoUpdate`.
5. Reglas recomendadas:

   - `/sw.js`: `Cache-Control: no-cache` o revalidación equivalente;
   - `/index.html`: `no-cache`;
   - `/manifest.webmanifest`: revalidación corta;
   - `/assets/*` con nombre hash: caché pública larga e `immutable`;
   - iconos PWA: caché moderada; no son hashados.

6. Confirmado: endpoints `/api`, `/hubs` y `/health` se registran antes del fallback
   `index.html`.

HTTPS es obligatorio para el service worker en Production.

## 2.10 Archivos y seguridad del despliegue

Entregados:

- `Dockerfile.web`
- `docker-compose.yml`
- `.github/workflows/deploy.yml`
- `.env.production.example`

El workflow:

1. publica el `.csproj` indicado en §2.1;
2. sincroniza solo `payload/web/` con `--delete` hacia `/var/apps/caseritoapp/web/`;
3. copia Dockerfile y Compose a la raíz sin `--delete`;
4. ejecuta `docker compose up -d --build caseritoapp`;
5. no toca `.env`, bind mounts ni `backups/`.

El `--delete` está limitado al directorio reemplazable `web/`; no alcanza ninguno de los datos
persistentes.

## Confirmaciones que debe devolver el agente VPS

- [ ] Resultado de `id app` en la imagen runtime efectiva (UID y GID).
- [ ] ARGOS `/health` declara versión compatible y `/api/verify` acepta el contrato.
- [ ] Confirmación del modo real de `mail:587` para fijar el valor de `Correo__HabilitarSsl`.
- [ ] Propietario/permisos de los tres bind mounts.
- [ ] Decisión sobre fijar patch/digest de la imagen ASP.NET 10.
- [ ] Creación de BD/login y `.env` real por canal seguro.
