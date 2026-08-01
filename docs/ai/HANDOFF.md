# Handoff de sesión

## Objetivo

Continuar la autenticación web con Google y Facebook para la PWA, conservando correo/contraseña y la sesión JWT/refresh propia de Caserito.

## Rama y estado de Git

- Rama: `feat/auth-google-facebook`.
- El Bloque 1, tareas 1–3, está implementado y versionado.
- El árbol estaba limpio antes de reemplazar este handoff.
- No se hizo push ni merge.

## Spec y plan activos

- `docs/superpowers/specs/2026-08-01-auth-google-facebook-design.md`
- `docs/superpowers/plans/2026-08-01-auth-google-facebook.md`

## Implementado

### Tarea 1 — Emisión común de sesión

- `IEmisorSesion`/`EmisorSesion` centralizan roles, permisos, KYC, identidad habilitada, JWT y refresh.
- Login tradicional usa el emisor común.
- Refresh consume el token anterior y delega la nueva sesión completa al emisor, conservando la detección defensiva de reutilización.
- La cookie refresh sigue siendo responsabilidad de Host.

### Tarea 2 — Google y Facebook configurables

- Paquetes Google/Facebook `10.0.0` con versiones centralizadas.
- Bearer permanece como esquema predeterminado de autenticación y challenge de la API.
- Cookie `IdentityConstants.ExternalScheme`: 10 minutos, `HttpOnly`, `SameSite=Lax`, ruta `/api/auth/external` y `Secure` fuera de Development/Testing.
- Credenciales tipadas bajo `Authentication:Google` y `Authentication:Facebook`; `appsettings.json` solo contiene valores vacíos.
- Fuera de Development/Testing ambos proveedores se validan al arrancar; en esos entornos pueden quedar deshabilitados.
- `GET /api/auth/external/providers` anuncia únicamente proveedores con el par de credenciales completo.
- La factory de integración usa valores explícitamente ficticios; no se llama a Google ni Meta.

### Tarea 3 — Login externo pendiente protegido

- Modelo `LoginExternoPendiente` y proyección segura sin email ni clave externa.
- Gestor Data Protection con propósito `CaseritoApp.LoginExternoPendiente.v1`, `TimeProvider` y vigencia máxima de 10 minutos.
- Lista cerrada `google`/`facebook`, normalización y validación de retorno local.
- Facebook fuerza `EmailConfiable=false`; Google conserva únicamente la decisión recibida del mapeo posterior.
- Cookie pendiente corta, `HttpOnly`, `SameSite=Lax` y limitada a `/api/auth/external`.
- Tickets expirados o manipulados fallan sin registrar ni devolver PII.

## Decisiones vigentes

- Se mantienen todas las decisiones del spec; no surgieron contradicciones ni ampliaciones.
- No hay credenciales reales ni tráfico de red a proveedores en tests.
- No se persisten tokens sociales.

## Verificaciones ejecutadas

- Unitarias dirigidas: `LoginExterno` — 9/9 verdes.
- Integración dirigida: `AuthFlowTests`, `RefreshTokensTests` y `AuthExternaConfiguracionTests` — 17/17 verdes con Testcontainers.MsSql.
- `dotnet build CaseritoApp.sln --no-restore --verbosity quiet` — correcto, 0 advertencias y 0 errores.
- `dotnet format CaseritoApp.sln --verify-no-changes --no-restore --verbosity quiet` — limpio.
- Hooks de formato superados en los tres commits funcionales.

## Commits del bloque

- `6a016d9 refactor(auth): centraliza emisión de sesión`
- `b17c061 feat(auth): configura Google y Facebook`
- `87a90a9 feat(auth): protege login externo pendiente`

## Tarea exacta siguiente

Iniciar el Bloque 2 por la **Tarea 4: Challenge, callback y login de usuario ya asociado**. Leer únicamente esa sección del plan y las secciones de flujos/seguridad del spec que necesite.

Crear `AuthExternaEndpoints.cs`, mapearlo en `Program.cs` y comenzar con `AuthExternaLoginTests.cs` rojo. La prueba debe sustituir el esquema externo por una identidad controlada, sin red, y cubrir sesión/refresh de un usuario asociado, cancelación sin alta, retorno externo reemplazado por `/perfil` e idempotencia.

## Riesgos o bloqueos

- Ninguno para iniciar la Tarea 4.
- Las credenciales ficticias de Testing solo permiten construir los esquemas; los tests de callback deben instalar handlers controlados antes de iniciar un challenge.
- Las pruebas manuales reales requerirán más adelante credenciales sandbox y callbacks HTTPS, pero no forman parte de la suite automatizada.
