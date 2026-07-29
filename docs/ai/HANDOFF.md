# Handoff — Confirmación de email y notificaciones de KYC

> Fecha: 2026-07-29  
> Contexto: continuación desde el cierre del bloque KYC automático con ARGOS.  
> Estado: **en implementación: Tasks 1 y 2 completadas; Task 3 pendiente**.

## Contexto de esta sesión

1. Se probó el levantamiento local de **Caserito + ARGOS** usando `docker-compose.dev.yml` + `docker-compose.argos.yml`.
2. Se encontró y corrigió un problema en `docker-compose.argos.yml`: `api` tenía `depends_on` con `condition: service_healthy` hacia `argos`, lo que hacía que Caserito no arrancara si ARGOS no estaba healthy. Se eliminó esa dependencia cíclica.
3. Se discutieron medidas anti-fraude tipo Wallapop. Se acordó que el **número de documento de identidad es el ancla** (1 CI = 1 cuenta), con ARGOS como filtro previo y validación humana como control final.
4. Se decidió no usar OCR de pago en el MVP; el usuario ingresará el CI manualmente y un revisor humano lo valida.
5. Se confirmó que `MailApiService` es un relay Postfix → Brevo, sin código propio. Las apps envían correo por SMTP a `mail:587` dentro de `trajano-shared-network`.
6. Se aprobó implementar:
   - Confirmación de email en el registro.
   - Notificación por email de KYC aprobado/rechazado.
   - Caserito genera sus propias plantillas; MailApiService sigue siendo relay.

## Spec y plan

- Spec: `docs/superpowers/specs/2026-07-29-confirmacion-email-notificaciones-kyc-design.md`
- Plan: `docs/superpowers/plans/2026-07-29-confirmacion-email-notificaciones-kyc.md`

## Archivos modificados en esta sesión

- `docker-compose.argos.yml` — eliminado `depends_on` cíclico de `api` hacia `argos`.
- `docs/superpowers/specs/2026-07-29-confirmacion-email-notificaciones-kyc-design.md` — creado.
- `docs/superpowers/plans/2026-07-29-confirmacion-email-notificaciones-kyc.md` — creado.
- `CaseritoApp/Directory.Packages.props` — agregado `MailKit` 4.16.0 (versión actualizada respecto al plan por vulnerabilidades conocidas en 4.11.0).
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/OpcionesCorreo.cs` — creado.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` — registradas `OpcionesCorreo` y `IServicioCorreo`.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IServicioCorreo.cs` — creado.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/MensajeCorreo.cs` — creado.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/ServicioCorreoSmtp.cs` — creado.
- `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/OpcionesCorreoTests.cs` — creado.
- `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/ServicioCorreoSmtpTests.cs` — creado.
- `docs/ai/HANDOFF.md` — este archivo.

## Decisiones importantes

- ARGOS es opcional para Caserito: la API debe arrancar aunque ARGOS no responda.
- MailApiService sigue siendo relay puro; no se agrega negocio ni plantillas centralizadas en este bloque.
- Caserito envía correos por SMTP a `mail:587` usando MailKit.
- Plantillas iniciales en texto plano (Scriban/HTML queda para fase futura).
- Tokens de confirmación de email con `IDataProtector` + expiración de 24h.
- El email confirmado es requisito previo para enviar KYC y publicar avisos.

## Estado de los servicios al cerrar

Todos los contenedores de desarrollo estaban levantados y healthy:
- `caserito-sqlserver` ✅ healthy
- `caserito-argos` ✅ healthy
- `caserito-api` ✅ up
- `caserito-web` ✅ up

## Progreso de implementación

- ✅ Task 1 — Configuración de correo y registro DI.
- ✅ Task 2 — Puerto e implementación de `IServicioCorreo` con MailKit 4.16.0.
- ⏳ Task 3 — Puerto e implementación de `IPlantillaCorreo`.

## Próximo paso

Continuar con la Task 3 del plan: crear el puerto `IPlantillaCorreo`, la implementación en texto plano, registrarla en DI y agregar sus tests unitarios.

## Restricciones

- Clean Architecture / CQRS-lite: domain puro, puertos en Application, adaptadores en Infrastructure.
- Anti-PII: nunca loguear tokens completos, contraseñas, contenido de correos ni datos sensibles.
- No hardcodear credenciales de Brevo; Caserito solo conoce host/puerto/remitente.
- Versiones de paquetes exclusivamente en `Directory.Packages.props`.
- No hacer push/merge sin autorización explícita.
- Textos de UI y comentarios en español correcto.

## Verificaciones esperadas por paso

- Backend: `dotnet build CaseritoApp.sln`, `dotnet test CaseritoApp.sln`, `dotnet format CaseritoApp.sln --verify-no-changes`.
- Frontend: `npm run typecheck`, `npm run lint`, `npm run test -- --run`, `npm run build`.
