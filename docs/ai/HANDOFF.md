# Handoff de sesión

## Objetivo

Implementar autenticación web con Google y Facebook para la PWA, conservando correo/contraseña y la sesión JWT/refresh propia de Caserito.

## Rama y estado de Git

- Rama: `feat/auth-google-facebook`.
- La sesión de diseño no modificó código funcional.
- Verificar al iniciar con `git status --short --branch` y `git log -5 --oneline`.

## Spec y plan activos

- `docs/superpowers/specs/2026-08-01-auth-google-facebook-design.md`
- `docs/superpowers/plans/2026-08-01-auth-google-facebook.md`

## Decisiones aprobadas

- Proveedores: Facebook y Google; correo/contraseña permanece.
- OAuth/OIDC web iniciado y validado por backend; ningún secreto en React.
- Caserito emite su JWT y refresh actuales; no guarda tokens sociales.
- Asociación por proveedor + clave estable, nunca por email.
- Email existente exige autenticarse con un método ya asociado; no hay vinculación automática.
- Google confirma email solo con `email_verified=true`; Facebook usa confirmación local.
- Si faltan email, nombre o ciudad, onboarding mínimo antes de crear la cuenta.
- No hay migración prevista: se reutiliza `AspNetUserLogins`.

## Completado

- Brainstorming cerrado.
- Spec aprobado y versionado.
- Plan TDD dividido en cuatro sesiones y versionado.

## Tarea exacta en curso

Iniciar la **Tarea 1 del plan**: extraer `IEmisorSesion`/`EmisorSesion` para reutilizar la emisión de JWT y refresh sin cambiar el comportamiento de login y refresh tradicionales. Escribir primero el test dirigido.

## Archivos relevantes

- `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/GeneradorTokensAcceso.cs`
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ServicioRefreshTokens.cs`
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthFlowTests.cs`
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/RefreshTokensTests.cs`

## Verificaciones ejecutadas

- `git diff --check` limpio después de corregir el formato documental.
- No se ejecutaron build/tests: solo cambiaron Markdown.

## Fallos o bloqueos

- Ninguno para la Tarea 1.
- Las pruebas reales contra Google/Meta requerirán más adelante credenciales sandbox, callbacks HTTPS y configuración en sus portales; la suite automatizada no depende de ellas.

## Próximo paso

Abrir una sesión nueva, leer este handoff y las secciones Tarea 1/reglas del plan, verificar Git y ejecutar TDD desde `CaseritoApp/`. Al terminar tareas 1–3, reemplazar este handoff para la siguiente sesión.

## Commits

- `10d7758 docs(auth): diseña acceso con Google y Facebook`
- `37ed634 docs(auth): precisa vinculación de cuentas externas`
- `220bf08 docs(auth): planifica acceso con Google y Facebook`
- `ec1e5a6 docs(auth): limpia formato del plan`
