# Handoff — Confirmación de email y notificaciones de KYC

> Fecha: 2026-07-29  
> Contexto: continuación desde el cierre del bloque KYC automático con ARGOS.  
> Estado: **en implementación: Tasks 1 a 8 completadas; Task 9 (frontend) pendiente**.

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
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/EnviarConfirmacionEmailHandler.cs` — creado; **corregido en esta sesión** para capturar y loguear fallos de SMTP sin propagar la excepción (cumple con el spec: registro no se bloquea si el relay no responde).
- `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs` — modificado para publicar `UsuarioRegistrado` tras registro exitoso; **agregados endpoints** `POST /api/auth/confirm-email` y `POST /api/auth/resend-confirmation`.
- `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/EnviarConfirmacionEmailHandlerTests.cs` — creado; **extendido** con test de tolerancia a fallo SMTP.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/ConfirmarEmailCommand.cs` — creado.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/IRepositorioConfirmacionEmail.cs` — creado (puerto para mantener Application libre de Infrastructure).
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/RepositorioConfirmacionEmail.cs` — creado (implementación sobre `UserManager<ApplicationUser>`).
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` — registrado `IRepositorioConfirmacionEmail`.
- `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/ConfirmarEmailCommandHandlerTests.cs` — creado.
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthFlowTests.cs` — **extendido** con flujos de confirmación de email y reenvío.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/KycResuelto.cs` — creado (evento de dominio con usuario, solicitud, estado y motivo de rechazo).
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/NotificarKycResueltoHandler.cs` — creado; captura y loguea fallos SMTP sin propagar (mismo criterio que la confirmación de email).
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/IConsultaVerificacionKyc.cs` — extendido con `ObtenerUsuarioAsync` y el record `UsuarioKycDto`.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConsultaVerificacionKycEfCore.cs` — implementado `ObtenerUsuarioAsync` sobre `db.Users`.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/AprobarSolicitudKycCommand.cs` — publica `KycResuelto` (MediatR `IPublisher`) tras aprobar.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/RechazarSolicitudKycCommand.cs` — publica `KycResuelto` con el motivo tras rechazar.
- `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/NotificarKycResueltoHandlerTests.cs` — creado (aprobado, rechazado con motivo, usuario no encontrado, tolerancia a fallo SMTP).
- `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/AprobarSolicitudKycCommandHandlerTests.cs` — actualizado al nuevo constructor y con aserción de `KycResuelto`.
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/KycNotificacionTests.cs` — creado (aprobar/rechazar KYC como admin envía el correo correcto, con `IServicioCorreo` capturador en memoria).
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/OrdersAdaptadoresTests.cs` — fake de `IConsultaVerificacionKyc` actualizado al nuevo miembro de la interfaz.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Autorizacion/ClaimsApp.cs` — agregado claim `EmailConfirmado` ("emailConfirmed").
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/PoliticasAutorizacion.cs` — agregada constante de policy `EmailConfirmado`.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/GeneradorTokensAcceso.cs` — el JWT incluye el claim `emailConfirmed` desde `ApplicationUser.EmailConfirmed`.
- `CaseritoApp/src/Host/CaseritoApp.Host/Auth/RequisitoEmailConfirmado.cs` — creado (requirement + handler de autorización).
- `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs` — registrada la policy `EmailConfirmado` y el `IAuthorizationHandler`.
- `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs` — grupo `/api/kyc` exige la policy `EmailConfirmado`.
- `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs` — crear y editar aviso exigen la policy `EmailConfirmado`.
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthFlowTests.cs` — agregado test: usuario sin email confirmado recibe 403 al enviar KYC.
- Helpers de registro en `KycFlujoTests`, `KycArgosFlujoTests`, `AvisosFlujoTests`, `DescubrimientoAvisosTests`, `FotosAvisoIntegrationTests`, `FlujoCriticoTests`, `OrdersFlujoTests` y `ModeracionAvisosTests` — ahora marcan `EmailConfirmed = true` antes del login, porque esos flujos exigen la policy.
- `docs/ai/HANDOFF.md` — este archivo.

## Decisiones importantes

- ARGOS es opcional para Caserito: la API debe arrancar aunque ARGOS no responda.
- MailApiService sigue siendo relay puro; no se agrega negocio ni plantillas centralizadas en este bloque.
- Caserito envía correos por SMTP a `mail:587` usando MailKit.
- Plantillas iniciales en texto plano (Scriban/HTML queda para fase futura).
- Tokens de confirmación de email con `IDataProtector` + expiración de 24h.
- El email confirmado es requisito previo para enviar KYC y publicar avisos.
- Para preservar Clean Architecture, `ConfirmarEmailCommandHandler` no depende directamente de `UserManager<ApplicationUser>`; usa el puerto `IRepositorioConfirmacionEmail` con implementación en Infrastructure.
- El handler de confirmación por email **no propaga excepciones de SMTP**: se loguea el fallo y el registro devuelve 200, tal como indica el spec.
- `NotificarKycResueltoHandler` sigue el mismo criterio: un fallo SMTP se loguea y no revierte la resolución del KYC.
- `KycResuelto` se publica con `IPublisher` de MediatR desde los handlers de aprobar/rechazar (no desde los endpoints), porque el comando no expone el `UsuarioId` al endpoint.
- La policy `EmailConfirmado` se basa en el claim `emailConfirmed` del JWT: un usuario que confirma su correo desbloquea KYC y avisos en su siguiente login o refresh (no en el token ya emitido).
- La policy se aplicó a todo el grupo `/api/kyc` (incluido `GET /estado`) y solo a crear/editar de `/api/avisos`; pausar, reactivar, eliminar y fotos quedan como estaban (autenticación + reglas de dominio).

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
- ✅ Task 6 — Comando y endpoints para confirmar/reenviar email.
- ✅ Task 7 — Notificación de KYC (evento `KycResuelto` + handler `NotificarKycResueltoHandler`).
- ✅ Task 8 — Restricciones de autorización por `EmailConfirmed` en KYC y avisos.
- ⏳ Task 9 — Frontend: pantallas post-registro y confirmación de email.

## Próximo paso

Continuar con la Task 9 del plan (frontend en `web/`): funciones `confirmarEmail`/`reenviarConfirmacionEmail` en `web/src/api/auth.ts`, pantalla `ConfirmarEmailPage.tsx`, mensaje post-registro en `RegisterPage.tsx`, ruta en el router y tests. Verificaciones: `npm run typecheck`, `npm run lint`, `npm run test -- --run`, `npm run build`.

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

## Checks ejecutados en esta sesión

- `dotnet build CaseritoApp.sln` ✅
- `dotnet test CaseritoApp.sln` ✅ (Unit: 344, Architecture: 57, Integration: 176)
- `dotnet format CaseritoApp.sln --verify-no-changes` ✅ (requirió corregir finales de línea LF→CRLF en los archivos nuevos con `dotnet format`)
