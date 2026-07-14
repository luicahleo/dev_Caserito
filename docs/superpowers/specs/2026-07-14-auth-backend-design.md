# Diseño: Auth backend (Fase 1 — Bloque B1)

- **Fecha**: 2026-07-14
- **Estado**: Aprobado (brainstorming)
- **Alcance**: Backend de identidad y sesión del contexto **Identity**: ASP.NET Core Identity + JWT (access) + refresh token en cookie httpOnly, endpoints de registro/login/refresh/logout, perfil (ver/editar) vía CQRS, y la **primera migración real** (tablas de Identity). NO incluye el frontend (Bloque B2) ni RBAC operativo/KYC (bloques posteriores).

## Contexto

Fase 1 (Identidad) por bloques; el Bloque A ya dejó contenedores + SQL Server + el host conectado (migrate-on-dev) + Testcontainers. Preferencia del equipo: **reutilizar frameworks maduros** → auth con **ASP.NET Core Identity**. Decisión de sesión: **access token JWT en memoria (cliente) + refresh token en cookie httpOnly Secure**. Como la SPA siempre llama a la API a través de su propio origen (proxy de Vite en dev, NGINX en prod), la cookie es *same-site* → `SameSite=Strict` mitiga CSRF sin CORS con credenciales cross-origin.

## Decisiones tomadas

| Tema | Decisión |
|---|---|
| Identidad | **ASP.NET Core Identity** (`ApplicationUser : IdentityUser<Guid>`, `IdentityRole<Guid>`); hashing/lockout/validación de Identity |
| DbContext | `IdentityDbContext` hereda de `Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`, schema `identity` |
| Access token | **JWT** corto (~15 min) con claims `sub`, `email`, `name` (roles luego); firma HS256, clave en user-secrets/env |
| Refresh token | Token opaco aleatorio, **almacenado hasheado en BD** (entidad `RefreshToken`), con expiración y **rotación** en cada uso; entregado en **cookie httpOnly, Secure, SameSite=Strict, Path=/api/auth** |
| Endpoints auth | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout` (minimal API, fuera del pipeline MediatR) |
| Perfil | `GET /api/perfil`, `PUT /api/perfil` vía **CQRS** (MediatR + `Result`), `[Authorize]` |
| Migración | Primera migración EF real del contexto Identity (tablas de Identity + `RefreshToken`) |
| Tests | Integración con **Testcontainers.MsSql**: registro→login→acceso a perfil→refresh→logout |

## Modelo

- `ApplicationUser : IdentityUser<Guid>` con campos de perfil: `Nombre` (string), `Ciudad` (string). (El estado de verificación KYC y el "vendedor verificado" llegan en el bloque KYC.)
- `RefreshToken` (aggregate simple en Domain o entidad en Infrastructure): `Id`, `UserId`, `TokenHash` (nunca el token en claro), `ExpiraEn`, `CreadoEn`, `RevocadoEn?`, `ReemplazadoPorHash?` (para rotación/auditoría).
- `IdentityDbContext` hereda del contexto de Identity de Microsoft; `OnModelCreating` mantiene `HasDefaultSchema("identity")` y llama a `base.OnModelCreating`. Resolver la colisión de nombre con la clase base de Microsoft con un `using` calificado/alias.

## Flujo de sesión

- **Registro**: `UserManager.CreateAsync` (aplica política de contraseñas de Identity). Devuelve `Result`; errores de Identity → `Result.Fallo`.
- **Login**: `SignInManager.CheckPasswordSignInAsync` (respeta lockout). Si ok: emite **access JWT** (cuerpo de la respuesta) + crea un **refresh token**, lo guarda hasheado y lo pone en la **cookie httpOnly**.
- **Refresh**: lee la cookie, valida el refresh (no expirado, no revocado), **rota** (revoca el anterior, emite uno nuevo + nueva cookie) y devuelve un nuevo access JWT. Rotación con detección de reuso (si llega un refresh ya revocado, se revoca toda la cadena del usuario).
- **Logout**: revoca el refresh actual y borra la cookie.
- Claves y parámetros JWT (issuer, audience, key) desde configuración (user-secrets/env; nunca versionados). El host añade `AddAuthentication().AddJwtBearer(...)` + `AddAuthorization()`.

## Perfil (CQRS)

- `ObtenerPerfilQuery` → datos del usuario autenticado (id del claim `sub`).
- `ActualizarPerfilCommand(Nombre, Ciudad)` → actualiza vía `UserManager`; `Result`.
- Endpoints `[Authorize]` que despachan por MediatR y traducen `Result` a HTTP (200 / 400 ProblemDetails).

## CORS y cookie

- Dev: la SPA llama por su propio origen (proxy de Vite → `api`), así que no se necesita CORS con credenciales cross-origin; la cookie de refresh viaja same-site. Se configura CORS mínimo solo si se requiere acceso directo.
- Cookie de refresh: `HttpOnly=true`, `SameSite=Strict`, `Path=/api/auth`, y **`Secure` dependiente del entorno**: `true` en Production/https, `false` en Development y Testing (para que el flujo funcione sobre http local y en el TestServer de los tests de integración). `SameSite=Strict` funciona igual sobre http, así que la protección CSRF se mantiene en dev.

## Migración y arranque

- Se genera la primera migración EF del contexto Identity (`dotnet ef migrations add`), que crea las tablas de Identity + `RefreshToken` bajo schema `identity`.
- `MigrateAsync` en Development (Bloque A) la aplica al arrancar; en tests, la fixture de Testcontainers migra.
- Como `IdentityDbContext` ahora tiene modelo real, EF necesita el paquete `Microsoft.EntityFrameworkCore.Design` y (para generar migraciones) que el proyecto tenga un punto de diseño; documentar el comando `dotnet ef` (contexto y proyecto de startup = host).

## Tests (Testcontainers)

Sobre `CaseritoApiFactory` (Bloque A): un flujo de integración que registra un usuario, hace login (recibe access + cookie de refresh), llama a `/api/perfil` con el bearer (200), actualiza el perfil, usa `/api/auth/refresh` (rota y devuelve nuevo access), y `/api/auth/logout` (invalida). Verifica también rechazo sin token (401) y credenciales inválidas.

## Fuera de alcance (a bloques posteriores)

- **Frontend** (store de sesión, interceptor, pantallas) → Bloque B2.
- **RBAC operativo** (permisos en claims/policies, seed de roles) → bloque RBAC.
- **KYC + PII** (documentos, estado de verificación, badge) → bloque KYC.
- Confirmación de email, 2FA, reset de contraseña (Identity los soporta; se activan cuando se necesiten).

## Verificación

1. `dotnet ef migrations add` genera la migración de Identity; `dotnet build` 0 warnings.
2. `dotnet test` (Testcontainers) verde: registro→login→perfil→refresh→logout, más 401 sin token y login inválido.
3. Levantando el stack dev (`rebuild.ps1`), `POST /api/auth/register` + `POST /api/auth/login` devuelven access JWT y setean la cookie de refresh; `GET /api/perfil` con el bearer responde 200; `POST /api/auth/refresh` rota; `POST /api/auth/logout` invalida.
4. No hay secretos (clave JWT, etc.) en archivos versionados; el refresh se guarda **hasheado** (nunca en claro) en BD.
5. `dotnet format --verify-no-changes` limpio; reglas de arquitectura del contexto Identity siguen verdes.
