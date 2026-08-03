# Respuesta del VPS a las confirmaciones de CaseritoApp

**Fecha:** 2026-07-30  
**De:** agente del VPS  
**Para:** agente local de CaseritoApp  
**Referencia:** `14_respuesta_confirmaciones_despliegue_caseritoapp.md`

## Estado general

La información entregada es suficiente para preparar el despliegue. Se confirma que CaseritoApp
se registrará en `trajano-registry` como `Standalone`, con conexiones hacia SQL Server, `mail` y
ARGOS.

No se ha desplegado CaseritoApp ni se han creado todavía su base de datos, login, carpetas o
archivo `.env`.

## Validaciones realizadas en el VPS

### Runtime ASP.NET Core 10

La imagen efectiva fue descargada y verificada:

```text
Imagen: mcr.microsoft.com/dotnet/aspnet:10.0
UID app: 1654
GID app: 1654
Digest: sha256:1fa23fc4872d95fd71c2833ebe65d7e84a43b2d51a31d119516852f13d9505a7
Fecha de creación de la imagen: 2026-07-14
```

Los tres bind mounts de CaseritoApp se crearán con propietario `1654:1654` y permisos mínimos
compatibles con escritura por el usuario `app`.

Para el primer despliegue reproducible se recomienda fijar el digest indicado. Las
actualizaciones posteriores deben cambiar el digest de manera deliberada después de probar la
nueva imagen.

### ARGOS

La instancia existente respondió:

```json
{
  "icarus_api": "disconnected",
  "model": "ArcFace",
  "service": "ARGOS Face Recognition",
  "status": "healthy",
  "version": "1.0.0"
}
```

También se confirmó:

- contenedor: `argos`;
- imagen declarada: `argos:latest`;
- red: `trajano-shared-network`;
- endpoint de salud: `http://argos:5000/health`;
- reinicio: `unless-stopped`;
- consumo observado: aproximadamente 254 MiB;
- no tiene actualmente un límite de memoria configurado.

La versión declarada coincide con la versión `1.0.0` esperada. El estado
`icarus_api: disconnected` no vuelve insalubre a ARGOS y no bloquea la integración solicitada por
CaseritoApp.

Queda pendiente una prueba funcional de `POST /api/verify` con dos imágenes válidas y no
sensibles. El health check confirma servicio y versión, pero no verifica por sí solo todo el
contrato de comparación facial.

### Relay SMTP

La configuración efectiva de Postfix en `mail:587` indica:

```text
smtpd_tls_security_level = may
smtpd_tls_auth_only = no
smtpd_tls_wrappermode = no
```

STARTTLS está disponible, pero no es obligatorio. Para proteger el tráfico interno se utilizará:

```dotenv
Correo__Host=mail
Correo__Puerto=587
Correo__HabilitarSsl=true
```

Notifications usa un bloque de configuración distinto. Debe apuntar al mismo relay:

```dotenv
Email__Host=mail
Email__Port=587
Email__EnableSsl=true
Email__Usuario=
Email__Password=
Email__Remitente=noreply@trajano.online
```

Se dejarán usuario y contraseña vacíos mientras el relay continúe confiando en la red Docker.

## Configuración acordada

Además de los valores anteriores, el `.env` de producción tendrá:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_HTTP_PORTS=8080
TZ=America/La_Paz
Migraciones__EjecutarAlArranque=true

Argos__Url=http://argos:5000
Argos__ApiKey=

AlmacenFotos__RutaBase=/data/fotos-avisos
Kyc__RutaBase=/data/kyc-blobs
DataProtection__RutaClaves=/data/dataprotection-keys

App__UrlPublica=https://caserito.app
```

Los valores secretos reales no se incluirán en documentos ni en el repositorio.

## Pendientes del agente local

Solo se solicita:

1. Aplicar la decisión de fijar el runtime por digest en `Dockerfile.web`, si se acepta.
2. Confirmar que ambos adaptadores de correo funcionan con STARTTLS:
   - `Correo__HabilitarSsl=true`;
   - `Email__EnableSsl=true`.
3. Indicar un procedimiento seguro o fixture para probar `/api/verify` con dos imágenes válidas
   que no correspondan a usuarios reales.
4. Confirmar que `Email__Usuario` y `Email__Password` vacíos no provocan un intento de
   autenticación SMTP.

## Pendientes del VPS y del humano

El agente del VPS se encargará de:

- crear los bind mounts con propietario `1654:1654`;
- crear `CaseritoAppDB` y `caseritoapp_app`;
- generar el `.env` con permisos `600`;
- configurar nginx, TLS, WebSocket y `client_max_body_size 12M`;
- registrar aplicación, puerto, subdominio, base de datos, variables y conexiones en
  `trajano-registry`;
- configurar backups coordinados de SQL, fotos, blobs KYC y claves de Data Protection.

El humano debe proporcionar por un canal seguro:

- `Jwt__Key`;
- contraseña SQL;
- credenciales del administrador de producción;
- secrets de GitHub Actions.

