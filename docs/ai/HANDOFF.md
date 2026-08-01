# Handoff — Restablecimiento de contraseña

> Fecha: 2026-08-01
> Rama: `feat/restablecimiento-password`
> Estado: tareas 1–4 completas; continuar en tarea 5 (endpoints y rate limiting).

## Fuente de verdad

- Spec aprobado: `docs/superpowers/specs/2026-08-01-restablecimiento-password-design.md`
- Plan TDD: `docs/superpowers/plans/2026-08-01-restablecimiento-password.md`

## Decisiones aprobadas

- Token exclusivo de ASP.NET Identity con vigencia de 30 minutos.
- Solicitud anti-enumeración; SMTP fallido mantiene respuesta genérica.
- 3 solicitudes/hora/IP y 10 consumos/15 minutos/IP.
- Token en fragmento URL, limpiado inmediatamente por React.
- Primer cambio exitoso invalida enlaces anteriores y todos los refresh tokens.
- JWT existentes vencen en un máximo de 15 minutos.
- Recuperar no confirma el correo; política actual de mínimo 8 caracteres.
- Sin login automático, migración ni persistencia propia de tokens.

## Implementado y commiteado

- `1003a42`: proveedor exclusivo de 30 minutos, puerto y repositorio sobre `UserManager`.
- `bcc717d`: comando anti-enumeración, correo con fragmento seguro y plantilla.
- `b874676`: puerto y adaptación para revocar todos los refresh tokens; test SQL Server separa usuarios.
- `b69426e`: comando que restablece y solo después revoca sesiones.

También existen commits separados del spec (`96892ec`) y plan (`05db49b`).

## Verificaciones observadas

- Test proveedor: 1 verde.
- Solicitud/plantillas/confirmación vecina: 8 verdes.
- Revocación total en integración con SQL Server/Testcontainers: 1 verde.
- Handler de restablecimiento: 2 verdes.
- `dotnet format CaseritoApp.sln --verify-no-changes --no-restore`: verde después de cada bloque.
- Build de Identity Infrastructure: verde, 0 warnings.

## Próximo paso exacto

Ejecutar la tarea 5 del plan:

1. Crear `RestablecimientoPasswordTests.cs` con prueba roja de contratos y flujo.
2. Añadir `OlvidePasswordRequest` y `RestablecerPasswordRequest` a `AuthEndpoints.cs`.
3. Mapear `POST /api/auth/forgot-password` y `POST /api/auth/reset-password` por MediatR.
4. Añadir en `Program.cs` las políticas `auth-forgot-password` (3/h) y
   `auth-reset-password` (10/15 min), particionadas con `Particion(contexto)`.
5. Probar contraseña anterior/nueva, reutilización, enlaces múltiples, correo no confirmado,
   refresh revocado y respuestas genéricas.
6. Después regenerar OpenAPI y continuar con las dos pantallas frontend.

## Restricciones

- Verificar afirmaciones con Git al reanudar.
- Nunca loguear correo, usuario, token, contraseña, URL, payload ni cuerpo del correo.
- Preservar Clean Architecture y no agregar migración.
- No push ni merge sin autorización explícita.
