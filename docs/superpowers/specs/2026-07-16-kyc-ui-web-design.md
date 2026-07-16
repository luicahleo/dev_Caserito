# KYC — UI web (subida del usuario + revisión del admin)

**Fecha:** 2026-07-16
**Estado:** aprobado (brainstorming)
**Contexto:** el backend de KYC manual + PII (Fase 1) ya está completo. Este bloque
añade las dos pantallas web que lo consumen.

## Objetivo

Dar al frontend web (`web/`) dos flujos:

1. **Usuario:** subir documento + selfie y ver el estado de su verificación.
2. **Administrador** (permiso `kyc.revisar`): listar solicitudes, previsualizar los
   documentos y aprobar/rechazar.

## Contrato del backend (ya existente)

**Usuario** — `/api/kyc`, requiere sesión:

- `POST /` — multipart con `documento` + `selfie` (IFormFile). No lleva otros campos.
  → `204` | `400` (validación) | `409` (`YaVerificado`, `SolicitudPendienteExiste`).
- `GET /estado` → `{ estado, motivoRechazo }`.
  Estados: `NoIniciado`, `Pendiente`, `Aprobada`, `Rechazada`.

**Admin** — `/api/admin/kyc`, requiere permiso `kyc.revisar`:

- `GET /?estado=&pagina=&tamano=` → `SolicitudKycResumen[]`
  (`solicitudId`, `usuarioId`, `estado`, `tipoDocumento`, `enviadaEn`, `resueltaEn`).
- `GET /{id}/documento` y `GET /{id}/selfie` → archivo de imagen (endpoint protegido:
  requiere header `Authorization`, no sirve para `<img src>` directo).
- `POST /{id}/aprobar` → `204`.
- `POST /{id}/rechazar` con `{ motivo }` → `204`.

**Claims del JWT** (ya emitidos): `perm` (uno por permiso, incluye `kyc.revisar` para
admins) y `verificado` (`"true"`/`"false"`). El access token vive en memoria en el
cliente (`auth/session`).

**Límites de imagen (a espejar en el cliente):** tipos `image/jpeg` y `image/png`;
tamaño máximo **5 MiB** por archivo.

## Decisiones (brainstorming)

- Estado del usuario: **página `/kyc` dedicada** + **chip resumen en `/perfil`** con enlace.
- Gate de admin: **decodificar el claim `perm` del JWT** en el frontend (sin llamada extra).
- Validación de archivos: **espejar límites del backend en el cliente** (tipo + tamaño).

## Rutas

| Ruta | Guard | Contenido |
|------|-------|-----------|
| `/kyc` | `ProtectedRoute` (sesión) | Formulario de subida + máquina de estados |
| `/admin/kyc` | `ProtectedRoute` + permiso `kyc.revisar` | Lista + panel de revisión |
| `/perfil` | (existente) | Se añade chip de estado con enlace a `/kyc` |

## Componentes

### Capa API (`web/src/api/`)

Extender `client.ts` con dos capacidades nuevas, reutilizando el flujo refresh-on-401:

- `postForm(ruta, FormData)` — multipart; **no** fijar `Content-Type` a mano (el browser
  pone el boundary).
- `getBlob(ruta)` — descarga con `Authorization`; devuelve `Blob`.

Nuevo `api/kyc.ts`:

```ts
export type EstadoKyc = 'NoIniciado' | 'Pendiente' | 'Aprobada' | 'Rechazada';
export interface EstadoKycDto { estado: EstadoKyc; motivoRechazo: string | null; }
export interface SolicitudKycResumen {
  solicitudId: string; usuarioId: string; estado: EstadoKyc;
  tipoDocumento: string; enviadaEn: string; resueltaEn: string | null;
}
obtenerEstadoKyc(): Promise<EstadoKycDto>
enviarKyc(documento: File, selfie: File): Promise<void>
listarSolicitudesKyc(estado?, pagina?, tamano?): Promise<SolicitudKycResumen[]>
obtenerImagenKyc(solicitudId, tipo: 'documento' | 'selfie'): Promise<string> // objectURL
aprobarKyc(solicitudId): Promise<void>
rechazarKyc(solicitudId, motivo): Promise<void>
```

### AuthContext + JWT (`web/src/auth/`)

- Nuevo `jwt.ts`: `decodificarClaims(token)` puro (base64url del payload, sin librería).
  Tolera `perm` como string único o array; `verificado` como `"true"`.
- `AuthContext` expone `permisos: string[]`, `verificado: boolean`, `tienePermiso(p)`.
  Se recalcula cuando cambia el access token (login, refresh, registro).
- Nuevo guard `RequierePermiso` (envuelve `ProtectedRoute`): sin el permiso → `Navigate`
  a `/perfil`. Oculta enlace y ruta de admin a usuarios normales.

### Página usuario `/kyc` (`KycPage.tsx`)

Máquina de estados según `GET /estado`:

| Estado | UI |
|--------|-----|
| `NoIniciado` | Formulario: file inputs documento + selfie con previsualización local, validación cliente (jpg/png ≤5 MiB), botón Enviar |
| `Pendiente` | Mensaje "En revisión", sin formulario |
| `Aprobada` | Chip verde "Identidad verificada" |
| `Rechazada` | `Alert` con `motivoRechazo` + formulario para reenviar |

- TanStack Query: `useQuery` del estado, `useMutation` de envío (invalida el estado al éxito).
- 409 (`YaVerificado`, `SolicitudPendienteExiste`) → mensaje amable + refetch.

### Página admin `/admin/kyc` (`AdminKycPage.tsx`)

- **Lista:** `useQuery` de `listarSolicitudesKyc`, filtro por estado (default `Pendiente`),
  tabla MUI (usuario, tipo doc, fecha, estado, botón Revisar).
- **Detalle** (panel/dialog): previsualización de documento y selfie vía
  `obtenerImagenKyc` (objectURL, liberado con `URL.revokeObjectURL` al desmontar);
  botón Aprobar y botón Rechazar (campo motivo obligatorio).
- Las mutations invalidan la lista al resolver.

## Errores y política anti-PII

- Errores de red/validación → `Alert`/`Snackbar` en español.
- **Nunca** loguear en consola contenido de imágenes, tokens ni respuestas de blob.
- Los `objectURL` se revocan siempre (no retener referencias a documentos).

## Testing (Vitest + RTL)

- `jwt.test.ts` — token válido, sin claim, `perm` único vs array.
- `KycPage.test.tsx` — cada estado renderiza lo correcto; validación cliente rechaza
  archivo grande / tipo inválido; envío exitoso.
- `AdminKycPage.test.tsx` — lista visible; aprobar/rechazar llaman al endpoint; rechazo
  exige motivo.
- Mocks de la capa `api/kyc`.

## Fuera de alcance

- Generación del cliente tipado desde OpenAPI (se hará en su propio bloque).
- Notificaciones push al usuario cuando cambia el estado.
- Paginación avanzada / búsqueda en la lista de admin (solo filtro por estado + páginas simples).
