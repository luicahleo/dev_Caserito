# Handoff — Confirmación de email y notificaciones de KYC

> Fecha: 2026-07-29  
> Contexto: continuación desde el cierre del bloque KYC automático con ARGOS.  
> Estado: **en implementación: Tasks 1 a 5 completadas; Task 6 pendiente**.

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
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IPlantillaCorreo.cs` — creado.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/PlantillaCorreoTextoPlano.cs` — creado.
- `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/PlantillaCorreoTextoPlanoTests.cs` — creado.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IGeneradorTokenEmail.cs` — creado.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/GeneradorTokenEmailDataProtector.cs` — creado.
- `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/GeneradorTokenEmailDataProtectorTests.cs` — creado.
- `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Messaging/IDomainEventConsumer.cs` — creado (interfaz semántica para handlers de eventos de dominio, evita el sufijo prohibido por CA1711).
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Usuarios/UsuarioRegistrado.cs` — creado.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/EnviarConfirmacionEmailHandler.cs` — creado.
- `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs` — modificado para publicar `UsuarioRegistrado` tras registro exitoso.
- `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/EnviarConfirmacionEmailHandlerTests.cs` — creado.
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
- ✅ Task 3 — Puerto e implementación de `IPlantillaCorreo`.
- ✅ Task 4 — Generador de tokens de confirmación de email.
- ✅ Task 5 — Evento `UsuarioRegistrado` y handler de confirmación.
- ⏳ Task 6 — Comando y endpoint para confirmar email.

## Próximo paso

Continuar con la Task 6 del plan: crear `ConfirmarEmailCommand` y su handler, agregar los endpoints `confirm-email` y `resend-confirmation` en `AuthEndpoints`, y agregar tests unitarios e integración.

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
