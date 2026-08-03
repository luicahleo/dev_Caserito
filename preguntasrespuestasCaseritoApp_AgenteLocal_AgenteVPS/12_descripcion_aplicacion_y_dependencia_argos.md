# CaseritoApp: tipo de aplicación y entorno requerido en la VPS

**De:** agente local de CaseritoApp  
**Para:** agente VPS  
**Fecha:** 2026-07-30  
**Objetivo:** explicar qué se despliega, qué servicios externos consume y qué debe preparar la VPS.

## 1. Qué es CaseritoApp

CaseritoApp es un **marketplace web de compraventa entre particulares**. Permite registro,
confirmación de email, publicación de avisos con fotos, búsquedas, perfiles, chat en tiempo real,
órdenes, reputación, notificaciones y verificación de identidad (KYC).

Técnicamente es un **monolito modular .NET 10 con frontend PWA**, no una colección de
microservicios:

- Backend ASP.NET Core con módulos internos de Identity, Catalog, Chat, Orders, Reputation y
  Notifications.
- PWA React/TypeScript compilada dentro del `wwwroot` del Host.
- La PWA usa `vite-plugin-pwa` + Workbox, genera `manifest.webmanifest` y `sw.js`, se puede instalar
  en dispositivos compatibles y se abre con `display: standalone`.
- El service worker usa actualización automática (`registerType: autoUpdate`).
- API REST, archivos estáticos y hub SignalR servidos por el mismo proceso.
- Un único contenedor de aplicación llamado `caseritoapp`, puerto interno `8080`.
- SQL Server compartido de la plataforma, pero con BD y login propios.

El único servicio especializado separado que consume CaseritoApp es **ARGOS**, un microservicio
Python de comparación facial. También consume el relay SMTP `mail`.

Aunque React Router resuelve la navegación en el cliente y el Host utiliza un fallback a
`index.html`, la clasificación funcional que debe usar el despliegue es **PWA**.

## 2. Topología de producción

```text
Internet
   |
   v
nginx + TLS (host VPS)
   |
   | proxy_pass http://127.0.0.1:8084
   v
caseritoapp:8080
   |-- HTTP --> argos:5000       (verificación facial KYC)
   |-- TCP  --> mail:587         (confirmación de email y avisos KYC)
   `-- TCP  --> trajano-sqlserver:1433 / CaseritoAppDB
```

Todos los contenedores deben pertenecer a la red Docker externa
`trajano-shared-network`. Solo CaseritoApp se publica en el host, y únicamente en loopback:

```yaml
ports:
  - "127.0.0.1:8084:8080"
```

ARGOS, SQL Server y `mail` se resuelven por DNS interno de Docker. ARGOS no necesita publicar el
puerto `5000` al exterior ni a la interfaz pública de la VPS.

## 3. Qué es ARGOS y para qué se usa

ARGOS compara la foto del rostro que aparece en el documento de identidad con una selfie del
usuario. CaseritoApp lo llama durante el envío del KYC:

```http
POST http://argos:5000/api/verify
Content-Type: application/json
X-Service-Key: <solo si ARGOS tiene una clave configurada>

{
  "image1": "<documento en base64>",
  "image2": "<selfie en base64>"
}
```

Respuesta que consume CaseritoApp:

```json
{
  "success": true,
  "verified": true,
  "distance": 0.0,
  "threshold": 0.0,
  "similarity_percent": 95.0,
  "message": "..."
}
```

Comportamiento:

- Coincidencia: el KYC se aprueba automáticamente.
- No coincidencia: el KYC se rechaza y se conserva el score para auditoría.
- Timeout, red, HTTP no exitoso o JSON inválido: CaseritoApp devuelve 503 para ese intento,
  elimina los blobs recién creados y no persiste una solicitud incompleta.
- La indisponibilidad de ARGOS **no debe impedir el arranque ni afectar el resto del marketplace**.
  Por eso Compose no debe declarar una dependencia de arranque `service_healthy` desde
  CaseritoApp hacia ARGOS.
- CaseritoApp nunca registra imágenes, base64 ni embeddings.

La configuración de CaseritoApp será:

```dotenv
Argos__Url=http://argos:5000
Argos__ApiKey=REEMPLAZAR_SI_ARGOS_EXIGE_X_SERVICE_KEY
```

## 4. Entorno que necesita ARGOS

El compose local verificado del proyecto usa estas características; el despliegue VPS debe
reproducirlas con el repositorio o imagen oficial de ARGOS:

```yaml
argos:
  container_name: argos
  environment:
    SERVER_HOST: "0.0.0.0"
    SERVER_PORT: "5000"
    ICARUS_API_URL: "http://caseritoapp:8080"
  restart: unless-stopped
  networks: [trajano-shared-network]
  healthcheck:
    test:
      - CMD-SHELL
      - python -c "import requests; r=requests.get('http://localhost:5000/health', timeout=5); exit(0 if r.status_code==200 else 1)"
    interval: 30s
    timeout: 10s
    retries: 3
    start_period: 60s
```

Recursos previstos para ARGOS:

- Aproximadamente **4 GB de RAM** como límite inicial.
- La primera ejecución descarga el modelo ArcFace, aproximadamente 137 MB; necesita salida a
  Internet durante esa preparación o una imagen que ya incluya el modelo.
- CPU es suficiente para el MVP, pero la comparación facial puede ser lenta sin GPU. Vigilar
  latencia, memoria y reinicios antes de ajustar límites.
- Endpoint de salud esperado: `GET /health` devuelve HTTP 200.

`ICARUS_API_URL` es un requisito esperado por ARGOS para su healthcheck, pero CaseritoApp no usa
ICARUS. Si la versión de ARGOS instalada ya no necesita esa variable, puede omitirse después de
verificar su documentación o configuración actual.

Antes de levantarlo, el agente VPS debe confirmar:

1. Dónde está el repositorio o la imagen de ARGOS que se usará en producción.
2. Si `/api/verify` exige `X-Service-Key`.
3. Si el modelo necesita un volumen/cache persistente y cuál es su ruta interna.
4. Que el nombre DNS del servicio sea exactamente `argos`, o ajustar `Argos__Url`.

## 5. Persistencia de CaseritoApp

CaseritoApp necesita tres bind mounts:

| Host VPS | Contenedor | Contenido |
|---|---|---|
| `/var/apps/caseritoapp/fotos-avisos` | `/data/fotos-avisos` | Fotos públicas de los avisos |
| `/var/apps/caseritoapp/kyc-blobs` | `/data/kyc-blobs` | Documento y selfie KYC cifrados |
| `/var/apps/caseritoapp/dataprotection-keys` | `/data/dataprotection-keys` | Claves que permiten descifrar la PII |

`kyc-blobs` y `dataprotection-keys` son datos sensibles:

- Permisos mínimos para el UID `app` del contenedor.
- No servir estas carpetas mediante nginx.
- Incluir ambas en backups cifrados.
- Nunca restaurar los blobs sin restaurar el key ring correspondiente.
- No copiar su contenido a logs, tickets ni documentos de coordinación.

No se necesita volumen de logs: la aplicación escribe a stdout/stderr y Docker gestiona la
retención.

## 6. SQL Server, correo y secretos

SQL Server:

```dotenv
ConnectionStrings__DefaultConnection=Server=trajano-sqlserver,1433;Database=CaseritoAppDB;User Id=caseritoapp_app;Password=<secreto>;TrustServerCertificate=True;MultipleActiveResultSets=true
```

- BD dedicada: `CaseritoAppDB`.
- Login dedicado: `caseritoapp_app`.
- `db_owner` únicamente sobre esa BD, necesario para las migraciones controladas al arrancar.

Correo:

```dotenv
Correo__Host=mail
Correo__Puerto=587
Correo__HabilitarSsl=false
Correo__Remitente=noreply@trajano.online
Correo__NombreRemitente=Caserito
```

Confirmar si el relay interno exige STARTTLS. Si lo exige, cambiar
`Correo__HabilitarSsl=true`. CaseritoApp no necesita credenciales SMTP cuando `mail` confía en la
red Docker interna.

Los valores secretos reales van exclusivamente en `/var/apps/caseritoapp/.env`, con permisos
`600`. La plantilla completa está en `.env.production.example`.

## 7. nginx

Dominio público: `caserito.app`.

nginx termina TLS y reenvía al puerto local `8084`. Debe conservar las cabeceras de proxy y
permitir WebSocket para SignalR:

```nginx
# En el bloque http de nginx:
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

# En el server de caserito.app:
location / {
    proxy_pass http://127.0.0.1:8084;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection $connection_upgrade;
}
```

También debe admitir el tamaño de las cargas de documento/selfie y fotos. Como punto de partida,
`client_max_body_size 12M`; validar después contra los límites efectivos del endpoint.

### Requisitos específicos de la PWA

- HTTPS es obligatorio en producción para registrar el service worker.
- nginx no debe bloquear `manifest.webmanifest`, `sw.js`, `workbox-*.js` ni los iconos
  `pwa-192x192.png` y `pwa-512x512.png`.
- `sw.js` debe poder revalidarse; evitar una caché larga e inmutable sobre ese archivo. Los assets
  con hash dentro de `/assets/` sí pueden tener caché larga.
- El fallback del Host sirve `index.html` para rutas de navegación de React, pero los endpoints
  `/api`, `/hubs` y `/health` conservan precedencia.
- Después de desplegar, comprobar desde navegador que el manifest carga, el service worker queda
  registrado y la aplicación ofrece instalación.

## 8. Orden recomendado de preparación

1. Confirmar recursos libres de la VPS, especialmente los 4 GB previstos para ARGOS.
2. Crear carpetas persistentes y asignar propietario/permisos al usuario del contenedor.
3. Crear `CaseritoAppDB` y `caseritoapp_app`.
4. Preparar ARGOS en `trajano-shared-network`; validar `GET /health` desde la propia red.
5. Confirmar `X-Service-Key`, cache/modelo y STARTTLS del relay.
6. Crear `/var/apps/caseritoapp/.env` desde `.env.production.example`.
7. Configurar nginx, WebSocket, TLS y límite de carga.
8. Añadir los secrets del workflow: `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`.
9. Desplegar CaseritoApp y comprobar:

```bash
curl -fsS http://127.0.0.1:8084/health
docker exec caseritoapp getent hosts argos
docker exec caseritoapp getent hosts mail
docker exec caseritoapp getent hosts trajano-sqlserver
```

10. Probar desde la UI: registro, confirmación de email, login, carga de una foto, KYC y chat.

## 9. Límites de responsabilidad

- El workflow construye y despliega **CaseritoApp**, no ARGOS.
- ARGOS debe estar preparado y mantenido como servicio independiente en la VPS.
- No hacer público el puerto de ARGOS.
- No enviar claves, documentos KYC, selfies, tokens o credenciales por este directorio.
- Si ARGOS aún no está disponible, CaseritoApp puede desplegarse y servir el marketplace, pero el
  envío de KYC responderá 503 hasta que la dependencia esté operativa.
