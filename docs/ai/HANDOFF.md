# Handoff de sesión

## Objetivo

Continuar la autenticación web con Google y Facebook para la PWA, conservando correo/contraseña y la sesión JWT/refresh propia de Caserito.

## Rama y estado de Git

- Rama: `feat/auth-google-facebook`.
- Los bloques 1 y 2, tareas 1–6, están implementados y versionados.
- El árbol estaba limpio antes de reemplazar este handoff.
- No se hizo push ni merge.

## Spec y plan activos

- `docs/superpowers/specs/2026-08-01-auth-google-facebook-design.md`
- `docs/superpowers/plans/2026-08-01-auth-google-facebook.md`

## Decisiones aprobadas

- Se mantienen las decisiones del spec; no surgieron contradicciones ni ampliaciones.
- `LoginProvider` se persiste normalizado como `google` o `facebook`; los nombres de esquema del middleware solo se usan durante el challenge.
- Un intento de vinculación incorrecto invalida el ticket pendiente.
- Los tests construyen tickets protegidos e identidades externas controladas; no usan credenciales reales ni llaman a Google o Meta.

## Completado

### Tarea 4 — Challenge, callback y usuario asociado

- `AuthExternaEndpoints` expone proveedores, challenge y callback interno.
- Usa propiedades de autenticación de `SignInManager` y la cookie externa de Identity.
- Un login asociado recibe refresh propio y redirección local sin tokens ni PII en query.
- Cancelación/error limpia la cookie externa y usa códigos opacos.
- El retorno no local se reemplaza por `/perfil`; repetir el callback no duplica usuarios ni asociaciones.

### Tarea 5 — Alta externa y onboarding mínimo

- `GET /api/auth/external/pending` proyecta solo campos necesarios y si requiere vinculación.
- `POST /api/auth/external/complete` valida email, nombre y ciudad con los límites de Perfil.
- `ServicioRegistroExterno` reconsulta login/email, crea sin contraseña, asigna `Cliente`, añade el login y publica `UsuarioRegistrado`.
- Google solo confirma el email recibido y verificado; Facebook queda sin confirmar, incluso si aporta email.
- Fallos de rol/login compensan eliminando el usuario nuevo; carreras se resuelven reconsultando login/email.
- El éxito emite sesión, borra el ticket y la repetición es idempotente.

### Tarea 6 — Vinculación segura

- `POST /api/auth/external/link` exige JWT válido.
- Solo el usuario cuyo email normalizado coincide exactamente con el ticket puede añadir el login.
- Un usuario distinto falla con respuesta genérica y el ticket se invalida.
- Google y Facebook pueden quedar asociados a una misma cuenta.
- Una identidad ya vinculada a otra cuenta no se mueve.

## Tarea exacta en curso

Ninguna. El Bloque 2 está cerrado.

## Archivos relevantes

- `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthExternaEndpoints.cs`
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ServicioRegistroExterno.cs`
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthExternaLoginTests.cs`
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthExternaRegistroTests.cs`
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthExternaVinculacionTests.cs`

## Verificaciones ejecutadas

- Rojo observado por tarea antes de implementar el endpoint correspondiente.
- Suite dirigida externa (`AuthExternaLoginTests`, `AuthExternaRegistroTests`, `AuthExternaVinculacionTests`): 13/13 verdes.
- Regresión vecina junto con `AuthFlowTests`, `RefreshTokensTests` y `AuthExternaConfiguracionTests`: 30/30 verdes en total con Testcontainers.MsSql.
- `dotnet build CaseritoApp.sln --no-restore --verbosity quiet`: correcto, 0 advertencias y 0 errores.
- `dotnet format CaseritoApp.sln --verify-no-changes --no-restore --verbosity quiet`: limpio.
- `git diff --check`: limpio.
- Hooks de formato superados en los tres commits funcionales.
- No se ejecutó la suite completa de la solución; el plan reserva la regresión completa para el cierre.

## Fallos o bloqueos

- Ninguno.
- Las pruebas manuales con proveedores reales siguen diferidas y requieren credenciales sandbox/callbacks HTTPS; no forman parte de la suite automatizada.

## Próximo paso

Iniciar el Bloque 3 por la tarea 7: UI de login, retorno y onboarding. Leer primero `web/AGENTS.md`, la sección exacta de la tarea 7 y solo las secciones de UI/contrato del spec necesarias. Empezar con los tests frontend rojos indicados en el plan.

## Commits

- `8dd3a3c feat(auth): inicia sesión con proveedor externo`
- `88f60b9 feat(auth): registra usuarios desde Google y Facebook`
- `92cfbe8 feat(auth): vincula cuentas externas de forma segura`
