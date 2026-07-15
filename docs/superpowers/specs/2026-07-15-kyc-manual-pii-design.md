# Diseño — Bloque "KYC manual + PII" (backend)

> Bloque de Fase 1 (Identidad). Deriva del brief `marketplace-bolivia-mvp-brief.md`
> (§2.1, §2.3, §4.4, §8.1, §10) y del plan `plan-desarrollo-mvp-v1.md` (Fase 1).
> Fecha: 2026-07-15.

## 1. Objetivo y alcance

Permitir que un usuario suba su documento de identidad (CI) + selfie para
verificación **manual** por un administrador, almacenando esa PII biométrica
cifrada en reposo y con acceso auditado, y exponiendo el estado "verificado"
en el perfil. Es el activo de mayor riesgo legal del MVP (brief §10), por lo
que el cuidado de PII es el eje del diseño.

**Dentro de este bloque (todo backend, bounded context Identity):**

- Dominio de verificación KYC con historial de solicitudes y máquina de estados.
- Subida de CI + selfie (endpoint multipart).
- Almacenamiento de blobs cifrados en reposo, fuera de la BD, vía puerto/adaptador.
- Acceso auditado a los blobs (log append-only).
- Casos de uso de administrador: listar, ver documento/selfie, aprobar, rechazar.
- Emisión del evento de integración `UserVerified` al aprobar.
- Exposición del badge "verificado": derivado en `GET /api/perfil` + claim `verificado` en el JWT.
- Tests unitarios (máquina de estados) e integración (Testcontainers).

**Fuera de este bloque (queda para el siguiente, "panel admin / UI"):**

- Toda la UI: pantalla de subida del usuario y panel admin en React. Consumirán esta API.
- Job de purga automática por política de retención (ver §8, follow-up).
- KYC automático (OCR/liveness) — fuera de v1.0 por completo (brief §2.2).

## 2. Dominio (`Identity.Domain/Kyc/`)

Los **bytes de las imágenes nunca entran al Domain**: el dominio solo maneja
**referencias opacas** (claves de blob). El cifrado y la escritura física
ocurren en Infrastructure.

### `VerificacionKyc : AggregateRoot`

Un agregado por usuario (`UserId`), que agrupa el **historial** de solicitudes.

- `EnviarSolicitud(referenciaDocumento, referenciaSelfie, tipoDocumento)`:
  crea una `SolicitudKyc` en estado `Pendiente`.
  - Invariante: rechaza (`Result` con `Error`) si ya hay una solicitud `Pendiente`
    (`Kyc.SolicitudPendienteExiste`) o si el usuario ya está `Aprobado`
    (`Kyc.YaVerificado`). **Decisión MVP: no hay re-verificación tras aprobado.**
  - Reintento tras `Rechazada` sí se permite: crea una **nueva** solicitud sin
    borrar la anterior (trazabilidad + base para OCR futuro, brief §4.4).
- `Aprobar(solicitudId, revisorId)`: solo válido desde `Pendiente`
  (`Kyc.TransicionInvalida` en otro caso). Setea `Estado=Aprobada`, `ResueltaEn`,
  `ResueltaPor`. Levanta evento de dominio que se traduce a `UserVerified`.
- `Rechazar(solicitudId, revisorId, motivo)`: solo desde `Pendiente`. Setea
  `Estado=Rechazada`, `MotivoRechazo`, `ResueltaEn`, `ResueltaPor`.

### `SolicitudKyc : Entity`

- `Id` (Guid)
- `Estado`: `EstadoKyc` — `Pendiente | Aprobada | Rechazada`
- `ReferenciaDocumento` / `ReferenciaSelfie`: claves opacas de blob (string), nunca bytes
- `TipoDocumento`: `CI` (enum extensible)
- `MotivoRechazo`: string nullable
- `EnviadaEn`: `DateTimeOffset`
- `ResueltaEn`: `DateTimeOffset?`
- `ResueltaPor`: `Guid?` (id del revisor)

### Estado efectivo

El estado de verificación del usuario es el de su **última solicitud**
(por `EnviadaEn`). El badge "verificado" es cierto si existe una solicitud
`Aprobada`.

### Errores de dominio

`Kyc.SolicitudPendienteExiste`, `Kyc.YaVerificado`, `Kyc.SolicitudNoEncontrada`,
`Kyc.TransicionInvalida`. Mapeo a HTTP en §6.

## 3. Cifrado y almacenamiento

### `IEncryptor` (BuildingBlocks.Infrastructure.Security)

Se añade path binario a la abstracción existente:

```csharp
byte[] Cifrar(byte[] datos);
byte[] Descifrar(byte[] datos);
```

`PassthroughEncryptor` (dev) los implementa como identidad. El **envelope
encryption** real (DEK por blob envuelta por KEK del KMS) queda como detalle
**interno del adaptador real** cuando se defina el hosting/KMS; el resto del
sistema solo ve "texto plano ↔ cifrado". No se modela DEK/KEK en el dominio.

### `IAlmacenBlobsKyc` (Identity.Application, puerto)

```csharp
Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct); // → clave opaca
Task<BlobKyc> ObtenerAsync(string clave, CancellationToken ct);                          // → bytes + contentType
Task EliminarAsync(string clave, CancellationToken ct);
```

Cifra con `IEncryptor` **antes** de escribir y descifra al leer.

### `AlmacenBlobsKycDisco` (Identity.Infrastructure, adaptador dev)

Escribe los blobs cifrados a un **volumen de disco montado**, fuera de la BD,
con claves opacas (GUID). Ruta base configurable por env. En prod se sustituye
por un adaptador de object storage (S3/MinIO/Azure Blob) sin tocar Domain ni
Application.

## 4. Casos de uso (`Identity.Application/Kyc/`, CQRS-lite)

### Usuario final (`[Authorize]`)

- `EnviarSolicitudKycCommand(userId, documento, selfie)`:
  1. Valida tipo/tamaño (ver §6).
  2. Guarda ambos blobs vía `IAlmacenBlobsKyc` → obtiene referencias.
  3. Carga/crea el agregado `VerificacionKyc`, llama `EnviarSolicitud`, persiste.
  4. **Compensación**: si la persistencia falla tras escribir blobs, se intenta
     `EliminarAsync` de los blobs recién escritos (best-effort, registrado sin PII).
- `ObtenerEstadoKycQuery(userId)`: estado efectivo + motivo de rechazo si aplica.

### Administrador (policy `kyc.revisar`)

- `ListarSolicitudesKycQuery(estado?, pagina, tamano)`: paginado, **sin blobs**
  (solo metadatos). Reutiliza el patrón `ResultadoPaginado` del Bloque D.
- `ObtenerDocumentoKycQuery(solicitudId)` / `ObtenerSelfieKycQuery(solicitudId)`:
  descifra el blob y **registra el acceso** vía `IPiiAccessAuditor.RegistrarAccesoAsync`
  (recurso = clave/solicitud, actor = id del admin) antes de devolver los bytes.
- `AprobarSolicitudKycCommand(solicitudId, revisorId)`.
- `RechazarSolicitudKycCommand(solicitudId, revisorId, motivo)`.

### Puertos

- `IRepositorioVerificacionKyc` (cargar por usuario / por solicitud, persistir).
- `IAlmacenBlobsKyc` (§3).

## 5. Infraestructura y persistencia

- Configuración EF Core de `VerificacionKyc` + `SolicitudKyc` en
  **`IdentityDbContext`**, schema `identity` (mismo bounded context).
- Migración nueva: **`KycInicial`**.
- Emisión de `UserVerified` mediante el dispatcher de eventos de integración
  existente al aprobar una solicitud.
- Registro de repositorio y adaptador de blobs en `DependencyInjection` de Identity.

## 6. Endpoints (Host)

| Método | Ruta | Auth | Contrato |
|---|---|---|---|
| POST | `/api/kyc` | `[Authorize]` | multipart (documento + selfie) → `201/204`; `400` validación; `409` ya pendiente/aprobado |
| GET | `/api/kyc/estado` | `[Authorize]` | `→ { estado, motivoRechazo? }` |
| GET | `/api/admin/kyc?estado=&pagina=&tamano=` | policy `kyc.revisar` | listado paginado de solicitudes (metadatos) |
| GET | `/api/admin/kyc/{solicitudId}/documento` | policy `kyc.revisar` | bytes del documento (auditado) |
| GET | `/api/admin/kyc/{solicitudId}/selfie` | policy `kyc.revisar` | bytes de la selfie (auditado) |
| POST | `/api/admin/kyc/{solicitudId}/aprobar` | policy `kyc.revisar` | `→ 204`; `404`; `409` transición inválida |
| POST | `/api/admin/kyc/{solicitudId}/rechazar` | policy `kyc.revisar` | `(motivo) → 204`; `404`; `409` |

- La policy `kyc.revisar` **ya existe** (permiso `Permisos.KycRevisar`, mapeado a
  `AdminKyc` y `AdminPlataforma`). No se toca RBAC.
- **Validación de subida**: whitelist de content-type (`image/jpeg`, `image/png`),
  límite de tamaño por archivo, y verificación de *magic bytes* (no confiar en la
  extensión ni en el content-type declarado).
- Mapeo `Result`→HTTP con el helper de `ValidationProblem` ya extraído en el Bloque D.

## 7. Badge "verificado"

- `GET /api/perfil`: se extiende `PerfilDto` con `verificado: bool`, derivado del
  estado KYC efectivo (se extiende `IRepositorioPerfil`/su adaptador).
- **Claim `verificado` en el JWT**: se extiende la firma de `IGeneradorTokensAcceso`
  para recibir la señal de verificación, calculada en login y refresh desde el
  estado KYC. Se propaga al **siguiente refresh (≤ 15 min)**, consistente con la
  decisión stateless ya tomada para roles (Bloque D).

## 8. PII, auditoría y retención

- **Anti-PII en logs (no negociable)**: jamás loguear bytes de CI/selfie, ni claves
  de blob, ni content del documento. Usar `PiiRedaction` para cualquier campo
  sensible. Los logs de auditoría de cambios de estado solo llevan ids/estado/
  acción/resultado (sin email), siguiendo el patrón `[LoggerMessage]` del Bloque D.
- **Acceso auditado**: cada lectura de un blob por un admin genera una entrada
  append-only vía `IPiiAccessAuditor` (actor = id del admin, recurso = solicitud/clave).
- **Acceso mínimo**: los blobs solo se sirven bajo la policy `kyc.revisar`; el
  usuario dueño ve su estado pero no un endpoint de descarga de su propio blob
  (no se necesita en el MVP).
- **Retención**: se capturan timestamps (`EnviadaEn`, `ResueltaEn`) y se documenta
  la política de retención. La **purga automática queda como follow-up**
  (no bloqueante; YAGNI al volumen de ~20 usuarios). Se deja anotada como deuda.

## 9. Tests

- **Unit (`CaseritoApp.UnitTests`)**: máquina de estados de `VerificacionKyc`
  — transiciones válidas e inválidas, invariante de un solo `Pendiente`, bloqueo
  de reenvío tras `Aprobado`, reintento permitido tras `Rechazada`, aprobar/rechazar
  solo desde `Pendiente`.
- **Integración (Testcontainers.MsSql, `CaseritoApiFactory`)**: flujo end-to-end
  — subir CI+selfie → listar como admin → acceder a blob (verificar que se escribió
  la entrada de auditoría) → aprobar → `UserVerified` emitido → badge `verificado`
  en `GET /api/perfil` y claim `verificado` en el token tras refresh.
- **Aserciones de PII**: no aparece PII en logs; los blobs en disco pasan por el
  pipeline de cifrado (con `PassthroughEncryptor` se valida el pipeline, no el
  cifrado real).

## 10. Deja preparado el futuro

- El historial de solicitudes con documento+selfie+estado permite migrar a
  OCR/liveness automático sin re-solicitar documentos (brief §4.4).
- El puerto `IAlmacenBlobsKyc` y el path binario de `IEncryptor` permiten cambiar
  a object storage + KMS real sin tocar dominio ni casos de uso.
- El evento `UserVerified` ya deja el gancho para que otros contextos reaccionen
  (p. ej. "acceso a QR/envío solo verificados", brief §9.2).

## 11. Follow-ups (no bloqueantes)

1. Job de purga automática según política de retención.
2. Adaptador real de object storage (S3/MinIO/Azure) + envelope encryption con KMS.
3. Endurecimiento de subida: escaneo antivirus/malware de los archivos subidos.
4. Rate-limiting del endpoint de subida de KYC.
