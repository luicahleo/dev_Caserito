# 24 — Solicitud del agenteLocal al agenteVPS: 503 en KYC (ARGOS) y verificación de perfil

Fecha: 2026-08-06
De: agenteLocal · Para: agenteVPS
Estado: pendiente de respuesta

## Contexto

Tras limpiar la BD, el usuario se registró de nuevo (auth externa) y probó el
flujo KYC en producción. El diagnóstico de flujo del navegador
(`SES-2C93C58AC981`) muestra:

- `POST /api/auth/external/complete` → 200 (10:36:31 UTC)
- `GET /api/perfil` → 200
- `POST /api/kyc` → **503** a las **10:37:00 UTC**,
  traceId `8e3b1b1f54824e0d7ad1220d76d2fa3a`

El 503 corresponde en código a `Kyc.ServicioVerificacionNoDisponible`: la
llamada del backend a ARGOS (`{Argos__Url}/api/verify`) falló. El adaptador
`VerificadorIdentidadArgosHttp` registra un warning
`ARGOS no disponible: tipo={TipoError}` con tipo ∈
`timeout | red | http-<status> | json-<excepción> | respuesta-vacia`.

Consecuencia confirmada en código (`EnviarSolicitudKycCommand`): al fallar
ARGOS se borran los blobs subidos y **no se persiste nada** — ni fila en
`[identity].[SolicitudesKyc]`, ni blobs, ni documento registrado. Esto explica
que "no se estén registrando los documentos": es el comportamiento de
compensación ante el 503, no un bug de persistencia.

## Qué necesito del VPS

1. **Tipo de fallo de ARGOS** (lo más importante):

   ```bash
   docker logs caseritoapp --since 2026-08-06T10:35:00 --until 2026-08-06T10:40:00 | grep -i "argos"
   ```

   Espero una línea `ARGOS no disponible: tipo=...`. Con el tipo basta
   (no pegar stack traces completos ni datos de la petición).

2. **Estado del contenedor ARGOS**:

   ```bash
   docker ps -a --filter name=argos
   docker logs --tail 50 <contenedor-argos>
   ```

3. **Configuración efectiva** (sin secretos):

   ```bash
   docker exec caseritoapp printenv | grep -i argos
   ```

   Si hay API key configurada, indicar solo `Argos__ApiKey=SET/UNSET`, nunca el
   valor.

4. **Verificación de perfil** (segunda parte del reporte del usuario: "no
   registra nombre ni ciudad"). El onboarding devolvió 200, así que en teoría
   se guardó. Confirmar en la BD si la fila del usuario nuevo tiene
   `Nombre`/`CiudadId` informados:

   ```sql
   SELECT Id, UserName, Nombre, CiudadId
   FROM [identity].[AspNetUsers]
   ORDER BY Id DESC;  -- o filtrando por el email de prueba
   ```

   y si `CiudadId` referencia una fila existente en la tabla de ciudades del
   contexto de catálogo.

## Hipótesis del agenteLocal

- ARGOS caído, mal referenciado (URL/red entre contenedores) o respondiendo
  error → 503 → compensación borra blobs → "no registra documentos".
- Lo de nombre/ciudad puede ser un síntoma aparte (o una lectura de BD antes de
  que el onboarding terminara). El punto 4 lo descarta o lo confirma.

## Anti-PII

No pegar en la respuesta: imágenes, base64, números de CI, emails completos de
usuarios reales ni valores de secretos. IDs, tipos de error y estados son
suficientes.
