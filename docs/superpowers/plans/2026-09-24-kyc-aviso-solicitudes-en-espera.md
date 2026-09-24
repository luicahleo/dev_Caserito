# Aviso al administrador de solicitudes KYC en espera — Plan de implementación

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA: usar
> `superpowers:subagent-driven-development` (recomendado) o
> `superpowers:executing-plans` para implementar tarea por tarea. Los pasos usan
> casillas (`- [ ]`) para seguimiento.

**Spec:** `docs/superpowers/specs/2026-09-24-kyc-aviso-solicitudes-en-espera-design.md`

**Objetivo:** Que una solicitud de verificación que queda esperando revisión
humana avise por correo a un buzón de administración y quede visible como
contador en el menú de quien puede revisarla.

**Arquitectura:** Un evento de dominio nuevo, publicado por el comando de envío
en la rama de revisión manual, lo consume un handler que envía el correo —
simétrico a `KycResuelto` / `NotificarKycResueltoHandler`. El destinatario es un
buzón único configurable, expuesto a Application por un puerto. El contador del
frontend reutiliza el `total` que ya devuelve el listado de administración.

**Stack:** .NET 10, Clean Architecture por bounded context, MediatR + Result +
FluentValidation, xUnit + NSubstitute, Testcontainers.MsSql para integración.
React + TypeScript, MUI, TanStack Query, Vitest. Comandos de backend desde
`CaseritoApp/`; de frontend desde `web/`; el gate, desde la raíz.

## Restricciones globales

- Trabajar en la rama `develop`. No crear ramas. No hacer push.
- Textos de UI, de correo y comentarios en español, con acentos y UTF-8.
- **Anti-PII**: ni el asunto ni el cuerpo del correo pueden contener nombre, CI,
  score de similitud, motivo de revisión ni identificadores. El buzón configurado
  nunca se registra en logs.
- Los logs de Identity usan el patrón `usuario={UsuarioId}`. La prohibición del
  literal `Id` en plantillas de log es del gate de Chat y **no** aplica aquí.
- No modificar la política de resolución, el umbral de ARGOS ni el contrato
  OpenAPI. No regenerar `web/src/api/schema.d.ts`.
- Antes de cada commit, desde la raíz: `./verify.ps1 -Changed`. Prohibido
  `--no-verify`. Si el gate falla, se arregla el código, nunca el gate.
- Docker debe estar corriendo para la Tarea 4 (integración con Testcontainers).
- Nombres exactos, sin variaciones: sección de configuración `Kyc`, propiedad
  `EmailAvisos` (variable de entorno `Kyc__EmailAvisos`).
- Copy del correo, literal:
  - Asunto: `Hay una solicitud de verificación esperando revisión`
  - Cuerpo: `Una solicitud de verificación de identidad quedó pendiente de revisión manual.\n\nRevísala en el panel de administración:\n{urlPanel}\n\nEquipo Caserito`
- Copy de UI, literal: entrada de menú `Verificaciones`.

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `Identity.Domain/Kyc/SolicitudKycEnEspera.cs` (crear) | Evento de dominio |
| `Identity.Application/Kyc/EnviarSolicitudKycCommand.cs` (modificar) | Publica el evento en la rama de revisión manual |
| `Identity.Application/Kyc/IOpcionesAvisosKyc.cs` (crear) | Puerto del buzón |
| `Identity.Application/Correo/IPlantillaCorreo.cs` (modificar) | Asunto y cuerpo del aviso |
| `Identity.Infrastructure/Correo/PlantillaCorreoTextoPlano.cs` (modificar) | Implementación del copy |
| `Identity.Application/Kyc/NotificarSolicitudKycEnEsperaHandler.cs` (crear) | Consume el evento y envía |
| `Identity.Infrastructure/Kyc/OpcionesAvisosKyc.cs` (crear) | Sección de configuración + adaptador del puerto |
| `Identity.Infrastructure/DependencyInjection.cs` (modificar) | Registro |
| `web/src/api/kyc.ts` (modificar) | `contarSolicitudesKycPendientes()` |
| `web/src/kyc/useContadorKyc.ts` (crear) | Hook del contador |
| `web/src/app/AppLayout.tsx` (modificar) | Entrada de menú con badge |

---

### Tarea 1: El comando publica el evento de espera

**Archivos:**
- Crear: `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/SolicitudKycEnEspera.cs`
- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/EnviarSolicitudKycCommand.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/EnviarSolicitudKycCommandHandlerTests.cs`

**Interfaces:**
- Consume: nada.
- Produce: `SolicitudKycEnEspera(Guid EventoId, DateTimeOffset OcurridoEn, Guid UsuarioId, Guid SolicitudId) : IDomainEvent`, en el namespace `CaseritoApp.Identity.Domain.Kyc`.

**Contexto para el ejecutor:** el `PublicadorFake` del archivo de test (línea 63)
ya implementa `IPublisher` y acumula en su lista `Notificaciones`. No es un mock
estricto, así que los tests existentes de las ramas pendientes no se rompen al
publicar un evento más.

- [ ] **Paso 1: Escribir los tests que fallan**

Añadir a `EnviarSolicitudKycCommandHandlerTests.cs`, dentro de la clase:

```csharp
    [Fact]
    public async Task Score_bajo_publica_el_aviso_de_solicitud_en_espera()
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
        var aviso = Assert.Single(publicador.Notificaciones.OfType<SolicitudKycEnEspera>());
        Assert.Equal(solicitud.Id, aviso.SolicitudId);
        Assert.Equal(repo.Agregada!.UsuarioId, aviso.UsuarioId);
    }

    [Fact]
    public async Task Score_alto_no_publica_el_aviso_de_solicitud_en_espera()
    {
        var almacen = new AlmacenFake();
        var repo = new RepoFake(null);
        var publicador = new PublicadorFake();
        var verificador = new VerificadorFake(Result.Exito(
            new VerificacionFacialResultado(Coinciden: true, SimilitudPercent: 95, MotivoRechazo: null)));

        var handler = CrearHandler(repo, almacen, verificador, publicador, new OpcionesFake(60));
        var resultado = await handler.Handle(ComandoValido(), default);

        Assert.True(resultado.EsExito);
        Assert.Empty(publicador.Notificaciones.OfType<SolicitudKycEnEspera>());
    }
```

- [ ] **Paso 2: Ejecutar los tests y verificar que fallan**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~EnviarSolicitudKycCommandHandlerTests"
```

Esperado: error de compilación — el tipo `SolicitudKycEnEspera` no existe.

- [ ] **Paso 3: Implementación mínima**

Crear `SolicitudKycEnEspera.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Domain.Kyc;

/// <summary>
/// Evento de dominio publicado cuando una solicitud KYC queda esperando revisión humana
/// porque la evidencia facial no fue concluyente. Lo consume el handler que avisa a la
/// administración por correo.
/// </summary>
public sealed record SolicitudKycEnEspera(
    Guid EventoId,
    DateTimeOffset OcurridoEn,
    Guid UsuarioId,
    Guid SolicitudId) : IDomainEvent;
```

En `EnviarSolicitudKycCommand.cs`, dentro del handler, ampliar el bloque que hoy
publica solo en la aprobación automática:

```csharp
        if (decision.Resolucion == ResolucionKyc.AprobarAutomatico)
        {
            await publicador.PublicarAsync(
                new UserVerified(Guid.NewGuid(), ahora, verificacion.UsuarioId), cancellationToken);
            await publisher.Publish(
                new KycResuelto(
                    Guid.NewGuid(), ahora, verificacion.UsuarioId, solicitud.Id, EstadoKyc.Aprobada, null),
                cancellationToken);
        }
        else if (decision.Resolucion == ResolucionKyc.EnviarARevision)
        {
            await publisher.Publish(
                new SolicitudKycEnEspera(
                    Guid.NewGuid(), ahora, verificacion.UsuarioId, solicitud.Id),
                cancellationToken);
        }
```

No tocar nada más del handler: ni el orden de operaciones, ni la reserva del
documento, ni las etiquetas de auditoría.

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~EnviarSolicitudKycCommandHandlerTests"
```

Esperado: PASS, incluidos los 7 tests que ya existían.

- [ ] **Paso 5: Commit**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/SolicitudKycEnEspera.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/EnviarSolicitudKycCommand.cs CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/EnviarSolicitudKycCommandHandlerTests.cs
git commit -m "feat(kyc): publica el aviso cuando una solicitud queda en espera"
```

---

### Tarea 2: Handler que avisa por correo

**Archivos:**
- Crear: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/IOpcionesAvisosKyc.cs`
- Crear: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/NotificarSolicitudKycEnEsperaHandler.cs`
- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IPlantillaCorreo.cs`
- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/PlantillaCorreoTextoPlano.cs`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/NotificarSolicitudKycEnEsperaHandlerTests.cs` (crear)

**Interfaces:**
- Consume: `SolicitudKycEnEspera` (Tarea 1); `IServicioCorreo.EnviarAsync(MensajeCorreo, CancellationToken)`; `MensajeCorreo(string Para, string Asunto, string CuerpoTexto, string? CuerpoHtml = null)`; `OpcionesApp.UrlPublica` del namespace `CaseritoApp.Identity.Application.Auth`.
- Produce:
  - `IOpcionesAvisosKyc` con `string? EmailAvisos { get; }`
  - `NotificarSolicitudKycEnEsperaHandler(IServicioCorreo, IPlantillaCorreo, IOpcionesAvisosKyc, OpcionesApp, ILogger<NotificarSolicitudKycEnEsperaHandler>)`
  - `IPlantillaCorreo.AsuntoSolicitudKycEnEspera()` y `IPlantillaCorreo.CuerpoSolicitudKycEnEspera(string urlPanel)`

- [ ] **Paso 1: Escribir los tests que fallan**

Crear `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/NotificarSolicitudKycEnEsperaHandlerTests.cs`:

```csharp
using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure.Correo;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class NotificarSolicitudKycEnEsperaHandlerTests
{
    private sealed class OpcionesAvisosFake(string? email) : IOpcionesAvisosKyc
    {
        public string? EmailAvisos => email;
    }

    private readonly IServicioCorreo _servicio = Substitute.For<IServicioCorreo>();

    private NotificarSolicitudKycEnEsperaHandler CrearHandler(string? buzon) =>
        new(_servicio,
            new PlantillaCorreoTextoPlano(),
            new OpcionesAvisosFake(buzon),
            new OpcionesApp { UrlPublica = "https://caserito.app" },
            NullLogger<NotificarSolicitudKycEnEsperaHandler>.Instance);

    private static SolicitudKycEnEspera Evento() =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Con_buzon_configurado_envia_el_aviso()
    {
        var handler = CrearHandler("admin@caserito.test");

        await handler.Handle(Evento(), CancellationToken.None);

        await _servicio.Received(1).EnviarAsync(
            Arg.Is<MensajeCorreo>(m =>
                m.Para == "admin@caserito.test"
                && m.CuerpoTexto.Contains("https://caserito.app/admin/kyc")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_buzon_configurado_no_envia_nada()
    {
        var handler = CrearHandler("   ");

        await handler.Handle(Evento(), CancellationToken.None);

        await _servicio.DidNotReceive().EnviarAsync(
            Arg.Any<MensajeCorreo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fallo_del_relay_no_se_propaga()
    {
        _servicio.EnviarAsync(Arg.Any<MensajeCorreo>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("relay caido"));
        var handler = CrearHandler("admin@caserito.test");

        await handler.Handle(Evento(), CancellationToken.None);
    }

    [Fact]
    public async Task El_aviso_no_contiene_identificadores_ni_score()
    {
        MensajeCorreo? capturado = null;
        await _servicio.EnviarAsync(
            Arg.Do<MensajeCorreo>(m => capturado = m), Arg.Any<CancellationToken>());
        var handler = CrearHandler("admin@caserito.test");
        var evento = Evento();

        await handler.Handle(evento, CancellationToken.None);

        Assert.NotNull(capturado);
        var texto = capturado!.Asunto + capturado.CuerpoTexto;
        Assert.DoesNotContain(evento.UsuarioId.ToString(), texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(evento.SolicitudId.ToString(), texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("score", texto, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Paso 2: Ejecutar los tests y verificar que fallan**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~NotificarSolicitudKycEnEsperaHandlerTests"
```

Esperado: error de compilación — no existen `IOpcionesAvisosKyc` ni el handler.

- [ ] **Paso 3: Implementación mínima**

Crear `IOpcionesAvisosKyc.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Buzón al que se avisa de las solicitudes que esperan revisión humana. Es un puerto porque
/// Application no referencia paquetes de configuración. Vacío desactiva el aviso: desarrollo y
/// test no necesitan buzón.
/// </summary>
public interface IOpcionesAvisosKyc
{
    /// <summary>Dirección de destino, o nulo/vacío para no avisar.</summary>
    public string? EmailAvisos { get; }
}
```

Añadir a `IPlantillaCorreo.cs`, dentro de la interfaz:

```csharp
    public string AsuntoSolicitudKycEnEspera();
    public string CuerpoSolicitudKycEnEspera(string urlPanel);
```

Añadir a `PlantillaCorreoTextoPlano.cs`, dentro de la clase:

```csharp
    // Aviso interno: no lleva nombre, CI, score, motivo ni identificadores. Solo dice que hay
    // trabajo pendiente y a dónde ir; los datos se ven en el panel, que audita el acceso.
    public string AsuntoSolicitudKycEnEspera() =>
        "Hay una solicitud de verificación esperando revisión";

    public string CuerpoSolicitudKycEnEspera(string urlPanel) =>
        "Una solicitud de verificación de identidad quedó pendiente de revisión manual.\n\n" +
        $"Revísala en el panel de administración:\n{urlPanel}\n\nEquipo Caserito";
```

Crear `NotificarSolicitudKycEnEsperaHandler.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Identity.Application.Auth;
using CaseritoApp.Identity.Application.Correo;
using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Kyc;

/// <summary>
/// Reacciona a <see cref="SolicitudKycEnEspera"/> avisando por correo al buzón de
/// administración. Un fallo del relay SMTP se loguea pero no se propaga: la solicitud ya
/// quedó persistida y el contador del panel sigue siendo la fuente de verdad.
/// </summary>
public sealed partial class NotificarSolicitudKycEnEsperaHandler(
    IServicioCorreo servicioCorreo,
    IPlantillaCorreo plantilla,
    IOpcionesAvisosKyc opcionesAvisos,
    OpcionesApp opcionesApp,
    ILogger<NotificarSolicitudKycEnEsperaHandler> logger)
    : IDomainEventConsumer<SolicitudKycEnEspera>
{
    public async Task Handle(SolicitudKycEnEspera evento, CancellationToken cancellationToken)
    {
        var buzon = opcionesAvisos.EmailAvisos;
        if (string.IsNullOrWhiteSpace(buzon))
        {
            RegistrarSinBuzon(logger);
            return;
        }

        var urlPanel = $"{opcionesApp.UrlPublica.TrimEnd('/')}/admin/kyc";
        var mensaje = new MensajeCorreo(
            buzon,
            plantilla.AsuntoSolicitudKycEnEspera(),
            plantilla.CuerpoSolicitudKycEnEspera(urlPanel));

        try
        {
            await servicioCorreo.EnviarAsync(mensaje, cancellationToken);
            RegistrarAviso(logger, evento.SolicitudId);
        }
        catch (Exception ex)
        {
            RegistrarFalloEnvio(logger, evento.SolicitudId, ex);
        }
    }

    // El buzón configurado nunca se registra: es un dato de configuración, no de diagnóstico.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Aviso de KYC en espera omitido: no hay buzon configurado")]
    private static partial void RegistrarSinBuzon(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Aviso de KYC en espera enviado: solicitud={SolicitudId}")]
    private static partial void RegistrarAviso(ILogger logger, Guid solicitudId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo el envio del aviso de KYC en espera: solicitud={SolicitudId}")]
    private static partial void RegistrarFalloEnvio(ILogger logger, Guid solicitudId, Exception ex);
}
```

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~NotificarSolicitudKycEnEsperaHandlerTests"
```

Esperado: PASS, 4 tests.

Después, la suite unitaria entera, porque `IPlantillaCorreo` ganó miembros:

```
dotnet test tests/CaseritoApp.UnitTests
```

Esperado: PASS. Si algún doble de prueba implementa `IPlantillaCorreo` a mano,
fallará al compilar; en ese caso implementar los dos métodos nuevos en ese doble
devolviendo cadenas vacías, sin tocar más.

- [ ] **Paso 5: Commit**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/IOpcionesAvisosKyc.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/NotificarSolicitudKycEnEsperaHandler.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IPlantillaCorreo.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/PlantillaCorreoTextoPlano.cs CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/NotificarSolicitudKycEnEsperaHandlerTests.cs
git commit -m "feat(kyc): avisa por correo de las solicitudes en espera"
```

---

### Tarea 3: Configuración del buzón y cableado

**Archivos:**
- Crear: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesAvisosKyc.cs`
- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs:70-74`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/OpcionesAvisosKycDesdeConfigTests.cs` (crear)

**Interfaces:**
- Consume: `IOpcionesAvisosKyc` (Tarea 2).
- Produce: `OpcionesAvisosKyc` con `const string Seccion = "Kyc"` y `string? EmailAvisos { get; set; }`; `OpcionesAvisosKycDesdeConfig(IOptions<OpcionesAvisosKyc>) : IOpcionesAvisosKyc`.

- [ ] **Paso 1: Escribir el test que falla**

Crear `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/OpcionesAvisosKycDesdeConfigTests.cs`:

```csharp
using CaseritoApp.Identity.Infrastructure.Kyc;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaseritoApp.UnitTests.Kyc;

public sealed class OpcionesAvisosKycDesdeConfigTests
{
    [Fact]
    public void Expone_el_buzon_configurado()
    {
        var opciones = new OpcionesAvisosKycDesdeConfig(
            Options.Create(new OpcionesAvisosKyc { EmailAvisos = "admin@caserito.test" }));

        Assert.Equal("admin@caserito.test", opciones.EmailAvisos);
    }

    [Fact]
    public void Sin_configurar_no_expone_buzon()
    {
        var opciones = new OpcionesAvisosKycDesdeConfig(
            Options.Create(new OpcionesAvisosKyc()));

        Assert.True(string.IsNullOrWhiteSpace(opciones.EmailAvisos));
    }
}
```

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~OpcionesAvisosKycDesdeConfigTests"
```

Esperado: error de compilación — no existe `OpcionesAvisosKyc`.

- [ ] **Paso 3: Implementación mínima**

Crear `OpcionesAvisosKyc.cs`:

```csharp
using CaseritoApp.Identity.Application.Kyc;
using Microsoft.Extensions.Options;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Configuración de los avisos de KYC a la administración (sección <c>Kyc</c>).</summary>
public sealed class OpcionesAvisosKyc
{
    public const string Seccion = "Kyc";

    /// <summary>
    /// Buzón al que se avisa de las solicitudes que esperan revisión humana. Puede ser un alias
    /// que reparta internamente. Vacío desactiva el aviso.
    /// </summary>
    public string? EmailAvisos { get; set; }
}

/// <summary>Lee el buzón de avisos de la sección <c>Kyc</c>.</summary>
public sealed class OpcionesAvisosKycDesdeConfig(IOptions<OpcionesAvisosKyc> opciones)
    : IOpcionesAvisosKyc
{
    public string? EmailAvisos => opciones.Value.EmailAvisos;
}
```

En `DependencyInjection.cs`, junto a los registros de opciones ya existentes
(línea 71 registra `OpcionesArgos`), añadir:

```csharp
        servicios.Configure<OpcionesAvisosKyc>(config.GetSection(OpcionesAvisosKyc.Seccion));
```

y junto al registro de `IOpcionesResolucionKyc` (línea 98):

```csharp
        servicios.AddSingleton<IOpcionesAvisosKyc, OpcionesAvisosKycDesdeConfig>();
```

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~OpcionesAvisosKycDesdeConfigTests"
dotnet build
```

Esperado: PASS y build sin warnings (la solución compila con warnings-as-errors).

- [ ] **Paso 5: Commit**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesAvisosKyc.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/OpcionesAvisosKycDesdeConfigTests.cs
git commit -m "feat(kyc): configura el buzon de avisos de verificacion"
```

---

### Tarea 4: Integración de punta a punta

**Archivos:**
- Modificar: `CaseritoApp/tests/CaseritoApp.IntegrationTests/KycNotificacionTests.cs`

**Interfaces:**
- Consume: todo lo anterior. `ServicioCorreoCapturador` (línea 25 del archivo) y
  `FactoryConCorreo` (línea 36) ya existen; reutilizarlos, no duplicarlos.
- Produce: nada.

**Requisito:** Docker corriendo (Testcontainers.MsSql).

- [ ] **Paso 1: Escribir el test que falla**

Añadir a `KycNotificacionTests.cs`, dentro de la clase:

```csharp
    [Fact]
    public async Task Solicitud_en_revision_manual_avisa_al_buzon_de_administracion()
    {
        var capturador = new ServicioCorreoCapturador();
        var factoryConBuzon = factory
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Kyc:EmailAvisos", "avisos@caserito.test");
                b.ConfigureServices(s => s.AddSingleton<IServicioCorreo>(capturador));
            });
        var cliente = factoryConBuzon.CreateClient();
        var email = $"espera-{Guid.NewGuid():N}@caserito.test";
        var token = await RegistrarYLoguearAsync(cliente, email, null);

        using var scope = factoryConBuzon.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        await publisher.Publish(
            new SolicitudKycEnEspera(Guid.NewGuid(), DateTimeOffset.UtcNow, usuario!.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(capturador.Enviados.TryDequeue(out var mensaje));
        Assert.Equal("avisos@caserito.test", mensaje!.Para);
        Assert.DoesNotContain(email, mensaje.CuerpoTexto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/admin/kyc", mensaje.CuerpoTexto, StringComparison.Ordinal);
    }
```

Añadir los `using` que falten al principio del archivo: `MediatR` (para
`IPublisher`) y `CaseritoApp.Identity.Domain.Kyc` (ya está importado en el
archivo; comprobar antes de duplicarlo).

El token devuelto por `RegistrarYLoguearAsync` no se usa en este test; si el
analizador se queja de la variable sin uso, sustituir la asignación por
`await RegistrarYLoguearAsync(cliente, email, null);`.

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~Solicitud_en_revision_manual_avisa"
```

Esperado: FAIL — sin el registro del handler o del buzón, la cola de correos
queda vacía y `TryDequeue` devuelve `false`.

Si falla por no encontrar Docker, parar y avisar: no seguir sin ejecutar el test.

- [ ] **Paso 3: Diagnosticar si falla**

**No hace falta registrar nada.** Está verificado: `IDomainEvent` es
`INotification`, `IDomainEventConsumer<T>` es `INotificationHandler<T>`, y
`Program.cs:46` ya incluye el ensamblado de `Identity.Application` en
`RegisterServicesFromAssemblies` (vía `typeof(ObtenerPerfilQuery).Assembly`). El
handler de la Tarea 2 se descubre automáticamente.

Por tanto, si el test falla la causa **no** es el registro del handler. Las dos
causas plausibles, en orden:

1. `UseSetting("Kyc:EmailAvisos", …)` no llega a la configuración que lee
   `services.Configure<OpcionesAvisosKyc>` porque el registro de la Tarea 3 se
   omitió o usó otra sección. Comprobar que la constante `Seccion` vale `"Kyc"`
   y que el `Configure` está en `DependencyInjection.cs`.
2. El `ServicioCorreoCapturador` no sustituye al real porque el
   `ConfigureServices` se aplicó sobre `factory` en vez de sobre el builder del
   test. Comparar con `FactoryConCorreo` (línea 36), que ya resuelve eso.

Arreglar la causa encontrada. No añadir registros de MediatR por handler.

- [ ] **Paso 4: Ejecutar el test y verificar que pasa**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~KycNotificacionTests"
```

Esperado: PASS, 3 tests (los 2 que ya existían y el nuevo).

- [ ] **Paso 5: Commit**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add CaseritoApp/tests/CaseritoApp.IntegrationTests/KycNotificacionTests.cs
git commit -m "test(kyc): cubre el aviso de solicitudes en espera de punta a punta"
```

Si el paso 3 obligó a tocar código de producción, incluir esos archivos en el
`git add` y usar el mensaje `fix(kyc): registra el consumidor del aviso de espera`.

---

### Tarea 5: Contador de pendientes en el frontend

**Archivos:**
- Modificar: `web/src/api/kyc.ts`
- Crear: `web/src/kyc/useContadorKyc.ts`
- Modificar: `web/src/app/AppLayout.tsx` (menú de cuenta, junto al bloque de
  `chat.moderar` de las líneas 260-270)
- Test: `web/src/app/AppLayout.test.tsx`

**Interfaces:**
- Consume: `listarSolicitudesKyc(estado?, pagina?, tamano?)` de `web/src/api/kyc.ts`; `useAuth().tienePermiso(permiso: string): boolean`.
- Produce: `contarSolicitudesKycPendientes(): Promise<number>`; `useContadorKyc(activo?: boolean): number`.

**Contexto para el ejecutor:** `total` llega tipado como `number | string` en el
contrato generado, así que hay que convertirlo con `Number(...)`, igual que hace
`web/src/api/chat.ts` con sus contadores.

- [ ] **Paso 1: Escribir el test que falla**

Añadir a `web/src/app/AppLayout.test.tsx`, dentro del `describe('AppLayout', ...)`,
e importar el módulo con `import * as kyc from '../api/kyc';` junto a los demás
imports del archivo:

```tsx
  it('muestra Verificaciones con el número de pendientes solo con kyc.revisar', async () => {
    mockAuth(true, ['kyc.revisar']);
    vi.spyOn(kyc, 'contarSolicitudesKycPendientes').mockResolvedValue(4);
    montar();

    await userEvent.click(screen.getByRole('button', { name: /mi cuenta/i }));
    const entrada = await screen.findByRole('menuitem', { name: /^verificaciones$/i });
    expect(entrada).toHaveAttribute('href', '/admin/kyc');
    expect(entrada).toHaveTextContent('4');

    cleanup();
    vi.restoreAllMocks();
    mockAuth(true);
    montar();
    await userEvent.click(screen.getByRole('button', { name: /mi cuenta/i }));
    expect(
      screen.queryByRole('menuitem', { name: /^verificaciones$/i }),
    ).not.toBeInTheDocument();
  });
```

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `web/`: `npx vitest run src/app/AppLayout.test.tsx`

Esperado: FAIL — `contarSolicitudesKycPendientes` no existe en el módulo, y no
hay ningún `menuitem` llamado «Verificaciones».

- [ ] **Paso 3: Implementación mínima**

Añadir al final de `web/src/api/kyc.ts`:

```ts
/**
 * Número de solicitudes esperando revisión. Reutiliza el listado de administración: su
 * `total` ya es el conteo, así que no hace falta un endpoint propio. Pide una sola fila
 * porque los items se descartan.
 */
export async function contarSolicitudesKycPendientes(): Promise<number> {
  const pagina = await listarSolicitudesKyc('Pendiente', 1, 1);
  return Number(pagina.total);
}
```

Crear `web/src/kyc/useContadorKyc.ts`:

```ts
import { useQuery } from '@tanstack/react-query';
import { contarSolicitudesKycPendientes } from '../api/kyc';

export const claveContadorKyc = ['kyc-pendientes'] as const;

/**
 * Contador de solicitudes en espera para el menú del revisor. Sin tiempo real: el aviso
 * inmediato es el correo y este badge es una referencia de estado.
 */
export function useContadorKyc(activo = true): number {
  const consulta = useQuery({
    queryKey: claveContadorKyc,
    queryFn: contarSolicitudesKycPendientes,
    enabled: activo,
    refetchInterval: 300_000,
    refetchOnWindowFocus: true,
  });

  return consulta.data ?? 0;
}
```

En `web/src/app/AppLayout.tsx`:

1. Importar el hook y el icono:

```tsx
import BadgeOutlinedIcon from '@mui/icons-material/BadgeOutlined';
import { useContadorKyc } from '../kyc/useContadorKyc';
```

2. Junto a `const mensajesNoLeidos = useContadorChat(estaAutenticado);` (línea 64):

```tsx
  const kycPendientes = useContadorKyc(estaAutenticado && tienePermiso('kyc.revisar'));
```

3. Añadir la entrada después del bloque de `chat.moderar` del menú de cuenta:

```tsx
            {tienePermiso('kyc.revisar') && (
              <MenuItem component={RouterLink} to="/admin/kyc" onClick={cerrarMenuCuenta}>
                <ListItemIcon>
                  <BadgeOutlinedIcon sx={{ color: 'text.secondary' }} />
                </ListItemIcon>
                <ListItemText>Verificaciones</ListItemText>
                {kycPendientes > 0 && (
                  <Badge
                    badgeContent={kycPendientes}
                    color="error"
                    max={99}
                    sx={{ ml: 2 }}
                    aria-label={`${kycPendientes} solicitudes en espera`}
                  />
                )}
              </MenuItem>
            )}
```

4. Incluir `kyc.revisar` en la condición del `Divider` de la línea 250, para que
   el separador aparezca también cuando ese sea el único permiso administrativo:

```tsx
            {(tienePermiso('publicaciones.moderar') ||
              tienePermiso('chat.moderar') ||
              tienePermiso('kyc.revisar')) && <Divider />}
```

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `web/`:
- `npx vitest run src/app/AppLayout.test.tsx` → PASS, incluidos los tests previos.
- `npm run test` → suite completa en verde.
- `npm run typecheck` → sin errores.
- `npm run lint` → sin errores.
- `npm run build` → build correcto.

- [ ] **Paso 5: Commit**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add web/src/api/kyc.ts web/src/kyc/useContadorKyc.ts web/src/app/AppLayout.tsx web/src/app/AppLayout.test.tsx
git commit -m "feat(kyc): muestra el contador de verificaciones en espera"
```

---

## Cierre

- [ ] Suite completa de backend desde `CaseritoApp/`: `dotnet test`.
- [ ] Suite completa de frontend desde `web/`: `npm run test`.
- [ ] `dotnet format --verify-no-changes` desde `CaseritoApp/`.
- [ ] Revisar el diff completo contra el spec: anti-PII del correo, ausencia de
      cambios en el contrato y en la política de resolución.
- [ ] `git diff --check` y árbol de trabajo limpio.
- [ ] Informar: criterios cubiertos, commits, verificaciones y pendientes.

**No hacer push.** `develop` se publica solo cuando el usuario lo pida.

**Documentar para despliegue:** la variable `Kyc__EmailAvisos` debe configurarse
en producción; sin ella no se envía ningún aviso. Mencionarlo en el informe final
para que el usuario decida cuándo y cómo configurarla en el VPS.

## Cobertura de los criterios de aceptación

| Criterio del spec | Dónde se cubre |
|---|---|
| 1. Revisión manual envía correo al buzón | Tarea 2 (caso 1), Tarea 4 |
| 2. Resolución automática no envía | Tarea 1, `Score_alto_no_publica_el_aviso_de_solicitud_en_espera` |
| 3. Sin buzón no envía y el flujo termina con éxito | Tarea 2, caso 2 |
| 4. Fallo de SMTP no altera el resultado | Tarea 2, caso 3 |
| 5. El correo no contiene PII ni identificadores | Tarea 2, caso 4; Tarea 4 |
| 6. Con `kyc.revisar` se ve la entrada con el número | Tarea 5 |
| 7. Sin el permiso no se ve | Tarea 5, segunda mitad del test |
