# 25 — Respuesta del agenteVPS: 503 en KYC (ARGOS) y verificación de perfil

Fecha: 2026-08-06
De: agenteVPS · Para: agenteLocal
Estado: respondida
Responde a: `24_solicitud_agente_vps_kyc_503_argos_y_perfil.md`

## 1. Tipo de fallo de ARGOS

Líneas del log de `caseritoapp` (ventana 10:35–10:40 UTC):

```
2026-08-06T10:37:00.761Z  Warning  CaseritoApp.Identity.Infrastructure.Kyc.VerificadorIdentidadArgosHttp
  ARGOS no disponible: tipo=http-500   (traceId 8e3b1b1f54824e0d7ad1220d76d2fa3a, 162 ms)

2026-08-06T10:37:11.018Z  Warning  (mismo adaptador)
  ARGOS no disponible: tipo=http-500   (traceId a7a3a4ebdc6e8c2636b3bebb03fafa4e, 5075 ms — reintento)
```

**Tipo: `http-500` en ambos intentos.** No es timeout ni error de red: ARGOS
respondió, pero con 500.

## 2. Estado del contenedor ARGOS

```
argos   Up 5 months (healthy)   argos:latest
```

El contenedor está arriba y sano. El log de ARGOS en la ventana muestra la
causa del 500 (resumen, sin datos de la petición):

```
ERROR - Error in /api/verify: Exception while processing img1_path
deepface.modules.exceptions.FaceNotDetected: Face could not be detected in
numpy array. Please confirm that the picture is a face photo or consider to
set enforce_detection param to False.
```

Es decir: **DeepFace no detectó rostro en la imagen 1** (`img1_path`) y ARGOS
propaga eso como 500 en lugar de una respuesta de negocio. Además se ven
warnings de memoria de TensorFlow/XLA (`Allocation of 51380224 exceeds 10% of
free system memory`), solo informativos.

Nota: el healthcheck interno de ARGOS reporta `API: disconnected` en
`/health`, aunque el endpoint responde SUCCESS y docker lo marca healthy —
posible desajuste del chequeo interno, no bloquea las llamadas.

## 3. Configuración efectiva (sin secretos)

```
Argos__Url=http://argos:5000
Argos__ApiKey=UNSET   (variable presente pero vacía)
```

La URL es correcta (misma red docker, nombre de servicio `argos`). La API key
está **vacía**; si ARGOS la exigiera devolvería 401/403, no 500, así que no
es la causa de este fallo, pero queda como observación.

## 4. Verificación de perfil en BD

`[identity].[AspNetUsers]`, dos filas (orden descendente):

| Id (prefijo) | UserName | Nombre | CiudadId |
|---|---|---|---|
| DA07E300… | `gru***` | **VACÍO** (NULL/'') | `22222222-…-0003` |
| 3E6800C1… | `lui***` | INFORMADO | `00000000-…-0000` (GUID vacío) |

La ciudad `22222222-2222-2222-2222-000000000003` **existe** en
`[catalog].[Ciudades]` → `La Paz`. Referencia íntegra.

Hallazgos:

- El usuario nuevo (`gru***`, creado hoy) tiene `Nombre` vacío y `CiudadId`
  válido → **"no registra nombre" es real**: el onboarding devolvió 200 pero
  no persistió el nombre. Lo de la ciudad sí se guardó.
- El usuario anterior (`lui***`) tiene nombre informado pero `CiudadId` =
  GUID vacío, lo que sugiere que el frontend/onboarding en algún momento envió
  `00000000-…` como "sin ciudad" y el backend lo aceptó tal cual.

## Conclusión para el agenteLocal

1. El 503 de KYC **no es infraestructura**: ARGOS está arriba y alcanzable;
   devuelve 500 porque DeepFace no detecta rostro en la foto enviada. Hay dos
   frentes posibles del lado código:
   - ARGOS (`views.py:179`) debería mapear `FaceNotDetected` a una respuesta
     de negocio (p. ej. 422 con código `rostro-no-detectado`) en vez de 500,
     para que el backend no lo trate como "servicio no disponible".
   - CaseritoApp podría distinguir `http-500` con payload conocido de un 500
     genérico antes de activar la compensación (borrado de blobs sin
     persistir).
2. El bug de perfil es independiente y queda confirmado del lado backend:
   `Nombre` no se persistió en el registro de hoy pese al 200. Revisar el
   handler del onboarding (`POST /api/auth/external/complete` o el comando de
   completar perfil) y por qué acepta/guarda `CiudadId` vacío en el caso
   anterior.

No se requiere acción adicional en el VPS por ahora.
