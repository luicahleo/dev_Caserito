# Endurecimiento pre-producción del KYC — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cerrar dos follow-ups PRE-PRODUCCIÓN del KYC (Bloque E): fail-fast del encryptor fuera de Dev/Testing y concurrencia optimista (rowversion) en el agregado `VerificacionKyc`, con conflicto mapeado a HTTP 409.

**Architecture:** Backend .NET 10, Clean Architecture, bounded context Identity. Componente 1 es un guardrail de composición de servicios (patrón fail-fast de `Jwt:Key`). Componente 2 añade un token de concurrencia rowversion en la raíz + "touch-root" en `SaveChanges` para que cambios en la hija `SolicitudKyc` bumpeen la versión de la raíz, y traduce el `DbUpdateConcurrencyException` a una excepción neutral (Infrastructure) que el Host mapea a 409 (mismo patrón que `ValidationException`).

**Tech Stack:** .NET 10, EF Core 10 (SQL Server), ASP.NET Core Identity, MediatR, xUnit, Testcontainers.MsSql.

## Global Constraints

- `Nullable` enable, **warnings-as-errors**, analizadores .NET + Roslynator + Sonar. Nada compila si viola las reglas (`CaseritoApp/.editorconfig`, `Directory.Build.props`).
- Namespaces **file-scoped**; `using` fuera del namespace, System primero.
- Versiones de paquete **solo** en `CaseritoApp/Directory.Packages.props` (CPM). **Nunca** `Version=` en un `.csproj`.
- **Anti-PII en logs (no negociable):** jamás loguear CI/selfie, claves de blob, tokens. Ningún log nuevo de este bloque debe incluir PII.
- Comandos desde `CaseritoApp/`: build `dotnet build CaseritoApp.sln`; test `dotnet test CaseritoApp.sln`.
- Rama `feat/kyc-endurecimiento-preprod` (creada por el orquestador antes de ejecutar). El implementer **solo** hace `git add <archivos>` + `git commit`; nada de `reset`/`rebase`/`checkout`/`amend`.
- Spec de referencia: `docs/superpowers/specs/2026-07-17-kyc-endurecimiento-preprod-design.md`.

---

## Task 1: Fail-fast del encryptor

Fuera de Development/Testing, componer los servicios de Identity debe **abortar** si no hay encryptor real (hoy solo existe `PassthroughEncryptor`), para no escribir PII biométrica en claro. Se decide en la composición (antes de `builder.Build()`), criterio de entorno idéntico al de la clave efímera de JWT.

**Files:**
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` (firma de `AgregarIdentity` + registro condicional de `IEncryptor`)
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs:29` (call site)
- Modify: `CaseritoApp/tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj` (paquetes para componer servicios en el test)
- Modify: `CaseritoApp/Directory.Packages.props` (versiones CPM de esos paquetes, si faltan)
- Create: `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/FailFastEncryptorTests.cs`

**Interfaces:**
- Consumes: `IEncryptor`, `PassthroughEncryptor` (namespace `CaseritoApp.BuildingBlocks.Infrastructure.Security`); `IHostEnvironment` (`Microsoft.Extensions.Hosting`).
- Produces: nueva firma `AgregarIdentity(this IServiceCollection servicios, IConfiguration config, IHostEnvironment entorno)` que **lanza `InvalidOperationException`** cuando `!(entorno.IsDevelopment() || entorno.IsEnvironment("Testing"))`.

- [ ] **Step 1: Asegurar paquetes del test project**

En `CaseritoApp/tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj`, añadir al `ItemGroup` de `PackageReference` (sin `Version=`, CPM):

```xml
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.Extensions.Configuration" />
```

En `CaseritoApp/Directory.Packages.props`, si no existen, añadir al `ItemGroup` de "Orquestación, validación y persistencia":

```xml
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="10.0.0" />
```

- [ ] **Step 2: Write the failing test**

Crear `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/FailFastEncryptorTests.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using CaseritoApp.Identity.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class FailFastEncryptorTests
{
    [Fact]
    public void Fuera_de_dev_o_testing_sin_encryptor_real_falla_al_componer()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AgregarIdentity(config, new EntornoStub("Production")));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void En_dev_o_testing_resuelve_passthrough_encryptor(string entorno)
    {
        var config = new ConfigurationBuilder().Build();

        var provider = new ServiceCollection()
            .AddLogging()
            .AgregarIdentity(config, new EntornoStub(entorno))
            .BuildServiceProvider();

        Assert.IsType<PassthroughEncryptor>(provider.GetRequiredService<IEncryptor>());
    }

    private sealed class EntornoStub(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet build CaseritoApp.sln`
Expected: FALLA de compilación — `AgregarIdentity` aún tiene la firma de 2 parámetros; el test invoca la de 3.

- [ ] **Step 4: Cambiar la firma y el registro condicional**

En `DependencyInjection.cs`, cambiar la firma de `AgregarIdentity` (añadir `IHostEnvironment entorno`):

```csharp
    public static IServiceCollection AgregarIdentity(
        this IServiceCollection servicios, IConfiguration config, IHostEnvironment entorno)
```

Reemplazar el registro incondicional del encryptor (hoy la línea `servicios.AddSingleton<IEncryptor, PassthroughEncryptor>();`) por:

```csharp
        // Fail-fast de PII: fuera de Development/Testing no existe un encryptor real cableado
        // (envelope/KMS diferido), así que se aborta la composición en vez de escribir CI/selfie en
        // claro. Mismo criterio de entorno que la clave efímera de JWT. Sin escape hatch: prod no
        // arranca hasta cablear cifrado real.
        if (!(entorno.IsDevelopment() || entorno.IsEnvironment("Testing")))
        {
            throw new InvalidOperationException(
                "IEncryptor está configurado como PassthroughEncryptor fuera de Development/Testing: " +
                "se requiere un encryptor real (envelope/KMS) antes de producción.");
        }

        servicios.AddSingleton<IEncryptor, PassthroughEncryptor>();
```

(`using Microsoft.Extensions.Hosting;` ya está presente en el archivo.)

- [ ] **Step 5: Ajustar el call site en Program.cs**

En `Program.cs` (hoy línea 29), pasar el entorno:

```csharp
builder.Services.AgregarIdentity(builder.Configuration, builder.Environment);
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~FailFastEncryptorTests"`
Expected: PASS (3 casos: 1 Fact + 2 del Theory).

- [ ] **Step 7: Build + suite completa**

Run: `dotnet build CaseritoApp.sln` y `dotnet test CaseritoApp.sln`
Expected: build sin warnings (warnings-as-errors) y toda la suite en verde (los tests de integración con `Testing` siguen resolviendo `PassthroughEncryptor`).

- [ ] **Step 8: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs CaseritoApp/src/Host/CaseritoApp.Host/Program.cs CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/FailFastEncryptorTests.cs CaseritoApp/tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj CaseritoApp/Directory.Packages.props
git commit -m "feat(kyc): fail-fast del encryptor fuera de Development/Testing"
```

---

## Task 2: Concurrencia optimista (rowversion en la raíz) + mapeo a 409

Añade un token de concurrencia rowversion en `VerificacionKyc`, propaga los cambios de la hija a la raíz vía "touch-root" en `SaveChangesAsync`, traduce el `DbUpdateConcurrencyException` a una excepción neutral en Infrastructure y la mapea a **409** en los endpoints KYC. Cubre doble aprobación y dos `Pendiente` concurrentes sobre un agregado existente.

**Files:**
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Abstractions/ConflictoConcurrenciaException.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/ErroresKyc.cs` (código nuevo)
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConfiguracionKyc.cs` (rowversion)
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/IdentityDbContext.cs` (override `SaveChangesAsync` + touch-root)
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/UnitOfWorkIdentity.cs` (catch → excepción neutral)
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs` (catch → 409)
- Create (generado): migración `KycConcurrencia` en `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Migrations/`
- Create: `CaseritoApp/tests/CaseritoApp.IntegrationTests/KycConcurrenciaTests.cs`

**Interfaces:**
- Consumes: `IdentityDbContext`, `VerificacionKyc` (`Crear`, `EnviarSolicitud`, `Aprobar`, `Rechazar`), `SolicitudKyc`, `TipoDocumento.CedulaIdentidad`, `EstadoKyc`, `IUnitOfWork`, `ErroresKyc`.
- Produces:
  - `ConflictoConcurrenciaException(string message, Exception innerException)` en `CaseritoApp.BuildingBlocks.Application.Abstractions`.
  - `ErroresKyc.ConflictoConcurrencia = "Kyc.ConflictoConcurrencia"`.
  - Propiedad shadow `"Version"` (`int`, `IsConcurrencyToken()`) en `VerificacionKyc`.
  - `IdentityDbContext.SaveChangesAsync(CancellationToken)` override que **incrementa** el `Version` de la raíz `VerificacionKyc` cuando cambia cualquier `SolicitudKyc`.

- [ ] **Step 1: Excepción neutral (BuildingBlocks.Application)**

Crear `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Abstractions/ConflictoConcurrenciaException.cs`:

```csharp
namespace CaseritoApp.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Señala que una operación perdió una carrera de concurrencia optimista al persistir (otra
/// transacción modificó el mismo agregado). Es neutral respecto a la persistencia (no expone tipos
/// de EF Core); la capa de presentación la mapea a HTTP 409.
/// </summary>
public sealed class ConflictoConcurrenciaException : Exception
{
    public ConflictoConcurrenciaException()
    {
    }

    public ConflictoConcurrenciaException(string message) : base(message)
    {
    }

    public ConflictoConcurrenciaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

(Los tres constructores estándar satisfacen CA1032; se usa el de `(string, Exception)`.)

- [ ] **Step 2: Código de error de dominio**

En `ErroresKyc.cs`, añadir la constante:

```csharp
    public const string ConflictoConcurrencia = "Kyc.ConflictoConcurrencia";
```

- [ ] **Step 3: Rowversion en la raíz**

En `ConfiguracionKyc.cs`, dentro del bloque `builder.Entity<VerificacionKyc>(e => { ... })`, añadir tras la configuración de la relación:

```csharp
            // Token de concurrencia optimista de la raíz (entero incremental, no rowversion: la raíz
            // no tiene columnas escalares propias, así que un rowversion no generaría UPDATE al tocar
            // la raíz). El override de SaveChangesAsync lo incrementa cuando cambia una hija, para que
            // dos operaciones concurrentes sobre el mismo agregado colisionen (doble aprobación / dos
            // Pendiente).
            e.Property<int>("Version").IsConcurrencyToken();
```

- [ ] **Step 4: Override de SaveChangesAsync (touch-root)**

En `IdentityDbContext.cs`, añadir el `using Microsoft.EntityFrameworkCore.ChangeTracking;` no es necesario; sí se usan `EntityState` y `ChangeTracker` (ambos de `Microsoft.EntityFrameworkCore`, ya importado). Añadir tras `OnModelCreating`:

```csharp
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        MarcarRaicesKycModificadas();
        return base.SaveChangesAsync(cancellationToken);
    }

    // EF no bumpea la versión de la raíz cuando solo cambia una hija: la raíz no entra en el UPDATE
    // y su token no se chequea. Se incrementa el Version de la raíz para forzar ese UPDATE con el
    // chequeo de concurrencia, de modo que dos transacciones concurrentes sobre el mismo agregado
    // colisionen. Incrementar la propiedad la marca como modificada (y a la raíz como Modified).
    private void MarcarRaicesKycModificadas()
    {
        foreach (var hija in ChangeTracker.Entries<SolicitudKyc>())
        {
            if (hija.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var verificacionId = hija.Property<Guid>("VerificacionKycId").CurrentValue;
            var raiz = ChangeTracker.Entries<VerificacionKyc>()
                .FirstOrDefault(v => v.Entity.Id == verificacionId);

            if (raiz is { State: EntityState.Unchanged })
            {
                var version = raiz.Property<int>("Version");
                version.CurrentValue += 1;
            }
        }
    }
```

- [ ] **Step 5: Generar la migración**

Desde `CaseritoApp/`:

```bash
dotnet ef migrations add KycConcurrencia --project src/Identity/CaseritoApp.Identity.Infrastructure --startup-project src/Host/CaseritoApp.Host --output-dir Migrations
```

Si EF falla en tiempo de diseño por falta de cadena de conexión, exportar una dummy y reintentar:
`export ConnectionStrings__DefaultConnection="Server=localhost;Database=x;User Id=sa;Password=x;TrustServerCertificate=true"` (bash) — no se conecta, solo satisface el diseño.
Verificar que la migración añade la columna `Version` (`int NOT NULL DEFAULT 0`, marcada como concurrency token) a `VerificacionesKyc`.

- [ ] **Step 6: Traducir el conflicto en UnitOfWorkIdentity**

En `UnitOfWorkIdentity.cs`, reemplazar el cuerpo de `GuardarCambiosAsync`:

```csharp
    public async Task<int> GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            return await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoConcurrenciaException(
                "Conflicto de concurrencia al persistir los cambios.", ex);
        }
    }
```

Añadir usings: `using CaseritoApp.BuildingBlocks.Application.Abstractions;` (ya presente, para `IUnitOfWork`) y `using Microsoft.EntityFrameworkCore;`.

- [ ] **Step 7: Mapear a 409 en los endpoints KYC**

En `KycEndpoints.cs`, añadir `using CaseritoApp.BuildingBlocks.Application.Abstractions;`. Añadir un helper junto a `DesdeResult`:

```csharp
    private static IResult Conflicto409() =>
        Results.Problem(
            title: ErroresKyc.ConflictoConcurrencia,
            detail: "La operación entró en conflicto con otra concurrente. Reintente.",
            statusCode: StatusCodes.Status409Conflict);
```

En `AprobarAsync`, envolver el `Send` en try/catch:

```csharp
        try
        {
            var resultado = await sender.Send(new AprobarSolicitudKycCommand(solicitudId, adminId), ct);
            return DesdeResult(resultado);
        }
        catch (ConflictoConcurrenciaException)
        {
            return Conflicto409();
        }
```

En `EnviarAsync` y `RechazarAsync`, que ya tienen `catch (ValidationException ex)`, añadir un segundo catch **antes** del de validación (o después; el orden no importa porque son tipos distintos):

```csharp
        catch (ConflictoConcurrenciaException)
        {
            return Conflicto409();
        }
```

- [ ] **Step 8: Write the failing tests**

Crear `CaseritoApp/tests/CaseritoApp.IntegrationTests/KycConcurrenciaTests.cs`:

```csharp
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Concurrencia optimista del KYC: rowversion en la raíz + mapeo a 409.</summary>
public sealed class KycConcurrenciaTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string Email(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

    // Mecanismo: dos aprobaciones concurrentes del mismo agregado → la segunda pierde la carrera.
    [Fact]
    public async Task Dos_aprobaciones_concurrentes_del_mismo_agregado_la_segunda_lanza_concurrencia()
    {
        var usuarioId = Guid.NewGuid();
        Guid solicitudId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var verificacion = VerificacionKyc.Crear(usuarioId);
            var envio = verificacion.EnviarSolicitud("doc-0", "selfie-0", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
            solicitudId = envio.Valor.Id;
            db.VerificacionesKyc.Add(verificacion);
            await db.SaveChangesAsync();
        }

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var verA = await dbA.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);
        var verB = await dbB.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);

        verA.Aprobar(solicitudId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        verB.Aprobar(solicitudId, Guid.NewGuid(), DateTimeOffset.UtcNow);

        await dbA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    // Mecanismo: dos solicitudes nuevas concurrentes sobre un agregado existente (tras rechazo) →
    // la segunda pierde. Valida que "añadir una hija" también bumpea la raíz (touch-root).
    [Fact]
    public async Task Dos_solicitudes_concurrentes_sobre_agregado_existente_la_segunda_lanza_concurrencia()
    {
        var usuarioId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var verificacion = VerificacionKyc.Crear(usuarioId);
            var envio = verificacion.EnviarSolicitud("doc-0", "selfie-0", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
            verificacion.Rechazar(envio.Valor.Id, Guid.NewGuid(), "ilegible", DateTimeOffset.UtcNow);
            db.VerificacionesKyc.Add(verificacion);
            await db.SaveChangesAsync();
        }

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var verA = await dbA.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);
        var verB = await dbB.VerificacionesKyc.Include(v => v.Solicitudes).FirstAsync(v => v.Id == usuarioId);

        verA.EnviarSolicitud("doc-a", "selfie-a", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
        verB.EnviarSolicitud("doc-b", "selfie-b", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);

        await dbA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    // Surfacing end-to-end: dos aprobaciones concurrentes por HTTP → exactamente una 204 y una 409.
    [Fact]
    public async Task Dos_aprobaciones_concurrentes_por_http_una_204_y_una_409()
    {
        using var cliente = factory.CreateClient();
        var email = Email("kyc-conc");
        var tokenUsuario = await RegistrarYLoguearAsync(cliente, email, rolExtra: null);
        var tokenAdmin = await RegistrarYLoguearAsync(cliente, Email("kyc-conc-admin"), RolesApp.AdminKyc);

        using var subir = Autorizada(HttpMethod.Post, "/api/kyc/", tokenUsuario);
        subir.Content = Formulario();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subir)).StatusCode);

        using var listar = Autorizada(HttpMethod.Get, "/api/admin/kyc/?estado=Pendiente&tamano=100", tokenAdmin);
        var pagina = await (await cliente.SendAsync(listar)).Content.ReadFromJsonAsync<PaginaKycResponse>();

        Guid usuarioId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            usuarioId = (await userManager.FindByEmailAsync(email))!.Id;
        }
        var solicitudId = pagina!.Items.Single(s => s.UsuarioId == usuarioId).SolicitudId;

        var url = $"/api/admin/kyc/{solicitudId}/aprobar";
        var respuestas = await Task.WhenAll(
            cliente.SendAsync(Autorizada(HttpMethod.Post, url, tokenAdmin)),
            cliente.SendAsync(Autorizada(HttpMethod.Post, url, tokenAdmin)));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    private async Task<string> RegistrarYLoguearAsync(HttpClient cliente, string email, string? rolExtra)
    {
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register", new RegistroRequest(email, "Password123!", "Usuario", "La Paz"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        if (rolExtra is not null)
        {
            using var scope = factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(usuario!, rolExtra);
        }

        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
    }

    private static MultipartFormDataContent Formulario()
    {
        var contenido = new MultipartFormDataContent();
        var doc = new ByteArrayContent(_png);
        doc.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        contenido.Add(doc, "documento", "ci.png");
        var selfie = new ByteArrayContent(_png);
        selfie.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        contenido.Add(selfie, "selfie", "selfie.png");
        return contenido;
    }

    private static HttpRequestMessage Autorizada(HttpMethod metodo, string url, string token)
    {
        var solicitud = new HttpRequestMessage(metodo, url);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return solicitud;
    }
}

// Copias file-scoped de los DTO de deserialización (los de KycFlujoTests son `file`, no compartibles).
sealed file record SolicitudKycResponse(
    Guid SolicitudId, Guid UsuarioId, string Estado, string TipoDocumento, DateTimeOffset EnviadaEn, DateTimeOffset? ResueltaEn);

sealed file record PaginaKycResponse(SolicitudKycResponse[] Items, int Pagina, int Tamano, int Total);
```

- [ ] **Step 9: Run tests to verify they fail correctly**

Requiere Docker (Testcontainers). Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~KycConcurrenciaTests"`
Expected antes de Steps 3-4 (si se ejecutara sin touch-root): los tests de mecanismo NO lanzarían y fallarían. Con Steps 3-7 ya aplicados en orden, corre este paso como verificación de que **pasan**. Si se quiere ver el rojo, comentar temporalmente el cuerpo de `MarcarRaicesKycModificadas` y observar que los dos tests de mecanismo fallan (dos `SaveChanges` exitosos); revertir el comentario después.

- [ ] **Step 10: Run tests to verify they pass**

Run: `dotnet test CaseritoApp.sln --filter "FullyQualifiedName~KycConcurrenciaTests"`
Expected: PASS los 3 tests.

- [ ] **Step 11: Build + suite completa**

Run: `dotnet build CaseritoApp.sln` y `dotnet test CaseritoApp.sln`
Expected: build sin warnings; toda la suite en verde (incluidos los KycFlujoTests existentes, que siguen funcionando con la nueva columna y el touch-root).

- [ ] **Step 12: Commit**

```bash
git add CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Abstractions/ConflictoConcurrenciaException.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/ErroresKyc.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConfiguracionKyc.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/IdentityDbContext.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/UnitOfWorkIdentity.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Migrations CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs CaseritoApp/tests/CaseritoApp.IntegrationTests/KycConcurrenciaTests.cs
git commit -m "feat(kyc): concurrencia optimista (rowversion en la raíz) con conflicto mapeado a 409"
```

---

## Notas de cierre (post-tasks)

- **Residual conocido (no se resuelve aquí):** el publish de `UserVerified` ocurre en el handler antes del commit; el perdedor de una doble aprobación loguea una línea de evento y luego falla el commit (→409). Como el publicador es solo-log y el outbox está diferido, es cosmético; el fix (publish-after-commit) pertenece al bloque de outbox.
- **Caveat de PK:** dos subidas concurrentes cuando el agregado aún no existe colisionan por PK (`DbUpdateException`, no `DbUpdateConcurrencyException`) y suben como 500; el resultado sigue siendo seguro (una sola `Pendiente`). El 409 limpio cubre el caso de agregado existente y la doble aprobación.
- Actualizar la memoria `caserito-roadmap.md` al mergear (Bloque G): estos dos follow-ups PRE-PRODUCCIÓN quedan cerrados; siguen diferidos encryptor real/KMS y purga por retención.
