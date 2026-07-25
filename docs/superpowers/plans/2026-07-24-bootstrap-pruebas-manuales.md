# Plan — Bootstrap y pruebas manuales de fases 1–3

Fecha: 2026-07-24
Spec: `docs/superpowers/specs/2026-07-24-bootstrap-pruebas-manuales-design.md`

## 1. Contrato probado del bootstrap

- Rutas: nuevo componente bajo
  `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/`.
- Pruebas:
  `CaseritoApp/tests/CaseritoApp.IntegrationTests/BootstrapUsuariosPruebaTests.cs`.
- Escribir primero pruebas que fallen por ausencia del componente:
  Development habilitado crea roles exactos y KYC no verificado; configuración
  desactivada/incompleta y entorno no Development no crean; reejecución y usuario
  existente no modifican; captura de logs no contiene valores configurados.
- Verificación dirigida:
  `dotnet test --filter FullyQualifiedName~BootstrapUsuariosPruebaTests`.

## 2. Implementación mínima

- Añadir opciones tipadas para tres cuentas y una extensión de `IServiceProvider`.
- Validar entorno, flag y configuración antes de resolver `UserManager`.
- Crear solo usuarios inexistentes con `UserManager.CreateAsync` y asignar el rol
  exacto mediante `AddToRoleAsync`.
- Invocar desde `Program.cs` después de `SembrarRolesAsync`.
- No añadir migraciones, endpoints ni referencias entre bounded contexts.
- Repetir el test dirigido hasta verde.

## 3. Configuración local segura

- Actualizar `docker-compose.dev.yml` con traducción de variables, usando flag
  `false` y valores vacíos por defecto.
- Reemplazar el contenido sensible de `.env.example` por nombres y valores vacíos.
- Comprobar que `.env` continúa ignorado y que ninguna credencial aparece en Git.

## 4. Procedimiento reproducible

- Actualizar `README.md` con preparación, datos sintéticos, sesiones A/B,
  recorridos funcionales y resultados esperados.
- Corregir referencias obsoletas a fotos.
- Incluir comandos de salud, suites y cierre; no incluir correos ni contraseñas.

## 5. Integración automatizada

Desde `CaseritoApp/`:

- `dotnet build CaseritoApp.sln`
- `dotnet test CaseritoApp.sln`
- `dotnet format CaseritoApp.sln --verify-no-changes`

Desde `web/`:

- `npm run typecheck`
- `npm run lint`
- `npm run test -- --run`
- `npm run build`

Desde la raíz:

- `git diff --check`

## 6. Entorno y prueba manual

- Validar que el `.env` local tiene todas las variables sin imprimir valores.
- Ejecutar `.\rebuild.ps1`.
- Ejecutar `docker compose -f docker-compose.dev.yml ps`.
- Comprobar HTTP 200 en API `/health` y web.
- Guiar al usuario paso a paso y registrar sus observaciones.
- No declarar éxito manual hasta recibir confirmación del navegador.

No se hará push, merge ni trabajo de Fase 4.
