# Despliegue VPS CaseritoApp — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dejar el repo listo para desplegar CaseritoApp en la VPS Trajano (un contenedor `caseritoapp`, API + SPA + SignalR) con workflow de GitHub Actions, según el spec `docs/superpowers/specs/2026-07-30-despliegue-vps-caseritoapp-design.md`.

**Architecture:** El Host .NET sirve la SPA desde `wwwroot` (un solo contenedor, patrón decoraciones). La PII de KYC se cifra con ASP.NET Core Data Protection (key ring persistido por volumen), reemplazando el fail-fast que hoy impide arrancar en Production. Deploy: runner publica API + compila web → rsync → build runtime-only en VPS.

**Tech Stack:** .NET 10 (warnings-as-errors), xUnit + NSubstitute, ASP.NET Core Data Protection, React/Vite, GitHub Actions, Docker Compose v2.

## Global Constraints

- Warnings-as-errors: todo el código nuevo compila sin warnings (`dotnet build`).
- Comentarios y textos de UI en español, UTF-8 con acentos (regla del repo).
- Anti-PII: logs sin datos personales; solo códigos de error de Identity.
- Formato: `dotnet format CaseritoApp.sln --verify-no-changes` debe pasar (comandos desde `CaseritoApp/`).
- La cadena de conexión y secretos NUNCA en el repo; solo variables `Xxx__Yyy` documentadas en `.env.production.example`.
- Entornos con comportamiento especial: `Development`, `Testing`, `Production` (el stub de tests usa `IHostEnvironment` con esos nombres).
- Política de contraseña Identity: `RequiredLength = 8` + defaults (mayús, minús, dígito, símbolo).
- Los commits de cada tarea requieren confirmación explícita del usuario (regla del repo: no mutaciones git sin autorización). Preguntar antes de cada `git commit`.
- Tests de integración usan Testcontainers.MsSql: requieren Docker corriendo; los tests unitarios no.

---

### Task 1: `DataProtectionEncryptor` real (desbloquea arranque en Production)

**Files:**
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/Security/DataProtectionEncryptor.cs`
- Modify: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/CaseritoApp.BuildingBlocks.Infrastructure.csproj` (añadir FrameworkReference)
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs:79-90` (reemplazar throw + registro)
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/DataProtectionEncryptorTests.cs` (nuevo)
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/FailFastEncryptorTests.cs` (actualizar primer test)

**Interfaces:**
- Consumes: `IEncryptor` (`Cifrar(string)→string`, `Cifrar(byte[])→byte[]`, `Descifrar(string)→string`, `Descifrar(byte[])→byte[]`), `IDataProtectionProvider` de `Microsoft.AspNetCore.DataProtection`.
- Produces: `DataProtectionEncryptor(IDataProtectionProvider)` registrado como `IEncryptor` fuera de Development/Testing. Config `DataProtection:RutaClaves` (env `DataProtection__RutaClaves`, default `/data/dataprotection-keys`). Purpose fijo: `"CaseritoApp.KycPii.v1"`.

- [ ] **Step 1: Escribir el test que falla** — `DataProtectionEncryptorTests.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class DataProtectionEncryptorTests
{
    private static DataProtectionEncryptor CrearEncryptor(IDataProtectionProvider? provider = null)
        => new(provider ?? DataProtectionProvider.Create("CaseritoTest"));

    [Fact]
    public void Cifrar_y_descifrar_bytes_round_trip()
    {
        var encryptor = CrearEncryptor();
        var datos = new byte[] { 1, 2, 3, 250, 0, 17 };

        var cifrado = encryptor.Cifrar(datos);

        Assert.NotEqual(datos, cifrado);
        Assert.Equal(datos, encryptor.Descifrar(cifrado));
    }

    [Fact]
    public void Cifrar_y_descifrar_texto_round_trip_utf8()
    {
        var encryptor = CrearEncryptor();

        var cifrado = encryptor.Cifrar("CI 1234567 — José Ñañez");

        Assert.NotEqual("CI 1234567 — José Ñañez", cifrado);
        Assert.Equal("CI 1234567 — José Ñañez", encryptor.Descifrar(cifrado));
    }

    [Fact]
    public void Descifrar_payload_alterado_falla()
    {
        var encryptor = CrearEncryptor();
        var cifrado = encryptor.Cifrar(new byte[] { 9, 9, 9 });
        cifrado[0] ^= 0xFF;

        Assert.Throws<System.Security.Cryptography.CryptographicException>(
            () => encryptor.Descifrar(cifrado));
    }

    [Fact]
    public void Otro_proposito_no_descifra()
    {
        var provider = DataProtectionProvider.Create("CaseritoTest");
        var cifrado = CrearEncryptor(provider).Cifrar("secreto");
        var otro = provider.CreateProtector("Otro.Proposito");

        Assert.Throws<System.Security.Cryptography.CryptographicException>(
            () => otro.Unprotect(System.Buffers.Text.Base64Url.DecodeFromChars(cifrado)));
    }
}
```

Y actualizar el primer test de `FailFastEncryptorTests.cs` (el throw ya no aplica):

```csharp
    [Fact]
    public void Fuera_de_dev_o_testing_registra_encryptor_real()
    {
        var rutaTemporal = Path.Combine(Path.GetTempPath(), $"caserito-keys-{Guid.NewGuid():N}");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:RutaClaves"] = rutaTemporal,
            })
            .Build();

        var provider = new ServiceCollection()
            .AddLogging()
            .AgregarIdentity(config, new EntornoStub("Production"))
            .BuildServiceProvider();

        Assert.IsType<DataProtectionEncryptor>(provider.GetRequiredService<IEncryptor>());
    }
```

(Añadir `using System.Buffers.Text;` no es necesario en este archivo; mantener los tests de dev/testing que ya existen.)

- [ ] **Step 2: Ejecutar los tests y verlos fallar**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~DataProtectionEncryptorTests|FullyQualifiedName~FailFastEncryptorTests"`
Expected: FAIL (tipo `DataProtectionEncryptor` no existe — error de compilación).

- [ ] **Step 3: Añadir FrameworkReference al csproj de BuildingBlocks**

En `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/CaseritoApp.BuildingBlocks.Infrastructure.csproj`, dentro de un `<ItemGroup>` nuevo o existente:

```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

- [ ] **Step 4: Implementar `DataProtectionEncryptor`**

```csharp
using System.Buffers.Text;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace CaseritoApp.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Encryptor real basado en ASP.NET Core Data Protection (AES-256-CBC + HMAC, con rotación de
/// claves incorporada). Reemplaza al PassthroughEncryptor fuera de Development/Testing para que
/// la PII de KYC (CI, selfies) no se escriba en claro. El key ring se persiste por volumen
/// (config "DataProtection:RutaClaves"); sin ese volumen los datos cifrados no sobreviven un
/// recreate del contenedor. El envelope/KMS externo sigue diferido: cambiar la implementación
/// registrada en DI no afecta a los consumidores de <see cref="IEncryptor"/>.
/// </summary>
public sealed class DataProtectionEncryptor : IEncryptor
{
    private readonly IDataProtector _protector;

    public DataProtectionEncryptor(IDataProtectionProvider proveedor)
    {
        _protector = proveedor.CreateProtector("CaseritoApp.KycPii.v1");
    }

    public string Cifrar(string textoPlano)
        => Base64Url.EncodeToString(Cifrar(Encoding.UTF8.GetBytes(textoPlano)));

    public byte[] Cifrar(byte[] datos) => _protector.Protect(datos);

    public string Descifrar(string textoCifrado)
        => Encoding.UTF8.GetString(Descifrar(Base64Url.DecodeFromChars(textoCifrado)));

    public byte[] Descifrar(byte[] datos) => _protector.Unprotect(datos);
}
```

- [ ] **Step 5: Cablear en `AgregarIdentity`** — reemplazar en `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` el bloque actual (comentario fail-fast + `throw` + registro de Passthrough, líneas 79-90) por:

```csharp
        // Cifrado de PII: en Development/Testing se mantiene el Passthrough (sin cifrado, cómodo
        // para depurar y tests). Fuera de esos entornos se cablea el encryptor real sobre
        // ASP.NET Core Data Protection, con el key ring persistido en disco (volumen del host)
        // para que sobreviva al recreate del contenedor. Mismo criterio de entorno que la clave
        // efímera de JWT.
        if (entorno.IsDevelopment() || entorno.IsEnvironment("Testing"))
        {
            servicios.AddSingleton<IEncryptor, PassthroughEncryptor>();
        }
        else
        {
            var rutaClaves = config.GetValue<string>("DataProtection:RutaClaves")
                ?? "/data/dataprotection-keys";
            servicios.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(rutaClaves));
            servicios.AddSingleton<IEncryptor, DataProtectionEncryptor>();
        }
```

(`DirectoryInfo` ya está disponible vía `System.IO` implícito; `PersistKeysToFileSystem` está en `Microsoft.AspNetCore.DataProtection`, ya importado en el archivo.)

- [ ] **Step 6: Ejecutar los tests y verlos pasar**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~DataProtectionEncryptorTests|FullyQualifiedName~FailFastEncryptorTests"`
Expected: PASS (5 tests).

- [ ] **Step 7: Verificar que no se rompió nada más + formato**

Run: `cd CaseritoApp && dotnet build CaseritoApp.sln --configuration Release && dotnet format CaseritoApp.sln --verify-no-changes`
Expected: 0 errores, 0 warnings, formato limpio.

- [ ] **Step 8: Commit** (pedir confirmación al usuario primero)

```bash
git add CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs CaseritoApp/tests/CaseritoApp.UnitTests/Kyc
git commit -m "feat(kyc): encryptor real con Data Protection para producción"
```

---

### Task 2: URL pública configurable en el correo de confirmación

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/OpcionesApp.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/EnviarConfirmacionEmailHandler.cs:18`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` (registrar `OpcionesApp` junto a `OpcionesCorreo`, ~línea 61)
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/EnviarConfirmacionEmailHandlerTests.cs`

**Interfaces:**
- Consumes: nada de otras tareas.
- Produces: `OpcionesApp` (sección `App`, propiedad `UrlPublica`, env `App__UrlPublica`, default `http://localhost:5173`). El handler recibe `IOptions<OpcionesApp>` como **primer** parámetro nuevo del constructor (después de los existentes, mantener orden: servicioCorreo, plantilla, generadorToken, opcionesApp, logger).

- [ ] **Step 1: Actualizar los tests primero** — en `EnviarConfirmacionEmailHandlerTests.cs`:

Añadir usings: `using Microsoft.Extensions.Options;`. En ambos tests, cambiar la construcción del handler para incluir las opciones:

```csharp
        var handler = new EnviarConfirmacionEmailHandler(
            servicio,
            new PlantillaFake(),
            new GeneradorTokenFake(),
            Options.Create(new OpcionesApp { UrlPublica = "https://app.ejemplo.test/" }),
            NullLogger<EnviarConfirmacionEmailHandler>.Instance);
```

Y reforzar el primer test para fijar la URL base (con barra final en config, que debe recortarse):

```csharp
        Assert.Contains(
            "https://app.ejemplo.test/confirmar-email?userId=", servicio.UltimoMensaje.CuerpoTexto);
        Assert.Contains("token-fake", servicio.UltimoMensaje.CuerpoTexto);
```

(En el segundo test — fallo de envío — usar las mismas opciones por consistencia.)

- [ ] **Step 2: Ejecutar y ver fallar**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~EnviarConfirmacionEmailHandlerTests"`
Expected: FAIL (no existe el constructor con `IOptions<OpcionesApp>` — error de compilación).

- [ ] **Step 3: Crear `OpcionesApp`**

```csharp
namespace CaseritoApp.Identity.Application.Auth;

/// <summary>Opciones globales de la aplicación (sección de configuración <c>App</c>).</summary>
public sealed class OpcionesApp
{
    public const string Seccion = "App";

    /// <summary>
    /// URL pública base de la SPA, usada para construir enlaces en correos. En producción:
    /// https://caserito.app. Default: dev server de Vite.
    /// </summary>
    public string UrlPublica { get; set; } = "http://localhost:5173";
}
```

- [ ] **Step 4: Usar la opción en el handler** — en `EnviarConfirmacionEmailHandler.cs`:

```csharp
public sealed partial class EnviarConfirmacionEmailHandler(
    IServicioCorreo servicioCorreo,
    IPlantillaCorreo plantilla,
    IGeneradorTokenEmail generadorToken,
    IOptions<OpcionesApp> opcionesApp,
    ILogger<EnviarConfirmacionEmailHandler> logger)
    : IDomainEventConsumer<UsuarioRegistrado>
{
    public async Task Handle(UsuarioRegistrado evento, CancellationToken cancellationToken)
    {
        var token = generadorToken.Generar(evento.UsuarioId);
        var urlBase = opcionesApp.Value.UrlPublica.TrimEnd('/');
        var url = $"{urlBase}/confirmar-email?userId={evento.UsuarioId}&token={Uri.EscapeDataString(token)}";
        // ... resto igual
```

(Añadir `using Microsoft.Extensions.Options;`.)

- [ ] **Step 5: Registrar `OpcionesApp` en DI** — en `DependencyInjection.cs`, junto al `Configure<OpcionesCorreo>` existente:

```csharp
        servicios.Configure<OpcionesApp>(config.GetSection(OpcionesApp.Seccion));
```

- [ ] **Step 6: Ejecutar y ver pasar**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~EnviarConfirmacionEmailHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 7: Build + formato**

Run: `cd CaseritoApp && dotnet build CaseritoApp.sln --configuration Release && dotnet format CaseritoApp.sln --verify-no-changes`
Expected: limpio.

- [ ] **Step 8: Commit** (pedir confirmación)

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs CaseritoApp/tests/CaseritoApp.UnitTests/Auth/EnviarConfirmacionEmailHandlerTests.cs
git commit -m "fix(correo): URL de confirmación configurable vía App:UrlPublica"
```

---

### Task 3: Seed de admin idempotente en Production

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/SeedAdminPlataforma.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs:287` (tras `SembrarRolesAsync`)
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Autorizacion/SeedAdminPlataformaTests.cs` (nuevo)

**Interfaces:**
- Consumes: `RolesApp.AdminPlataforma`, `ApplicationUser` (props `UserName`, `Email`, `Nombre`, `Ciudad`, `EmailConfirmed`), patrón de `BootstrapUsuariosPrueba` (`LoggerMessage` parciales).
- Produces: `OpcionesSeedAdmin` (sección `SeedSettings`: `AdminEmail`, `AdminPassword`, `AdminNombre`, `AdminCiudad`) y `SeedAdminPlataforma(UserManager<ApplicationUser>, ILogger<SeedAdminPlataforma>)` con `Task EjecutarAsync(OpcionesSeedAdmin, CancellationToken)`.

- [ ] **Step 1: Escribir los tests que fallan** — `SeedAdminPlataformaTests.cs`:

```csharp
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class SeedAdminPlataformaTests
{
    private static UserManager<ApplicationUser> CrearUserManagerSustituto()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    private static OpcionesSeedAdmin OpcionesCompletas() => new()
    {
        AdminEmail = "admin@caserito.test",
        AdminPassword = "Clave$ecreta1",
        AdminNombre = "Admin",
        AdminCiudad = "Cochabamba",
    };

    private static SeedAdminPlataforma CrearSeed(UserManager<ApplicationUser> usuarios)
        => new(usuarios, NullLogger<SeedAdminPlataforma>.Instance);

    [Fact]
    public async Task Sin_configuracion_completa_no_crea_nada()
    {
        var usuarios = CrearUserManagerSustituto();

        await CrearSeed(usuarios).EjecutarAsync(new OpcionesSeedAdmin(), CancellationToken.None);

        await usuarios.DidNotReceive().CreateAsync(
            Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Admin_ya_existente_es_no_op()
    {
        var usuarios = CrearUserManagerSustituto();
        usuarios.FindByEmailAsync("admin@caserito.test")
            .Returns(new ApplicationUser { Email = "admin@caserito.test" });

        await CrearSeed(usuarios).EjecutarAsync(OpcionesCompletas(), CancellationToken.None);

        await usuarios.DidNotReceive().CreateAsync(
            Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Crea_admin_confirmado_con_rol_admin_plataforma()
    {
        var usuarios = CrearUserManagerSustituto();
        usuarios.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        usuarios.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        usuarios.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        await CrearSeed(usuarios).EjecutarAsync(OpcionesCompletas(), CancellationToken.None);

        await usuarios.Received(1).CreateAsync(
            Arg.Is<ApplicationUser>(u =>
                u.Email == "admin@caserito.test" && u.EmailConfirmed),
            "Clave$ecreta1");
        await usuarios.Received(1).AddToRoleAsync(
            Arg.Any<ApplicationUser>(), RolesApp.AdminPlataforma);
    }

    [Fact]
    public async Task Fallo_de_contrasena_no_lanza_excepcion()
    {
        var usuarios = CrearUserManagerSustituto();
        usuarios.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        usuarios.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "x" }));

        var excepcion = await Record.ExceptionAsync(
            () => CrearSeed(usuarios).EjecutarAsync(OpcionesCompletas(), CancellationToken.None));

        Assert.Null(excepcion);
        await usuarios.DidNotReceive().AddToRoleAsync(
            Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }
}
```

- [ ] **Step 2: Ejecutar y ver fallar**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~SeedAdminPlataformaTests"`
Expected: FAIL (tipos no existen — error de compilación).

- [ ] **Step 3: Implementar `SeedAdminPlataforma.cs`**

```csharp
using CaseritoApp.Identity.Domain.Autorizacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Configuración opt-in del seed del administrador de plataforma (producción).</summary>
public sealed record OpcionesSeedAdmin
{
    public const string Seccion = "SeedSettings";

    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminNombre { get; set; } = string.Empty;
    public string AdminCiudad { get; set; } = string.Empty;

    internal bool EstaCompleta =>
        !string.IsNullOrWhiteSpace(AdminEmail)
        && !string.IsNullOrWhiteSpace(AdminPassword)
        && !string.IsNullOrWhiteSpace(AdminNombre)
        && !string.IsNullOrWhiteSpace(AdminCiudad);
}

/// <summary>
/// Crea el primer AdminPlataforma en producción cuando no existe (idempotente: busca por email).
/// Equivalente al seed de decoraciones: nunca modifica un usuario existente. Los fallos de
/// Identity (p. ej. contraseña que no cumple la política: ≥8, mayús, minús, dígito y símbolo) se
/// loguean como Error SIN tumbar la app — el sitio público sigue sirviendo y basta corregir el
/// .env y reiniciar (el seed reintenta en cada arranque). Anti-PII: solo se loguean códigos de
/// Identity, nunca la contraseña ni el email... el email del admin NO es PII de usuario final pero
/// se omite igual por higiene; el código de error basta para diagnosticar.
/// </summary>
public sealed partial class SeedAdminPlataforma(
    UserManager<ApplicationUser> usuarios,
    ILogger<SeedAdminPlataforma> logger)
{
    public async Task EjecutarAsync(OpcionesSeedAdmin opciones, CancellationToken ct = default)
    {
        if (!opciones.EstaCompleta)
        {
            SeedOmitido(logger);
            return;
        }

        if (await usuarios.FindByEmailAsync(opciones.AdminEmail) is not null)
        {
            SeedYaExistente(logger);
            return;
        }

        var usuario = new ApplicationUser
        {
            UserName = opciones.AdminEmail,
            Email = opciones.AdminEmail,
            Nombre = opciones.AdminNombre,
            Ciudad = opciones.AdminCiudad,
            EmailConfirmed = true,
        };

        var creado = await usuarios.CreateAsync(usuario, opciones.AdminPassword);
        if (!creado.Succeeded)
        {
            SeedFallo(logger, Codigos(creado));
            return;
        }

        var asignado = await usuarios.AddToRoleAsync(usuario, RolesApp.AdminPlataforma);
        if (!asignado.Succeeded)
        {
            SeedFallo(logger, Codigos(asignado));
            return;
        }

        SeedCreado(logger);
        ct.ThrowIfCancellationRequested();
    }

    private static string Codigos(IdentityResult resultado)
        => string.Join(", ", resultado.Errors.Select(e => e.Code));

    [LoggerMessage(EventId = 1110, Level = LogLevel.Warning,
        Message = "Seed de admin omitido: configuración SeedSettings incompleta.")]
    private static partial void SeedOmitido(ILogger logger);

    [LoggerMessage(EventId = 1111, Level = LogLevel.Debug,
        Message = "Seed de admin omitido: el usuario ya existe.")]
    private static partial void SeedYaExistente(ILogger logger);

    [LoggerMessage(EventId = 1112, Level = LogLevel.Error,
        Message = "Falló el seed de admin. Códigos Identity: {Codigos}. Verificar que la contraseña cumple la política (≥8, mayúscula, minúscula, dígito y símbolo).")]
    private static partial void SeedFallo(ILogger logger, string codigos);

    [LoggerMessage(EventId = 1113, Level = LogLevel.Information,
        Message = "Seed de admin creado con rol AdminPlataforma.")]
    private static partial void SeedCreado(ILogger logger);
}
```

(Verificar el nombre exacto de las props de `ApplicationUser` — `Nombre` y `Ciudad` según
`BootstrapUsuariosPrueba.cs:78-84`. Si difieren, ajustar.)

- [ ] **Step 4: Cablear en `Program.cs`** — justo después de `await app.Services.SembrarRolesAsync();` (línea 287) y antes del bloque bootstrap:

```csharp
    using (var scopeSeed = app.Services.CreateScope())
    {
        var opcionesSeed = app.Configuration
            .GetSection(OpcionesSeedAdmin.Seccion)
            .Get<OpcionesSeedAdmin>() ?? new OpcionesSeedAdmin();
        var seedAdmin = new SeedAdminPlataforma(
            scopeSeed.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>(),
            scopeSeed.ServiceProvider.GetRequiredService<ILogger<SeedAdminPlataforma>>());
        await seedAdmin.EjecutarAsync(opcionesSeed);
    }
```

- [ ] **Step 5: Ejecutar y ver pasar**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~SeedAdminPlataformaTests"`
Expected: PASS (4 tests).

- [ ] **Step 6: Build + formato + suite de integración de arranque**

Run: `cd CaseritoApp && dotnet build CaseritoApp.sln --configuration Release && dotnet format CaseritoApp.sln --verify-no-changes && dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~HealthEndpointTests|FullyQualifiedName~BootstrapUsuariosPruebaTests"`
Expected: limpio y PASS (Docker corriendo para Testcontainers).

- [ ] **Step 7: Commit** (pedir confirmación)

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/SeedAdminPlataforma.cs CaseritoApp/src/Host/CaseritoApp.Host/Program.cs CaseritoApp/tests/CaseritoApp.UnitTests/Autorizacion/SeedAdminPlataformaTests.cs
git commit -m "feat(identity): seed de admin idempotente en producción (SeedSettings)"
```

---

### Task 4: Host sirve la SPA + ForwardedHeaders

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs` (middleware ~línea 337 y endpoints ~línea 368)
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/HealthEndpointTests.cs` (añadir un caso)

**Interfaces:**
- Consumes: nada de otras tareas.
- Produces: pipeline con `UseForwardedHeaders` primero, `UseStaticFiles`, y `MapFallbackToFile("index.html")` como último endpoint. Sin cambios de firma para otras tareas.

- [ ] **Step 1: Escribir el test que falla** — añadir a `HealthEndpointTests.cs` (o al archivo de integración más cercano, siguiendo su patrón de factory):

```csharp
    [Fact]
    public async Task Ruta_api_inexistente_devuelve_404_no_spa()
    {
        // En Testing no hay wwwroot: el fallback SPA no puede capturar /api/* y una ruta
        // inexistente sigue devolviendo 404 (regresión contra el nuevo fallback a index.html).
        var respuesta = await _client.GetAsync("/api/ruta-inexistente");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
```

(Ajustar el cliente/factory al patrón real del archivo; leer `HealthEndpointTests.cs` antes de escribir.)

- [ ] **Step 2: Ejecutar y ver fallar**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~Ruta_api_inexistente"`
Expected: compila y **puede pasar ya** (sin wwwroot el comportamiento es 404 de todos modos) — es un test de caracterización/regresión, no estrictamente rojo. Aceptable: su valor es congelar el comportamiento tras añadir el fallback. Si pasa en verde desde el inicio, continuar.

- [ ] **Step 3: Añadir ForwardedHeaders** — en `Program.cs`, inmediatamente antes de `app.UseAuthentication();` (línea 337):

```csharp
// nginx del host termina el TLS y reenvía por HTTP: sin ForwardedHeaders la app vería esquema
// http e IPs internas de Docker (enlaces de correo y logs de rate-limit saldrían mal). Se
// limpian KnownNetworks/KnownProxies porque el proxy llega por la red bridge de Docker, que no
// es loopback; la app solo es alcanzable vía nginx (puerto publicado en 127.0.0.1).
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});
```

(`KnownNetworks`/`KnownProxies`: evaluar en ejecución si hay que limpiarlos — con
`XForwardedProto` desde un proxy no-loopback puede ser necesario
`opciones.KnownNetworks.Clear(); opciones.KnownProxies.Clear();` en un objeto options nombrado.
Decisión del implementador tras probar; documentar la elección en el comentario.)

Añadir `using Microsoft.AspNetCore.HttpOverrides;`.

- [ ] **Step 4: Servir estáticos + fallback SPA** — después de `app.UseAuthorization();` (línea 339):

```csharp
// Estáticos de la SPA (wwwroot): en producción el pipeline de deploy copia web/dist aquí.
app.UseStaticFiles();
```

y como último endpoint, después de `app.MapHealthChecks("/health");` (línea 368):

```csharp
// Fallback SPA: cualquier ruta sin endpoint (navegación del router React) sirve index.html.
// Los endpoints /api, /hubs y /health tienen precedencia por orden de registro.
app.MapFallbackToFile("index.html");
```

- [ ] **Step 5: Ejecutar tests afectados**

Run: `cd CaseritoApp && dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~HealthEndpointTests|FullyQualifiedName~AuthFlowTests"`
Expected: PASS (Docker corriendo).

- [ ] **Step 6: Build + formato**

Run: `cd CaseritoApp && dotnet build CaseritoApp.sln --configuration Release && dotnet format CaseritoApp.sln --verify-no-changes`
Expected: limpio.

- [ ] **Step 7: Commit** (pedir confirmación)

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Program.cs CaseritoApp/tests/CaseritoApp.IntegrationTests/HealthEndpointTests.cs
git commit -m "feat(host): SPA estática con fallback y ForwardedHeaders para producción"
```

---

### Task 5: Archivos de despliegue (Dockerfile, compose, env, workflow) + limpieza

> Actualización 2026-08-03: la implementación literal de esta tarea quedó
> sustituida por `2026-08-03-endurecimiento-deploy.md`: releases por SHA,
> `.dockerignore` de denegación, actions fijadas y rollback automático. Los
> fragmentos siguientes se conservan únicamente como registro del plan inicial.

**Files:**
- Create: `Dockerfile.web` (raíz del repo)
- Modify: `docker-compose.yml` (raíz — reescritura a servicio único)
- Create: `.env.production.example` (raíz)
- Create: `.github/workflows/deploy.yml`
- Delete: `web/Dockerfile.prod`, `CaseritoApp/src/Host/CaseritoApp.Host/Dockerfile.prod`
- Modify (si hay referencias): `AGENTS.md`, `CLAUDE.md`, `CaseritoApp/AGENTS.md`, `CaseritoApp/CLAUDE.md` (buscar `Dockerfile.prod`)

**Interfaces:**
- Consumes: `App__UrlPublica` (Task 2), `SeedSettings__*` (Task 3), `DataProtection__RutaClaves` (Task 1).
- Produces: artefactos consumidos por el agenteVPS según `preguntasrespuestasCaseritoApp_AgenteLocal_AgenteVPS/11_respuestas_agente_local.md`.

- [ ] **Step 1: Buscar referencias a los Dockerfiles que se eliminan**

Run: `cd CaseritoApp && cd .. && grep -rn "Dockerfile.prod" --include="*.md" . | head -20`
Expected: lista de archivos a actualizar en Step 6.

- [ ] **Step 2: Crear `Dockerfile.web`** (raíz):

```dockerfile
# Dockerfile de producción de CaseritoApp (estándar VPS Trajano).
# Runtime-only: copia el payload publicado por CI (binarios .NET + wwwroot de la SPA).
# El contexto de build es /var/apps/caseritoapp/ en la VPS, con el payload en web/.
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

# curl para el healthcheck del compose (la imagen aspnet no lo trae).
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY web/ .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "CaseritoApp.Host.dll"]
```

- [ ] **Step 3: Reescribir `docker-compose.yml`** (raíz) — contenido completo:

```yaml
# Producción VPS Trajano. La imagen se construye en el VPS sobre el payload que llega por rsync
# (ver .github/workflows/deploy.yml). Desarrollo local: docker-compose.dev.yml.
services:
  caseritoapp:
    build:
      context: .
      dockerfile: Dockerfile.web
    image: caseritoapp:latest
    container_name: caseritoapp
    restart: unless-stopped
    env_file: .env
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_HTTP_PORTS: "8080"
      TZ: "America/La_Paz"
      # Ruta de migración controlada: aplica esquema pendiente + siembra roles (idempotente).
      Migraciones__EjecutarAlArranque: "true"
    ports:
      # Solo loopback: nginx del host hace proxy_pass a 127.0.0.1:8084.
      - "127.0.0.1:8084:8080"
    volumes:
      - /var/apps/caseritoapp/fotos-avisos:/data/fotos-avisos
      - /var/apps/caseritoapp/kyc-blobs:/data/kyc-blobs
      - /var/apps/caseritoapp/dataprotection-keys:/data/dataprotection-keys
    networks: [trajano-shared-network]
    mem_limit: 512m
    cpus: 1.0
    healthcheck:
      test: ["CMD-SHELL", "curl -fsS http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

networks:
  trajano-shared-network:
    external: true
```

- [ ] **Step 4: Crear `.env.production.example`** (raíz):

```bash
# Plantilla del .env de producción (el agente VPS crea el real en /var/apps/caseritoapp/.env,
# chmod 600). NUNCA commitear valores reales. Canal seguro para secretos.

# Obligatorias — fail-fast si faltan:
ConnectionStrings__DefaultConnection=Server=trajano-sqlserver,1433;Database=CaseritoAppDB;User Id=caseritoapp_app;Password=REEMPLAZAR;TrustServerCertificate=True;MultipleActiveResultSets=true
Jwt__Key=REEMPLAZAR_cadena_aleatoria_de_al_menos_32_bytes

# Seed del admin (idempotente). Password: ≥8, mayúscula, minúscula, dígito y símbolo.
SeedSettings__AdminEmail=REEMPLAZAR
SeedSettings__AdminPassword=REEMPLAZAR
SeedSettings__AdminNombre=Administrador
SeedSettings__AdminCiudad=REEMPLAZAR

# ARGOS (KYC facial), contenedor en la misma red Docker:
Argos__Url=http://argos:5000
Argos__ApiKey=

# Correo vía relay interno del VPS (sin auth ni TLS en el salto privado CaseritoApp -> Postfix):
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

# Rutas de volúmenes (bind mounts del host):
AlmacenFotos__RutaBase=/data/fotos-avisos
Kyc__RutaBase=/data/kyc-blobs
DataProtection__RutaClaves=/data/dataprotection-keys

# URL pública de la SPA (enlaces en correos):
App__UrlPublica=https://caserito.app
```

- [ ] **Step 5: Crear `.github/workflows/deploy.yml`**:

```yaml
name: Deploy

on:
  push:
    branches: [ master ]
  workflow_dispatch:

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: CaseritoApp/global.json

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: npm
          cache-dependency-path: web/package-lock.json

      - name: Publish API (Release)
        run: dotnet publish src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj -c Release -o ../../payload/web
        working-directory: CaseritoApp

      - name: Build SPA
        run: npm ci && npm run build
        working-directory: web

      - name: Copiar SPA al wwwroot del payload
        run: mkdir -p payload/web/wwwroot && cp -r web/dist/. payload/web/wwwroot/

      - name: Copiar archivos de despliegue al payload
        run: cp Dockerfile.web docker-compose.yml payload/

      - name: SSH agent
        uses: webfactory/ssh-agent@v0.9.0
        with:
          ssh-private-key: ${{ secrets.VPS_SSH_KEY }}

      - name: Known hosts
        run: |
          mkdir -p ~/.ssh
          ssh-keyscan -H ${{ secrets.VPS_HOST }} >> ~/.ssh/known_hosts

      - name: Rsync payload al VPS
        run: |
          rsync -avz --delete payload/web/ ${{ secrets.VPS_USER }}@${{ secrets.VPS_HOST }}:/var/apps/caseritoapp/web/
          rsync -avz payload/Dockerfile.web payload/docker-compose.yml ${{ secrets.VPS_USER }}@${{ secrets.VPS_HOST }}:/var/apps/caseritoapp/

      - name: Build y despliegue en el VPS
        run: |
          ssh ${{ secrets.VPS_USER }}@${{ secrets.VPS_HOST }} << 'ENDSSH'
            set -e
            cd /var/apps/caseritoapp
            docker build --no-cache -f Dockerfile.web -t caseritoapp:latest .
            docker compose up -d caseritoapp
          ENDSSH

      - name: Smoke check
        run: |
          ssh ${{ secrets.VPS_USER }}@${{ secrets.VPS_HOST }} "curl -fsS http://127.0.0.1:8084/health"
```

- [ ] **Step 6: Eliminar Dockerfiles obsoletos y actualizar referencias**

```bash
git rm web/Dockerfile.prod CaseritoApp/src/Host/CaseritoApp.Host/Dockerfile.prod
```

Actualizar los `.md` encontrados en Step 1 para referenciar `Dockerfile.web` (raíz) y el nuevo
flujo. Verificar también que `.gitignore` excluye `.env` (buscar línea `.env` en `.gitignore` raíz;
añadirla si falta, manteniendo `!.env.example` y `!.env.production.example`).

- [ ] **Step 7: Verificación local del payload (smoke)**

Run (Windows, Git Bash, desde la raíz del repo):

```bash
cd CaseritoApp && dotnet publish src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj -c Release -o ../payload/web && cd ..
cd web && npm ci && npm run build && cd ..
mkdir -p payload/web/wwwroot && cp -r web/dist/. payload/web/wwwroot/
cp Dockerfile.web docker-compose.yml payload/
```

Expected: `payload/web/CaseritoApp.Host.dll` y `payload/web/wwwroot/index.html` existen.
(Opcional si Docker local disponible: `cd payload && docker build -f Dockerfile.web -t caseritoapp:smoke .` — el `up` real requiere la red externa y el .env del VPS, no aplica en local.)
Limpiar después: `rm -rf payload/` y asegurar que `payload/` no queda commiteado (añadir a
`.gitignore` si no está).

- [ ] **Step 8: Verificación final completa**

Run: `cd CaseritoApp && dotnet test CaseritoApp.sln --configuration Release && dotnet format CaseritoApp.sln --verify-no-changes && cd ../web && npm run lint && npm run typecheck && npm run test`
Expected: todo verde.

- [ ] **Step 9: Commit** (pedir confirmación)

```bash
git add Dockerfile.web docker-compose.yml .env.production.example .github/workflows/deploy.yml .gitignore
git commit -m "feat(deploy): Dockerfile runtime-only, compose VPS, env example y workflow de despliegue"
```

---

## Notas de cierre (post-plan, fuera de las tareas)

### Estado de ejecución — 2026-07-30

Las Tasks 1 a 5 están implementadas y verificadas. Los checkboxes de commit permanecen
intencionalmente pendientes: no se creó ningún commit porque requieren autorización explícita.

- Backend: build y suites completas en verde; `dotnet format --verify-no-changes` en verde.
- Frontend: lint, typecheck, 134 tests y build en verde.
- Payload: existen `CaseritoApp.Host.dll` y `wwwroot/index.html`.
- Despliegue real, push y comunicación con el agente VPS no ejecutados.

- Actualizar el documento `preguntasrespuestasCaseritoApp_AgenteLocal_AgenteVPS/11_respuestas_agente_local.md`
  marcando como hecho lo commiteado (sección 8) cuando terminen las 5 tareas.
- NO enviar nada al agenteVPS ni hacer push: el usuario decidió terminar el desarrollo primero.
- Deploy real bloqueado por externos: secrets GitHub del humano, BD/login y .env del agenteVPS,
  respuestas STARTTLS/ARGOS.
