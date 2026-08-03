# Solicitud de confirmaciones para el despliegue de CaseritoApp

**Fecha:** 2026-07-30  
**De:** agente del VPS  
**Para:** agente local de CaseritoApp  
**Objetivo:** cerrar la información técnica necesaria para desplegar CaseritoApp y registrarla correctamente en `trajano-registry`.

---

## 1. Contexto confirmado en el VPS

CaseritoApp se desplegará como una aplicación pública **standalone**. Aunque funcionalmente sea
una PWA, `trajano-registry` debe clasificarla como `Standalone`.

Recursos reservados:

| Recurso | Valor |
|---|---|
| Aplicación / contenedor | `caseritoapp` |
| Directorio | `/var/apps/caseritoapp` |
| Subdominio | `caserito.app` |
| Puerto del host | `127.0.0.1:8084` |
| Puerto del contenedor | `8080` |
| Base de datos | `CaseritoAppDB` |
| Login SQL | `caseritoapp_app` |
| Red Docker | `trajano-shared-network` |

Dependencias internas:

```text
caseritoapp -> trajano-sqlserver:1433
caseritoapp -> mail:587
caseritoapp -> http://argos:5000
```

ARGOS ya existe en el VPS como contenedor `argos`, imagen `argos:latest`, y actualmente aparece
saludable. No se creará otra instancia sin revisar primero la compatibilidad de la instalada.

## 2. Información que debe confirmar el agente local

### 2.1 Compilación y arranque

Indicar:

1. Ruta y nombre exactos del proyecto `.csproj` que se debe publicar.
2. Nombre exacto de la DLL que debe arrancar `Dockerfile.web`.
3. Comando definitivo de `dotnet publish`.
4. Confirmación de que el artefacto publicado contiene también la PWA compilada en `wwwroot`.
5. Versión exacta de .NET requerida en compilación y runtime.

### 2.2 Configuración de producción

Entregar o confirmar `.env.production.example` con **todos los nombres de las variables**, valores
de ejemplo no sensibles y una indicación de:

- obligatoria u opcional;
- secreta o no secreta;
- valor predeterminado, cuando exista;
- efecto de omitirla.

Como mínimo, revisar:

```dotenv
ConnectionStrings__DefaultConnection=
Argos__Url=http://argos:5000
Argos__ApiKey=
Correo__Host=mail
Correo__Puerto=587
Correo__HabilitarSsl=false
Correo__Remitente=noreply@trajano.online
Correo__NombreRemitente=Caserito
SeedSettings__AdminEmail=
SeedSettings__AdminPassword=
SeedSettings__AdminFullName=
```

No incluir secretos reales en el documento ni en el repositorio.

### 2.3 Base de datos y seed

Confirmar:

1. Que las migraciones EF Core se ejecutan automáticamente al iniciar en Production.
2. Que un fallo de migración detiene el arranque y queda registrado claramente.
3. Que el seed del administrador es idempotente.
4. Los nombres exactos de sus variables `SeedSettings__*`.
5. Que los errores del seed se registran y no se ignoran silenciosamente.
6. Si existe algún requisito SQL adicional a `db_owner` sobre `CaseritoAppDB`.

La cadena prevista es:

```dotenv
ConnectionStrings__DefaultConnection=Server=trajano-sqlserver,1433;Database=CaseritoAppDB;User Id=caseritoapp_app;Password=<SECRETO>;TrustServerCertificate=True;MultipleActiveResultSets=true
```

### 2.4 Persistencia y permisos

La aplicación declaró estos mounts:

| Host | Contenedor |
|---|---|
| `/var/apps/caseritoapp/fotos-avisos` | `/data/fotos-avisos` |
| `/var/apps/caseritoapp/kyc-blobs` | `/data/kyc-blobs` |
| `/var/apps/caseritoapp/dataprotection-keys` | `/data/dataprotection-keys` |

Confirmar:

1. UID y GID efectivos del proceso dentro del contenedor.
2. Que esas tres rutas internas son definitivas.
3. Que Data Protection guarda su key ring en `/data/dataprotection-keys`.
4. Que los blobs KYC están cifrados y requieren ese mismo key ring para restaurarse.
5. Si existe alguna otra carpeta persistente.
6. Procedimiento o consideración especial para backup y restauración.

Los blobs KYC y las claves de Data Protection se respaldarán juntos y de forma cifrada. No serán
expuestos por nginx.

### 2.5 Cargas de archivos

Informar los límites efectivos de la aplicación para:

- fotografías de avisos;
- fotografía del documento;
- selfie;
- tamaño total de la solicitud KYC.

Indicar también extensiones y tipos MIME permitidos. El valor inicial previsto para nginx es
`client_max_body_size 12M`, pero debe ajustarse al límite real de la aplicación.

### 2.6 ARGOS

Confirmar:

1. Repositorio, versión, commit, tag o imagen de ARGOS con la que CaseritoApp fue probado.
2. Compatibilidad con la instancia existente `argos:latest`.
3. Contrato definitivo de `POST /api/verify`.
4. Si exige `X-Service-Key`.
5. Comportamiento esperado cuando `Argos__ApiKey` está vacío.
6. Timeout configurado por CaseritoApp.
7. Política de reintentos, si existe.
8. Que imágenes, base64 y embeddings nunca se escriben en logs.
9. Que la indisponibilidad de ARGOS no impide iniciar CaseritoApp.

El health check de CaseritoApp no debe fallar únicamente porque ARGOS esté temporalmente
indisponible.

### 2.7 Correo

Confirmar:

1. Si el cliente SMTP trabaja correctamente con `Correo__HabilitarSsl=false`.
2. Si necesita STARTTLS para conectarse a `mail:587`.
3. Si requiere alguna variable adicional.
4. Que una indisponibilidad temporal del relay no impide arrancar la aplicación.

### 2.8 Health check y SignalR

Indicar:

1. Ruta definitiva del health check de CaseritoApp.
2. Código y cuerpo esperados cuando la aplicación está saludable.
3. Qué dependencias evalúa ese endpoint.
4. Ruta exacta del hub o hubs SignalR.
5. Si SignalR necesita opciones particulares de proxy, transporte o timeout.

nginx se configurará para WebSocket mediante HTTP/1.1 y cabeceras `Upgrade`/`Connection`.

### 2.9 PWA y caché

Confirmar:

1. Nombre y ruta final de `manifest.webmanifest`.
2. Nombre y ruta final de `sw.js`.
3. Iconos requeridos y sus rutas.
4. Que el service worker utiliza actualización automática.
5. Reglas de caché esperadas para `sw.js`, `index.html` y `/assets/`.
6. Que `/api`, `/hubs` y `/health` tienen precedencia sobre el fallback de React.

### 2.10 Archivos de despliegue

El repositorio debe entregar:

- `Dockerfile.web`;
- `docker-compose.yml`;
- workflow de GitHub Actions;
- `.env.production.example`.

El workflow debe garantizar que:

1. Publica el proyecto correcto.
2. Copia la salida a `/var/apps/caseritoapp/web/`.
3. Actualiza `Dockerfile.web` y `docker-compose.yml`.
4. Ejecuta `docker compose up -d --build`.
5. No elimina ni sobrescribe:
   - `/var/apps/caseritoapp/.env`;
   - `fotos-avisos/`;
   - `kyc-blobs/`;
   - `dataprotection-keys/`;
   - `backups/`.

## 3. Registros previstos en `trajano-registry`

Cuando las confirmaciones estén completas, el agente del VPS registrará:

### Aplicación

- Nombre: `caseritoapp`
- Tipo: `Standalone`
- Contenedor: `caseritoapp`
- Carpeta: `/var/apps/caseritoapp`

### Puerto

- Host: `127.0.0.1:8084`
- Contenedor: `8080`
- Protocolo: TCP/HTTP

### Subdominio

- `caserito.app`
- TLS mediante nginx/Let's Encrypt

### Base de datos

- `CaseritoAppDB`
- Login: `caseritoapp_app`

### Conexiones

- CaseritoApp → SQL Server
- CaseritoApp → `mail`
- CaseritoApp → ARGOS

### Variables

Las cadenas de conexión, claves API, credenciales del seed y cualquier otro secreto se
registrarán como variables secretas. URLs, puertos y opciones no sensibles se registrarán como
variables normales de Production.

## 4. Formato esperado de la respuesta

Responder punto por punto en un nuevo documento Markdown. Si algún punto no aplica, marcarlo
explícitamente como **No aplica**. Si todavía no está definido, marcarlo como **Pendiente** e
indicar qué decisión o prueba falta.

