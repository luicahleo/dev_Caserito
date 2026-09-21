# Plan — Resolución automática de KYC

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA: usar
> `superpowers:subagent-driven-development` (recomendada) o
> `superpowers:executing-plans` para implementar tarea por tarea. Los pasos usan
> casillas (`- [ ]`) para seguimiento.

Spec: `docs/superpowers/specs/2026-09-19-kyc-resolucion-automatica-design.md`

**Objetivo:** que una solicitud de KYC se resuelva sola cuando la evidencia es
clara, y que todo lo demás quede encolado para revisión humana con el motivo
registrado, sin perder ningún intento.

**Arquitectura:** la regla de decisión vive en un tipo puro del dominio
(`PoliticaResolucionKyc`), que recibe el resultado de ARGOS traducido a
primitivos y el umbral, y devuelve una de tres resoluciones.
`EnviarSolicitudKycCommandHandler` la consulta y actúa: aprueba publicando los
mismos eventos que la aprobación manual, encola, o rechaza. El umbral entra por
un puerto de Application implementado en Infrastructure sobre `OpcionesArgos`.

**Stack:** .NET 10, EF Core sobre SQL Server, MediatR + Result +
FluentValidation, xUnit, Testcontainers.MsSql para integración.

## Restricciones globales

- Ejecutar todos los comandos `dotnet` desde `CaseritoApp/`.
- Trabajar directamente sobre `develop`. No crear ramas.
- Textos de UI y comentarios en español, con acentos, UTF-8. Nunca mojibake.
- Anti-PII: **el score de similitud nunca se escribe en logs.** Tampoco CI,
  nombre, correo, clave de blob, bytes ni content-type.
- `Identity.Application` **no** puede referenciar `Microsoft.Extensions.Options`
  ni ningún paquete de configuración. Su csproj solo referencia
  `Identity.Domain` y `BuildingBlocks.Application`.
- `Identity.Domain` no depende de nada hacia afuera.
- Versiones exclusivamente en `Directory.Packages.props`.
- Warnings como errores: el build falla ante cualquier advertencia.
- Antes de **cada** commit: `./verify.ps1 -Changed` desde la raíz. Prohibido
  `--no-verify`.
- Un cambio en `src/` sin cambio en `tests/` exige `[sin-test] <motivo>` en el
  mensaje del commit.
- El umbral por defecto es **60**, inclusivo (`score >= umbral` aprueba).

## Estructura de archivos

**Se crean**

| Ruta | Responsabilidad |
|---|---|
| `src/Identity/CaseritoApp.Identity.Domain/Kyc/ResolucionKyc.cs` | Enum de las tres resoluciones |
| `src/Identity/CaseritoApp.Identity.Domain/Kyc/MotivoRevisionKyc.cs` | Enum del motivo de encolado |
| `src/Identity/CaseritoApp.Identity.Domain/Kyc/PoliticaResolucionKyc.cs` | La regla pura, más `EntradaResolucionKyc` y `DecisionKyc` |
| `src/Identity/CaseritoApp.Identity.Application/Kyc/IOpcionesResolucionKyc.cs` | Puerto del umbral |
| `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesResolucionKycDesdeConfig.cs` | Adaptador sobre `OpcionesArgos` |
| `tests/CaseritoApp.UnitTests/Kyc/PoliticaResolucionKycTests.cs` | Tests de la regla |

**Se modifican**

| Ruta | Cambio |
|---|---|
| `src/Identity/CaseritoApp.Identity.Domain/Kyc/SolicitudKyc.cs` | Propiedad y método del motivo de revisión |
| `src/Identity/CaseritoApp.Identity.Application/Kyc/IRepositorioVerificacionKyc.cs` | Método de solo lectura para comprobar la huella |
| `src/Identity/CaseritoApp.Identity.Application/Kyc/EnviarSolicitudKycCommand.cs` | Reorden y aplicación de la política |
| `src/Identity/CaseritoApp.Identity.Application/Kyc/DtosKyc.cs` | Motivo en el resumen |
| `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesArgos.cs` | Propiedad del umbral |
| `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConfiguracionKyc.cs` | Mapeo del enum nuevo |
| `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/RepositorioVerificacionKycEfCore.cs` | Método nuevo y proyección |
| `src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` | Registro del puerto |
| `tests/CaseritoApp.UnitTests/Kyc/EnviarSolicitudKycCommandHandlerTests.cs` | Tests de las tres ramas |
| `tests/CaseritoApp.IntegrationTests/KycArgosFlujoTests.cs` | Test del 503 cambia de expectativa, más tests nuevos |

---

### Tarea 1: Política de resolución en el dominio

**Archivos**
- Crear: `src/Identity/CaseritoApp.Identity.Domain/Kyc/ResolucionKyc.cs`
- Crear: `src/Identity/CaseritoApp.Identity.Domain/Kyc/MotivoRevisionKyc.cs`
- Crear: `src/Identity/CaseritoApp.Identity.Domain/Kyc/PoliticaResolucionKyc.cs`
- Test: `tests/CaseritoApp.UnitTests/Kyc/PoliticaResolucionKycTests.cs`

**Interfaces**
- Consume: nada.
- Produce: `ResolucionKyc`, `MotivoRevisionKyc`,
  `EntradaResolucionKyc(bool ServicioRespondio, bool RostroDetectado, bool Coinciden, double? Score)`,
  `DecisionKyc(ResolucionKyc Resolucion, MotivoRevisionKyc? Motivo)` y
  `PoliticaResolucionKyc.Decidir(EntradaResolucionKyc entrada, double umbralAutoAprobacion)`.

- [ ] **Paso 1: escribir el test que falla**

Crear `tests/CaseritoApp.UnitTests/Kyc/PoliticaResolucionKycTests.cs`:

```csharp
using CaseritoApp.Identity.Domain.Kyc;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class PoliticaResolucionKycTests
{
    private const double Umbral = 60;

    private static EntradaResolucionKyc Ok(double score) =>
        new(ServicioRespondio: true, RostroDetectado: true, Coinciden: true, Score: score);

    [Fact]
    public void Score_por_encima_del_umbral_aprueba_automaticamente()
    {
        var decision = PoliticaResolucionKyc.Decidir(Ok(85), Umbral);

        Assert.Equal(ResolucionKyc.AprobarAutomatico, decision.Resolucion);
        Assert.Null(decision.Motivo);
    }

    [Fact]
    public void Score_igual_al_umbral_aprueba_automaticamente()
    {
        // El límite es inclusivo por decisión del spec; este test lo fija.
        var decision = PoliticaResolucionKyc.Decidir(Ok(60), Umbral);

        Assert.Equal(ResolucionKyc.AprobarAutomatico, decision.Resolucion);
    }

    [Fact]
    public void Score_por_debajo_del_umbral_va_a_revision()
    {
        var decision = PoliticaResolucionKyc.Decidir(Ok(59.9), Umbral);

        Assert.Equal(ResolucionKyc.EnviarARevision, decision.Resolucion);
        Assert.Equal(MotivoRevisionKyc.ScoreInsuficiente, decision.Motivo);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(10)]
    public void Sin_coincidencia_rechaza_automaticamente_sea_cual_sea_el_score(double score)
    {
        var entrada = new EntradaResolucionKyc(
            ServicioRespondio: true, RostroDetectado: true, Coinciden: false, Score: score);

        var decision = PoliticaResolucionKyc.Decidir(entrada, Umbral);

        Assert.Equal(ResolucionKyc.RechazarAutomatico, decision.Resolucion);
        Assert.Null(decision.Motivo);
    }

    [Fact]
    public void Rostro_no_detectado_va_a_revision_con_su_motivo()
    {
        var entrada = new EntradaResolucionKyc(
            ServicioRespondio: true, RostroDetectado: false, Coinciden: false, Score: null);

        var decision = PoliticaResolucionKyc.Decidir(entrada, Umbral);

        Assert.Equal(ResolucionKyc.EnviarARevision, decision.Resolucion);
        Assert.Equal(MotivoRevisionKyc.RostroNoDetectado, decision.Motivo);
    }

    [Fact]
    public void Servicio_sin_responder_va_a_revision_con_su_motivo()
    {
        var entrada = new EntradaResolucionKyc(
            ServicioRespondio: false, RostroDetectado: false, Coinciden: false, Score: null);

        var decision = PoliticaResolucionKyc.Decidir(entrada, Umbral);

        Assert.Equal(ResolucionKyc.EnviarARevision, decision.Resolucion);
        Assert.Equal(MotivoRevisionKyc.ServicioNoDisponible, decision.Motivo);
    }

    [Fact]
    public void Coincidencia_sin_score_va_a_revision()
    {
        // Defensa: si ARGOS dijera que coincide pero no diera score, no se aprueba sola.
        var entrada = new EntradaResolucionKyc(
            ServicioRespondio: true, RostroDetectado: true, Coinciden: true, Score: null);

        var decision = PoliticaResolucionKyc.Decidir(entrada, Umbral);

        Assert.Equal(ResolucionKyc.EnviarARevision, decision.Resolucion);
        Assert.Equal(MotivoRevisionKyc.ScoreInsuficiente, decision.Motivo);
    }
}
```

- [ ] **Paso 2: ejecutar el test y verlo fallar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~PoliticaResolucionKycTests
```

Esperado: error de compilación, no existen `PoliticaResolucionKyc`,
`ResolucionKyc`, `MotivoRevisionKyc`, `EntradaResolucionKyc` ni `DecisionKyc`.

- [ ] **Paso 3: crear los dos enums**

`src/Identity/CaseritoApp.Identity.Domain/Kyc/ResolucionKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>Qué hacer con una solicitud recién enviada, según la evidencia facial.</summary>
public enum ResolucionKyc
{
    /// <summary>Evidencia clara a favor: se aprueba sin intervención humana.</summary>
    AprobarAutomatico,

    /// <summary>Evidencia insuficiente o no concluyente: la revisa una persona.</summary>
    EnviarARevision,

    /// <summary>Evidencia clara en contra: se rechaza sin intervención humana.</summary>
    RechazarAutomatico,
}
```

`src/Identity/CaseritoApp.Identity.Domain/Kyc/MotivoRevisionKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Por qué una solicitud quedó esperando revisión humana. Es una categoría técnica: no describe
/// a la persona y puede registrarse en logs.
/// </summary>
public enum MotivoRevisionKyc
{
    /// <summary>El rostro coincide, pero el parecido no alcanza el umbral de aprobación automática.</summary>
    ScoreInsuficiente,

    /// <summary>No se pudo detectar un rostro en alguna de las dos imágenes.</summary>
    RostroNoDetectado,

    /// <summary>El servicio de comparación facial no respondió o falló al procesar.</summary>
    ServicioNoDisponible,
}
```

- [ ] **Paso 4: crear la política**

`src/Identity/CaseritoApp.Identity.Domain/Kyc/PoliticaResolucionKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Resultado de la comparación facial, traducido a primitivos para que el dominio no dependa
/// del adaptador HTTP ni de sus tipos.
/// </summary>
/// <param name="ServicioRespondio">false si el servicio no respondió o falló al procesar.</param>
/// <param name="RostroDetectado">false si no se detectó rostro en alguna imagen.</param>
/// <param name="Coinciden">Veredicto del servicio, con su propio umbral ya aplicado.</param>
/// <param name="Score">Similitud 0-100, o null si no hubo comparación.</param>
public sealed record EntradaResolucionKyc(
    bool ServicioRespondio,
    bool RostroDetectado,
    bool Coinciden,
    double? Score);

/// <summary>Decisión tomada: qué resolución aplicar y, si queda en revisión, por qué.</summary>
public sealed record DecisionKyc(ResolucionKyc Resolucion, MotivoRevisionKyc? Motivo);

/// <summary>
/// Regla que convierte el resultado de la comparación facial en una resolución. El corte inferior
/// lo decide el servicio externo con su propio umbral; aquí solo se añade el corte superior a
/// partir del cual la aprobación es automática.
/// </summary>
public static class PoliticaResolucionKyc
{
    public static DecisionKyc Decidir(EntradaResolucionKyc entrada, double umbralAutoAprobacion)
    {
        if (!entrada.ServicioRespondio)
        {
            return new DecisionKyc(ResolucionKyc.EnviarARevision, MotivoRevisionKyc.ServicioNoDisponible);
        }

        if (!entrada.RostroDetectado)
        {
            return new DecisionKyc(ResolucionKyc.EnviarARevision, MotivoRevisionKyc.RostroNoDetectado);
        }

        if (!entrada.Coinciden)
        {
            return new DecisionKyc(ResolucionKyc.RechazarAutomatico, null);
        }

        return entrada.Score is { } score && score >= umbralAutoAprobacion
            ? new DecisionKyc(ResolucionKyc.AprobarAutomatico, null)
            : new DecisionKyc(ResolucionKyc.EnviarARevision, MotivoRevisionKyc.ScoreInsuficiente);
    }
}
```

- [ ] **Paso 5: ejecutar el test y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~PoliticaResolucionKycTests
```

Esperado: 8 tests en verde.

- [ ] **Paso 6: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/ResolucionKyc.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/MotivoRevisionKyc.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/PoliticaResolucionKyc.cs CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/PoliticaResolucionKycTests.cs
git commit -m "feat(kyc): modela la politica de resolucion automatica"
```

---

### Tarea 2: Motivo de revisión en la solicitud

**Archivos**
- Modificar: `src/Identity/CaseritoApp.Identity.Domain/Kyc/SolicitudKyc.cs`
- Modificar: `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConfiguracionKyc.cs`
- Crear: migración `KycMotivoRevision`
- Test: `tests/CaseritoApp.UnitTests/Kyc/SolicitudKycTests.cs` (crear si no existe)

**Interfaces**
- Consume: `MotivoRevisionKyc` de la Tarea 1.
- Produce: `SolicitudKyc.MotivoRevision` (`MotivoRevisionKyc?`) y
  `SolicitudKyc.RegistrarMotivoRevision(MotivoRevisionKyc motivo)`.

- [ ] **Paso 1: escribir el test que falla**

Añadir a `tests/CaseritoApp.UnitTests/Kyc/SolicitudKycTests.cs` (crearlo con
este contenido si no existe):

```csharp
using CaseritoApp.Identity.Domain.Kyc;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class SolicitudKycTests
{
    [Fact]
    public void Solicitud_nueva_no_tiene_motivo_de_revision()
    {
        var verificacion = VerificacionKyc.Crear(Guid.NewGuid());
        var solicitud = verificacion.EnviarSolicitud(
            "doc", "selfie", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow).Valor;

        Assert.Null(solicitud.MotivoRevision);
    }

    [Fact]
    public void Registrar_motivo_de_revision_lo_conserva()
    {
        var verificacion = VerificacionKyc.Crear(Guid.NewGuid());
        var solicitud = verificacion.EnviarSolicitud(
            "doc", "selfie", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow).Valor;

        solicitud.RegistrarMotivoRevision(MotivoRevisionKyc.RostroNoDetectado);

        Assert.Equal(MotivoRevisionKyc.RostroNoDetectado, solicitud.MotivoRevision);
    }
}
```

- [ ] **Paso 2: ejecutar el test y verlo fallar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~SolicitudKycTests
```

Esperado: error de compilación, `MotivoRevision` y `RegistrarMotivoRevision` no
existen.

- [ ] **Paso 3: añadir la propiedad y el método**

En `SolicitudKyc.cs`, junto a `ScoreSimilitud`:

```csharp
    /// <summary>Por qué la solicitud quedó esperando revisión humana. Null si no aplica.</summary>
    public MotivoRevisionKyc? MotivoRevision { get; private set; }

    /// <summary>Registra el motivo del encolado. No se limpia al resolver: es traza histórica.</summary>
    public void RegistrarMotivoRevision(MotivoRevisionKyc motivo)
    {
        MotivoRevision = motivo;
    }
```

- [ ] **Paso 4: mapear la columna**

En `ConfiguracionKyc.cs`, dentro de `builder.Entity<SolicitudKyc>(e => {...})`,
justo debajo de la línea de `ScoreSimilitud`:

```csharp
            e.Property(s => s.MotivoRevision).HasConversion<string>().HasMaxLength(30);
```

Nullable a propósito: sin `IsRequired`.

- [ ] **Paso 5: generar la migración**

Desde `CaseritoApp/`:

```powershell
dotnet ef migrations add KycMotivoRevision --project src/Identity/CaseritoApp.Identity.Infrastructure --startup-project src/Host/CaseritoApp.Host --output-dir Migrations
```

Revisar el archivo generado: debe contener **solo** `AddColumn<string>` de
`MotivoRevision` en `SolicitudesKyc` (schema `identity`), con `maxLength: 30` y
`nullable: true`. Si contiene cualquier otra cosa, borrar la migración,
averiguar por qué y repetir.

- [ ] **Paso 6: ejecutar el test y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~SolicitudKycTests
```

Esperado: 2 tests en verde.

- [ ] **Paso 7: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src/Identity CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/SolicitudKycTests.cs
git commit -m "feat(kyc): registra el motivo de revision de una solicitud"
```

---

### Tarea 3: Umbral configurable

**Archivos**
- Crear: `src/Identity/CaseritoApp.Identity.Application/Kyc/IOpcionesResolucionKyc.cs`
- Crear: `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesResolucionKycDesdeConfig.cs`
- Modificar: `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesArgos.cs`
- Modificar: `src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`
- Test: `tests/CaseritoApp.UnitTests/Kyc/OpcionesResolucionKycDesdeConfigTests.cs`

**Interfaces**
- Consume: nada de tareas anteriores.
- Produce: `IOpcionesResolucionKyc` con la propiedad
  `double UmbralAutoAprobacionSimilitud { get; }`, que consume la Tarea 5.

- [ ] **Paso 1: escribir el test que falla**

Crear `tests/CaseritoApp.UnitTests/Kyc/OpcionesResolucionKycDesdeConfigTests.cs`:

```csharp
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Infrastructure.Kyc;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class OpcionesResolucionKycDesdeConfigTests
{
    [Fact]
    public void Expone_el_umbral_configurado()
    {
        IOpcionesResolucionKyc opciones = new OpcionesResolucionKycDesdeConfig(
            Options.Create(new OpcionesArgos { UmbralAutoAprobacion = 72.5 }));

        Assert.Equal(72.5, opciones.UmbralAutoAprobacionSimilitud);
    }

    [Fact]
    public void Sin_configuracion_explicita_el_umbral_por_defecto_es_60()
    {
        IOpcionesResolucionKyc opciones = new OpcionesResolucionKycDesdeConfig(
            Options.Create(new OpcionesArgos()));

        Assert.Equal(60, opciones.UmbralAutoAprobacionSimilitud);
    }
}
```

- [ ] **Paso 2: ejecutar el test y verlo fallar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~OpcionesResolucionKycDesdeConfigTests
```

Esperado: error de compilación, no existen `IOpcionesResolucionKyc`,
`OpcionesResolucionKycDesdeConfig` ni `OpcionesArgos.UmbralAutoAprobacion`.

- [ ] **Paso 3: crear el puerto**

`src/Identity/CaseritoApp.Identity.Application/Kyc/IOpcionesResolucionKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Umbral de similitud a partir del cual una verificación se aprueba sin intervención humana.
/// Es un puerto porque Application no referencia paquetes de configuración.
/// </summary>
public interface IOpcionesResolucionKyc
{
    /// <summary>Similitud mínima (0-100) para aprobar automáticamente. Inclusivo.</summary>
    public double UmbralAutoAprobacionSimilitud { get; }
}
```

- [ ] **Paso 4: añadir la propiedad de configuración**

En `OpcionesArgos.cs`, dentro de la clase:

```csharp
    /// <summary>
    /// Similitud mínima (0-100) para aprobar una verificación sin revisión humana. ARGOS corta la
    /// no-coincidencia en 32; este umbral es el corte superior propio de la aplicación.
    /// </summary>
    public double UmbralAutoAprobacion { get; set; } = 60;
```

- [ ] **Paso 5: crear el adaptador**

`src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesResolucionKycDesdeConfig.cs`:

```csharp
using CaseritoApp.Identity.Application.Kyc;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Lee el umbral de auto-aprobación de la sección de configuración de ARGOS.</summary>
public sealed class OpcionesResolucionKycDesdeConfig(IOptions<OpcionesArgos> opciones)
    : IOpcionesResolucionKyc
{
    public double UmbralAutoAprobacionSimilitud => opciones.Value.UmbralAutoAprobacion;
}
```

- [ ] **Paso 6: registrar en DI**

En `DependencyInjection.cs` de `Identity.Infrastructure`, junto al resto de
registros de KYC (cerca de la línea 96, donde se registra
`AddHttpClient<IVerificadorIdentidadArgos, ...>`):

```csharp
        servicios.AddSingleton<IOpcionesResolucionKyc, OpcionesResolucionKycDesdeConfig>();
```

- [ ] **Paso 7: ejecutar el test y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~OpcionesResolucionKycDesdeConfigTests
```

Esperado: 2 tests en verde.

- [ ] **Paso 8: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src/Identity CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/OpcionesResolucionKycDesdeConfigTests.cs
git commit -m "feat(kyc): hace configurable el umbral de aprobacion automatica"
```

---

### Tarea 4: Corregir el orden de reserva del CI

Esta tarea **no** cambia el comportamiento visible: corrige un defecto por el
cual la reserva del CI se persistía aunque el envío fallara, porque
`UnitOfWorkBehavior` invoca `GuardarCambiosAsync` sin mirar si el `Result` fue
éxito o fallo.

**Archivos**
- Modificar: `src/Identity/CaseritoApp.Identity.Application/Kyc/IRepositorioVerificacionKyc.cs`
- Modificar: `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/RepositorioVerificacionKycEfCore.cs`
- Modificar: `src/Identity/CaseritoApp.Identity.Application/Kyc/EnviarSolicitudKycCommand.cs`
- Test: `tests/CaseritoApp.IntegrationTests/KycArgosFlujoTests.cs`

**Interfaces**
- Consume: nada de tareas anteriores.
- Produce: `IRepositorioVerificacionKyc.HuellaPerteneceAOtroUsuarioAsync(string huella, Guid usuarioId, CancellationToken ct)`
  que devuelve `Task<bool>`.

- [ ] **Paso 1: escribir el test de regresión que falla**

Añadir a `KycArgosFlujoTests.cs`. Usa los helpers privados que ya existen en esa
clase (`Email`, `Formulario`, `Autorizada`, `RegistrarYLoguearAsync`) y el fake
`VerificadorArgosEstatico` del final del archivo:

```csharp
    [Fact]
    public async Task Envio_rechazado_por_el_dominio_no_deja_el_ci_reservado()
    {
        using var cliente = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ =>
                new VerificadorArgosEstatico(Result.Exito(
                    new VerificacionFacialResultado(Coinciden: true, SimilitudPercent: 95, MotivoRechazo: null))));
        })).CreateClient();

        var email = Email("kyc-reserva");
        var token = await RegistrarYLoguearAsync(cliente, email, null);
        const string ci = "1234599";

        // Primer envío: queda pendiente y reserva el CI legítimamente.
        using var primero = Autorizada(
            HttpMethod.Post, $"/api/kyc/?numeroCi={ci}&departamentoExpedicion=LaPaz", token);
        primero.Content = Formulario();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(primero)).StatusCode);

        // Segundo envío del mismo usuario: el dominio lo rechaza por solicitud pendiente.
        using var segundo = Autorizada(
            HttpMethod.Post, $"/api/kyc/?numeroCi={ci}&departamentoExpedicion=LaPaz", token);
        segundo.Content = Formulario();
        var respuesta = await cliente.SendAsync(segundo);
        Assert.NotEqual(HttpStatusCode.NoContent, respuesta.StatusCode);

        // Un usuario distinto que presenta el mismo CI debe seguir recibiendo conflicto,
        // y un CI nunca presentado con éxito no debe quedar bloqueado por un envío fallido.
        var otroEmail = Email("kyc-reserva-otro");
        var otroToken = await RegistrarYLoguearAsync(cliente, otroEmail, null);
        using var tercero = Autorizada(
            HttpMethod.Post, "/api/kyc/?numeroCi=1234598&departamentoExpedicion=LaPaz", otroToken);
        tercero.Content = Formulario();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(tercero)).StatusCode);
    }
```

- [ ] **Paso 2: ejecutar el test y verlo fallar**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~Envio_rechazado_por_el_dominio_no_deja_el_ci_reservado
```

Requiere Docker en marcha (Testcontainers.MsSql).

Esperado: falla. El segundo envío persiste una reserva pese a devolver error, o
el estado queda inconsistente.

- [ ] **Paso 3: añadir el método de solo lectura al puerto**

En `IRepositorioVerificacionKyc.cs`, junto a `ReservarDocumentoAsync`:

```csharp
    /// <summary>
    /// Indica si la huella del CI ya está registrada a nombre de OTRO usuario. Solo lectura: no
    /// reserva nada, a diferencia de <see cref="ReservarDocumentoAsync"/>.
    /// </summary>
    public Task<bool> HuellaPerteneceAOtroUsuarioAsync(string huella, Guid usuarioId, CancellationToken ct) =>
        Task.FromResult(false);
```

El cuerpo por defecto mantiene compilando los fakes de los tests, igual que ya
hacen los otros métodos de este puerto.

- [ ] **Paso 4: implementarlo en EF Core**

En `RepositorioVerificacionKycEfCore.cs`, junto a `ReservarDocumentoAsync`:

```csharp
    public async Task<bool> HuellaPerteneceAOtroUsuarioAsync(
        string huella, Guid usuarioId, CancellationToken ct)
    {
        var existente = await db.DocumentosKycRegistrados
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.HuellaCi == huella, ct);
        return existente is not null && existente.UsuarioId != usuarioId;
    }
```

- [ ] **Paso 5: reordenar el handler**

En `EnviarSolicitudKycCommand.cs`, sustituir el bloque que hoy empieza en la
línea 60 (`if (!await repositorio.ReservarDocumentoAsync(...)`) por una
comprobación de solo lectura:

```csharp
        if (await repositorio.HuellaPerteneceAOtroUsuarioAsync(
            protegido.HuellaCi, request.UsuarioId, cancellationToken))
        {
            RegistrarEnvio(logger, request.UsuarioId, "Kyc.DocumentoEnUso");
            return Result.Fallo(new Error("Kyc.DocumentoEnUso", "No se pudo registrar el documento."));
        }
```

Y mover la reserva real a **después** de que el dominio acepte la solicitud, es
decir inmediatamente después del bloque
`if (!resultado.EsExito) { ...borra blobs y devuelve fallo... }`:

```csharp
        var solicitud = resultado.Valor;

        await repositorio.ReservarDocumentoAsync(
            new DocumentoKycRegistrado(
                request.UsuarioId, protegido.HuellaCi, protegido.NumeroCiCifrado,
                protegido.ComplementoCiCifrado, protegido.DepartamentoExpedicion,
                tiempo.GetUtcNow()),
            cancellationToken);
```

Se conserva `ReservarDocumentoAsync` sin comprobar su retorno: en este punto ya
se sabe que la huella no es de otro usuario, y su comprobación interna evita una
condición de carrera entre ambos momentos.

- [ ] **Paso 6: ejecutar el test y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~Envio_rechazado_por_el_dominio_no_deja_el_ci_reservado
```

Esperado: verde.

- [ ] **Paso 7: ejecutar los tests vecinos de KYC**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~Kyc
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~Kyc
```

Esperado: todo verde. Si algo falla aquí, es por el reorden: arreglarlo antes de
continuar, no seguir a la Tarea 5.

- [ ] **Paso 8: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src/Identity CaseritoApp/tests/CaseritoApp.IntegrationTests/KycArgosFlujoTests.cs
git commit -m "fix(kyc): reserva el CI solo cuando la solicitud se persiste"
```

---

### Tarea 5: Aplicar la política en el envío

**Archivos**
- Modificar: `src/Identity/CaseritoApp.Identity.Application/Kyc/EnviarSolicitudKycCommand.cs`
- Test: `tests/CaseritoApp.UnitTests/Kyc/EnviarSolicitudKycCommandHandlerTests.cs`

**Interfaces**
- Consume: `PoliticaResolucionKyc.Decidir` (Tarea 1),
  `SolicitudKyc.RegistrarMotivoRevision` (Tarea 2),
  `IOpcionesResolucionKyc.UmbralAutoAprobacionSimilitud` (Tarea 3).
- Produce: el comportamiento final del caso de uso. No expone tipos nuevos.

- [ ] **Paso 1: escribir los tests que fallan**

En `EnviarSolicitudKycCommandHandlerTests.cs`. Añadir `using MediatR;` a la
cabecera del archivo (hace falta para `IPublisher` e `INotification`); el resto
de `using` necesarios ya están.

El fake de repositorio de esa clase (`RepoFake`) y el `PublicadorFake` ya
existen; añadir un fake de opciones dentro de la clase de tests:

```csharp
    private sealed class OpcionesFake(double umbral) : IOpcionesResolucionKyc
    {
        public double UmbralAutoAprobacionSimilitud => umbral;
    }
```

Y los tests de las tres ramas. Construir el handler con las dependencias que
tenga tras el cambio, en el mismo estilo que los tests ya presentes en el
archivo:

```csharp
    [Fact]
    public async Task Score_alto_aprueba_y_publica_los_dos_eventos()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Exito(
            new VerificacionFacialResultado(Coinciden: true, SimilitudPercent: 90, MotivoRechazo: null)));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoKyc.Aprobada, repo.Agregada!.Solicitudes.Single().Estado);
        Assert.Equal(SistemaActor.Id, repo.Agregada.Solicitudes.Single().ResueltaPor);
        Assert.Contains(publicador.Eventos, e => e is UserVerified);
    }

    [Fact]
    public async Task Score_bajo_deja_pendiente_con_motivo_y_no_publica()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Exito(
            new VerificacionFacialResultado(Coinciden: true, SimilitudPercent: 40, MotivoRechazo: null)));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        var solicitud = repo.Agregada!.Solicitudes.Single();
        Assert.Equal(EstadoKyc.Pendiente, solicitud.Estado);
        Assert.Equal(MotivoRevisionKyc.ScoreInsuficiente, solicitud.MotivoRevision);
        Assert.DoesNotContain(publicador.Eventos, e => e is UserVerified);
    }

    [Fact]
    public async Task Argos_caido_deja_pendiente_conserva_blobs_y_devuelve_exito()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Fallo<VerificacionFacialResultado>(
            new Error(ErroresKyc.ServicioVerificacionNoDisponible, "No disponible")));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        var solicitud = repo.Agregada!.Solicitudes.Single();
        Assert.Equal(EstadoKyc.Pendiente, solicitud.Estado);
        Assert.Equal(MotivoRevisionKyc.ServicioNoDisponible, solicitud.MotivoRevision);
        Assert.Null(solicitud.ScoreSimilitud);
        Assert.Equal(0, almacen.Eliminados);
    }

    [Fact]
    public async Task Rostro_no_detectado_deja_pendiente_con_su_motivo()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Fallo<VerificacionFacialResultado>(
            new Error(ErroresKyc.RostroNoDetectado, "No se detecto rostro")));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        Assert.Equal(
            MotivoRevisionKyc.RostroNoDetectado,
            repo.Agregada!.Solicitudes.Single().MotivoRevision);
        Assert.Equal(0, almacen.Eliminados);
    }

    [Fact]
    public async Task Sin_coincidencia_rechaza_y_no_publica_verificacion()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Exito(
            new VerificacionFacialResultado(Coinciden: false, SimilitudPercent: 10, MotivoRechazo: "no coincide")));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoKyc.Rechazada, repo.Agregada!.Solicitudes.Single().Estado);
        Assert.DoesNotContain(publicador.Eventos, e => e is UserVerified);
    }
```

Añadir los dos helpers privados a la clase de tests, para no repetir el armado:

```csharp
    private static EnviarSolicitudKycCommand ComandoValido() =>
        new(Guid.NewGuid(), [1, 2, 3], "image/png", [4, 5, 6], "image/png");

    private static EnviarSolicitudKycCommandHandler CrearHandler(
        RepoFake repo,
        AlmacenFake almacen,
        VerificadorFake verificador,
        PublicadorFake publicador,
        IOpcionesResolucionKyc opciones) =>
        // PublicadorFake cubre tres puertos: protector de documento, publicador de
        // integración y IPublisher. Por eso aparece tres veces seguidas.
        new(repo, almacen, verificador, publicador, publicador, publicador, opciones,
            TimeProvider.System, NullLogger<EnviarSolicitudKycCommandHandler>.Instance);
```

`PublicadorFake` hoy declara `IPublicadorEventosIntegracion, IProtectorDocumentoKyc`
pero **no** `IPublisher`. Ampliar su declaración y añadirle estos dos métodos,
que es lo que exige la interfaz de MediatR:

```csharp
    private sealed class PublicadorFake
        : IPublicadorEventosIntegracion, IProtectorDocumentoKyc, IPublisher
    {
        // ... miembros existentes sin cambios ...

        public List<object> Notificaciones { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Notificaciones.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Notificaciones.Add(notification!);
            return Task.CompletedTask;
        }
    }
```

En el test de aprobación, comprobar también el evento de dominio:

```csharp
        Assert.Contains(publicador.Notificaciones, n => n is KycResuelto);
```

**Nota para el implementador:** los tests ya existentes en este archivo
construyen el handler posicionalmente con la firma vieja —por ejemplo
`new EnviarSolicitudKycCommandHandler(repo, almacen, verificador, publicadorFake, TimeProvider.System, NullLogger<...>.Instance)`—
y dejarán de compilar. Adaptarlos al helper `CrearHandler`. El test
`Envio_con_argos_caido_compensa_blobs_y_devuelve_ServicioVerificacionNoDisponible`
(línea 158) describe el comportamiento **anterior** y queda obsoleto: se elimina,
porque `Argos_caido_deja_pendiente_conserva_blobs_y_devuelve_exito` lo
reemplaza.

- [ ] **Paso 2: ejecutar los tests y verlos fallar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~EnviarSolicitudKycCommandHandlerTests
```

Esperado: error de compilación por la firma del handler.

- [ ] **Paso 3: ampliar las dependencias del handler**

En `EnviarSolicitudKycCommand.cs`, la declaración del handler pasa a:

```csharp
public sealed partial class EnviarSolicitudKycCommandHandler(
    IRepositorioVerificacionKyc repositorio,
    IAlmacenBlobsKyc almacen,
    IVerificadorIdentidadArgos verificador,
    IProtectorDocumentoKyc protectorDocumento,
    IPublicadorEventosIntegracion publicador,
    IPublisher publisher,
    IOpcionesResolucionKyc opcionesResolucion,
    TimeProvider tiempo,
    ILogger<EnviarSolicitudKycCommandHandler> logger)
    : ICommandHandler<EnviarSolicitudKycCommand>
```

Añadir los `using` necesarios: `CaseritoApp.BuildingBlocks.Contracts.Identity` y
`MediatR`.

- [ ] **Paso 4: traducir el resultado de ARGOS a la entrada de la política**

Sustituir el bloque que hoy devuelve fallo cuando ARGOS no responde
(`if (!resultadoArgos.EsExito) { ...borra blobs... return Result.Fallo(...); }`)
por la traducción, **sin borrar los blobs**:

```csharp
        var entrada = resultadoArgos.EsExito
            ? new EntradaResolucionKyc(
                ServicioRespondio: true,
                RostroDetectado: true,
                Coinciden: resultadoArgos.Valor.Coinciden,
                Score: resultadoArgos.Valor.SimilitudPercent)
            : new EntradaResolucionKyc(
                ServicioRespondio: resultadoArgos.Error.Code == ErroresKyc.RostroNoDetectado,
                RostroDetectado: false,
                Coinciden: false,
                Score: null);
```

El servicio «respondió» cuando devolvió un diagnóstico sobre la imagen
(`RostroNoDetectado`); cualquier otro error —incluido
`VerificacionFacialFallida`— se trata como servicio no disponible.

- [ ] **Paso 5: aplicar la decisión**

Después de crear la solicitud y reservar el documento (Tarea 4), reemplazar el
bloque actual de resolución por:

```csharp
        var decision = PoliticaResolucionKyc.Decidir(
            entrada, opcionesResolucion.UmbralAutoAprobacionSimilitud);

        if (entrada.Score is { } score)
        {
            solicitud.RegistrarScoreSimilitud(score);
        }

        if (decision.Motivo is { } motivo)
        {
            solicitud.RegistrarMotivoRevision(motivo);
        }

        var ahora = tiempo.GetUtcNow();
        var resolucion = decision.Resolucion switch
        {
            ResolucionKyc.AprobarAutomatico =>
                verificacion.Aprobar(solicitud.Id, SistemaActor.Id, ahora),
            ResolucionKyc.RechazarAutomatico =>
                verificacion.Rechazar(
                    solicitud.Id, SistemaActor.Id, "La validación facial no fue satisfactoria.", ahora),
            _ => Result.Exito(),
        };

        if (!resolucion.EsExito)
        {
            // Estado inconsistente: la solicitud acaba de crearse y debe ser resoluble.
            await almacen.EliminarAsync(claveDoc, cancellationToken);
            await almacen.EliminarAsync(claveSelfie, cancellationToken);
            RegistrarEnvio(logger, request.UsuarioId, resolucion.Error.Code);
            return Result.Fallo(resolucion.Error);
        }

        if (esNueva)
        {
            repositorio.Agregar(verificacion);
        }

        if (decision.Resolucion == ResolucionKyc.AprobarAutomatico)
        {
            await publicador.PublicarAsync(
                new UserVerified(Guid.NewGuid(), ahora, verificacion.UsuarioId), cancellationToken);
            await publisher.Publish(
                new KycResuelto(
                    Guid.NewGuid(), ahora, verificacion.UsuarioId, solicitud.Id, EstadoKyc.Aprobada, null),
                cancellationToken);
        }

        RegistrarEnvio(logger, request.UsuarioId, EtiquetaAuditoria(decision));
        return Result.Exito();
```

Y añadir el traductor de etiqueta al final de la clase, junto a
`RegistrarEnvio`:

```csharp
    // Etiquetas de auditoría. Nunca incluyen el score: es un dato biométrico derivado.
    private static string EtiquetaAuditoria(DecisionKyc decision) => decision switch
    {
        { Resolucion: ResolucionKyc.AprobarAutomatico } => "aprobado-automatico",
        { Resolucion: ResolucionKyc.RechazarAutomatico } => "rechazado-automatico",
        { Motivo: MotivoRevisionKyc.RostroNoDetectado } => "pendiente-rostro-no-detectado",
        { Motivo: MotivoRevisionKyc.ServicioNoDisponible } => "pendiente-servicio",
        _ => "pendiente-score",
    };
```

- [ ] **Paso 6: actualizar el comentario XML del handler**

El comentario actual (líneas 30-33) describe un comportamiento que hasta ahora
no existía. Dejarlo así:

```csharp
/// <summary>
/// Handler: valida invariants, guarda los blobs cifrados, consulta a ARGOS y aplica la política de
/// resolución. Aprueba o rechaza automáticamente cuando la evidencia es clara y deja la solicitud
/// pendiente de revisión humana en cualquier otro caso, conservando las imágenes. Solo publica
/// <see cref="UserVerified"/> y <see cref="KycResuelto"/> en la aprobación automática.
/// </summary>
```

- [ ] **Paso 7: ejecutar los tests y verlos pasar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~EnviarSolicitudKycCommandHandlerTests
```

Esperado: verde, incluidos los tests preexistentes adaptados.

- [ ] **Paso 8: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src/Identity CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/EnviarSolicitudKycCommandHandlerTests.cs
git commit -m "feat(kyc): resuelve automaticamente las verificaciones concluyentes"
```

---

### Tarea 6: Exponer el motivo en la API de administración

**Archivos**
- Modificar: `src/Identity/CaseritoApp.Identity.Application/Kyc/DtosKyc.cs`
- Modificar: `src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/RepositorioVerificacionKycEfCore.cs:99-107`
- Modificar: `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` (regenerado)
- Modificar: `web/src/api/schema.d.ts` (regenerado)

**Interfaces**
- Consume: `SolicitudKyc.MotivoRevision` (Tarea 2).
- Produce: campo `MotivoRevision` (`string?`) en `SolicitudKycResumenDto`, que
  consumirá el bloque 3 al pintar el panel.

- [ ] **Paso 1: escribir el test que falla**

Añadir a `tests/CaseritoApp.IntegrationTests/KycArgosFlujoTests.cs`:

```csharp
    [Fact]
    public async Task Listado_de_administracion_expone_el_motivo_de_revision()
    {
        using var cliente = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ =>
                new VerificadorArgosEstatico(Result.Fallo<VerificacionFacialResultado>(
                    new Error(ErroresKyc.RostroNoDetectado, "No se detecto rostro"))));
        })).CreateClient();

        var email = Email("kyc-motivo");
        var token = await RegistrarYLoguearAsync(cliente, email, RolesApp.AdminKyc);

        using var subir = Autorizada(
            HttpMethod.Post, "/api/kyc/?numeroCi=1234597&departamentoExpedicion=LaPaz", token);
        subir.Content = Formulario();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subir)).StatusCode);

        using var listar = Autorizada(HttpMethod.Get, "/api/admin/kyc?estado=Pendiente", token);
        var pagina = await (await cliente.SendAsync(listar))
            .Content.ReadFromJsonAsync<
                CaseritoApp.Identity.Application.Autorizacion.ResultadoPaginado<SolicitudKycResumenDto>>();

        Assert.Contains(pagina!.Items, s => s.MotivoRevision == "RostroNoDetectado");
    }
```

Valores ya verificados, no hay que buscarlos: el rol es `RolesApp.AdminKyc`
(`RolesApp.cs:13`), el grupo de endpoints es `/api/admin/kyc`
(`KycEndpoints.cs:37`), y `ResultadoPaginado` vive en el namespace
`CaseritoApp.Identity.Application.Autorizacion` —por eso se escribe con nombre
completo, igual que hace `KycFlujoTests.cs:92`.

- [ ] **Paso 2: ejecutar el test y verlo fallar**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~Listado_de_administracion_expone_el_motivo_de_revision
```

Esperado: error de compilación, `SolicitudKycResumenDto` no tiene
`MotivoRevision`.

- [ ] **Paso 3: añadir el campo al DTO**

En `DtosKyc.cs`, al final del record para no alterar el orden de los existentes:

```csharp
public sealed record SolicitudKycResumenDto(
    Guid SolicitudId,
    Guid UsuarioId,
    string Estado,
    string TipoDocumento,
    DateTimeOffset EnviadaEn,
    DateTimeOffset? ResueltaEn,
    double? ScoreSimilitud,
    Guid? ResueltaPor,
    string? MotivoRevision);
```

- [ ] **Paso 4: proyectarlo**

En `RepositorioVerificacionKycEfCore.cs`, dentro del `.Select(s => new SolicitudKycResumenDto(...))`
de `ListarAsync`, añadir como último argumento:

```csharp
                s.MotivoRevision == null ? null : s.MotivoRevision.ToString(),
```

Compilar y corregir cualquier otra construcción de `SolicitudKycResumenDto` que
el compilador señale.

- [ ] **Paso 5: regenerar contrato y cliente**

Desde `CaseritoApp/`:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Testing"; dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj -p:GenerateOpenApi=true
```

Desde `web/`:

```powershell
npm run generate:api
```

- [ ] **Paso 6: ejecutar el test y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~Listado_de_administracion_expone_el_motivo_de_revision
```

Esperado: verde.

- [ ] **Paso 7: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp web
git commit -m "feat(kyc): expone el motivo de revision en el listado de administracion"
```

---

### Tarea 7: Cierre del flujo extremo a extremo

**Archivos**
- Modificar: `tests/CaseritoApp.IntegrationTests/KycArgosFlujoTests.cs`

**Interfaces**
- Consume: todo lo anterior.
- Produce: nada.

- [ ] **Paso 1: actualizar el test obsoleto del 503**

El test `Subir_con_argos_caido_devuelve_503` (línea 114 aproximadamente) describe
el comportamiento anterior. Sustituirlo por:

```csharp
    [Fact]
    public async Task Subir_con_argos_caido_deja_la_solicitud_pendiente()
    {
        using var cliente = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ =>
                new VerificadorArgosEstatico(Result.Fallo<VerificacionFacialResultado>(
                    new Error(ErroresKyc.ServicioVerificacionNoDisponible, "No disponible"))));
        })).CreateClient();

        var email = Email("kyc-down");
        var token = await RegistrarYLoguearAsync(cliente, email, null);

        using var subir = Autorizada(
            HttpMethod.Post, "/api/kyc/?numeroCi=1234563&departamentoExpedicion=LaPaz", token);
        subir.Content = Formulario();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subir)).StatusCode);

        using var estado = Autorizada(HttpMethod.Get, "/api/kyc/estado", token);
        var dto = await (await cliente.SendAsync(estado)).Content.ReadFromJsonAsync<EstadoKycDto>();
        Assert.Equal("Pendiente", dto!.Estado);
    }
```

- [ ] **Paso 2: añadir el test del camino feliz completo**

```csharp
    [Fact]
    public async Task Score_alto_verifica_al_usuario_sin_intervencion_humana()
    {
        using var cliente = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ =>
                new VerificadorArgosEstatico(Result.Exito(
                    new VerificacionFacialResultado(Coinciden: true, SimilitudPercent: 95, MotivoRechazo: null))));
        })).CreateClient();

        var email = Email("kyc-auto");
        var token = await RegistrarYLoguearAsync(cliente, email, null);

        using var subir = Autorizada(
            HttpMethod.Post, "/api/kyc/?numeroCi=1234596&departamentoExpedicion=LaPaz", token);
        subir.Content = Formulario();
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(subir)).StatusCode);

        using var estado = Autorizada(HttpMethod.Get, "/api/kyc/estado", token);
        var dto = await (await cliente.SendAsync(estado)).Content.ReadFromJsonAsync<EstadoKycDto>();
        Assert.Equal("Aprobada", dto!.Estado);
    }
```

- [ ] **Paso 3: ejecutar toda la batería de KYC**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~Kyc
```

Esperado: verde.

- [ ] **Paso 4: ejecutar la suite completa**

Desde `CaseritoApp/`:

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

Desde `web/`:

```powershell
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

Esperado: todo verde, sin deriva en el contrato ni en el cliente generado.

- [ ] **Paso 5: revisión final contra el spec**

Comprobar uno a uno los criterios de aceptación de la sección 12 del spec.
Prestar atención especial a:

- ningún log contiene score, CI, nombre ni clave de blob;
- la aprobación automática queda con `ResueltaPor = SistemaActor.Id`;
- ningún camino de fallo deja el CI reservado;
- sin mojibake en los textos nuevos.

- [ ] **Paso 6: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/tests/CaseritoApp.IntegrationTests/KycArgosFlujoTests.cs
git commit -m "test(kyc): cubre el flujo de resolucion automatica extremo a extremo"
```

---

## Notas de despliegue

Añadir a la configuración del entorno, si se quiere un valor distinto al 60 por
defecto del código:

```dotenv
Argos__UmbralAutoAprobacion=60
```

No es un secreto: puede figurar en los ficheros compose.

## Fuera de alcance

Confirmado en el spec y no se implementa aquí: gate de verificación al contactar
(bloque 2), aviso al administrador (bloque 3), distintivo de verificado en
listado, detalle y chat (bloque 4), y el mostrar el motivo de revisión en
`AdminKycPage`, que corresponde al bloque 3.
