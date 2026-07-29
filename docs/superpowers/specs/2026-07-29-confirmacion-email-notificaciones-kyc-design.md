# Diseño — Confirmación de email y notificaciones de KYC

> Fecha: 2026-07-29.  
> Contexto: CaseritoApp necesita confirmar que el email de un usuario es real antes de permitir acciones de riesgo, y notificarle el resultado de la validación humana de KYC. Este spec asume que el flujo de KYC ya incluye una etapa de revisión humana que publica `KycResuelto`. MailApiService es solo un relay Postfix a Brevo, por lo que Caserito genera y envía sus propios correos por SMTP.  
> Status: aprobado en brainstorming.

## 1. Objetivo y alcance

Agregar a Caserito:

1. **Confirmación de email en el registro**: el usuario recibe un token por correo y debe confirmarlo para desbloquear funciones sensibles.
2. **Notificación de resultado de KYC**: el usuario recibe un correo cuando la validación humana aprueba o rechaza su solicitud.

**Dentro de este bloque:**

- Generación y validación de token de confirmación de email.
- Envío de correos por SMTP al relay interno `mail:587`.
- Plantillas simples (texto plano o HTML básico) gestionadas por Caserito.
- Restricciones de autorización cuando el email no está confirmado.
- Notificación de KYC aprobado/rechazado.
- Tests unitarios e integración.

**Fuera de este bloque (diferido):**

- Plantillas de correo centralizadas en MailApiService.
- Branding avanzado de correos (logo, tipografía propia).
- Colas de correo propias o retry centralizado.
- Envío de SMS como alternativa de confirmación.

## 2. Flujo de alto nivel

### 2.1 Confirmación de email

```
Usuario (web)
  └─ POST /api/auth/register { email, password, nombre, ciudad }
        └─ Caserito.Host crea usuario con EmailConfirmed = false
        └─ Genera token de confirmación con expiración
        └─ Envía correo con link de confirmación vía SMTP a mail:587
        └─ Devuelve 200

Usuario hace clic en el link
  └─ POST /api/auth/confirm-email { userId, token }
        └─ Caserito.Host valida token
        └─ Marca EmailConfirmed = true
        └─ Devuelve 204
```

### 2.2 Notificación de KYC

```
Admin aprueba/rechaza solicitud KYC
  └─ Caserito.Host publica evento KycResuelto
        └─ Handler envía correo al usuario
              ├─ Aprobado: "Tu identidad fue verificada"
              └─ Rechazado: "Tu verificación fue rechazada" + motivo
```

## 3. Cambios por capa

### 3.1 Domain (`CaseritoApp.Identity.Domain/`)

- Reutilizar `ApplicationUser.EmailConfirmed` de ASP.NET Identity.
- Nuevo evento de dominio `UsuarioRegistrado` para desencadenar el envío de correo.
- Nuevo evento de dominio `KycResuelto` (incluye estado `Aprobada`/`Rechazada`, motivo y usuarioId) publicado desde el handler de aprobación/rechazo manual. El handler de notificación lo consume para enviar el correo correspondiente.

### 3.2 Application (`CaseritoApp.Identity.Application/`)

**Nuevos puertos:**

```csharp
public interface IServicioCorreo
{
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct);
}

public sealed record MensajeCorreo(
    string Para,
    string Asunto,
    string CuerpoTexto,
    string? CuerpoHtml = null);
```

```csharp
public interface IGeneradorTokenEmail
{
    string Generar(Guid usuarioId);
    bool Validar(string token, out Guid usuarioId);
}
```

**Nuevo handler:**

- `ConfirmarEmailCommandHandler`: valida token y marca `EmailConfirmed = true`.
- `EnviarConfirmacionEmailHandler` (reacciona a `UsuarioRegistrado`): genera token y envía correo.
- `NotificarKycResueltoHandler` (reacciona a `KycResuelto`): envía correo de aprobado/rechazado.

### 3.3 Infrastructure (`CaseritoApp.Identity.Infrastructure/`)

**Adaptador SMTP:**

```csharp
public sealed class ServicioCorreoSmtp(
    SmtpClient smtpClient,
    IOptions<OpcionesCorreo> opciones) : IServicioCorreo
```

Responsabilidades:

- Enviar `MailMessage` por SMTP a `mail:587`.
- Soportar STARTTLS.
- No exponer credenciales de Brevo; solo el host/puerto/remitente de Caserito.

**Configuración:**

```csharp
public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    public string Host { get; set; } = "mail";
    public int Puerto { get; set; } = 587;
    public string Remitente { get; set; } = "noreply@trajano.online";
    public string NombreRemitente { get; set; } = "Caserito";
    public bool HabilitarSsl { get; set; } = true;
}
```

**Generador de tokens:**

```csharp
public sealed class GeneradorTokenEmailSeguro : IGeneradorTokenEmail
```

- Usar `DataProtectorTokenProvider` de ASP.NET Identity o `IDataProtector`.
- Tokens con expiración configurable (default 24h).
- No almacenar tokens en BD; deben ser autovalidables.

**Plantillas:**

```csharp
public interface IPlantillaCorreo
{
    string AsuntoConfirmacionEmail(string nombre);
    string CuerpoConfirmacionEmail(string nombre, string urlConfirmacion);
    string AsuntoKycAprobado(string nombre);
    string CuerpoKycAprobado(string nombre);
    string AsuntoKycRechazado(string nombre);
    string CuerpoKycRechazado(string nombre, string motivo);
}
```

Implementación inicial en texto plano o HTML mínimo; vive en `Identity.Infrastructure` o `Host`.

### 3.4 Host (`CaseritoApp.Host/Endpoints/`)

- `AuthEndpoints`:
  - `POST /api/auth/register` → ahora publica `UsuarioRegistrado` y envía correo.
  - `POST /api/auth/confirm-email` → nuevo endpoint.
  - `POST /api/auth/resend-confirmation` → opcional, para reenviar correo.
- `KycEndpoints`:
  - Al aprobar/rechazar manualmente, publicar `KycResuelto`.
- Autorización:
  - Requerir `EmailConfirmed = true` para iniciar KYC (`/api/kyc`) y para crear avisos.
  - Mantener acceso temporal de KYC pendiente como se defina en el flujo de validación humana.

### 3.5 Frontend (`web/`)

- Pantalla de "Revisa tu correo" tras registro.
- Pantalla de "Email confirmado" tras usar el link.
- Botón para reenviar correo de confirmación.
- Banner cuando el usuario no ha confirmado email: "Confirma tu correo para publicar o verificar tu identidad".

## 4. PII, seguridad y auditoría

- Los tokens de confirmación deben ser criptográficamente seguros y de un solo uso.
- No loguear el token completo; solo un prefijo para depuración.
- No exponer en la respuesta si un email ya está registrado (timing/enum protection).
- Los correos solo contienen links con tokens; nunca contraseñas ni datos sensibles.
- El remitente será `noreply@trajano.online`.
- En local/dev se puede usar un logger fake o un sink de prueba; nunca enviar a Brevo desde tests.

## 5. Manejo de errores

| Escenario | Resultado | HTTP |
|---|---|---|
| Registro exitoso | Correo enviado (async) | 200 |
| Token inválido o expirado | Error genérico | 400 |
| Email ya confirmado | Éxito silencioso o 204 | 204 |
| SMTP no disponible | Se loguea, no se bloquea el registro | 200 (el correo se reintentará manualmente) |
| KYC aprobado | Correo de aprobación enviado | - |
| KYC rechazado | Correo de rechazo enviado | - |

## 6. Tests

**Unitarios:**

- `ConfirmarEmailCommandHandlerTests`: token válido, token inválido, token expirado, email ya confirmado.
- `NotificarKycResueltoHandlerTests`: aprobado envía correo correcto, rechazado incluye motivo.
- `ServicioCorreoSmtpTests`: usa `SmtpClient` con un `ISmtpClient` abstraído o un server de prueba.

**Integración:**

- `AuthFlowTests`: registro → confirmación de email → login → acceso a funciones protegidas.
- `KycNotificacionTests`: aprobar/rechazar KYC y verificar que se encola/envía correo.

**Frontend:**

- Tests de `RegisterPage` mostrando mensaje post-registro.
- Tests de pantalla de confirmación de email.

## 7. Follow-ups diferidos

1. **Plantillas centralizadas en MailApiService:** cuando haya varias apps compartiendo branding.
2. **Retry automático de correos:** cola local o servicio de outbox.
3. **Confirmación por SMS:** alternativa para usuarios sin acceso al correo.
4. **Rate limiting de reenvío de correos:** evitar abuso del endpoint de reenvío.

## 8. Dependencias externas

- `MailApiService` (contenedor Postfix relay → Brevo) debe estar desplegado y alcanzable como `mail:587` dentro de `trajano-shared-network`.
- Variables de configuración necesarias:
  - `Correo:Host`
  - `Correo:Puerto`
  - `Correo:Remitente`
  - `Correo:HabilitarSsl`
