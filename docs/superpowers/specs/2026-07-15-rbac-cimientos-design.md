# Diseño: Cimientos de RBAC + fix de `Jwt:Key`

- **Fecha**: 2026-07-15
- **Estado**: Aprobado (brainstorming)
- **Alcance**: Cimientos de RBAC (una feature/bloque por sesión) más el fix del bug diferido
  de `Jwt:Key`. NO incluye UI de administración, endpoints reales de moderación/KYC
  (Fase 2), ni asignación/gestión de roles por admin (bloque posterior).

## Contexto

CaseritoApp es un marketplace C2C para Bolivia. El brief §8.1 exige diseñar el modelo
de **roles y permisos desde v1.0** pensando en el crecimiento del equipo, con una
decisión de diseño explícita: **separar Roles de Permisos** (RBAC simple), en vez de
hardcodear `if (user.Role == Admin)`. Así, cuando el equipo crezca (p. ej. un moderador
que no es admin de plataforma), solo se reasignan permisos a roles, sin reescribir la
lógica de autorización.

Estado actual (verificado en código):

- Existe el andamiaje de Identity Roles (`AddRoles<IdentityRole<Guid>>`, tablas
  `AspNetRoles`/`AspNetUserRoles`/`AspNetRoleClaims` en la migración `InicialIdentity`),
  pero **no hay RBAC funcional**: sin claims de rol/permiso en el JWT, sin `RoleManager`
  en uso, sin `[Authorize(Roles=)]` ni policies, y `ApplicationUser` no toca roles.
- `Identity.Domain` está vacío (sin archivos fuente).
- Bug de `Jwt:Key`: en Development, si falta la clave, el arranque no falla (por diseño,
  `permiteClaveEfimera`), pero la **validación** usa una clave efímera GUID mientras que
  el **generador** firma con `_o.Key` (vacío) → login/validación rotos con firma y
  validación divergentes. Además la validación relee la config a mano
  (`DependencyInjection.cs:95`) en vez de usar el `IOptions<OpcionesJwt>` bindeado.

## Roles del MVP (brief §8.1, plan línea 77)

| Rol | Operativo en v1 | Responsabilidad de permisos |
|---|---|---|
| **Cliente** | Sí (rol por defecto de todo registrado; comprador y vendedor) | Baseline, sin permisos elevados |
| **Moderador** | Sí | Revisar/ocultar/eliminar publicaciones y chat reportados |
| **Admin KYC** | Sí | Aprobar/rechazar verificación de identidad |
| **Admin plataforma** | Sí | Acceso total / gestión |
| **Soporte** | No operativo aún, definido | Tickets (futuro Disputes) |
| **Sistema** (machine user) | No operativo aún, definido | Automatización (OCR/bots) |

**"Vendedor verificado" NO es un rol**: es un *atributo/estado* del Cliente (badge de KYC
aprobado). Se modela como atributo, fuera del alcance de este bloque.

## Sección 1 — Modelo: Roles ≠ Permisos

Autorización por **permiso**, no por rol. Los roles agrupan permisos; el código chequea
permisos. Todo el modelo puro vive en `Identity.Domain` (hoy vacío), sin dependencias:

- **`RolesApp`** (constantes): `Cliente`, `Moderador`, `AdminKyc`, `AdminPlataforma`,
  `Soporte`, `Sistema`.
- **`Permisos`** (constantes), set inicial alineado a los contextos MVP:
  - `publicaciones.moderar`
  - `chat.moderar`
  - `kyc.revisar`
  - `usuarios.gestionar`
  - `soporte.tickets`
  - Ampliable en Fase 2 sin cambiar el mecanismo.
- **Mapa Rol→Permisos** (dato puro en `Identity.Domain`):
  - `Cliente` → {} (baseline)
  - `Moderador` → { `publicaciones.moderar`, `chat.moderar` }
  - `AdminKyc` → { `kyc.revisar` }
  - `AdminPlataforma` → todos los permisos
  - `Soporte` → { `soporte.tickets` } (definido, no operativo en v1)
  - `Sistema` → {} por ahora (definido, no operativo)

## Sección 2 — Persistencia y seeding

- Los permisos de cada rol se persisten como **RoleClaims** (`AspNetRoleClaims`, tabla ya
  existente) con claim type `perm`.
- **Seeder idempotente** ejecutado al arrancar, **después** de aplicar migraciones:
  crea los 6 roles si faltan y **sincroniza** sus RoleClaims al mapa de `Domain`
  (agrega los que falten; no duplica). Re-ejecutarlo no produce cambios.
- **Registro** (`POST /api/auth/register`): a cada usuario nuevo se le asigna el rol
  **`Cliente`** por defecto tras `CreateAsync`.

## Sección 3 — Claims en el JWT y autorización

- **Generador de JWT** (`GeneradorTokensAcceso`): además de `sub/email/name`, emite los
  **permisos agregados** del usuario (unión de los permisos de todos sus roles) como
  claims con type `perm` (uno por permiso). Chequeo de autorización stateless, sin
  consulta a BD.
  - **Decisión (aprobada)**: se emiten **permisos**, no roles. Ventaja: autz sin BD y
    alineado a "chequear permiso". Trade-off: un cambio en los permisos de un rol se
    refleja al renovar el access token (≤ 15 min). Aceptable para el MVP.
- **Policies** basadas en permiso: helper `RequirePermission("<permiso>")` (registro de
  policies o requisito `RequireClaim("perm", ...)` equivalente). Sustituye a
  `[Authorize(Roles=)]`.
- **Endpoint de ejemplo** (andamiaje mínimo, marcado como tal): `GET /api/admin/ping`,
  protegido con una policy de permiso (p. ej. `usuarios.gestionar`), para demostrar y
  testear el pipeline end-to-end. Los endpoints reales de moderación/KYC son Fase 2.

## Sección 4 — Fix de `Jwt:Key`

Prerequisito real: sin esto, emitir claims nuevos en el JWT arrastra el mismo bug.

- **Clave compartida**: una clave efímera única (singleton) que usen **tanto** el
  generador como la validación en Development/Testing cuando `Jwt:Key` falte. Firma y
  validación nunca divergen.
- La validación consume `IOptions<OpcionesJwt>` (o la misma fuente de clave que el
  generador) en vez de releer `config.GetSection(...).Get<OpcionesJwt>()` a mano.
- Fuera de Development/Testing se mantiene el fail-fast (`ValidateOnStart`, clave ≥ 32
  bytes obligatoria).

## Sección 5 — Tests

- **Unit** (sin BD): el mapa Rol→Permisos, y la agregación de permisos de un usuario con
  múltiples roles (unión correcta, sin duplicados).
- **Integración** (Testcontainers, entorno `Testing`):
  - Seeder idempotente: correrlo 2× no altera roles/claims.
  - Registro asigna rol `Cliente`.
  - Login emite claims `perm` esperados.
  - Endpoint de ejemplo: `200` con el permiso, `403` sin él.
  - Regresión del fix `Jwt:Key`: login + acceso a `/api/perfil` funciona en Testing.

## Política anti-PII

Ningún claim, log ni test debe exponer CI, imágenes de documento/selfie, tokens de
sesión ni cadenas de QR/pago. Los claims `perm` no son PII. Mantener el criterio del
backend.

## Fuera de alcance (bloques posteriores)

- Endpoints/casos de uso para asignar/quitar roles a usuarios (admin).
- Endpoints reales de moderación (ocultar/eliminar publicaciones y chat) y de revisión
  KYC (Fase 2).
- Modelado del atributo "Vendedor verificado" (badge KYC).
- Activación operativa de Soporte y Sistema.
