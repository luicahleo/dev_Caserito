# KYC manual + PII — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Añadir al bounded context Identity el backend de verificación de identidad (KYC) manual: subida de CI+selfie, almacenamiento cifrado y auditado de esa PII, máquina de estados con historial de solicitudes, casos de uso de admin (aprobar/rechazar), publicación de `UserVerified` y exposición del badge "verificado".

**Architecture:** Clean Architecture / CQRS-lite (MediatR + `Result` + FluentValidation) dentro del contexto Identity. Dominio puro con agregado `VerificacionKyc` (historial de `SolicitudKyc`); puertos en Application (`IAlmacenBlobsKyc`, `IRepositorioVerificacionKyc`, `IConsultaVerificacionKyc`, `IPublicadorEventosIntegracion`); adaptadores en Infrastructure (EF Core sobre `IdentityDbContext`, blobs cifrados en disco, publicador/auditor que loguean sin PII). Los bytes de las imágenes nunca entran al dominio: solo referencias opacas.

**Tech Stack:** .NET 10, EF Core (SQL Server), ASP.NET Core Minimal API, MediatR, FluentValidation, xUnit, Testcontainers.MsSql.

## Global Constraints

- **Nullable enable + warnings-as-errors + analizadores (Roslynator/Sonar)**: nada compila si viola las reglas de `.editorconfig`/`Directory.Build.props`.
- **Namespaces file-scoped**; `using` fuera del namespace, System primero; `PascalCase`/`camelCase`/`_camelCase`; `I` en interfaces.
- **Versiones de paquete solo en `Directory.Packages.props` (CPM)**; nunca `Version=` en un `.csproj`.
- **Anti-PII (NO negociable)**: jamás loguear bytes/contenido de CI/selfie, claves de blob, ni email en logs de auditoría. Solo ids/estado/acción. Usar `PiiRedaction` para cualquier campo sensible.
- **Capas** (verificadas por `CaseritoApp.ArchitectureTests`): `Domain` no depende de Application/Infrastructure; ningún contexto depende de otro contexto. `CaseritoApp.BuildingBlocks.Contracts` no es un contexto → puede referenciarse desde Application.
- **Comandos** (desde `CaseritoApp/`): build `dotnet build CaseritoApp.sln`; test `dotnet test CaseritoApp.sln`; formato `dotnet format CaseritoApp.sln`.
- **Idioma**: identificadores, mensajes y comentarios en español.
- **Spec de referencia**: `docs/superpowers/specs/2026-07-15-kyc-manual-pii-design.md`.

---

## Mapa de archivos

**Crear:**
- `src/Identity/CaseritoApp.Identity.Domain/Kyc/EstadoKyc.cs`
- `src/Identity/CaseritoApp.Identity.Domain/Kyc/TipoDocumento.cs`
- `src/Identity/CaseritoApp.Identity.Domain/Kyc/SolicitudKyc.cs`
- `src/Identity/CaseritoApp.Identity.Domain/Kyc/VerificacionKyc.cs`
- `src/Identity/CaseritoApp.Identity.Domain/Kyc/ErroresKyc.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/IAlmacenBlobsKyc.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/IRepositorioVerificacionKyc.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/IConsultaVerificacionKyc.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/DtosKyc.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/ValidacionImagenKyc.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/EnviarSolicitudKycCommand.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/ObtenerEstadoKycQuery.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/ListarSolicitudesKycQuery.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/ObtenerBlobKycQuery.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/AprobarSolicitudKycCommand.cs`
- `src/Identity/CaseritoApp.Identity.Application/Kyc/RechazarSolicitudKycCommand.cs`
- `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Abstractions/IPublicadorEventosIntegracion.cs`
- `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConfiguracionKyc.cs`
- `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/RepositorioVerificacionKycEfCore.cs`
- `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConsultaVerificacionKycEfCore.cs`
- `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesAlmacenKyc.cs`
- `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/AlmacenBlobsKycDisco.cs`
- `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/PublicadorEventosIntegracionLog.cs`
- `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/AuditorAccesoPiiLog.cs`
- `src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs`
- Tests: `tests/CaseritoApp.UnitTests/Kyc/VerificacionKycTests.cs`, `.../Kyc/ValidacionImagenKycTests.cs`, `.../Kyc/AprobarSolicitudKycCommandHandlerTests.cs`, `.../Kyc/EnviarSolicitudKycCommandHandlerTests.cs`
- Test integración: `tests/CaseritoApp.IntegrationTests/KycFlujoTests.cs`

**Modificar:**
- `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/Security/IEncryptor.cs` (path byte[])
- `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/Security/PassthroughEncryptor.cs`
- `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/CaseritoApp.BuildingBlocks.Application.csproj` (ref a Contracts)
- `src/Identity/CaseritoApp.Identity.Infrastructure/IdentityDbContext.cs` (DbSet + OnModelCreating)
- `src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` (registros KYC)
- `src/Identity/CaseritoApp.Identity.Domain/Autorizacion/ClaimsApp.cs` (claim `verificado`)
- `src/Identity/CaseritoApp.Identity.Infrastructure/Auth/GeneradorTokensAcceso.cs` (claim)
- `src/Identity/CaseritoApp.Identity.Application/Perfil/IRepositorioPerfil.cs` (`PerfilDto.Verificado`)
- `src/Identity/CaseritoApp.Identity.Infrastructure/Perfil/RepositorioPerfilUserManager.cs`
- `src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs` (login/refresh calculan verificado)
- `src/Host/CaseritoApp.Host/Program.cs` (`MapKycEndpoints`)
- `tests/CaseritoApp.UnitTests/Auth/GeneradorTokensAccesoTests.cs` (nueva firma)
- `tests/CaseritoApp.IntegrationTests/PerfilTests.cs` (assert `verificado`)

---

## Task 1: Path binario en `IEncryptor`

**Files:**
- Modify: `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/Security/IEncryptor.cs`
- Modify: `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/Security/PassthroughEncryptor.cs`
- Test: `tests/CaseritoApp.ArchitectureTests/BuildingBlocks/EncryptorTests.cs`

**Interfaces:**
- Produces: `byte[] IEncryptor.Cifrar(byte[] datos)` y `byte[] IEncryptor.Descifrar(byte[] datos)` (además de las sobrecargas `string` existentes).

- [ ] **Step 1: Escribir el test que falla**

Añadir al final de la clase `EncryptorTests` (antes del `}` de cierre):

```csharp
    [Fact]
#pragma warning disable CA1859 // Se prueba a través de la abstracción IEncryptor, no de la implementación concreta.
    public void Passthrough_hace_roundtrip_de_bytes()
    {
        IEncryptor encryptor = new PassthroughEncryptor();
        var original = new byte[] { 0x01, 0xFF, 0x00, 0x42 };
        Assert.Equal(original, encryptor.Descifrar(encryptor.Cifrar(original)));
    }
#pragma warning restore CA1859
```

- [ ] **Step 2: Ejecutar y verificar que falla (no compila)**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~EncryptorTests`
Expected: FAIL de compilación (`IEncryptor` no define `Cifrar(byte[])`).

- [ ] **Step 3: Añadir el path binario a la interfaz**

En `IEncryptor.cs`, dentro de la interfaz, añadir:

```csharp
    public byte[] Cifrar(byte[] datos);
    public byte[] Descifrar(byte[] datos);
```

- [ ] **Step 4: Implementar en `PassthroughEncryptor`**

En `PassthroughEncryptor.cs`, añadir dentro de la clase:

```csharp
    public byte[] Cifrar(byte[] datos) => datos;
    public byte[] Descifrar(byte[] datos) => datos;
```

- [ ] **Step 5: Ejecutar y verificar que pasa**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~EncryptorTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/Security tests/CaseritoApp.ArchitectureTests/BuildingBlocks/EncryptorTests.cs
git commit -m "feat(security): path binario (byte[]) en IEncryptor para blobs KYC"
```

---

## Task 2: Dominio KYC (agregado + máquina de estados)

**Files:**
- Create: `src/Identity/CaseritoApp.Identity.Domain/Kyc/EstadoKyc.cs`, `TipoDocumento.cs`, `ErroresKyc.cs`, `SolicitudKyc.cs`, `VerificacionKyc.cs`
- Test: `tests/CaseritoApp.UnitTests/Kyc/VerificacionKycTests.cs`

**Interfaces:**
- Consumes: `AggregateRoot`, `Entity`, `Result`, `Result<T>`, `Error` (BuildingBlocks.Domain).
- Produces:
  - `enum EstadoKyc { Pendiente, Aprobada, Rechazada }`
  - `enum TipoDocumento { CedulaIdentidad }`
  - `VerificacionKyc.Crear(Guid usuarioId) : VerificacionKyc`; `Guid UsuarioId { get; }`; `IReadOnlyCollection<SolicitudKyc> Solicitudes`; `SolicitudKyc? SolicitudActual`; `bool EstaVerificado`;
    `Result<SolicitudKyc> EnviarSolicitud(string referenciaDocumento, string referenciaSelfie, TipoDocumento tipo, DateTimeOffset cuando)`;
    `Result Aprobar(Guid solicitudId, Guid revisorId, DateTimeOffset cuando)`;
    `Result Rechazar(Guid solicitudId, Guid revisorId, string motivo, DateTimeOffset cuando)`
  - `SolicitudKyc : Entity` con `EstadoKyc Estado`, `string ReferenciaDocumento`, `string ReferenciaSelfie`, `TipoDocumento TipoDocumento`, `string? MotivoRechazo`, `DateTimeOffset EnviadaEn`, `DateTimeOffset? ResueltaEn`, `Guid? ResueltaPor`
  - `ErroresKyc`: códigos `Kyc.SolicitudPendienteExiste`, `Kyc.YaVerificado`, `Kyc.SolicitudNoEncontrada`, `Kyc.TransicionInvalida`

- [ ] **Step 1: Escribir los tests que fallan**

Crear `tests/CaseritoApp.UnitTests/Kyc/VerificacionKycTests.cs`:

```csharp
using CaseritoApp.Identity.Domain.Kyc;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class VerificacionKycTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private static VerificacionKyc ConSolicitudPendiente(out Guid solicitudId)
    {
        var v = VerificacionKyc.Crear(Guid.NewGuid());
        var r = v.EnviarSolicitud("doc", "selfie", TipoDocumento.CedulaIdentidad, T0);
        solicitudId = r.Valor.Id;
        return v;
    }

    [Fact]
    public void Enviar_primera_solicitud_queda_pendiente_y_es_la_actual()
    {
        var v = VerificacionKyc.Crear(Guid.NewGuid());

        var r = v.EnviarSolicitud("doc", "selfie", TipoDocumento.CedulaIdentidad, T0);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoKyc.Pendiente, v.SolicitudActual!.Estado);
        Assert.False(v.EstaVerificado);
    }

    [Fact]
    public void Enviar_con_solicitud_pendiente_falla()
    {
        var v = ConSolicitudPendiente(out _);

        var r = v.EnviarSolicitud("doc2", "selfie2", TipoDocumento.CedulaIdentidad, T0);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.SolicitudPendienteExiste, r.Error.Code);
    }

    [Fact]
    public void Aprobar_pendiente_marca_verificado()
    {
        var v = ConSolicitudPendiente(out var id);
        var revisor = Guid.NewGuid();

        var r = v.Aprobar(id, revisor, T0);

        Assert.True(r.EsExito);
        Assert.Equal(EstadoKyc.Aprobada, v.SolicitudActual!.Estado);
        Assert.Equal(revisor, v.SolicitudActual.ResueltaPor);
        Assert.True(v.EstaVerificado);
    }

    [Fact]
    public void Enviar_tras_aprobado_falla_con_YaVerificado()
    {
        var v = ConSolicitudPendiente(out var id);
        v.Aprobar(id, Guid.NewGuid(), T0);

        var r = v.EnviarSolicitud("doc3", "selfie3", TipoDocumento.CedulaIdentidad, T0.AddDays(1));

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.YaVerificado, r.Error.Code);
    }

    [Fact]
    public void Rechazar_pendiente_permite_reenviar()
    {
        var v = ConSolicitudPendiente(out var id);
        v.Rechazar(id, Guid.NewGuid(), "Foto borrosa", T0);

        Assert.Equal(EstadoKyc.Rechazada, v.SolicitudActual!.Estado);
        Assert.Equal("Foto borrosa", v.SolicitudActual.MotivoRechazo);

        var r = v.EnviarSolicitud("doc4", "selfie4", TipoDocumento.CedulaIdentidad, T0.AddDays(1));

        Assert.True(r.EsExito);
        Assert.Equal(EstadoKyc.Pendiente, v.SolicitudActual!.Estado);
        Assert.Equal(2, v.Solicitudes.Count);
    }

    [Fact]
    public void Aprobar_solicitud_inexistente_falla()
    {
        var v = ConSolicitudPendiente(out _);

        var r = v.Aprobar(Guid.NewGuid(), Guid.NewGuid(), T0);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.SolicitudNoEncontrada, r.Error.Code);
    }

    [Fact]
    public void Aprobar_solicitud_ya_resuelta_falla_con_TransicionInvalida()
    {
        var v = ConSolicitudPendiente(out var id);
        v.Rechazar(id, Guid.NewGuid(), "motivo", T0);

        var r = v.Aprobar(id, Guid.NewGuid(), T0);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.TransicionInvalida, r.Error.Code);
    }
}
```

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~VerificacionKycTests`
Expected: FAIL de compilación (tipos KYC inexistentes).

- [ ] **Step 3: Crear enums y errores**

`EstadoKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>Estado de una solicitud de verificación de identidad.</summary>
public enum EstadoKyc
{
    Pendiente,
    Aprobada,
    Rechazada,
}
```

`TipoDocumento.cs`:

```csharp
namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>Tipo de documento de identidad presentado en la verificación.</summary>
public enum TipoDocumento
{
    CedulaIdentidad,
}
```

`ErroresKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>Códigos de error de dominio del contexto KYC.</summary>
public static class ErroresKyc
{
    public const string SolicitudPendienteExiste = "Kyc.SolicitudPendienteExiste";
    public const string YaVerificado = "Kyc.YaVerificado";
    public const string SolicitudNoEncontrada = "Kyc.SolicitudNoEncontrada";
    public const string TransicionInvalida = "Kyc.TransicionInvalida";
}
```

- [ ] **Step 4: Crear `SolicitudKyc`**

`SolicitudKyc.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Un intento de verificación: referencias (opacas) a los blobs de documento y selfie, su estado y
/// la resolución del revisor. Los bytes de las imágenes nunca entran al dominio.
/// </summary>
public sealed class SolicitudKyc : Entity
{
    // Constructor para EF Core.
    private SolicitudKyc()
    {
        ReferenciaDocumento = string.Empty;
        ReferenciaSelfie = string.Empty;
    }

    internal SolicitudKyc(
        string referenciaDocumento, string referenciaSelfie, TipoDocumento tipoDocumento, DateTimeOffset enviadaEn)
    {
        ReferenciaDocumento = referenciaDocumento;
        ReferenciaSelfie = referenciaSelfie;
        TipoDocumento = tipoDocumento;
        EnviadaEn = enviadaEn;
        Estado = EstadoKyc.Pendiente;
    }

    public EstadoKyc Estado { get; private set; }
    public string ReferenciaDocumento { get; private init; }
    public string ReferenciaSelfie { get; private init; }
    public TipoDocumento TipoDocumento { get; private init; }
    public string? MotivoRechazo { get; private set; }
    public DateTimeOffset EnviadaEn { get; private init; }
    public DateTimeOffset? ResueltaEn { get; private set; }
    public Guid? ResueltaPor { get; private set; }

    internal void MarcarAprobada(Guid revisorId, DateTimeOffset cuando)
    {
        Estado = EstadoKyc.Aprobada;
        ResueltaPor = revisorId;
        ResueltaEn = cuando;
    }

    internal void MarcarRechazada(Guid revisorId, string motivo, DateTimeOffset cuando)
    {
        Estado = EstadoKyc.Rechazada;
        MotivoRechazo = motivo;
        ResueltaPor = revisorId;
        ResueltaEn = cuando;
    }
}
```

- [ ] **Step 5: Crear `VerificacionKyc`**

`VerificacionKyc.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Raíz de agregado de verificación de identidad de un usuario. Su <see cref="Entity.Id"/> es el id
/// del usuario. Agrupa el historial de solicitudes; el estado efectivo es el de la última solicitud.
/// </summary>
public sealed class VerificacionKyc : AggregateRoot
{
    private readonly List<SolicitudKyc> _solicitudes = [];

    // Constructor para EF Core.
    private VerificacionKyc()
    {
    }

    private VerificacionKyc(Guid usuarioId) : base(usuarioId)
    {
    }

    /// <summary>Crea el agregado de verificación para el usuario indicado.</summary>
    public static VerificacionKyc Crear(Guid usuarioId) => new(usuarioId);

    /// <summary>Id del usuario dueño de la verificación (coincide con <see cref="Entity.Id"/>).</summary>
    public Guid UsuarioId => Id;

    /// <summary>Historial de solicitudes (orden de inserción).</summary>
    public IReadOnlyCollection<SolicitudKyc> Solicitudes => _solicitudes.AsReadOnly();

    /// <summary>Última solicitud enviada, o <c>null</c> si no hay ninguna.</summary>
    public SolicitudKyc? SolicitudActual =>
        _solicitudes.Count == 0 ? null : _solicitudes.OrderBy(s => s.EnviadaEn).Last();

    /// <summary>El usuario está verificado si alguna solicitud fue aprobada.</summary>
    public bool EstaVerificado => _solicitudes.Any(s => s.Estado == EstadoKyc.Aprobada);

    /// <summary>Registra una nueva solicitud si no hay una pendiente ni una ya aprobada.</summary>
    public Result<SolicitudKyc> EnviarSolicitud(
        string referenciaDocumento, string referenciaSelfie, TipoDocumento tipo, DateTimeOffset cuando)
    {
        if (EstaVerificado)
        {
            return Result.Fallo<SolicitudKyc>(
                new Error(ErroresKyc.YaVerificado, "El usuario ya está verificado."));
        }

        if (SolicitudActual?.Estado == EstadoKyc.Pendiente)
        {
            return Result.Fallo<SolicitudKyc>(
                new Error(ErroresKyc.SolicitudPendienteExiste, "Ya existe una solicitud pendiente."));
        }

        var solicitud = new SolicitudKyc(referenciaDocumento, referenciaSelfie, tipo, cuando);
        _solicitudes.Add(solicitud);
        return Result.Exito(solicitud);
    }

    /// <summary>Aprueba una solicitud pendiente.</summary>
    public Result Aprobar(Guid solicitudId, Guid revisorId, DateTimeOffset cuando) =>
        Resolver(solicitudId, s => s.MarcarAprobada(revisorId, cuando));

    /// <summary>Rechaza una solicitud pendiente con un motivo.</summary>
    public Result Rechazar(Guid solicitudId, Guid revisorId, string motivo, DateTimeOffset cuando) =>
        Resolver(solicitudId, s => s.MarcarRechazada(revisorId, motivo, cuando));

    private Result Resolver(Guid solicitudId, Action<SolicitudKyc> transicion)
    {
        var solicitud = _solicitudes.FirstOrDefault(s => s.Id == solicitudId);
        if (solicitud is null)
        {
            return Result.Fallo(new Error(ErroresKyc.SolicitudNoEncontrada, "La solicitud no existe."));
        }

        if (solicitud.Estado != EstadoKyc.Pendiente)
        {
            return Result.Fallo(new Error(
                ErroresKyc.TransicionInvalida, "Solo puede resolverse una solicitud pendiente."));
        }

        transicion(solicitud);
        return Result.Exito();
    }
}
```

- [ ] **Step 6: Ejecutar y verificar que pasan**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~VerificacionKycTests`
Expected: PASS (7 tests).

- [ ] **Step 7: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Domain/Kyc tests/CaseritoApp.UnitTests/Kyc/VerificacionKycTests.cs
git commit -m "feat(kyc): agregado VerificacionKyc con historial y máquina de estados"
```

---

## Task 3: Puertos, DTOs y publicador de eventos (Application + BuildingBlocks)

**Files:**
- Create: `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/Abstractions/IPublicadorEventosIntegracion.cs`
- Modify: `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application/CaseritoApp.BuildingBlocks.Application.csproj`
- Create: `src/Identity/CaseritoApp.Identity.Application/Kyc/DtosKyc.cs`, `IAlmacenBlobsKyc.cs`, `IRepositorioVerificacionKyc.cs`, `IConsultaVerificacionKyc.cs`

**Interfaces:**
- Produces:
  - `IPublicadorEventosIntegracion.PublicarAsync(IIntegrationEvent evento, CancellationToken ct) : Task`
  - `sealed record BlobKyc(byte[] Contenido, string ContentType)`
  - `sealed record EstadoKycDto(string Estado, string? MotivoRechazo)`
  - `sealed record SolicitudKycResumenDto(Guid SolicitudId, Guid UsuarioId, string Estado, string TipoDocumento, DateTimeOffset EnviadaEn, DateTimeOffset? ResueltaEn)`
  - `IAlmacenBlobsKyc`: `Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct)`, `Task<BlobKyc> ObtenerAsync(string clave, CancellationToken ct)`, `Task EliminarAsync(string clave, CancellationToken ct)`
  - `IRepositorioVerificacionKyc`: `Task<VerificacionKyc?> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct)`, `Task<VerificacionKyc?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct)`, `void Agregar(VerificacionKyc verificacion)`, `Task<ResultadoPaginado<SolicitudKycResumenDto>> ListarAsync(EstadoKyc? estado, int pagina, int tamano, CancellationToken ct)`
  - `IConsultaVerificacionKyc.EstaVerificadoAsync(Guid usuarioId, CancellationToken ct) : Task<bool>`
- Consumes: `ResultadoPaginado<T>` (de `CaseritoApp.Identity.Application.Autorizacion`), `IIntegrationEvent` (Contracts).

- [ ] **Step 1: Añadir referencia de proyecto a Contracts**

En `CaseritoApp.BuildingBlocks.Application.csproj`, dentro del `<ItemGroup>` de `ProjectReference`, añadir:

```xml
    <ProjectReference Include="..\CaseritoApp.BuildingBlocks.Contracts\CaseritoApp.BuildingBlocks.Contracts.csproj" />
```

- [ ] **Step 2: Crear el puerto de publicación de eventos**

`IPublicadorEventosIntegracion.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Contracts;

namespace CaseritoApp.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Publica eventos de integración entre bounded contexts. En el MVP no hay bus ni outbox: la
/// implementación registra el evento (sin PII). El outbox transaccional queda diferido.
/// </summary>
public interface IPublicadorEventosIntegracion
{
    public Task PublicarAsync(IIntegrationEvent evento, CancellationToken ct);
}
```

- [ ] **Step 3: Crear DTOs y puertos de Application**

`DtosKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Contenido descifrado de un blob KYC más su tipo MIME.</summary>
public sealed record BlobKyc(byte[] Contenido, string ContentType);

/// <summary>Estado de verificación efectivo del usuario para exposición al propio usuario.</summary>
public sealed record EstadoKycDto(string Estado, string? MotivoRechazo);

/// <summary>Metadatos de una solicitud para el listado del administrador (sin blobs).</summary>
public sealed record SolicitudKycResumenDto(
    Guid SolicitudId,
    Guid UsuarioId,
    string Estado,
    string TipoDocumento,
    DateTimeOffset EnviadaEn,
    DateTimeOffset? ResueltaEn);
```

`IAlmacenBlobsKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Almacén de blobs de PII (documento/selfie). Cifra en reposo (envelope) dentro del adaptador; el
/// resto del sistema solo maneja claves opacas. El adaptador de dev escribe a disco.
/// </summary>
public interface IAlmacenBlobsKyc
{
    /// <summary>Guarda el contenido cifrado y devuelve una clave opaca para recuperarlo.</summary>
    public Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct);

    /// <summary>Recupera y descifra el blob asociado a la clave.</summary>
    public Task<BlobKyc> ObtenerAsync(string clave, CancellationToken ct);

    /// <summary>Elimina el blob asociado a la clave (idempotente si no existe).</summary>
    public Task EliminarAsync(string clave, CancellationToken ct);
}
```

`IRepositorioVerificacionKyc.cs`:

```csharp
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Puerto de persistencia del agregado <see cref="VerificacionKyc"/>.</summary>
public interface IRepositorioVerificacionKyc
{
    /// <summary>Carga la verificación de un usuario con su historial, o <c>null</c> si no existe.</summary>
    public Task<VerificacionKyc?> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>Carga la verificación que contiene la solicitud indicada, o <c>null</c>.</summary>
    public Task<VerificacionKyc?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct);

    /// <summary>Marca un agregado nuevo para inserción (persistido por el UnitOfWork behavior).</summary>
    public void Agregar(VerificacionKyc verificacion);

    /// <summary>Lista paginada de solicitudes (metadatos), filtrable por estado, más recientes primero.</summary>
    public Task<ResultadoPaginado<SolicitudKycResumenDto>> ListarAsync(
        EstadoKyc? estado, int pagina, int tamano, CancellationToken ct);
}
```

`IConsultaVerificacionKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Consulta de solo lectura del estado de verificación, usada fuera del pipeline de MediatR
/// (login/refresh y perfil) para derivar el badge/claim "verificado".
/// </summary>
public interface IConsultaVerificacionKyc
{
    public Task<bool> EstaVerificadoAsync(Guid usuarioId, CancellationToken ct);
}
```

- [ ] **Step 4: Compilar**

Run: `dotnet build CaseritoApp.sln`
Expected: BUILD succeeded (los puertos aún no tienen adaptador; se registran en Task 6/7).

- [ ] **Step 5: Commit**

```bash
git add src/BuildingBlocks/CaseritoApp.BuildingBlocks.Application src/Identity/CaseritoApp.Identity.Application/Kyc
git commit -m "feat(kyc): puertos (blobs, repositorio, consulta) + DTOs + publicador de eventos"
```

---

## Task 4: Casos de uso del usuario (enviar solicitud + estado)

**Files:**
- Create: `src/Identity/CaseritoApp.Identity.Application/Kyc/ValidacionImagenKyc.cs`, `EnviarSolicitudKycCommand.cs`, `ObtenerEstadoKycQuery.cs`
- Test: `tests/CaseritoApp.UnitTests/Kyc/ValidacionImagenKycTests.cs`, `tests/CaseritoApp.UnitTests/Kyc/EnviarSolicitudKycCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioVerificacionKyc`, `IAlmacenBlobsKyc`, `TimeProvider`, `VerificacionKyc`, `EstadoKycDto`.
- Produces:
  - `EnviarSolicitudKycCommand(Guid UsuarioId, byte[] Documento, string DocumentoContentType, byte[] Selfie, string SelfieContentType) : ICommand`
  - `ObtenerEstadoKycQuery(Guid UsuarioId) : IQuery<Result<EstadoKycDto>>`
  - `static class ValidacionImagenKyc`: `const long LimiteBytes = 5 * 1024 * 1024;`, `static readonly string[] ContentTypesPermitidos`, `static bool EsImagenValida(byte[] contenido, string contentType)`

- [ ] **Step 1: Escribir los tests que fallan (validación de imagen)**

Crear `tests/CaseritoApp.UnitTests/Kyc/ValidacionImagenKycTests.cs`:

```csharp
using CaseritoApp.Identity.Application.Kyc;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class ValidacionImagenKycTests
{
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Jpeg_con_magic_bytes_y_content_type_es_valida() =>
        Assert.True(ValidacionImagenKyc.EsImagenValida(JpegMagic, "image/jpeg"));

    [Fact]
    public void Png_con_magic_bytes_y_content_type_es_valida() =>
        Assert.True(ValidacionImagenKyc.EsImagenValida(PngMagic, "image/png"));

    [Fact]
    public void Content_type_no_permitido_es_invalida() =>
        Assert.False(ValidacionImagenKyc.EsImagenValida(JpegMagic, "application/pdf"));

    [Fact]
    public void Magic_bytes_que_no_coinciden_con_content_type_es_invalida() =>
        Assert.False(ValidacionImagenKyc.EsImagenValida(PngMagic, "image/jpeg"));

    [Fact]
    public void Contenido_vacio_es_invalida() =>
        Assert.False(ValidacionImagenKyc.EsImagenValida([], "image/png"));
}
```

- [ ] **Step 2: Ejecutar y verificar que falla**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~ValidacionImagenKycTests`
Expected: FAIL de compilación.

- [ ] **Step 3: Implementar `ValidacionImagenKyc`**

`ValidacionImagenKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Reglas de aceptación de imágenes de KYC: whitelist de tipo MIME, límite de tamaño y verificación
/// de <em>magic bytes</em> (no se confía en la extensión ni en el content-type declarado).
/// </summary>
public static class ValidacionImagenKyc
{
    /// <summary>Tamaño máximo por archivo (5 MiB).</summary>
    public const long LimiteBytes = 5 * 1024 * 1024;

    /// <summary>Tipos MIME aceptados.</summary>
    public static readonly string[] ContentTypesPermitidos = ["image/jpeg", "image/png"];

    private static readonly byte[] FirmaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Indica si el contenido es una imagen aceptable y coherente con su content-type.</summary>
    public static bool EsImagenValida(byte[] contenido, string contentType)
    {
        if (contenido is null || contenido.Length == 0 || contenido.Length > LimiteBytes)
        {
            return false;
        }

        return contentType switch
        {
            "image/jpeg" => EmpiezaCon(contenido, FirmaJpeg),
            "image/png" => EmpiezaCon(contenido, FirmaPng),
            _ => false,
        };
    }

    private static bool EmpiezaCon(byte[] contenido, byte[] firma)
    {
        if (contenido.Length < firma.Length)
        {
            return false;
        }

        for (var i = 0; i < firma.Length; i++)
        {
            if (contenido[i] != firma[i])
            {
                return false;
            }
        }

        return true;
    }
}
```

- [ ] **Step 4: Ejecutar y verificar que pasan**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~ValidacionImagenKycTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Crear el comando de envío y su handler**

`EnviarSolicitudKycCommand.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Envía una solicitud de verificación con documento (CI) y selfie del usuario autenticado.</summary>
public sealed record EnviarSolicitudKycCommand(
    Guid UsuarioId,
    byte[] Documento,
    string DocumentoContentType,
    byte[] Selfie,
    string SelfieContentType) : ICommand;

/// <summary>
/// Handler: guarda ambos blobs cifrados, crea/actualiza el agregado y registra la solicitud. Si el
/// dominio rechaza el envío, borra los blobs recién escritos (compensación best-effort).
/// </summary>
public sealed partial class EnviarSolicitudKycCommandHandler(
    IRepositorioVerificacionKyc repositorio,
    IAlmacenBlobsKyc almacen,
    TimeProvider tiempo,
    ILogger<EnviarSolicitudKycCommandHandler> logger)
    : ICommandHandler<EnviarSolicitudKycCommand>
{
    public async Task<Result> Handle(EnviarSolicitudKycCommand request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorUsuarioAsync(request.UsuarioId, cancellationToken);
        var esNueva = verificacion is null;
        verificacion ??= VerificacionKyc.Crear(request.UsuarioId);

        var claveDoc = await almacen.GuardarAsync(request.Documento, request.DocumentoContentType, cancellationToken);
        var claveSelfie = await almacen.GuardarAsync(request.Selfie, request.SelfieContentType, cancellationToken);

        var resultado = verificacion.EnviarSolicitud(
            claveDoc, claveSelfie, TipoDocumento.CedulaIdentidad, tiempo.GetUtcNow());

        if (!resultado.EsExito)
        {
            // Compensación: los blobs quedaron escritos pero la solicitud no se creó.
            await almacen.EliminarAsync(claveDoc, cancellationToken);
            await almacen.EliminarAsync(claveSelfie, cancellationToken);
            RegistrarEnvio(logger, request.UsuarioId, resultado.Error.Code);
            return Result.Fallo(resultado.Error);
        }

        if (esNueva)
        {
            repositorio.Agregar(verificacion);
        }

        RegistrarEnvio(logger, request.UsuarioId, "ok");
        return Result.Exito();
    }

    // Auditoría sin PII: solo id de usuario y resultado; jamás bytes, content-type ni claves de blob.
    [LoggerMessage(Level = LogLevel.Information, Message = "Solicitud KYC enviada: usuario={UsuarioId} resultado={Resultado}")]
    private static partial void RegistrarEnvio(ILogger logger, Guid usuarioId, string resultado);
}

/// <summary>Valida tamaño, tipo y coherencia (magic bytes) de documento y selfie.</summary>
public sealed class EnviarSolicitudKycCommandValidator : AbstractValidator<EnviarSolicitudKycCommand>
{
    public EnviarSolicitudKycCommandValidator()
    {
        RuleFor(c => c)
            .Must(c => ValidacionImagenKyc.EsImagenValida(c.Documento, c.DocumentoContentType))
            .WithName("Documento")
            .WithMessage("El documento debe ser una imagen JPEG o PNG de hasta 5 MB.");

        RuleFor(c => c)
            .Must(c => ValidacionImagenKyc.EsImagenValida(c.Selfie, c.SelfieContentType))
            .WithName("Selfie")
            .WithMessage("La selfie debe ser una imagen JPEG o PNG de hasta 5 MB.");
    }
}
```

- [ ] **Step 6: Crear la query de estado y su handler**

`ObtenerEstadoKycQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Consulta el estado de verificación efectivo del usuario autenticado.</summary>
public sealed record ObtenerEstadoKycQuery(Guid UsuarioId) : IQuery<Result<EstadoKycDto>>;

/// <summary>Handler: proyecta el estado de la última solicitud, o "NoIniciado" si no hay ninguna.</summary>
public sealed class ObtenerEstadoKycQueryHandler(IRepositorioVerificacionKyc repositorio)
    : IQueryHandler<ObtenerEstadoKycQuery, Result<EstadoKycDto>>
{
    /// <summary>Estado expuesto cuando el usuario nunca envió una solicitud.</summary>
    public const string NoIniciado = "NoIniciado";

    public async Task<Result<EstadoKycDto>> Handle(ObtenerEstadoKycQuery request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorUsuarioAsync(request.UsuarioId, cancellationToken);
        var actual = verificacion?.SolicitudActual;

        var dto = actual is null
            ? new EstadoKycDto(NoIniciado, null)
            : new EstadoKycDto(actual.Estado.ToString(), actual.MotivoRechazo);

        return Result.Exito(dto);
    }
}
```

- [ ] **Step 7: Escribir el test del handler de envío (compensación)**

Crear `tests/CaseritoApp.UnitTests/Kyc/EnviarSolicitudKycCommandHandlerTests.cs`:

```csharp
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class EnviarSolicitudKycCommandHandlerTests
{
    private sealed class AlmacenFake : IAlmacenBlobsKyc
    {
        public int Guardados { get; private set; }
        public int Eliminados { get; private set; }

        public Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct)
        {
            Guardados++;
            return Task.FromResult($"clave-{Guardados}");
        }

        public Task<BlobKyc> ObtenerAsync(string clave, CancellationToken ct) =>
            Task.FromResult(new BlobKyc([], "image/png"));

        public Task EliminarAsync(string clave, CancellationToken ct)
        {
            Eliminados++;
            return Task.CompletedTask;
        }
    }

    private sealed class RepoFake(VerificacionKyc? existente) : IRepositorioVerificacionKyc
    {
        public VerificacionKyc? Agregada { get; private set; }

        public Task<VerificacionKyc?> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(existente);
        public Task<VerificacionKyc?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct) =>
            Task.FromResult<VerificacionKyc?>(null);
        public void Agregar(VerificacionKyc verificacion) => Agregada = verificacion;
        public Task<ResultadoPaginado<SolicitudKycResumenDto>> ListarAsync(
            EstadoKyc? estado, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<SolicitudKycResumenDto>([], pagina, tamano, 0));
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task Envio_nuevo_guarda_dos_blobs_y_agrega_agregado()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(existente: null);
        var handler = new EnviarSolicitudKycCommandHandler(
            repo, almacen, TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(
            new EnviarSolicitudKycCommand(Guid.NewGuid(), Png, "image/png", Png, "image/png"),
            CancellationToken.None);

        Assert.True(r.EsExito);
        Assert.Equal(2, almacen.Guardados);
        Assert.Equal(0, almacen.Eliminados);
        Assert.NotNull(repo.Agregada);
    }

    [Fact]
    public async Task Envio_con_pendiente_existente_borra_los_blobs_escritos()
    {
        var usuarioId = Guid.NewGuid();
        var existente = VerificacionKyc.Crear(usuarioId);
        existente.EnviarSolicitud("d", "s", TipoDocumento.CedulaIdentidad, DateTimeOffset.UnixEpoch);

        var almacen = new AlmacenFake();
        var repo = new RepoFake(existente);
        var handler = new EnviarSolicitudKycCommandHandler(
            repo, almacen, TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(
            new EnviarSolicitudKycCommand(usuarioId, Png, "image/png", Png, "image/png"),
            CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Equal(ErroresKyc.SolicitudPendienteExiste, r.Error.Code);
        Assert.Equal(2, almacen.Guardados);
        Assert.Equal(2, almacen.Eliminados);
    }
}
```

- [ ] **Step 8: Ejecutar y verificar que pasan**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~EnviarSolicitudKycCommandHandlerTests`
Expected: PASS (2 tests).

- [ ] **Step 9: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Application/Kyc tests/CaseritoApp.UnitTests/Kyc
git commit -m "feat(kyc): casos de uso de usuario (enviar solicitud + estado) con validación y compensación"
```

---

## Task 5: Casos de uso del administrador (listar, ver blob, aprobar, rechazar)

**Files:**
- Create: `src/Identity/CaseritoApp.Identity.Application/Kyc/ListarSolicitudesKycQuery.cs`, `ObtenerBlobKycQuery.cs`, `AprobarSolicitudKycCommand.cs`, `RechazarSolicitudKycCommand.cs`
- Test: `tests/CaseritoApp.UnitTests/Kyc/AprobarSolicitudKycCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioVerificacionKyc`, `IAlmacenBlobsKyc`, `IPiiAccessAuditor` (BuildingBlocks.Infrastructure.Security — accesible en Application vía... ver nota), `IPublicadorEventosIntegracion`, `TimeProvider`, `UserVerified` (Contracts), `ResultadoPaginado<T>`.
- Produces:
  - `ListarSolicitudesKycQuery(string? Estado, int Pagina, int Tamano) : IQuery<ResultadoPaginado<SolicitudKycResumenDto>>`
  - `ObtenerBlobKycQuery(Guid SolicitudId, Guid AdminId, TipoBlobKyc Tipo) : IQuery<Result<BlobKyc>>` + `enum TipoBlobKyc { Documento, Selfie }`
  - `AprobarSolicitudKycCommand(Guid SolicitudId, Guid RevisorId) : ICommand`
  - `RechazarSolicitudKycCommand(Guid SolicitudId, Guid RevisorId, string Motivo) : ICommand`

> **Nota de referencia (`IPiiAccessAuditor`)**: vive en `CaseritoApp.BuildingBlocks.Infrastructure.Security`, y `Identity.Application` **no** referencia BuildingBlocks.Infrastructure. Para no romper capas, en este task se **redeclara el puerto de auditoría en Application** como `IAuditorAccesoPii` (mismo contrato) y el adaptador de Task 7 lo implementa. NO se usa el tipo de BuildingBlocks.Infrastructure desde Application.

- [ ] **Step 1: Crear el puerto de auditoría en Application**

Crear `src/Identity/CaseritoApp.Identity.Application/Kyc/IAuditorAccesoPii.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Registra en un log append-only cada acceso a PII sensible (sin exponer la PII misma).</summary>
public interface IAuditorAccesoPii
{
    public Task RegistrarAccesoAsync(string recurso, string actor, CancellationToken ct);
}
```

- [ ] **Step 2: Crear la query de listado**

`ListarSolicitudesKycQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;
using FluentValidation;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Lista paginada de solicitudes KYC para el administrador, filtrable por estado.</summary>
public sealed record ListarSolicitudesKycQuery(string? Estado, int Pagina, int Tamano)
    : IQuery<ResultadoPaginado<SolicitudKycResumenDto>>;

/// <summary>Handler: traduce el filtro de estado y delega la paginación en el repositorio.</summary>
public sealed class ListarSolicitudesKycQueryHandler(IRepositorioVerificacionKyc repositorio)
    : IQueryHandler<ListarSolicitudesKycQuery, ResultadoPaginado<SolicitudKycResumenDto>>
{
    public Task<ResultadoPaginado<SolicitudKycResumenDto>> Handle(
        ListarSolicitudesKycQuery request, CancellationToken cancellationToken)
    {
        EstadoKyc? estado = Enum.TryParse<EstadoKyc>(request.Estado, out var e) ? e : null;
        return repositorio.ListarAsync(estado, request.Pagina, request.Tamano, cancellationToken);
    }
}

/// <summary>Valida los límites de paginación (mismo criterio que la búsqueda de usuarios).</summary>
public sealed class ListarSolicitudesKycQueryValidator : AbstractValidator<ListarSolicitudesKycQuery>
{
    public ListarSolicitudesKycQueryValidator()
    {
        RuleFor(q => q.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Tamano).InclusiveBetween(1, 100);
    }
}
```

- [ ] **Step 3: Crear la query de acceso a blob (auditado)**

`ObtenerBlobKycQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Cuál de los dos blobs de una solicitud se solicita.</summary>
public enum TipoBlobKyc
{
    Documento,
    Selfie,
}

/// <summary>Obtiene (descifrado) el documento o la selfie de una solicitud. Registra el acceso a PII.</summary>
public sealed record ObtenerBlobKycQuery(Guid SolicitudId, Guid AdminId, TipoBlobKyc Tipo)
    : IQuery<Result<BlobKyc>>;

/// <summary>Handler: localiza la solicitud, audita el acceso y devuelve el blob descifrado.</summary>
public sealed class ObtenerBlobKycQueryHandler(
    IRepositorioVerificacionKyc repositorio,
    IAlmacenBlobsKyc almacen,
    IAuditorAccesoPii auditor)
    : IQueryHandler<ObtenerBlobKycQuery, Result<BlobKyc>>
{
    public async Task<Result<BlobKyc>> Handle(ObtenerBlobKycQuery request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorSolicitudAsync(request.SolicitudId, cancellationToken);
        var solicitud = verificacion?.Solicitudes.FirstOrDefault(s => s.Id == request.SolicitudId);
        if (solicitud is null)
        {
            return Result.Fallo<BlobKyc>(new Error(ErroresKyc.SolicitudNoEncontrada, "La solicitud no existe."));
        }

        var clave = request.Tipo == TipoBlobKyc.Documento
            ? solicitud.ReferenciaDocumento
            : solicitud.ReferenciaSelfie;

        // Recurso auditado: id de solicitud + tipo; NUNCA la clave opaca del blob.
        await auditor.RegistrarAccesoAsync(
            $"kyc:{request.SolicitudId}:{request.Tipo}", request.AdminId.ToString(), cancellationToken);

        var blob = await almacen.ObtenerAsync(clave, cancellationToken);
        return Result.Exito(blob);
    }
}
```

- [ ] **Step 4: Crear los comandos aprobar/rechazar**

`AprobarSolicitudKycCommand.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Aprueba una solicitud pendiente y publica <c>UserVerified</c>.</summary>
public sealed record AprobarSolicitudKycCommand(Guid SolicitudId, Guid RevisorId) : ICommand;

/// <summary>Handler: aprueba en el agregado y, si tiene éxito, publica el evento de integración.</summary>
public sealed partial class AprobarSolicitudKycCommandHandler(
    IRepositorioVerificacionKyc repositorio,
    IPublicadorEventosIntegracion publicador,
    TimeProvider tiempo,
    ILogger<AprobarSolicitudKycCommandHandler> logger)
    : ICommandHandler<AprobarSolicitudKycCommand>
{
    public async Task<Result> Handle(AprobarSolicitudKycCommand request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorSolicitudAsync(request.SolicitudId, cancellationToken);
        if (verificacion is null)
        {
            RegistrarResolucion(logger, "aprobar", request.RevisorId, request.SolicitudId, ErroresKyc.SolicitudNoEncontrada);
            return Result.Fallo(new Error(ErroresKyc.SolicitudNoEncontrada, "La solicitud no existe."));
        }

        var ahora = tiempo.GetUtcNow();
        var resultado = verificacion.Aprobar(request.SolicitudId, request.RevisorId, ahora);

        if (resultado.EsExito)
        {
            await publicador.PublicarAsync(
                new UserVerified(Guid.NewGuid(), ahora, verificacion.UsuarioId), cancellationToken);
        }

        RegistrarResolucion(
            logger, "aprobar", request.RevisorId, request.SolicitudId,
            resultado.EsExito ? "ok" : resultado.Error.Code);

        return resultado;
    }

    // Auditoría sin PII: revisor, solicitud, resultado. Sin email ni datos del documento.
    [LoggerMessage(Level = LogLevel.Information, Message = "Resolución KYC {Accion}: revisor={RevisorId} solicitud={SolicitudId} resultado={Resultado}")]
    private static partial void RegistrarResolucion(
        ILogger logger, string accion, Guid revisorId, Guid solicitudId, string resultado);
}
```

`RechazarSolicitudKycCommand.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Kyc;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>Rechaza una solicitud pendiente con un motivo (visible al usuario).</summary>
public sealed record RechazarSolicitudKycCommand(Guid SolicitudId, Guid RevisorId, string Motivo) : ICommand;

/// <summary>Handler: rechaza en el agregado. No publica evento.</summary>
public sealed partial class RechazarSolicitudKycCommandHandler(
    IRepositorioVerificacionKyc repositorio,
    TimeProvider tiempo,
    ILogger<RechazarSolicitudKycCommandHandler> logger)
    : ICommandHandler<RechazarSolicitudKycCommand>
{
    public async Task<Result> Handle(RechazarSolicitudKycCommand request, CancellationToken cancellationToken)
    {
        var verificacion = await repositorio.ObtenerPorSolicitudAsync(request.SolicitudId, cancellationToken);
        if (verificacion is null)
        {
            RegistrarResolucion(logger, "rechazar", request.RevisorId, request.SolicitudId, ErroresKyc.SolicitudNoEncontrada);
            return Result.Fallo(new Error(ErroresKyc.SolicitudNoEncontrada, "La solicitud no existe."));
        }

        var resultado = verificacion.Rechazar(request.SolicitudId, request.RevisorId, request.Motivo, tiempo.GetUtcNow());

        RegistrarResolucion(
            logger, "rechazar", request.RevisorId, request.SolicitudId,
            resultado.EsExito ? "ok" : resultado.Error.Code);

        return resultado;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Resolución KYC {Accion}: revisor={RevisorId} solicitud={SolicitudId} resultado={Resultado}")]
    private static partial void RegistrarResolucion(
        ILogger logger, string accion, Guid revisorId, Guid solicitudId, string resultado);
}

/// <summary>Valida que el motivo de rechazo esté presente y sea de longitud razonable.</summary>
public sealed class RechazarSolicitudKycCommandValidator : AbstractValidator<RechazarSolicitudKycCommand>
{
    public RechazarSolicitudKycCommandValidator()
    {
        RuleFor(c => c.Motivo)
            .NotEmpty().WithMessage("El motivo de rechazo es obligatorio.")
            .MaximumLength(500).WithMessage("El motivo no puede superar los 500 caracteres.");
    }
}
```

- [ ] **Step 5: Escribir el test del handler de aprobar (publica evento)**

Crear `tests/CaseritoApp.UnitTests/Kyc/AprobarSolicitudKycCommandHandlerTests.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Contracts;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class AprobarSolicitudKycCommandHandlerTests
{
    private sealed class PublicadorFake : IPublicadorEventosIntegracion
    {
        public List<IIntegrationEvent> Publicados { get; } = [];
        public Task PublicarAsync(IIntegrationEvent evento, CancellationToken ct)
        {
            Publicados.Add(evento);
            return Task.CompletedTask;
        }
    }

    private sealed class RepoFake(VerificacionKyc? verificacion) : IRepositorioVerificacionKyc
    {
        public Task<VerificacionKyc?> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(verificacion);
        public Task<VerificacionKyc?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct) =>
            Task.FromResult(verificacion);
        public void Agregar(VerificacionKyc v) { }
        public Task<ResultadoPaginado<SolicitudKycResumenDto>> ListarAsync(
            EstadoKyc? estado, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<SolicitudKycResumenDto>([], pagina, tamano, 0));
    }

    [Fact]
    public async Task Aprobar_pendiente_publica_UserVerified()
    {
        var usuarioId = Guid.NewGuid();
        var v = VerificacionKyc.Crear(usuarioId);
        var solicitudId = v.EnviarSolicitud("d", "s", TipoDocumento.CedulaIdentidad, DateTimeOffset.UnixEpoch).Valor.Id;
        var publicador = new PublicadorFake();
        var handler = new AprobarSolicitudKycCommandHandler(
            new RepoFake(v), publicador, TimeProvider.System, NullLogger<AprobarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(new AprobarSolicitudKycCommand(solicitudId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(r.EsExito);
        var evento = Assert.Single(publicador.Publicados);
        Assert.Equal(usuarioId, Assert.IsType<UserVerified>(evento).UserId);
    }

    [Fact]
    public async Task Aprobar_solicitud_inexistente_no_publica()
    {
        var publicador = new PublicadorFake();
        var handler = new AprobarSolicitudKycCommandHandler(
            new RepoFake(null), publicador, TimeProvider.System, NullLogger<AprobarSolicitudKycCommandHandler>.Instance);

        var r = await handler.Handle(new AprobarSolicitudKycCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(r.EsExito);
        Assert.Empty(publicador.Publicados);
    }
}
```

- [ ] **Step 6: Ejecutar y verificar que pasan**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~AprobarSolicitudKycCommandHandlerTests`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Application/Kyc tests/CaseritoApp.UnitTests/Kyc/AprobarSolicitudKycCommandHandlerTests.cs
git commit -m "feat(kyc): casos de uso de admin (listar, ver blob auditado, aprobar, rechazar)"
```

---

## Task 6: Persistencia EF Core + migración

**Files:**
- Modify: `src/Identity/CaseritoApp.Identity.Infrastructure/IdentityDbContext.cs`
- Create: `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConfiguracionKyc.cs`, `RepositorioVerificacionKycEfCore.cs`, `ConsultaVerificacionKycEfCore.cs`
- Create (generado): migración `KycInicial` en `.../Infrastructure/Migrations/`

**Interfaces:**
- Consumes: `IRepositorioVerificacionKyc`, `IConsultaVerificacionKyc`, `VerificacionKyc`, `SolicitudKyc`, `EstadoKyc`.
- Produces: `IdentityDbContext.VerificacionesKyc : DbSet<VerificacionKyc>`; adaptadores concretos `RepositorioVerificacionKycEfCore`, `ConsultaVerificacionKycEfCore`.

- [ ] **Step 1: Añadir el DbSet y el mapeo al `IdentityDbContext`**

En `IdentityDbContext.cs`, añadir el `using` al inicio (tras los existentes):

```csharp
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure.Kyc;
```

Añadir la propiedad DbSet junto a `RefreshTokens`:

```csharp
    public DbSet<VerificacionKyc> VerificacionesKyc => Set<VerificacionKyc>();
```

Al final de `OnModelCreating` (antes del `}` del método), añadir:

```csharp
        ConfiguracionKyc.Configurar(builder);
```

- [ ] **Step 2: Crear la configuración EF del agregado**

`Kyc/ConfiguracionKyc.cs`:

```csharp
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Mapeo EF Core de <see cref="VerificacionKyc"/> y su historial de <see cref="SolicitudKyc"/>.</summary>
public static class ConfiguracionKyc
{
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<VerificacionKyc>(e =>
        {
            e.ToTable("VerificacionesKyc");
            e.HasKey(v => v.Id);

            e.HasMany(v => v.Solicitudes)
                .WithOne()
                .HasForeignKey("VerificacionKycId")
                .OnDelete(DeleteBehavior.Cascade);

            e.Navigation(v => v.Solicitudes).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<SolicitudKyc>(e =>
        {
            e.ToTable("SolicitudesKyc");
            e.HasKey(s => s.Id);
            e.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(s => s.TipoDocumento).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(s => s.ReferenciaDocumento).HasMaxLength(200).IsRequired();
            e.Property(s => s.ReferenciaSelfie).HasMaxLength(200).IsRequired();
            e.Property(s => s.MotivoRechazo).HasMaxLength(500);
            e.HasIndex("VerificacionKycId");
        });
    }
}
```

- [ ] **Step 3: Crear el repositorio EF**

`Kyc/RepositorioVerificacionKycEfCore.cs`:

```csharp
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Adaptador EF Core de <see cref="IRepositorioVerificacionKyc"/> sobre <see cref="IdentityDbContext"/>.</summary>
public sealed class RepositorioVerificacionKycEfCore(IdentityDbContext db) : IRepositorioVerificacionKyc
{
    public Task<VerificacionKyc?> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        db.VerificacionesKyc.Include(v => v.Solicitudes).FirstOrDefaultAsync(v => v.Id == usuarioId, ct);

    public Task<VerificacionKyc?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken ct) =>
        db.VerificacionesKyc.Include(v => v.Solicitudes)
            .FirstOrDefaultAsync(v => v.Solicitudes.Any(s => s.Id == solicitudId), ct);

    public void Agregar(VerificacionKyc verificacion) => db.VerificacionesKyc.Add(verificacion);

    public async Task<ResultadoPaginado<SolicitudKycResumenDto>> ListarAsync(
        EstadoKyc? estado, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = db.VerificacionesKyc
            .SelectMany(v => v.Solicitudes.Select(s => new SolicitudKycResumenDto(
                s.Id, v.Id, s.Estado.ToString(), s.TipoDocumento.ToString(), s.EnviadaEn, s.ResueltaEn)));

        if (estado is not null)
        {
            var texto = estado.Value.ToString();
            consulta = consulta.Where(s => s.Estado == texto);
        }

        var total = await consulta.CountAsync(ct);

        var items = await consulta
            .OrderByDescending(s => s.EnviadaEn)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(ct);

        return new ResultadoPaginado<SolicitudKycResumenDto>(items, pagina, tamano, total);
    }
}
```

- [ ] **Step 4: Crear la consulta de verificación**

`Kyc/ConsultaVerificacionKycEfCore.cs`:

```csharp
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Adaptador de solo lectura de <see cref="IConsultaVerificacionKyc"/>.</summary>
public sealed class ConsultaVerificacionKycEfCore(IdentityDbContext db) : IConsultaVerificacionKyc
{
    public Task<bool> EstaVerificadoAsync(Guid usuarioId, CancellationToken ct) =>
        db.VerificacionesKyc
            .Where(v => v.Id == usuarioId)
            .SelectMany(v => v.Solicitudes)
            .AnyAsync(s => s.Estado == EstadoKyc.Aprobada, ct);
}
```

- [ ] **Step 5: Registrar los adaptadores de persistencia en DI**

En `DependencyInjection.cs`, añadir el `using`:

```csharp
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Infrastructure.Kyc;
```

En `AgregarIdentity`, junto a los otros `AddScoped`, añadir:

```csharp
        servicios.AddScoped<IRepositorioVerificacionKyc, RepositorioVerificacionKycEfCore>();
        servicios.AddScoped<IConsultaVerificacionKyc, ConsultaVerificacionKycEfCore>();
```

- [ ] **Step 6: Compilar antes de generar la migración**

Run: `dotnet build CaseritoApp.sln`
Expected: BUILD succeeded (los puertos de blobs/publicador/auditor aún no están registrados; se registran en Task 7. Este build es de compilación, no de arranque).

- [ ] **Step 7: Generar la migración `KycInicial`**

Run (desde `CaseritoApp/`):

```bash
dotnet ef migrations add KycInicial \
  --project src/Identity/CaseritoApp.Identity.Infrastructure \
  --startup-project src/Host/CaseritoApp.Host \
  --output-dir Migrations
```

Expected: se crean `Migrations/*_KycInicial.cs` (+ Designer) con las tablas `identity.VerificacionesKyc` y `identity.SolicitudesKyc`. Verificar que el `Up` crea ambas tablas y la FK.

- [ ] **Step 8: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Infrastructure
git commit -m "feat(kyc): persistencia EF Core (VerificacionKyc/SolicitudKyc) + migración KycInicial"
```

---

## Task 7: Adaptadores de infraestructura (blobs, publicador, auditor) + DI

**Files:**
- Create: `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesAlmacenKyc.cs`, `AlmacenBlobsKycDisco.cs`, `PublicadorEventosIntegracionLog.cs`, `AuditorAccesoPiiLog.cs`
- Modify: `src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IAlmacenBlobsKyc`, `IPublicadorEventosIntegracion`, `IAuditorAccesoPii`, `IEncryptor` (byte[]), `PassthroughEncryptor`.
- Produces: registros DI de `IEncryptor`→`PassthroughEncryptor`, `IAlmacenBlobsKyc`→`AlmacenBlobsKycDisco`, `IPublicadorEventosIntegracion`→`PublicadorEventosIntegracionLog`, `IAuditorAccesoPii`→`AuditorAccesoPiiLog`; opciones `OpcionesAlmacenKyc` (sección `Kyc`).

- [ ] **Step 1: Crear las opciones del almacén**

`Kyc/OpcionesAlmacenKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Opciones del almacén de blobs KYC en disco (sección de configuración <c>Kyc</c>).</summary>
public sealed class OpcionesAlmacenKyc
{
    public const string Seccion = "Kyc";

    /// <summary>Directorio base donde se escriben los blobs cifrados. Debe estar fuera del repositorio.</summary>
    public string RutaBase { get; set; } = Path.Combine(Path.GetTempPath(), "caserito-kyc");
}
```

- [ ] **Step 2: Crear el adaptador de blobs en disco**

`Kyc/AlmacenBlobsKycDisco.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Infrastructure.Security;
using CaseritoApp.Identity.Application.Kyc;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>
/// Adaptador de desarrollo de <see cref="IAlmacenBlobsKyc"/>: escribe blobs cifrados a disco, fuera
/// de la BD, con claves opacas. El content-type va en un sidecar (no es PII). En prod se sustituye
/// por object storage + KMS sin tocar dominio ni casos de uso.
/// </summary>
public sealed class AlmacenBlobsKycDisco(IEncryptor encryptor, IOptions<OpcionesAlmacenKyc> opciones)
    : IAlmacenBlobsKyc
{
    private readonly string _rutaBase = opciones.Value.RutaBase;

    public async Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(_rutaBase);
        var clave = Guid.NewGuid().ToString("N");
        var cifrado = encryptor.Cifrar(contenido);
        await File.WriteAllBytesAsync(RutaBlob(clave), cifrado, ct);
        await File.WriteAllTextAsync(RutaMeta(clave), contentType, ct);
        return clave;
    }

    public async Task<BlobKyc> ObtenerAsync(string clave, CancellationToken ct)
    {
        var cifrado = await File.ReadAllBytesAsync(RutaBlob(clave), ct);
        var contenido = encryptor.Descifrar(cifrado);
        var contentType = await File.ReadAllTextAsync(RutaMeta(clave), ct);
        return new BlobKyc(contenido, contentType);
    }

    public Task EliminarAsync(string clave, CancellationToken ct)
    {
        File.Delete(RutaBlob(clave));
        File.Delete(RutaMeta(clave));
        return Task.CompletedTask;
    }

    private string RutaBlob(string clave) => Path.Combine(_rutaBase, $"{clave}.bin");
    private string RutaMeta(string clave) => Path.Combine(_rutaBase, $"{clave}.meta");
}
```

- [ ] **Step 3: Crear el publicador de eventos (log sin PII)**

`Kyc/PublicadorEventosIntegracionLog.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Contracts;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>
/// Implementación in-process de <see cref="IPublicadorEventosIntegracion"/>: registra el evento sin
/// PII (tipo + EventId). No hay bus ni outbox en el MVP; el outbox transaccional queda diferido.
/// </summary>
public sealed partial class PublicadorEventosIntegracionLog(ILogger<PublicadorEventosIntegracionLog> logger)
    : IPublicadorEventosIntegracion
{
    public Task PublicarAsync(IIntegrationEvent evento, CancellationToken ct)
    {
        RegistrarEvento(logger, evento.GetType().Name, evento.EventId);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Evento de integración publicado: tipo={Tipo} eventId={EventId}")]
    private static partial void RegistrarEvento(ILogger logger, string tipo, Guid eventId);
}
```

- [ ] **Step 4: Crear el auditor de acceso a PII**

`Kyc/AuditorAccesoPiiLog.cs`:

```csharp
using CaseritoApp.Identity.Application.Kyc;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>
/// Implementación append-only (log) de <see cref="IAuditorAccesoPii"/>: registra recurso + actor de
/// cada acceso a PII. Nunca registra la PII ni la clave del blob.
/// </summary>
public sealed partial class AuditorAccesoPiiLog(ILogger<AuditorAccesoPiiLog> logger) : IAuditorAccesoPii
{
    public Task RegistrarAccesoAsync(string recurso, string actor, CancellationToken ct)
    {
        RegistrarAcceso(logger, recurso, actor);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Acceso a PII: recurso={Recurso} actor={Actor}")]
    private static partial void RegistrarAcceso(ILogger logger, string recurso, string actor);
}
```

- [ ] **Step 5: Registrar los adaptadores en DI**

En `DependencyInjection.cs`, añadir los `using` necesarios (si faltan):

```csharp
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Infrastructure.Security;
```

En `AgregarIdentity`, junto a los registros KYC del Task 6, añadir:

```csharp
        servicios.Configure<OpcionesAlmacenKyc>(config.GetSection(OpcionesAlmacenKyc.Seccion));
        servicios.AddSingleton<IEncryptor, PassthroughEncryptor>();
        servicios.AddScoped<IAlmacenBlobsKyc, AlmacenBlobsKycDisco>();
        servicios.AddSingleton<IPublicadorEventosIntegracion, PublicadorEventosIntegracionLog>();
        servicios.AddSingleton<IAuditorAccesoPii, AuditorAccesoPiiLog>();
```

> Nota: `IEncryptor`→`PassthroughEncryptor` es solo para dev/test. En prod se registra el encryptor real (envelope + KMS) — follow-up del spec §11.

- [ ] **Step 6: Compilar**

Run: `dotnet build CaseritoApp.sln`
Expected: BUILD succeeded.

- [ ] **Step 7: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Infrastructure
git commit -m "feat(kyc): adaptadores de blobs cifrados en disco, publicador y auditor + DI"
```

---

## Task 8: Claim `verificado` en el JWT

**Files:**
- Modify: `src/Identity/CaseritoApp.Identity.Domain/Autorizacion/ClaimsApp.cs`
- Modify: `src/Identity/CaseritoApp.Identity.Infrastructure/Auth/GeneradorTokensAcceso.cs`
- Modify: `src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`
- Modify: `tests/CaseritoApp.UnitTests/Auth/GeneradorTokensAccesoTests.cs`

**Interfaces:**
- Produces: `ClaimsApp.Verificado = "verificado"`; nueva firma `IGeneradorTokensAcceso.Generar(ApplicationUser usuario, IReadOnlyCollection<string> permisos, bool verificado)`.

- [ ] **Step 1: Añadir la constante de claim**

En `ClaimsApp.cs`, añadir la constante `Verificado` junto a `Permiso` (leer el archivo primero para respetar el formato exacto). Debe quedar:

```csharp
    /// <summary>Tipo de claim que indica que el usuario tiene una verificación KYC aprobada.</summary>
    public const string Verificado = "verificado";
```

- [ ] **Step 2: Actualizar el test unitario del generador (nueva firma) — que falle**

En `tests/CaseritoApp.UnitTests/Auth/GeneradorTokensAccesoTests.cs`, actualizar las llamadas existentes a `Generar(usuario, permisos)` para pasar el tercer argumento, y añadir un test nuevo. Leer el archivo y adaptar; el test nuevo:

```csharp
    [Fact]
    public void Generar_incluye_claim_verificado_true_cuando_esta_verificado()
    {
        // Reutiliza el arrange del resto de la clase (usuario + generador). Ajustar nombres a los existentes.
        var token = CrearGenerador().Generar(UsuarioDePrueba(), [], verificado: true);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("true", jwt.Claims.Single(c => c.Type == "verificado").Value);
    }
```

> Nota para el implementador: adaptar `CrearGenerador()`/`UsuarioDePrueba()` a los helpers ya presentes en el archivo (o inline el arrange existente). El punto es: la llamada a `Generar` ahora lleva `verificado:` y se asserta el claim.

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~GeneradorTokensAccesoTests`
Expected: FAIL de compilación (firma de 3 args no existe aún).

- [ ] **Step 3: Actualizar la interfaz y el generador**

En `GeneradorTokensAcceso.cs`:

Cambiar la firma de la interfaz:

```csharp
    public string Generar(ApplicationUser usuario, IReadOnlyCollection<string> permisos, bool verificado);
```

Cambiar la firma del método e insertar el claim tras la línea de `permisos`:

```csharp
    public string Generar(ApplicationUser usuario, IReadOnlyCollection<string> permisos, bool verificado)
    {
```

y tras `claims.AddRange(permisos.Select(p => new Claim(ClaimsApp.Permiso, p)));` añadir:

```csharp
        claims.Add(new Claim(ClaimsApp.Verificado, verificado ? "true" : "false"));
```

- [ ] **Step 4: Actualizar login y refresh para calcular `verificado`**

En `AuthEndpoints.cs`:

Añadir el `using`:

```csharp
using CaseritoApp.Identity.Application.Kyc;
```

En `LoginAsync`, añadir el parámetro `IConsultaVerificacionKyc consultaKyc` a la firma (junto a los otros servicios) y cambiar la generación del token:

```csharp
        var roles = await userManager.GetRolesAsync(usuario);
        var permisos = MapaRolesPermisos.PermisosDe(roles);
        var verificado = await consultaKyc.EstaVerificadoAsync(usuario.Id, ct);
        var accessToken = generadorTokens.Generar(usuario, permisos, verificado);
```

En `RefreshAsync`, añadir el mismo parámetro `IConsultaVerificacionKyc consultaKyc` y cambiar:

```csharp
        var roles = await userManager.GetRolesAsync(usuario);
        var permisos = MapaRolesPermisos.PermisosDe(roles);
        var verificado = await consultaKyc.EstaVerificadoAsync(usuario.Id, ct);
        var accessToken = generadorTokens.Generar(usuario, permisos, verificado);
```

- [ ] **Step 5: Ejecutar y verificar que pasa**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~GeneradorTokensAccesoTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Domain src/Identity/CaseritoApp.Identity.Infrastructure/Auth src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs tests/CaseritoApp.UnitTests/Auth/GeneradorTokensAccesoTests.cs
git commit -m "feat(kyc): claim verificado en el JWT, calculado en login/refresh"
```

---

## Task 9: Badge `verificado` en el perfil

**Files:**
- Modify: `src/Identity/CaseritoApp.Identity.Application/Perfil/IRepositorioPerfil.cs`
- Modify: `src/Identity/CaseritoApp.Identity.Infrastructure/Perfil/RepositorioPerfilUserManager.cs`

**Interfaces:**
- Produces: `PerfilDto(Guid Id, string Email, string Nombre, string Ciudad, bool Verificado)` (campo `Verificado` añadido).

- [ ] **Step 1: Añadir `Verificado` al `PerfilDto`**

En `IRepositorioPerfil.cs`, cambiar la declaración del record:

```csharp
public sealed record PerfilDto(Guid Id, string Email, string Nombre, string Ciudad, bool Verificado);
```

- [ ] **Step 2: Poblar `Verificado` en el adaptador**

En `RepositorioPerfilUserManager.cs`:

Añadir el `using`:

```csharp
using CaseritoApp.Identity.Application.Kyc;
```

Cambiar la firma del constructor primario para inyectar la consulta:

```csharp
public sealed class RepositorioPerfilUserManager(
    UserManager<ApplicationUser> userManager, IConsultaVerificacionKyc consultaKyc)
    : IRepositorioPerfil
```

Cambiar `ObtenerAsync` para calcular y pasar el flag:

```csharp
    public async Task<PerfilDto?> ObtenerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            return null;
        }

        var verificado = await consultaKyc.EstaVerificadoAsync(usuario.Id, cancellationToken);
        return new PerfilDto(usuario.Id, usuario.Email ?? string.Empty, usuario.Nombre, usuario.Ciudad, verificado);
    }
```

- [ ] **Step 3: Compilar**

Run: `dotnet build CaseritoApp.sln`
Expected: BUILD succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Application/Perfil src/Identity/CaseritoApp.Identity.Infrastructure/Perfil
git commit -m "feat(kyc): badge verificado derivado en GET /api/perfil"
```

---

## Task 10: Endpoints HTTP (usuario + admin) y cableado

**Files:**
- Create: `src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs`
- Modify: `src/Host/CaseritoApp.Host/Program.cs`

**Interfaces:**
- Consumes: `EnviarSolicitudKycCommand`, `ObtenerEstadoKycQuery`, `ListarSolicitudesKycQuery`, `ObtenerBlobKycQuery`+`TipoBlobKyc`, `AprobarSolicitudKycCommand`, `RechazarSolicitudKycCommand`, `Permisos.KycRevisar`, `PoliticasAutorizacion.Permiso`.
- Produces: `MapKycEndpoints(this IEndpointRouteBuilder)`.

- [ ] **Step 1: Crear `KycEndpoints`**

`KycEndpoints.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure.Auth;
using FluentValidation;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>Cuerpo para rechazar una solicitud KYC.</summary>
public sealed record RechazarKycRequest(string Motivo);

/// <summary>Endpoints de KYC: subida y estado del usuario (<c>/api/kyc</c>) y gestión admin (<c>/api/admin/kyc</c>).</summary>
public static class KycEndpoints
{
    /// <summary>Mapea los grupos de KYC de usuario y de administrador.</summary>
    public static IEndpointRouteBuilder MapKycEndpoints(this IEndpointRouteBuilder app)
    {
        var usuario = app.MapGroup("/api/kyc").RequireAuthorization();
        usuario.MapPost("/", EnviarAsync).DisableAntiforgery();
        usuario.MapGet("/estado", EstadoAsync);

        var admin = app.MapGroup("/api/admin/kyc")
            .RequireAuthorization(PoliticasAutorizacion.Permiso(Permisos.KycRevisar));
        admin.MapGet("/", ListarAsync);
        admin.MapGet("/{solicitudId:guid}/documento", (Guid solicitudId, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => BlobAsync(solicitudId, TipoBlobKyc.Documento, u, s, ct));
        admin.MapGet("/{solicitudId:guid}/selfie", (Guid solicitudId, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => BlobAsync(solicitudId, TipoBlobKyc.Selfie, u, s, ct));
        admin.MapPost("/{solicitudId:guid}/aprobar", AprobarAsync);
        admin.MapPost("/{solicitudId:guid}/rechazar", RechazarAsync);

        return app;
    }

    private static async Task<IResult> EnviarAsync(
        IFormFile documento, IFormFile selfie, ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var docBytes = await LeerAsync(documento, ct);
        var selfieBytes = await LeerAsync(selfie, ct);

        try
        {
            var resultado = await sender.Send(
                new EnviarSolicitudKycCommand(
                    userId, docBytes, documento.ContentType, selfieBytes, selfie.ContentType), ct);
            return DesdeResult(resultado);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> EstadoAsync(ClaimsPrincipal usuario, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(usuario, out var userId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new ObtenerEstadoKycQuery(userId), ct);
        return resultado.EsExito ? Results.Ok(resultado.Valor) : DesdeResult(resultado);
    }

    private static async Task<IResult> ListarAsync(
        ISender sender, CancellationToken ct, string? estado = null, int pagina = 1, int tamano = 20)
    {
        try
        {
            var resultado = await sender.Send(new ListarSolicitudesKycQuery(estado, pagina, tamano), ct);
            return Results.Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<IResult> BlobAsync(
        Guid solicitudId, TipoBlobKyc tipo, ClaimsPrincipal admin, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(admin, out var adminId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new ObtenerBlobKycQuery(solicitudId, adminId, tipo), ct);
        return resultado.EsExito
            ? Results.File(resultado.Valor.Contenido, resultado.Valor.ContentType)
            : DesdeResult(resultado);
    }

    private static async Task<IResult> AprobarAsync(
        Guid solicitudId, ClaimsPrincipal admin, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(admin, out var adminId))
        {
            return Results.Unauthorized();
        }

        var resultado = await sender.Send(new AprobarSolicitudKycCommand(solicitudId, adminId), ct);
        return DesdeResult(resultado);
    }

    private static async Task<IResult> RechazarAsync(
        Guid solicitudId, RechazarKycRequest request, ClaimsPrincipal admin, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerUserId(admin, out var adminId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(
                new RechazarSolicitudKycCommand(solicitudId, adminId, request.Motivo), ct);
            return DesdeResult(resultado);
        }
        catch (ValidationException ex)
        {
            return ProblemaDeValidacion(ex);
        }
    }

    private static async Task<byte[]> LeerAsync(IFormFile archivo, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static bool TryObtenerUserId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(valor, out userId);
    }

    private static IResult ProblemaDeValidacion(ValidationException ex) =>
        Results.ValidationProblem(ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    // Mapea el Result de los casos de uso KYC a códigos HTTP.
    private static IResult DesdeResult(Result resultado)
    {
        if (resultado.EsExito)
        {
            return Results.NoContent();
        }

        return resultado.Error.Code switch
        {
            ErroresKyc.SolicitudNoEncontrada =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status404NotFound),
            ErroresKyc.YaVerificado or ErroresKyc.SolicitudPendienteExiste or ErroresKyc.TransicionInvalida =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status409Conflict),
            _ =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status400BadRequest),
        };
    }
}
```

- [ ] **Step 2: Registrar los validators de KYC y mapear los endpoints**

En `Program.cs`, tras `app.MapAdminEndpoints();` añadir:

```csharp
app.MapKycEndpoints();
```

Los validators ya se registran vía `AddValidatorsFromAssembly(typeof(ObtenerPerfilQuery).Assembly)` (mismo ensamblado `Identity.Application`), así que `EnviarSolicitudKycCommandValidator`, `RechazarSolicitudKycCommandValidator` y `ListarSolicitudesKycQueryValidator` se descubren automáticamente. No hace falta cambio adicional.

- [ ] **Step 3: Compilar**

Run: `dotnet build CaseritoApp.sln`
Expected: BUILD succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Host/CaseritoApp.Host
git commit -m "feat(kyc): endpoints /api/kyc (usuario) y /api/admin/kyc (admin) + cableado"
```

---

## Task 11: Test de integración del flujo completo

**Files:**
- Test: `tests/CaseritoApp.IntegrationTests/KycFlujoTests.cs`
- Modify: `tests/CaseritoApp.IntegrationTests/PerfilTests.cs` (asserta `verificado` en el JSON)

**Interfaces:**
- Consumes: `CaseritoApiFactory`, endpoints `/api/auth/*`, `/api/kyc/*`, `/api/admin/kyc/*`, `/api/perfil`. Patrón de token/Bearer como en `GestionRolesTests`.

- [ ] **Step 1: Escribir el test de flujo end-to-end**

Crear `tests/CaseritoApp.IntegrationTests/KycFlujoTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Flujo completo de KYC: subir → listar (admin) → aprobar → badge/claim verificado.</summary>
public sealed class KycFlujoTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string Email(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

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
        var doc = new ByteArrayContent(Png);
        doc.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        contenido.Add(doc, "documento", "ci.png");
        var selfie = new ByteArrayContent(Png);
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

    [Fact]
    public async Task Subir_aprobar_y_ver_verificado_en_perfil_y_token()
    {
        using var cliente = factory.CreateClient();
        var email = Email("kyc-user");
        var tokenUsuario = await RegistrarYLoguearAsync(cliente, email, rolExtra: null);
        var tokenAdmin = await RegistrarYLoguearAsync(cliente, Email("kyc-admin"), RolesApp.AdminKyc);

        // 1) Subir CI + selfie.
        using var subir = Autorizada(HttpMethod.Post, "/api/kyc/", tokenUsuario);
        subir.Content = Formulario();
        var respSubir = await cliente.SendAsync(subir);
        Assert.Equal(HttpStatusCode.NoContent, respSubir.StatusCode);

        // 2) Admin lista pendientes y localiza la solicitud del usuario.
        using var listar = Autorizada(HttpMethod.Get, "/api/admin/kyc/?estado=Pendiente&tamano=100", tokenAdmin);
        var pagina = await (await cliente.SendAsync(listar)).Content.ReadFromJsonAsync<PaginaKycResponse>();
        Assert.NotNull(pagina);

        Guid usuarioId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            usuarioId = (await userManager.FindByEmailAsync(email))!.Id;
        }
        var solicitud = pagina!.Items.Single(s => s.UsuarioId == usuarioId);

        // 3) Admin accede al blob (queda auditado) y aprueba.
        using var verDoc = Autorizada(HttpMethod.Get, $"/api/admin/kyc/{solicitud.SolicitudId}/documento", tokenAdmin);
        Assert.Equal(HttpStatusCode.OK, (await cliente.SendAsync(verDoc)).StatusCode);

        using var aprobar = Autorizada(HttpMethod.Post, $"/api/admin/kyc/{solicitud.SolicitudId}/aprobar", tokenAdmin);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(aprobar)).StatusCode);

        // 4) Perfil del usuario muestra verificado=true.
        using var perfil = Autorizada(HttpMethod.Get, "/api/perfil/", tokenUsuario);
        var perfilBody = await (await cliente.SendAsync(perfil)).Content.ReadFromJsonAsync<PerfilResponse>();
        Assert.True(perfilBody!.Verificado);

        // 5) Al re-loguear, el JWT trae claim verificado=true.
        var relogin = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        var tokenNuevo = (await relogin.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenNuevo);
        Assert.Equal("true", jwt.Claims.Single(c => c.Type == "verificado").Value);
    }

    [Fact]
    public async Task Subir_dos_veces_sin_resolver_da_409()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("kyc-dup"), rolExtra: null);

        using var primera = Autorizada(HttpMethod.Post, "/api/kyc/", token);
        primera.Content = Formulario();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(primera)).StatusCode);

        using var segunda = Autorizada(HttpMethod.Post, "/api/kyc/", token);
        segunda.Content = Formulario();
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.SendAsync(segunda)).StatusCode);
    }

    [Fact]
    public async Task Listar_kyc_sin_permiso_da_403()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("kyc-noperm"), rolExtra: null);

        using var listar = Autorizada(HttpMethod.Get, "/api/admin/kyc/", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.SendAsync(listar)).StatusCode);
    }
}

sealed file record PerfilResponse(Guid Id, string Email, string Nombre, string Ciudad, bool Verificado);

sealed file record SolicitudKycResponse(
    Guid SolicitudId, Guid UsuarioId, string Estado, string TipoDocumento, DateTimeOffset EnviadaEn, DateTimeOffset? ResueltaEn);

sealed file record PaginaKycResponse(SolicitudKycResponse[] Items, int Pagina, int Tamano, int Total);
```

- [ ] **Step 2: Ejecutar y verificar que pasan (requiere Docker)**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~KycFlujoTests`
Expected: PASS (3 tests). Requiere Docker para Testcontainers.

- [ ] **Step 3: Actualizar `PerfilTests` para asertar el campo `verificado`**

En `PerfilTests.cs`, en el/los tests que deserializan el perfil, asegurar que el record de respuesta local incluye `bool Verificado` y (donde el usuario no hizo KYC) asertar `Assert.False(perfil.Verificado)`. Leer el archivo, adaptar el record de respuesta local y añadir el assert donde se lee el perfil.

- [ ] **Step 4: Ejecutar toda la suite**

Run: `dotnet test CaseritoApp.sln`
Expected: PASS (suite completa; los ~83 previos + los nuevos de KYC).

- [ ] **Step 5: Verificar formato como el CI**

Run: `dotnet format CaseritoApp.sln --verify-no-changes`
Expected: sin cambios pendientes.

- [ ] **Step 6: Commit**

```bash
git add tests/CaseritoApp.IntegrationTests
git commit -m "test(kyc): flujo de integración completo (subida→aprobación→verificado) + perfil"
```

---

## Self-Review (completado por el autor del plan)

**Cobertura del spec:**
- §1 alcance backend → Tasks 2–11. ✅
- §2 dominio con historial + no re-verificación → Task 2 (tests incluidos). ✅
- §3 cifrado byte[] + IAlmacenBlobsKyc + disco → Tasks 1, 3, 7. ✅
- §4 casos de uso usuario/admin + compensación + auditoría → Tasks 4, 5. ✅
- §5 persistencia EF + migración + publicación UserVerified (puerto nuevo, sin dispatcher previo) → Tasks 3, 5, 6, 7. ✅
- §6 endpoints + policy `kyc.revisar` + validación multipart (magic bytes/tamaño/whitelist) + mapeo Result→HTTP → Tasks 4, 10. ✅
- §7 badge perfil + claim `verificado` (login/refresh) → Tasks 8, 9. ✅
- §8 anti-PII en logs + auditoría de acceso + retención (timestamps capturados; purga = follow-up) → Tasks 2, 5, 7 (logs sin PII, `[LoggerMessage]`). ✅
- §9 tests unit (máquina de estados, validación, handlers) + integración (flujo, audit, claim, badge, 409, 403) → Tasks 2, 4, 5, 11. ✅

**Consistencia de tipos:** `Generar(usuario, permisos, verificado)` usado igual en Tasks 8 (def) y consumido en login/refresh; `PerfilDto` con `Verificado` (Task 9) consumido por el test (Task 11); `IRepositorioVerificacionKyc`/`IAlmacenBlobsKyc`/`IConsultaVerificacionKyc`/`IAuditorAccesoPii`/`IPublicadorEventosIntegracion` definidos en Tasks 3/5 y adaptados en Tasks 6/7. `ErroresKyc.*` definidos en Task 2 y usados en el mapeo HTTP (Task 10). ✅

**Placeholders:** ninguno pendiente. Las dos adaptaciones sobre tests existentes (`GeneradorTokensAccesoTests`, `PerfilTests`) indican exactamente qué cambiar y por qué; requieren leer el archivo para respetar helpers locales, no inventar contenido.
