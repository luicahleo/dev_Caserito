# Diseño: RBAC operativo — gestión de roles por admin

- **Fecha**: 2026-07-15
- **Estado**: Aprobado (brainstorming)
- **Alcance**: Solo backend (API). Gestión de roles de usuarios por un administrador
  (listar catálogo de roles, buscar usuarios con sus roles, asignar y quitar roles) más
  el enforcement por permiso en los endpoints. **NO** incluye UI web (bloque frontend
  aparte, análogo a la partición B1/B2), ni endpoints reales de moderación/KYC (Fase 2),
  ni revocación inmediata de tokens.
- **Predecesor**: `2026-07-15-rbac-cimientos-design.md` (cimientos de RBAC ya mergeados:
  `RolesApp`, `Permisos`, `MapaRolesPermisos`, seeder idempotente, claims `perm` en el
  JWT, policies `RequireClaim("perm", …)`, endpoint ejemplo `GET /api/admin/ping`).

## Contexto

El brief §8.1 exige RBAC desde v1.0 con roles ≠ permisos. Los cimientos ya establecieron
el modelo puro y el pipeline de autorización stateless (permisos agregados en el JWT,
fuente autoritativa `MapaRolesPermisos`). Falta la parte **operativa**: que un
administrador (con el permiso `usuarios.gestionar`) pueda asignar y quitar roles a otros
usuarios, con guardrails que eviten auto-bloqueo y escaladas.

Estado actual verificado en código:

- `Identity.Domain/Autorizacion`: `RolesApp` (6 roles), `Permisos` (5 permisos),
  `MapaRolesPermisos` (mapa autoritativo rol→permisos), `ClaimsApp.Permiso = "perm"`.
- `Identity.Infrastructure/Auth`: generación de JWT con claims `perm`, policies por
  permiso, clave de firma compartida.
- `Identity.Application/Perfil` + `Identity.Infrastructure/Perfil`: patrón puerto
  (`IRepositorioPerfil`) / adaptador sobre `UserManager` + CQRS-lite (MediatR + `Result`
  + FluentValidation). **Este bloque espeja ese patrón.**
- `Host/Endpoints/AdminEndpoints.cs`: grupo `/api/admin` protegido con
  `RequireAuthorization(PoliticasAutorizacion.Permiso(Permisos.UsuariosGestionar))`,
  hoy solo `GET /ping`.
- Registro (`POST /api/auth/register`) asigna el rol `Cliente` por defecto.

## Decisión: propagación de cambios de rol (staleness del JWT)

Los permisos viven en el access token (≤ 15 min) y se recalculan desde `MapaRolesPermisos`
en el próximo login/refresh. **Decisión aprobada: aceptar esa latencia** (≤ 15 min), sin
revocación de refresh ni denylist. Es coherente con la decisión stateless ya tomada en los
cimientos (autz sin consulta a BD por request). Se documenta como comportamiento esperado:
asignar/quitar un rol se refleja en los permisos efectivos del usuario en el siguiente
refresh del access token.

## Sección 1 — Arquitectura (puerto/adaptador + CQRS)

Espeja Perfil; sin infraestructura nueva.

- **Puerto** `IRepositorioRolesUsuario` en `Identity.Application/Autorizacion`, con
  operaciones granulares para que los guardrails con estado vivan en el handler:
  - `Task<ResultadoPaginado<UsuarioConRolesDto>> BuscarUsuariosAsync(string? query, int pagina, int tamano, CancellationToken ct)`
  - `Task<bool> ExisteUsuarioAsync(Guid userId, CancellationToken ct)`
  - `Task<int> ContarEnRolAsync(string rol, CancellationToken ct)`
  - `Task<Result> AgregarRolAsync(Guid userId, string rol, CancellationToken ct)`
  - `Task<Result> QuitarRolAsync(Guid userId, string rol, CancellationToken ct)`
- **Adaptador** `RepositorioRolesUsuarioUserManager` en `Identity.Infrastructure/Autorizacion`,
  sobre `UserManager<ApplicationUser>`:
  - Búsqueda: `userManager.Users` (IQueryable) con filtro `query` sobre email/nombre
    (contains, case-insensitive), `Count()` para el total y `Skip/Take` para la página;
    roles por usuario vía `GetRolesAsync` (N+1 sobre la página, aceptable con `tamano`
    acotado; optimizable con join a `AspNetUserRoles` más adelante).
  - `ExisteUsuarioAsync`: `FindByIdAsync`.
  - `ContarEnRolAsync`: `GetUsersInRoleAsync(rol).Count`.
  - `AgregarRolAsync`/`QuitarRolAsync`: `AddToRoleAsync`/`RemoveFromRoleAsync`,
    traduciendo `IdentityResult` fallido a `Result.Fallo`.
- Registrado en el DI de Identity junto a `IRepositorioPerfil`.

## Sección 2 — Casos de uso (MediatR + Result + FluentValidation)

Ubicación: `Identity.Application/Autorizacion`.

- `ListarRolesQuery` → `IReadOnlyList<RolDto>`. Catálogo derivado de `RolesApp.Todos` +
  `MapaRolesPermisos.PermisosDeRol`. Sin BD.
- `BuscarUsuariosQuery(string? Query, int Pagina, int Tamano)` →
  `ResultadoPaginado<UsuarioConRolesDto>`. Validador: `Pagina ≥ 1`, `Tamano` en `1..100`.
- `AsignarRolCommand(Guid AdminId, Guid TargetUserId, string Rol)` → `Result`.
- `QuitarRolCommand(Guid AdminId, Guid TargetUserId, string Rol)` → `Result`.

**DTOs (en Application):**

- `RolDto(string Rol, IReadOnlyList<string> Permisos)`
- `UsuarioConRolesDto(Guid Id, string Email, string Nombre, string Ciudad, IReadOnlyList<string> Roles)`
- `ResultadoPaginado<T>(IReadOnlyList<T> Items, int Pagina, int Tamano, int Total)`

## Sección 3 — Guardrails

**Estáticos → FluentValidation (→ 400):**

- Solo roles conocidos: `Rol ∈ RolesApp.Todos` (rechaza typos / roles arbitrarios).
- `Sistema` no asignable ni quitable vía API (reservado a aprovisionamiento fuera de banda).
  Aplica a `AsignarRolCommand` y `QuitarRolCommand`.
- No quitarse a sí mismo `AdminPlataforma`: regla cruzada en `QuitarRolCommand`
  `!(AdminId == TargetUserId && Rol == AdminPlataforma)`.

**Con estado → handler (mapeado a HTTP):**

- Usuario inexistente (`ExisteUsuarioAsync` = false) → `404`.
- No eliminar el último `AdminPlataforma`: al quitar `AdminPlataforma`, exigir
  `ContarEnRolAsync(AdminPlataforma) > 1` → si no, `409 Conflict`.
- Idempotencia: asignar un rol ya presente, o quitar un rol ausente → `204` no-op
  (sin error). Los guardrails estáticos y de existencia se evalúan antes.

Códigos de error del `Result` (para mapear en el endpoint): `Roles.UsuarioNoEncontrado`
(→404), `Roles.UltimoAdminPlataforma` (→409). Las violaciones estáticas las lanza el
`ValidationBehavior` de MediatR como `ValidationException` (→400 `ValidationProblem`).

## Sección 4 — Endpoints (Host, ampliando `AdminEndpoints`)

Todos bajo el grupo `/api/admin` ya protegido con
`RequireAuthorization(Permiso(UsuariosGestionar))` (el `/ping` se mantiene). El `AdminId`
se obtiene del claim `sub` (mismo helper que `PerfilEndpoints.TryObtenerUserId`); si falta
→ `401`.

- `GET /api/admin/roles` → `200 [{ rol, permisos[] }]`
- `GET /api/admin/usuarios?query=&pagina=&tamano=` → `200 { items[], pagina, tamano, total }`
  (defaults: `pagina=1`, `tamano=20`).
- `POST /api/admin/usuarios/{id}/roles` body `{ rol }` → `204 | 400 | 401 | 403 | 404 | 409`
- `DELETE /api/admin/usuarios/{id}/roles/{rol}` → `204 | 400 | 401 | 403 | 404 | 409`

Mapeo `Result.Fallo` → HTTP en el endpoint: `Roles.UsuarioNoEncontrado`→404,
`Roles.UltimoAdminPlataforma`→409, resto→400. `ValidationException`→400 `ValidationProblem`.

## Sección 5 — Auditoría y anti-PII

- **Log estructurado** por cambio de rol (`ILogger` en el handler de cada command):
  propiedades `adminId`, `targetUserId`, `rol`, `accion` (`asignar`/`quitar`),
  `resultado` (`ok`/motivo de rechazo). **Sin email ni datos sensibles.** Suficiente para
  trazabilidad del MVP; la auditoría persistente en tabla llega con KYC (Fase 2).
- El `email` aparece en la respuesta de `GET /usuarios` (necesario para identificar
  usuarios en la futura UI admin; no está en la lista "no negociable" de PII: CI,
  imágenes de documento/selfie, tokens de sesión, cadenas de QR/pago). **Nunca en logs.**

## Sección 6 — Tests

- **Unit (sin BD)** — `CaseritoApp.UnitTests`:
  - Validador de `AsignarRolCommand`: rechaza rol desconocido y `Sistema`.
  - Validador de `QuitarRolCommand`: rechaza rol desconocido, `Sistema`, y auto-retiro de
    `AdminPlataforma` (`AdminId == TargetUserId`).
  - Validador de `BuscarUsuariosQuery`: rechaza `Pagina < 1` y `Tamano` fuera de `1..100`.
  - `ListarRolesQuery`: mapea los permisos correctos por rol.
- **Integración (Testcontainers, entorno `Testing`)**:
  - Asignar rol (p. ej. `Moderador`) → aparece en `GET /usuarios`.
  - **Propagación**: tras re-login, el JWT del usuario trae los permisos del rol nuevo.
  - Quitar rol → desaparece de `GET /usuarios`.
  - Guardrail último `AdminPlataforma` → `409`.
  - Auto-retiro del propio `AdminPlataforma` → `400`.
  - `Sistema` no asignable → `400`.
  - Rol desconocido → `400`.
  - Usuario inexistente → `404`.
  - Autorización: sin `usuarios.gestionar` → `403`; con el permiso → `200`.
  - Búsqueda/paginación básica (`query`, `pagina`, `tamano`, `total`).

## Fuera de alcance (bloques posteriores)

- UI web de administración de roles (bloque frontend).
- Endpoints reales de moderación (ocultar/eliminar publicaciones y chat) y de revisión
  KYC (Fase 2).
- Revocación inmediata de tokens / denylist (decisión stateless mantenida).
- Auditoría persistente en tabla dedicada (llega con KYC + PII).
- Activación operativa de `Soporte` y `Sistema`.
