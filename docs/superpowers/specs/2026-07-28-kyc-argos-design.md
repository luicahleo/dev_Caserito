# Diseño — Bloque "KYC automático con ARGOS"

> Fecha: 2026-07-28.  
> Contexto: CaseritoApp necesita usuarios con identidad verificada. El KYC actual es manual (admin revisa DNI + selfie). ARGOS es un microservicio Python propio que, con DeepFace/ArcFace, compara dos rostros vía `POST /api/verify`.  
> Status: aprobado en brainstorming.

## 1. Objetivo y alcance

Automatizar la decisión del KYC en Caserito: el usuario sube **foto del DNI + selfie**, el backend envía ambas imágenes a ARGOS y, si el rostro coincide, la solicitud se aprueba automáticamente. De lo contrario se rechaza con motivo.

**Dentro de este bloque:**

- Integración Caserito backend ↔ ARGOS (`POST /api/verify`).
- Decisión automática `Aprobada`/`Rechazada` en el agregado `VerificacionKyc`.
- Registro del score de similitud para auditoría.
- Manejo de errores de red y de "sin rostro detectado".
- Ajustes mínimos en UI para reflejar que la verificación es automática.
- Tests unitarios e integración (mock de ARGOS).

**Fuera de este bloque (diferido):**

- Liveness por video.
- OCR del DNI.
- Identificación 1:N.
- Umbral configurable desde Caserito (usaremos el umbral por defecto de ARGOS: `0.68` de distancia coseno).
- Revisión manual por admin como fallback (el admin verá el resultado, pero no aprobará).

## 2. Flujo de alto nivel

```
Usuario (web)
  └─ POST /api/kyc { DNI, selfie } ──► Caserito.Host
                                        ├─ Valida imágenes (tipo/tamaño/magic bytes)
                                        ├─ Guarda DNI y selfie cifrados (IAlmacenBlobsKyc)
                                        ├─ POST /api/verify a ARGOS
                                        │      { image1: DNI_base64, image2: selfie_base64 }
                                        ├─ Recibe { verified, similarity_percent, ... }
                                        ├─ Aplica Aprobar/Rechazar en VerificacionKyc
                                        └─ Publica UserVerified si aprobó
```

ARGOS es un servicio **interno**: solo Caserito backend puede llegar a él. Nunca es llamado desde el navegador.

## 3. Contrato con ARGOS

Usamos el endpoint existente:

```http
POST {ARGOS_URL}/api/verify
Content-Type: application/json
X-Service-Key: {ARGOS_SERVICE_KEY}

{
  "image1": "base64_del_DNI",
  "image2": "base64_de_la_selfie"
}
```

Respuesta esperada:

```json
{
  "success": true,
  "verified": true,
  "distance": 0.32,
  "threshold": 0.68,
  "similarity_percent": 68.0
}
```

Caserito usa `verified` como decisión y almacena `similarity_percent` en la solicitud.

## 4. Cambios por capa

### 4.1 Domain (`CaseritoApp.Identity.Domain/Kyc/`)

Se aprovechan `SolicitudKyc` y `VerificacionKyc.Aprobar(...)` / `Rechazar(...)`.

Cambios mínimos:

- `SolicitudKyc` añade `double? ScoreSimilitud` para auditoría del score devuelto por ARGOS.
- `ErroresKyc` añade:

```csharp
public const string VerificacionFacialFallida = "Kyc.VerificacionFacialFallida";
public const string ServicioVerificacionNoDisponible = "Kyc.ServicioVerificacionNoDisponible";
```

- La aprobación automática usará un actor de sistema (`Guid` constante conocido, p. ej. `SistemaActor.Id`) en lugar de un admin humano. Se agrega una constante `SistemaActor` en `Identity.Domain` o `Identity.Application`.

### 4.2 Application (`CaseritoApp.Identity.Application/Kyc/`)

Nuevo puerto:

```csharp
public interface IVerificadorIdentidadArgos
{
    Task<Result<VerificacionFacialResultado>> VerificarAsync(
        byte[] imagenDocumento, byte[] imagenSelfie, CancellationToken ct);
}

public sealed record VerificacionFacialResultado(
    bool Coinciden,
    double SimilitudPercent,
    string? MotivoRechazo);
```

El handler `EnviarSolicitudKycCommandHandler` pasa a:

1. Cargar `VerificacionKyc` existente o crearla; validar invariants (`YaVerificado`, `SolicitudPendienteExiste`) antes de cualquier llamada externa.
2. Guardar blobs de DNI y selfie.
3. Llamar a `IVerificadorIdentidadArgos.VerificarAsync`.
4. Si `Coinciden` es `true`:
   - `verificacion.EnviarSolicitud(...)` → crea la solicitud `Pendiente`.
   - `SolicitudActual.ScoreSimilitud = resultado.SimilitudPercent`.
   - `verificacion.Aprobar(solicitudId, SistemaActor.Id, tiempo.GetUtcNow())`.
   - Publicar `UserVerified`.
5. Si `Coinciden` es `false`:
   - `verificacion.EnviarSolicitud(...)`.
   - `SolicitudActual.ScoreSimilitud = resultado.SimilitudPercent`.
   - `verificacion.Rechazar(solicitudId, SistemaActor.Id, resultado.MotivoRechazo, tiempo.GetUtcNow())`.
6. Si ARGOS no responde: eliminar los blobs recién escritos (compensación) y devolver `Result.Fallo` con `ServicioVerificacionNoDisponible`. Nada se persiste en la BD.

### 4.3 Infrastructure (`CaseritoApp.Identity.Infrastructure/Kyc/`)

Nuevo adaptador:

```csharp
public sealed class VerificadorIdentidadArgosHttp(
    HttpClient httpClient,
    IOptions<OpcionesArgos> opciones) : IVerificadorIdentidadArgos
```

Responsabilidades:

- Convertir bytes a base64.
- Enviar POST a `{OpcionesArgos.Url}/api/verify`.
- Deserializar respuesta.
- Mapear excepciones de red a un `Result` fallido.
- Nunca loguear imágenes, embeddings ni base64.

También se actualiza `ConfiguracionKyc` para mapear `SolicitudKyc.ScoreSimilitud` y se genera una migración nueva.

Configuración:

```csharp
public sealed class OpcionesArgos
{
    public const string Seccion = "Argos";
    public string Url { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
}
```

Registro en `DependencyInjection.cs`:

```csharp
services.Configure<OpcionesArgos>(config.GetSection(OpcionesArgos.Seccion));
services.AddHttpClient<IVerificadorIdentidadArgos, VerificadorIdentidadArgosHttp>();
```

**Fail-fast:** fuera de `Development`/`Testing`, si `Argos:Url` está vacía la composición lanza (patrón igual al de `Jwt:Key`).

### 4.4 Host (`CaseritoApp.Host/Endpoints/`)

El endpoint `POST /api/kyc` no cambia de contrato. Se mantiene el manejo de `ConflictoConcurrenciaException`. Se agrega manejo del nuevo error de servicio no disponible → HTTP 503.

### 4.5 Frontend (`web/`)

- `KycPage.tsx`: se actualizan textos para indicar "verificación automática". Se mantiene la captura de documento + selfie.
- `AdminKycPage.tsx`: se muestra el score de similitud y la decisión automática; ya no se ofrecen botones Aprobar/Rechazar (salvo que se decida mantener override, fuera de alcance).
- `api/kyc.ts`: sin cambios de contrato.

## 5. PII, seguridad y auditoría

- Las imágenes se siguen almacenando cifradas fuera de la BD (`IAlmacenBlobsKyc`).
- El llamado a ARGOS viaja por red interna con base64 en el body.
- Logs de auditoría: solo `usuarioId`, `solicitudId`, `similitud`, `decisión`, `errorCode`. Jamás bytes, base64, contenido de imágenes ni embeddings.
- ARGOS debe estar en una red privada; el frontend no lo conoce.
- `ApiKey` opcional en header `X-Service-Key` si ARGOS lo requiere.

## 6. Manejo de errores

| Escenario | Resultado para el usuario | HTTP |
|---|---|---|
| Rostros coinciden | `Aprobada` | 204 |
| Rostros no coinciden | `Rechazada` ("El rostro no coincide con el documento") | 409 |
| No se detecta rostro en DNI o selfie | `Rechazada` ("No se detectó un rostro en la imagen") | 409 |
| ARGOS no responde | "Servicio de verificación no disponible, intente más tarde" | 503 |
| Solicitud pendiente/aprobada previa | Errores de dominio existentes | 409 |

## 7. Tests

**Unitarios:**

- `VerificadorIdentidadArgosHttpTests`: mapeo de respuesta exitosa, no coincidencia, excepción de red, excepción de timeout.
- `EnviarSolicitudKycCommandHandlerTests`: aprobación automática con mock de `IVerificadorIdentidadArgos`, rechazo por no coincidencia, rechazo por servicio caído.

**Integración:**

- `KycArgosFlujoTests`: con Testcontainers + mock HTTP de ARGOS (usando `WireMock.Net` o un delegating handler).
  - Subir DNI + selfie con respuesta `verified=true` → estado `Aprobada` + claim `verificado=true`.
  - Subir DNI + selfie con respuesta `verified=false` → estado `Rechazada`.
  - Subir con ARGOS caído → 503.

**Frontend:**

- Actualizar `KycPage.test.tsx` y `AdminKycPage.test.tsx` para los nuevos mensajes y estados.

## 8. Follow-ups diferidos

1. **Liveness por video:** requiere otro modelo/servicio; no lo cubre ARGOS.
2. **OCR del DNI:** extraer nombre, número de documento, etc.
3. **Umbral configurable:** hoy depende del umbral por defecto de ARGOS (`0.68`).
4. **1:N contra base de verificados:** requiere almacenar embeddings y exponer endpoint en ARGOS.
5. **Override manual del admin:** si el negocio lo pide, se puede agregar después.

## 9. Dependencias externas

- ARGOS debe estar desplegado y accesible desde Caserito backend.
- Variables de configuración necesarias:
  - `Argos:Url`
  - `Argos:ApiKey` (opcional, depende de la configuración de ARGOS).
