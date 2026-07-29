# Confirmación de email y notificaciones de KYC — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agregar confirmación de email en el registro y notificaciones por correo cuando el KYC es aprobado o rechazado, enviando todo por SMTP al relay interno `mail:587`.

**Architecture:** Caserito genera sus propios correos con plantillas simples y las envía por SMTP a MailApiService (Postfix relay → Brevo). El dominio publica eventos (`UsuarioRegistrado`, `KycResuelto`) que handlers de Application consumen para enviar correos de forma desacoplada. La confirmación de email es requisito previo para acciones de riesgo como KYC y publicar avisos.

**Tech Stack:** .NET 10, EF Core, ASP.NET Core Identity, MediatR, MailKit, Scriban (opcional para plantillas), xUnit, NSubstitute, React + TypeScript + MUI + TanStack Query.

## Global Constraints

- Clean Architecture: Domain no depende hacia afuera; Application solo de Domain; Infrastructure implementa puertos; Host compone y autoriza.
- CQRS-lite: MediatR + Result + FluentValidation; reglas de negocio en dominio.
- Anti-PII no negociable: nunca registrar tokens completos, contraseñas, contenido de correos ni datos sensibles.
- Textos de UI y comentarios en español, con acentos y UTF-8.
- Versiones de paquetes exclusivamente en `Directory.Packages.props`.
- No hacer push/merge sin autorización explícita.
- Tests de integración requieren Docker por Testcontainers.MsSql.

---

## File Structure

| File | Responsibility |
|------|----------------|
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/OpcionesCorreo.cs` | Configuración SMTP (host, puerto, remitente). |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IServicioCorreo.cs` | Puerto para enviar correos. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/ServicioCorreoSmtp.cs` | Adaptador SMTP con MailKit. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IPlantillaCorreo.cs` | Puerto para generar asuntos y cuerpos de correo. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/PlantillaCorreoTextoPlano.cs` | Implementación inicial en texto plano. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IGeneradorTokenEmail.cs` | Puerto para generar/validar tokens de confirmación. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/GeneradorTokenEmailDataProtector.cs` | Implementación con IDataProtector + timeout. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Usuarios/UsuarioRegistrado.cs` | Evento de dominio publicado tras registro. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/EnviarConfirmacionEmailHandler.cs` | Handler que reacciona a `UsuarioRegistrado`. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/ConfirmarEmailCommand.cs` | Comando y handler para confirmar email. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/KycResuelto.cs` | Evento de dominio publicado al resolver KYC. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/NotificarKycResueltoHandler.cs` | Handler que reacciona a `KycResuelto`. |
| `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs` | Nuevos endpoints `confirm-email` y `resend-confirmation`; registro publica evento. |
| `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs` | Ajustes de autorización `EmailConfirmed`. |
| `web/src/routes/ConfirmarEmailPage.tsx` | Pantalla post-confirmación de email. |
| `web/src/routes/RegisterPage.tsx` | Mensaje post-registro. |
| `web/src/api/auth.ts` | Clientes para confirmar email y reenviar. |

---

### Task 1: Configuración de correo y registro DI

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/OpcionesCorreo.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`
- Modify: `CaseritoApp/Directory.Packages.props`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/OpcionesCorreoTests.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `OpcionesCorreo` POCO, registro de `IOptions<OpcionesCorreo>` y `IServicioCorreo`.

- [ ] **Step 1: Agregar paquete MailKit a `Directory.Packages.props`**

```xml
<PackageVersion Include="MailKit" Version="4.11.0" />
```

- [ ] **Step 2: Crear `OpcionesCorreo.cs`**

```csharp
namespace CaseritoApp.Identity.Infrastructure.Correo;

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

- [ ] **Step 3: Registrar opciones en `DependencyInjection.cs`**

Dentro de `AgregarIdentity`:

```csharp
servicios.Configure<OpcionesCorreo>(config.GetSection(OpcionesCorreo.Seccion));
```

- [ ] **Step 4: Verificar build**

Run:
```powershell
dotnet build src/Identity/CaseritoApp.Identity.Infrastructure/CaseritoApp.Identity.Infrastructure.csproj
```

Expected: success.

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/Directory.Packages.props
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/OpcionesCorreo.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs
git commit -m "chore(correo): opciones de configuracion SMTP"
```

---

### Task 2: Puerto e implementación de `IServicioCorreo`

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IServicioCorreo.cs`
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/MensajeCorreo.cs`
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/ServicioCorreoSmtp.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/CaseritoApp.Identity.Infrastructure.csproj`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/ServicioCorreoSmtpTests.cs`

**Interfaces:**
- Consumes: `OpcionesCorreo`.
- Produces: `IServicioCorreo.EnviarAsync(MensajeCorreo, CancellationToken)`.

- [ ] **Step 1: Crear puerto y record en Application**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IServicioCorreo.cs
namespace CaseritoApp.Identity.Application.Correo;

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

- [ ] **Step 2: Crear adaptador SMTP en Infrastructure**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/ServicioCorreoSmtp.cs
using CaseritoApp.Identity.Application.Correo;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CaseritoApp.Identity.Infrastructure.Correo;

public sealed partial class ServicioCorreoSmtp(
    IOptions<OpcionesCorreo> opciones,
    ILogger<ServicioCorreoSmtp> logger) : IServicioCorreo
{
    private readonly OpcionesCorreo _opciones = opciones.Value;

    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_opciones.NombreRemitente, _opciones.Remitente));
        mime.To.Add(MailboxAddress.Parse(mensaje.Para));
        mime.Subject = mensaje.Asunto;

        var bodyBuilder = new BodyBuilder { TextBody = mensaje.CuerpoTexto };
        if (!string.IsNullOrWhiteSpace(mensaje.CuerpoHtml))
        {
            bodyBuilder.HtmlBody = mensaje.CuerpoHtml;
        }
        mime.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_opciones.Host, _opciones.Puerto, SecureSocketOptions.StartTls, ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
            RegistrarEnvio(logger, mensaje.Para, mensaje.Asunto);
        }
        catch (Exception ex)
        {
            RegistrarError(logger, mensaje.Para, mensaje.Asunto, ex.Message);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Correo enviado: para={Para} asunto={Asunto}")]
    private static partial void RegistrarEnvio(ILogger logger, string para, string asunto);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error enviando correo: para={Para} asunto={Asunto} error={Error}")]
    private static partial void RegistrarError(ILogger logger, string para, string asunto, string error);
}
```

- [ ] **Step 3: Registrar IServicioCorreo en DI**

```csharp
servicios.AddScoped<IServicioCorreo, ServicioCorreoSmtp>();
```

- [ ] **Step 4: Agregar referencia a MailKit en Infrastructure csproj**

```xml
<PackageReference Include="MailKit" />
```

- [ ] **Step 5: Crear test con IServicioCorreo fake**

```csharp
// CaseritoApp/tests/CaseritoApp.UnitTests/Correo/ServicioCorreoSmtpTests.cs
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Infrastructure.Correo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaseritoApp.UnitTests.Correo;

public sealed class ServicioCorreoSmtpTests
{
    [Fact]
    public void Construye_con_opciones_por_defecto()
    {
        var opciones = Options.Create(new OpcionesCorreo());
        var servicio = new ServicioCorreoSmtp(opciones, NullLogger<ServicioCorreoSmtp>.Instance);
        Assert.NotNull(servicio);
    }
}
```

- [ ] **Step 6: Verificar build y test**

Run:
```powershell
dotnet build CaseritoApp.sln
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter "FullyQualifiedName~Correo"
```

Expected: success.

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/CaseritoApp.Identity.Infrastructure.csproj
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs
git add CaseritoApp/tests/CaseritoApp.UnitTests/Correo/
git commit -m "feat(correo): puerto y adaptador SMTP con MailKit"
```

---

### Task 3: Puerto e implementación de `IPlantillaCorreo`

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IPlantillaCorreo.cs`
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/PlantillaCorreoTextoPlano.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/PlantillaCorreoTextoPlanoTests.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `IPlantillaCorreo` con métodos para confirmación de email y notificaciones KYC.

- [ ] **Step 1: Crear puerto en Application**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IPlantillaCorreo.cs
namespace CaseritoApp.Identity.Application.Correo;

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

- [ ] **Step 2: Crear implementación en Infrastructure**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/PlantillaCorreoTextoPlano.cs
using CaseritoApp.Identity.Application.Correo;

namespace CaseritoApp.Identity.Infrastructure.Correo;

public sealed class PlantillaCorreoTextoPlano : IPlantillaCorreo
{
    public string AsuntoConfirmacionEmail(string nombre) =>
        "Confirma tu cuenta en Caserito";

    public string CuerpoConfirmacionEmail(string nombre, string urlConfirmacion) =>
        $"Hola {nombre},\n\nPara activar tu cuenta, haz clic en el siguiente enlace:\n{urlConfirmacion}\n\n" +
        "Si no creaste esta cuenta, ignora este correo.\n\nEquipo Caserito";

    public string AsuntoKycAprobado(string nombre) =>
        "Tu identidad ha sido verificada";

    public string CuerpoKycAprobado(string nombre) =>
        $"Hola {nombre},\n\nTu verificación de identidad fue aprobada. Ya puedes publicar y vender en Caserito.\n\nEquipo Caserito";

    public string AsuntoKycRechazado(string nombre) =>
        "Tu verificación de identidad fue rechazada";

    public string CuerpoKycRechazado(string nombre, string motivo) =>
        $"Hola {nombre},\n\nTu verificación de identidad no pudo ser aprobada.\n\nMotivo: {motivo}\n\n" +
        "Puedes volver a intentarlo desde tu perfil.\n\nEquipo Caserito";
}
```

- [ ] **Step 3: Registrar en DI**

```csharp
servicios.AddScoped<IPlantillaCorreo, PlantillaCorreoTextoPlano>();
```

- [ ] **Step 4: Crear tests**

```csharp
// CaseritoApp/tests/CaseritoApp.UnitTests/Correo/PlantillaCorreoTextoPlanoTests.cs
using CaseritoApp.Identity.Infrastructure.Correo;
using Xunit;

namespace CaseritoApp.UnitTests.Correo;

public sealed class PlantillaCorreoTextoPlanoTests
{
    private readonly PlantillaCorreoTextoPlano _plantilla = new();

    [Fact]
    public void Confirmacion_email_incluye_url()
    {
        var cuerpo = _plantilla.CuerpoConfirmacionEmail("Luis", "https://caserito.test/confirmar?token=abc");
        Assert.Contains("https://caserito.test/confirmar?token=abc", cuerpo);
    }

    [Fact]
    public void Kyc_rechazado_incluye_motivo()
    {
        var cuerpo = _plantilla.CuerpoKycRechazado("Luis", "El rostro no coincide");
        Assert.Contains("El rostro no coincide", cuerpo);
    }
}
```

- [ ] **Step 5: Verificar build y tests**

Run:
```powershell
dotnet build CaseritoApp.sln
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter "FullyQualifiedName~Correo"
```

- [ ] **Step 6: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IPlantillaCorreo.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/PlantillaCorreoTextoPlano.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs
git add CaseritoApp/tests/CaseritoApp.UnitTests/Correo/PlantillaCorreoTextoPlanoTests.cs
git commit -m "feat(correo): plantillas de correo en texto plano"
```

---

### Task 4: Generador de tokens de confirmación de email

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IGeneradorTokenEmail.cs`
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/GeneradorTokenEmailDataProtector.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/GeneradorTokenEmailDataProtectorTests.cs`

**Interfaces:**
- Consumes: `IDataProtectionProvider`.
- Produces: `IGeneradorTokenEmail.Generar(Guid) -> string`, `Validar(string, out Guid) -> bool`.

- [ ] **Step 1: Crear puerto en Application**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IGeneradorTokenEmail.cs
namespace CaseritoApp.Identity.Application.Correo;

public interface IGeneradorTokenEmail
{
    string Generar(Guid usuarioId);
    bool Validar(string token, out Guid usuarioId);
}
```

- [ ] **Step 2: Crear implementación con DataProtector + timeout**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/GeneradorTokenEmailDataProtector.cs
using System.Buffers.Text;
using CaseritoApp.Identity.Application.Correo;
using Microsoft.AspNetCore.DataProtection;

namespace CaseritoApp.Identity.Infrastructure.Correo;

public sealed class GeneradorTokenEmailDataProtector : IGeneradorTokenEmail
{
    private readonly IDataProtector _protector;
    private readonly TimeSpan _vigencia;

    public GeneradorTokenEmailDataProtector(IDataProtectionProvider dataProtection, TimeSpan? vigencia = null)
    {
        _protector = dataProtection.CreateProtector("CaseritoApp.EmailConfirmation");
        _vigencia = vigencia ?? TimeSpan.FromHours(24);
    }

    public string Generar(Guid usuarioId)
    {
        var payload = $"{usuarioId:N}|{DateTimeOffset.UtcNow:O}";
        var protegido = _protector.Protect(payload);
        return Base64UrlEncode(protegido);
    }

    public bool Validar(string token, out Guid usuarioId)
    {
        usuarioId = Guid.Empty;
        try
        {
            var protegido = Base64UrlDecode(token);
            var payload = _protector.Unprotect(protegido);
            var partes = payload.Split('|');
            if (partes.Length != 2 || !Guid.TryParseExact(partes[0], "N", out var parsedId))
            {
                return false;
            }

            if (!DateTimeOffset.TryParseExact(partes[1], "O", null, DateTimeStyles.RoundtripKind, out var emitido))
            {
                return false;
            }

            if (DateTimeOffset.UtcNow - emitido > _vigencia)
            {
                return false;
            }

            usuarioId = parsedId;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Base64Url.EncodeToString(bytes);
    }

    private static string Base64UrlDecode(string input)
    {
        var bytes = Base64Url.DecodeFromChars(input.ToCharArray());
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
```

- [ ] **Step 3: Agregar using necesario**

Añadir `using System.Globalization;` al archivo.

- [ ] **Step 4: Registrar en DI**

```csharp
servicios.AddSingleton<IGeneradorTokenEmail>(sp =>
{
    var dataProtection = sp.GetRequiredService<IDataProtectionProvider>();
    return new GeneradorTokenEmailDataProtector(dataProtection);
});
```

- [ ] **Step 5: Crear tests**

```csharp
// CaseritoApp/tests/CaseritoApp.UnitTests/Correo/GeneradorTokenEmailDataProtectorTests.cs
using CaseritoApp.Identity.Infrastructure.Correo;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace CaseritoApp.UnitTests.Correo;

public sealed class GeneradorTokenEmailDataProtectorTests
{
    private static GeneradorTokenEmailDataProtector Crear(TimeSpan? vigencia = null)
    {
        var provider = DataProtectionProvider.Create("CaseritoTest");
        return new GeneradorTokenEmailDataProtector(provider, vigencia);
    }

    [Fact]
    public void Generar_y_validar_token_devuelve_usuarioId()
    {
        var generador = Crear();
        var usuarioId = Guid.NewGuid();
        var token = generador.Generar(usuarioId);

        Assert.True(generador.Validar(token, out var resultado));
        Assert.Equal(usuarioId, resultado);
    }

    [Fact]
    public void Token_expirado_no_es_valido()
    {
        var generador = Crear(TimeSpan.FromSeconds(-1));
        var token = generador.Generar(Guid.NewGuid());

        Assert.False(generador.Validar(token, out _));
    }

    [Fact]
    public void Token_invalido_no_es_valido()
    {
        var generador = Crear();
        Assert.False(generador.Validar("token-invalido", out _));
    }
}
```

- [ ] **Step 6: Verificar build y tests**

Run:
```powershell
dotnet build CaseritoApp.sln
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter "FullyQualifiedName~Correo"
```

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IGeneradorTokenEmail.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/GeneradorTokenEmailDataProtector.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs
git add CaseritoApp/tests/CaseritoApp.UnitTests/Correo/GeneradorTokenEmailDataProtectorTests.cs
git commit -m "feat(correo): generador de tokens de confirmacion de email"
```

---

### Task 5: Evento `UsuarioRegistrado` y handler de confirmación de email

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Usuarios/UsuarioRegistrado.cs`
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/EnviarConfirmacionEmailHandler.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/RegistrarUsuarioCommand.cs` (o equivalente)
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/EnviarConfirmacionEmailHandlerTests.cs`

**Interfaces:**
- Consumes: `IServicioCorreo`, `IPlantillaCorreo`, `IGeneradorTokenEmail`.
- Produces: `UsuarioRegistrado` evento publicado; correo enviado.

- [ ] **Step 1: Crear evento de dominio**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Usuarios/UsuarioRegistrado.cs
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Usuarios;

public sealed record UsuarioRegistrado(
    Guid EventoId,
    DateTimeOffset OcurridoEn,
    Guid UsuarioId,
    string Email,
    string Nombre) : DomainEvent;
```

- [ ] **Step 2: Crear handler en Application**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/EnviarConfirmacionEmailHandler.cs
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Domain.Usuarios;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Auth;

public sealed partial class EnviarConfirmacionEmailHandler(
    IServicioCorreo servicioCorreo,
    IPlantillaCorreo plantilla,
    IGeneradorTokenEmail generadorToken,
    ILogger<EnviarConfirmacionEmailHandler> logger)
    : IDomainEventHandler<UsuarioRegistrado>
{
    public async Task Handle(UsuarioRegistrado evento, CancellationToken ct)
    {
        var token = generadorToken.Generar(evento.UsuarioId);
        var url = $"https://caserito.trajano.online/confirmar-email?userId={evento.UsuarioId}&token={Uri.EscapeDataString(token)}";

        var mensaje = new MensajeCorreo(
            evento.Email,
            plantilla.AsuntoConfirmacionEmail(evento.Nombre),
            plantilla.CuerpoConfirmacionEmail(evento.Nombre, url));

        await servicioCorreo.EnviarAsync(mensaje, ct);
        RegistrarEnvio(logger, evento.UsuarioId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Correo de confirmacion enviado: usuario={UsuarioId}")]
    private static partial void RegistrarEnvio(ILogger logger, Guid usuarioId);
}
```

- [ ] **Step 3: Publicar evento tras registro**

En `AuthEndpoints.RegistrarAsync` (o en un command handler si existe), después de crear el usuario exitosamente, publicar el evento. Usar `IPublicadorEventosDominio` si existe, o inyectar `ISender`/`IMediator` y `IPublisher`.

Si el registro vive directamente en `AuthEndpoints`, inyectar `IPublicadorEventosIntegracion` o `IPublisher` de MediatR y publicar:

```csharp
await publicador.PublicarAsync(
    new UsuarioRegistrado(Guid.NewGuid(), tiempo.GetUtcNow(), usuario.Id, usuario.Email!, usuario.Nombre),
    ct);
```

- [ ] **Step 4: Crear test del handler con fakes**

```csharp
// CaseritoApp/tests/CaseritoApp.UnitTests/Auth/EnviarConfirmacionEmailHandlerTests.cs
using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Domain.Usuarios;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseritoApp.UnitTests.Auth;

public sealed class EnviarConfirmacionEmailHandlerTests
{
    private sealed class ServicioCorreoFake : IServicioCorreo
    {
        public MensajeCorreo? UltimoMensaje { get; private set; }
        public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
        {
            UltimoMensaje = mensaje;
            return Task.CompletedTask;
        }
    }

    private sealed class PlantillaFake : IPlantillaCorreo
    {
        public string AsuntoConfirmacionEmail(string nombre) => "Asunto";
        public string CuerpoConfirmacionEmail(string nombre, string url) => url;
        public string AsuntoKycAprobado(string nombre) => "";
        public string CuerpoKycAprobado(string nombre) => "";
        public string AsuntoKycRechazado(string nombre) => "";
        public string CuerpoKycRechazado(string nombre, string motivo) => "";
    }

    private sealed class GeneradorTokenFake : IGeneradorTokenEmail
    {
        public string Generar(Guid usuarioId) => "token-fake";
        public bool Validar(string token, out Guid usuarioId)
        {
            usuarioId = Guid.Empty;
            return false;
        }
    }

    [Fact]
    public async Task Handle_envia_correo_de_confirmacion_con_url()
    {
        var servicio = new ServicioCorreoFake();
        var handler = new EnviarConfirmacionEmailHandler(
            servicio, new PlantillaFake(), new GeneradorTokenFake(), NullLogger<EnviarConfirmacionEmailHandler>.Instance);

        var evento = new UsuarioRegistrado(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), "test@test.com", "Luis");
        await handler.Handle(evento, CancellationToken.None);

        Assert.NotNull(servicio.UltimoMensaje);
        Assert.Contains("token-fake", servicio.UltimoMensaje.CuerpoTexto);
        Assert.Equal("test@test.com", servicio.UltimoMensaje.Para);
    }
}
```

- [ ] **Step 5: Verificar build y tests**

Run:
```powershell
dotnet build CaseritoApp.sln
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter "FullyQualifiedName~ConfirmacionEmail"
```

- [ ] **Step 6: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Usuarios/UsuarioRegistrado.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/EnviarConfirmacionEmailHandler.cs
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs
git add CaseritoApp/tests/CaseritoApp.UnitTests/Auth/EnviarConfirmacionEmailHandlerTests.cs
git commit -m "feat(auth): evento UsuarioRegistrado y envio de confirmacion por email"
```

---

### Task 6: Comando y endpoint para confirmar email

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/ConfirmarEmailCommand.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/ConfirmarEmailCommandHandlerTests.cs`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthFlowTests.cs`

**Interfaces:**
- Consumes: `IGeneradorTokenEmail`, `UserManager<ApplicationUser>`.
- Produces: `ConfirmarEmailCommand` → 204 si éxito, 400 si token inválido.

- [ ] **Step 1: Crear command y handler**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/ConfirmarEmailCommand.cs
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Identity.Application.Auth;

public sealed record ConfirmarEmailCommand(Guid UsuarioId, string Token) : ICommand;

public sealed class ConfirmarEmailCommandHandler(
    UserManager<ApplicationUser> userManager,
    IGeneradorTokenEmail generadorToken)
    : ICommandHandler<ConfirmarEmailCommand>
{
    public async Task<Result> Handle(ConfirmarEmailCommand request, CancellationToken cancellationToken)
    {
        if (!generadorToken.Validar(request.Token, out var usuarioIdToken) || usuarioIdToken != request.UsuarioId)
        {
            return Result.Fallo(new Error("Auth.TokenConfirmacionInvalido", "El enlace de confirmación no es válido o ha expirado."));
        }

        var usuario = await userManager.FindByIdAsync(request.UsuarioId.ToString());
        if (usuario is null)
        {
            return Result.Fallo(new Error("Auth.UsuarioNoEncontrado", "El usuario no existe."));
        }

        if (usuario.EmailConfirmed)
        {
            return Result.Exito();
        }

        usuario.EmailConfirmed = true;
        var resultado = await userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            return Result.Fallo(new Error("Auth.ErrorActualizandoUsuario", "No se pudo confirmar el email."));
        }

        return Result.Exito();
    }
}
```

- [ ] **Step 2: Agregar endpoints en AuthEndpoints**

```csharp
grupo.MapPost("/confirm-email", ConfirmarEmailAsync)
    .Accepts<ConfirmarEmailRequest>("application/json")
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status400BadRequest);

grupo.MapPost("/resend-confirmation", ReenviarConfirmacionAsync)
    .RequireAuthorization()
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status401Unauthorized);
```

Handlers:

```csharp
private static async Task<IResult> ConfirmarEmailAsync(
    ConfirmarEmailRequest request,
    ISender sender,
    CancellationToken ct)
{
    var resultado = await sender.Send(new ConfirmarEmailCommand(request.UsuarioId, request.Token), ct);
    return resultado.EsExito ? Results.NoContent() : Results.Problem(
        title: resultado.Error.Code,
        detail: resultado.Error.Message,
        statusCode: StatusCodes.Status400BadRequest);
}

private static async Task<IResult> ReenviarConfirmacionAsync(
    ClaimsPrincipal usuario,
    UserManager<ApplicationUser> userManager,
    ISender sender,
    IPublisher publisher,
    CancellationToken ct)
{
    if (!TryObtenerUserId(usuario, out var userId))
    {
        return Results.Unauthorized();
    }

    var appUser = await userManager.FindByIdAsync(userId.ToString());
    if (appUser is null || appUser.EmailConfirmed)
    {
        return Results.NoContent();
    }

    await publisher.Publish(
        new UsuarioRegistrado(Guid.NewGuid(), DateTimeOffset.UtcNow, appUser.Id, appUser.Email!, appUser.Nombre),
        ct);

    return Results.NoContent();
}
```

Request record:

```csharp
public sealed record ConfirmarEmailRequest(Guid UsuarioId, string Token);
```

- [ ] **Step 3: Tests unitarios del handler**

```csharp
// CaseritoApp/tests/CaseritoApp.UnitTests/Auth/ConfirmarEmailCommandHandlerTests.cs
using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace CaseritoApp.UnitTests.Auth;

public sealed class ConfirmarEmailCommandHandlerTests
{
    [Fact]
    public async Task Token_valido_confirma_email()
    {
        var userId = Guid.NewGuid();
        var generador = Substitute.For<IGeneradorTokenEmail>();
        generador.Validar("token-valido", out Arg.Any<Guid>()).Returns(x => { x[1] = userId; return true; });

        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        userManager.FindByIdAsync(userId.ToString()).Returns(new ApplicationUser { Id = userId, Email = "test@test.com" });
        userManager.UpdateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);

        var handler = new ConfirmarEmailCommandHandler(userManager, generador);
        var resultado = await handler.Handle(new ConfirmarEmailCommand(userId, "token-valido"), CancellationToken.None);

        Assert.True(resultado.EsExito);
    }
}
```

- [ ] **Step 4: Verificar build y tests**

Run:
```powershell
dotnet build CaseritoApp.sln
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter "FullyQualifiedName~ConfirmarEmail"
```

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/ConfirmarEmailCommand.cs
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs
git add CaseritoApp/tests/CaseritoApp.UnitTests/Auth/ConfirmarEmailCommandHandlerTests.cs
git commit -m "feat(auth): endpoint para confirmar email y reenviar confirmacion"
```

---

### Task 7: Notificación de KYC (evento + handler)

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/KycResuelto.cs`
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/NotificarKycResueltoHandler.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/AprobarSolicitudKycCommand.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/RechazarSolicitudKycCommand.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/NotificarKycResueltoHandlerTests.cs`

**Interfaces:**
- Consumes: `IServicioCorreo`, `IPlantillaCorreo`, `IConsultaVerificacionKyc` (para obtener email/nombre del usuario).
- Produces: `KycResuelto` publicado al aprobar/rechazar; correo enviado.

- [ ] **Step 1: Crear evento de dominio**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/KycResuelto.cs
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Kyc;

public sealed record KycResuelto(
    Guid EventoId,
    DateTimeOffset OcurridoEn,
    Guid UsuarioId,
    Guid SolicitudId,
    EstadoKyc Estado,
    string? MotivoRechazo) : DomainEvent;
```

- [ ] **Step 2: Crear handler de notificación**

```csharp
// CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/NotificarKycResueltoHandler.cs
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

public sealed partial class NotificarKycResueltoHandler(
    IServicioCorreo servicioCorreo,
    IPlantillaCorreo plantilla,
    IConsultaVerificacionKyc consulta,
    ILogger<NotificarKycResueltoHandler> logger)
    : IDomainEventHandler<KycResuelto>
{
    public async Task Handle(KycResuelto evento, CancellationToken ct)
    {
        var usuario = await consulta.ObtenerUsuarioAsync(evento.UsuarioId, ct);
        if (usuario is null)
        {
            RegistrarUsuarioNoEncontrado(logger, evento.UsuarioId);
            return;
        }

        MensajeCorreo mensaje = evento.Estado switch
        {
            EstadoKyc.Aprobada => new MensajeCorreo(
                usuario.Email,
                plantilla.AsuntoKycAprobado(usuario.Nombre),
                plantilla.CuerpoKycAprobado(usuario.Nombre)),
            EstadoKyc.Rechazada => new MensajeCorreo(
                usuario.Email,
                plantilla.AsuntoKycRechazado(usuario.Nombre),
                plantilla.CuerpoKycRechazado(usuario.Nombre, evento.MotivoRechazo ?? "No especificado.")),
            _ => null!
        };

        if (mensaje is null)
        {
            return;
        }

        await servicioCorreo.EnviarAsync(mensaje, ct);
        RegistrarNotificacion(logger, evento.UsuarioId, evento.Estado.ToString());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Usuario no encontrado al notificar KYC: usuario={UsuarioId}")]
    private static partial void RegistrarUsuarioNoEncontrado(ILogger logger, Guid usuarioId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Notificacion KYC enviada: usuario={UsuarioId} estado={Estado}")]
    private static partial void RegistrarNotificacion(ILogger logger, Guid usuarioId, string estado);
}
```

- [ ] **Step 3: Extender `IConsultaVerificacionKyc`**

Agregar método:

```csharp
Task<UsuarioKycDto?> ObtenerUsuarioAsync(Guid usuarioId, CancellationToken ct);
```

y record:

```csharp
public sealed record UsuarioKycDto(string Email, string Nombre);
```

Implementar en `ConsultaVerificacionKycEfCore` usando `UserManager<ApplicationUser>`.

- [ ] **Step 4: Publicar `KycResuelto` desde aprobar/rechazar**

En `AprobarSolicitudKycCommandHandler`, después de `publicador.PublicarAsync(UserVerified...)`:

```csharp
await publisher.Publish(
    new KycResuelto(Guid.NewGuid(), ahora, verificacion.UsuarioId, request.SolicitudId, EstadoKyc.Aprobada, null),
    cancellationToken);
```

En `RechazarSolicitudKycCommandHandler`, después de rechazar:

```csharp
await publisher.Publish(
    new KycResuelto(Guid.NewGuid(), tiempo.GetUtcNow(), verificacion.UsuarioId, request.SolicitudId, EstadoKyc.Rechazada, request.Motivo),
    cancellationToken);
```

- [ ] **Step 5: Tests del handler**

```csharp
// CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/NotificarKycResueltoHandlerTests.cs
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class NotificarKycResueltoHandlerTests
{
    [Fact]
    public async Task Aprobado_envia_correo_de_aprobacion()
    {
        var servicio = Substitute.For<IServicioCorreo>();
        var plantilla = new PlantillaCorreoTextoPlano(); // o fake
        var consulta = Substitute.For<IConsultaVerificacionKyc>();
        consulta.ObtenerUsuarioAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new UsuarioKycDto("test@test.com", "Luis"));

        var handler = new NotificarKycResueltoHandler(servicio, plantilla, consulta, NullLogger<NotificarKycResueltoHandler>.Instance);
        await handler.Handle(new KycResuelto(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), EstadoKyc.Aprobada, null), CancellationToken.None);

        await servicio.Received(1).EnviarAsync(
            Arg.Is<MensajeCorreo>(m => m.Asunto.Contains("verificada")),
            Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 6: Verificar build y tests**

Run:
```powershell
dotnet build CaseritoApp.sln
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter "FullyQualifiedName~NotificarKyc"
```

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/KycResuelto.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/NotificarKycResueltoHandler.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/IConsultaVerificacionKyc.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConsultaVerificacionKycEfCore.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/AprobarSolicitudKycCommand.cs
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/RechazarSolicitudKycCommand.cs
git add CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/NotificarKycResueltoHandlerTests.cs
git commit -m "feat(kyc): evento KycResuelto y notificacion por email"
```

---

### Task 8: Restricciones de autorización por `EmailConfirmed`

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs` (o donde esté crear aviso)
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthFlowTests.cs`

**Interfaces:**
- Consumes: `IAuthorizationRequirement`/`IAuthorizationPolicyProvider`.
- Produces: política `EmailConfirmado` aplicada a endpoints sensibles.

- [ ] **Step 1: Crear requirement y handler**

```csharp
// CaseritoApp/src/Host/CaseritoApp.Host/Auth/RequisitoEmailConfirmado.cs
using Microsoft.AspNetCore.Authorization;

namespace CaseritoApp.Host.Auth;

public sealed class RequisitoEmailConfirmado : IAuthorizationRequirement;

public sealed class EmailConfirmadoHandler : AuthorizationHandler<RequisitoEmailConfirmado>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequisitoEmailConfirmado requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim("emailConfirmed", "true"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Configurar claim en el JWT**

En `GeneradorTokensAcceso` (o similar), incluir claim `emailConfirmed` basado en `usuario.EmailConfirmed`.

- [ ] **Step 3: Registrar política**

En `Program.cs`:

```csharp
services.AddAuthorizationBuilder()
    .AddPolicy("EmailConfirmado", policy =>
        policy.Requirements.Add(new RequisitoEmailConfirmado()));

services.AddSingleton<IAuthorizationHandler, EmailConfirmadoHandler>();
```

- [ ] **Step 4: Aplicar política a endpoints**

En `KycEndpoints`:

```csharp
var usuario = app.MapGroup("/api/kyc")
    .RequireAuthorization("EmailConfirmado");
```

En `AvisosEndpoints`, al grupo de crear/editar avisos:

```csharp
grupo.MapPost("/", CrearAsync).RequireAuthorization("EmailConfirmado");
```

- [ ] **Step 5: Test de integración**

```csharp
// CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthFlowTests.cs
[Fact]
public async Task Usuario_sin_email_confirmado_no_puede_enviar_kyc()
{
    // registrar usuario
    // login
    // intentar POST /api/kyc
    // esperar 403
}
```

- [ ] **Step 6: Verificar build y tests**

Run:
```powershell
dotnet build CaseritoApp.sln
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~Email"
```

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Auth/
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs
git add CaseritoApp/src/Host/CaseritoApp.Host/Program.cs
git add CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthFlowTests.cs
git commit -m "feat(auth): requiere email confirmado para KYC y publicar avisos"
```

---

### Task 9: Frontend — pantallas post-registro y confirmación de email

**Files:**
- Modify: `web/src/routes/RegisterPage.tsx`
- Create: `web/src/routes/ConfirmarEmailPage.tsx`
- Modify: `web/src/api/auth.ts`
- Modify: `web/src/router.tsx`
- Test: `web/src/routes/ConfirmarEmailPage.test.tsx`

**Interfaces:**
- Consumes: endpoints `POST /api/auth/confirm-email` y `POST /api/auth/resend-confirmation`.
- Produces: UI de confirmación y mensajes post-registro.

- [ ] **Step 1: Agregar funciones en `web/src/api/auth.ts`**

```typescript
export async function confirmarEmail(usuarioId: string, token: string): Promise<void> {
  await api.post('/api/auth/confirm-email', { json: { usuarioId, token } });
}

export async function reenviarConfirmacionEmail(): Promise<void> {
  await api.post('/api/auth/resend-confirmation');
}
```

- [ ] **Step 2: Crear `ConfirmarEmailPage.tsx`**

```tsx
// web/src/routes/ConfirmarEmailPage.tsx
import { useEffect, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Box, Typography, Alert, CircularProgress, Button } from '@mui/material';
import { confirmarEmail } from '@/api/auth';

export default function ConfirmarEmailPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [estado, setEstado] = useState<'cargando' | 'exito' | 'error'>('cargando');

  useEffect(() => {
    const usuarioId = searchParams.get('userId');
    const token = searchParams.get('token');

    if (!usuarioId || !token) {
      setEstado('error');
      return;
    }

    confirmarEmail(usuarioId, token)
      .then(() => setEstado('exito'))
      .catch(() => setEstado('error'));
  }, [searchParams]);

  return (
    <Box sx={{ maxWidth: 500, mx: 'auto', mt: 8, p: 3 }}>
      <Typography variant="h4" gutterBottom>Confirmación de email</Typography>
      {estado === 'cargando' && <CircularProgress />}
      {estado === 'exito' && (
        <Alert severity="success">
          Tu email ha sido confirmado. Ahora puedes iniciar sesión.
          <Button onClick={() => navigate('/login')}>Iniciar sesión</Button>
        </Alert>
      )}
      {estado === 'error' && (
        <Alert severity="error">
          El enlace no es válido o ha expirado. Solicita uno nuevo desde tu perfil.
        </Alert>
      )}
    </Box>
  );
}
```

- [ ] **Step 3: Modificar `RegisterPage.tsx` para mostrar mensaje post-registro**

Después de registro exitoso, mostrar:

```tsx
<Alert severity="info">
  Te enviamos un correo de confirmación. Revisa tu bandeja de entrada y haz clic en el enlace para activar tu cuenta.
</Alert>
```

- [ ] **Step 4: Agregar ruta en `router.tsx`**

```typescript
{ path: '/confirmar-email', element: <ConfirmarEmailPage /> }
```

- [ ] **Step 5: Tests frontend**

```tsx
// web/src/routes/ConfirmarEmailPage.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import ConfirmarEmailPage from './ConfirmarEmailPage';

vi.mock('@/api/auth', () => ({
  confirmarEmail: vi.fn().mockResolvedValue(undefined),
}));

describe('ConfirmarEmailPage', () => {
  it('muestra exito cuando el token es valido', async () => {
    render(
      <MemoryRouter initialEntries={['/confirmar-email?userId=123&token=abc']}>
        <ConfirmarEmailPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/email ha sido confirmado/i)).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 6: Verificar typecheck, lint y tests**

Run:
```powershell
npm run typecheck
npm run lint
npm run test -- --run
```

- [ ] **Step 7: Commit**

```bash
git add web/src/routes/ConfirmarEmailPage.tsx
git add web/src/routes/ConfirmarEmailPage.test.tsx
git add web/src/routes/RegisterPage.tsx
git add web/src/api/auth.ts
git add web/src/router.tsx
git commit -m "feat(web): pantalla de confirmacion de email y mensaje post-registro"
```

---

### Task 10: Verificación global y cierre

**Files:**
- Todo el solution.

- [ ] **Step 1: Build backend**

```powershell
dotnet build CaseritoApp.sln
```

- [ ] **Step 2: Tests backend**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj
```

- [ ] **Step 3: Formato**

```powershell
dotnet format CaseritoApp.sln --verify-no-changes
```

- [ ] **Step 4: Frontend checks**

```powershell
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

- [ ] **Step 5: Commit final de verificación**

```bash
git commit -m "chore(correo-kyc): verificacion global y formato" --allow-empty
```

- [ ] **Step 6: Actualizar `docs/ai/HANDOFF.md`**

Resumir el trabajo, archivos tocados y checks ejecutados.
