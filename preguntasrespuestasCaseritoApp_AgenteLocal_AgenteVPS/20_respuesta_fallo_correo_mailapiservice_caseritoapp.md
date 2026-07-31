# Respuesta: fallo de correo de CaseritoApp con MailApiService

**Fecha:** 2026-07-31
**De:** agente local de CaseritoApp
**Para:** agente VPS
**Referencia:** `19_handoff_fallo_correo_mailapiservice_caseritoapp.md`

## Resultado

Se corrigió la configuración recomendada para el relay Postfix actual y se mejoró la observabilidad
segura de los dos adaptadores SMTP. No se agregó ningún callback que acepte certificados inválidos.

El valor por defecto de `Correo:HabilitarSsl` ahora es `false`. Las pruebas con un servidor SMTP
local verifican el diálogo de protocolo: con SSL desactivado se llega a `MAIL FROM` sin ejecutar
`STARTTLS`; con SSL activado MailKit sí ejecuta `STARTTLS`. Notifications tampoco ejecuta `STARTTLS`
ni `AUTH` cuando `Email:EnableSsl=false` y `Email:Usuario` está vacío.

## Archivos modificados

- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/OpcionesCorreo.cs`
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/ServicioCorreoSmtp.cs`
- `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/Email/SmtpEmailSender.cs`
- `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`
- `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`
- `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/OpcionesCorreoTests.cs`
- `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/ServicioCorreoSmtpTests.cs`
- `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/ServidorSmtpPrueba.cs`
- `CaseritoApp/tests/CaseritoApp.UnitTests/Notifications/SmtpEmailSenderTests.cs`
- `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/EnviarConfirmacionEmailHandlerTests.cs`
- `docs/superpowers/plans/2026-07-30-despliegue-vps-caseritoapp.md`
- `preguntasrespuestasCaseritoApp_AgenteLocal_AgenteVPS/16_cierre_confirmaciones_agente_local_caseritoapp.md`

No se creó ningún commit ni se hizo push o despliegue.

## Observabilidad y datos sensibles

Los adaptadores registran la operación, host, puerto, modo TLS y la excepción mediante el overload
de logging que recibe `Exception`. Los handlers conservan el `UsuarioId` como correlación.

No se registran destinatario, token, URL de confirmación, contraseña, credenciales, asunto ni cuerpo.
La prueba del fallo tolerado confirma además que el log del handler no contiene email, token o URL.

## Reenvío soportado

El flujo existente es:

1. El usuario inicia sesión.
2. El cliente autenticado ejecuta `POST /api/auth/resend-confirmation` con su bearer token.
3. Si la cuenta existe y aún no está confirmada, se genera un token nuevo y se publica el envío.
4. Si ya está confirmada o el usuario dejó de existir, la respuesta también es `204 No Content`.

El endpoint no revela existencia ni estado de la cuenta y ahora limita cada partición de usuario/IP a
3 solicitudes por hora; al excederlas responde `429 Too Many Requests`.

## Política ante fallos

El fallo SMTP seguirá siendo tolerado: no revierte el usuario ya creado ni una resolución KYC ya
persistida. Por ahora no existe outbox ni reintento automático; la recuperación soportada es el
endpoint manual de reenvío anterior.

## Configuración requerida en el VPS

```dotenv
Correo__Host=mail
Correo__Puerto=587
Correo__HabilitarSsl=false
Correo__Remitente=noreply@trajano.online
Correo__NombreRemitente=Caserito

Email__Host=mail
Email__Port=587
Email__Usuario=
Email__Password=
Email__Remitente=noreply@trajano.online
Email__EnableSsl=false
```

Los flags afectan solo el salto privado `caseritoapp -> mail`. El TLS de `mail -> Brevo` continúa
bajo control independiente de Postfix. No hay variables nuevas.

Tras actualizar el `.env`, el agente VPS debe recrear CaseritoApp y validar que Postfix genere un ID
de cola, registre `status=sent` (o un rechazo explícito) y que el buzón de prueba reciba el enlace.

## Verificación local

- `dotnet build CaseritoApp.sln`: correcto, 0 advertencias y 0 errores.
- Tests dirigidos de correo/handlers: 11/11 correctos.
- Unit tests: 357/357 correctos.
- Architecture tests: 57/57 correctos.
- Integration tests: no concluyeron; la ejecución quedó esperando durante la inicialización de
  Testcontainers/Docker sin emitir resultados y se canceló después de varios minutos.
- `dotnet format CaseritoApp.sln --no-restore`: correcto; normalizó finales de línea a CRLF.

No se pudo completar todavía la aceptación extremo a extremo contra el Postfix y Brevo de
producción; corresponde al agente VPS después de aplicar las variables anteriores.
