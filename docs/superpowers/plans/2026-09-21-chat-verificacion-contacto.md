# Plan — Verificación al contactar y conversación retenida (backend)

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA: usar
> `superpowers:subagent-driven-development` (recomendada) o
> `superpowers:executing-plans` para implementar tarea por tarea. Los pasos usan
> casillas (`- [ ]`) para seguimiento.

Spec: `docs/superpowers/specs/2026-09-21-chat-verificacion-contacto-design.md`

**Objetivo:** que el mensaje de un comprador solo llegue al vendedor si el
comprador tiene identidad verificada, sin bloquearle el teclado mientras se
resuelve su verificación y sin perder lo que escribió.

**Arquitectura:** la conversación gana un estado `RetenidaPorVerificacion`. Un
comprador sin identidad habilitada obtiene una conversación en ese estado, donde
puede escribir; el vendedor no la percibe por ninguna vía. Al aprobarse el KYC,
un handler de `UserVerified` la libera; si ese evento se pierde, la libera el
siguiente `IniciarConversacionCommand` del comprador.

**Stack:** .NET 10, EF Core sobre SQL Server, MediatR + Result +
FluentValidation, SignalR, xUnit, Testcontainers.MsSql.

## Restricciones globales

- Ejecutar los comandos `dotnet` desde `CaseritoApp/` y los de `npm` desde `web/`.
- Trabajar directamente sobre `develop`. No crear ramas. No hacer push.
- Textos y comentarios en español con acentos, UTF-8, sin mojibake.
- Anti-PII: ningún log, error, evento ni notificación puede contener texto de
  mensajes ni identidades. Solo ids técnicos.
- `Chat` no puede referenciar `Identity`: la comunicación entre contextos va por
  un puerto de `Chat.Application` implementado por un adaptador del **Host**.
- Warnings como errores: cualquier advertencia rompe el build.
- Antes de **cada** commit: `./verify.ps1 -Changed` desde la raíz. Prohibido
  `--no-verify`. Si el gate falla, se arregla el código, nunca el gate.
- Un cambio en `src/` sin cambio en `tests/` exige `[sin-test] <motivo>`.
- **El valor nuevo del enum va al final.** `Estado` se mapea por convención como
  `int`; insertarlo en medio cambiaría el significado de las filas existentes.
- No hay migración en este bloque.

## Estructura de archivos

**Se crean**

| Ruta | Responsabilidad |
|---|---|
| `src/Chat/CaseritoApp.Chat.Application/Conversaciones/IConsultaVerificacionComprador.cs` | Puerto hacia el estado de verificación |
| `src/Chat/CaseritoApp.Chat.Application/Conversaciones/LiberarConversacionesAlVerificarHandler.cs` | Handler de `UserVerified` |
| `src/Host/CaseritoApp.Host/Chat/ConsultaVerificacionCompradorAdapter.cs` | Adaptador hacia Identity |
| `tests/CaseritoApp.UnitTests/Chat/ConversacionRetencionTests.cs` | Máquina de estados de la retención |
| `tests/CaseritoApp.UnitTests/Chat/LiberarConversacionesAlVerificarHandlerTests.cs` | Handler de liberación |

**Se modifican**

| Ruta | Cambio |
|---|---|
| `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/EstadoConversacion.cs` | Valor nuevo al final |
| `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/Conversacion.cs` | Nacer retenida, liberar, regla de envío |
| `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/EventosConversacion.cs` | Evento de liberación |
| `src/Chat/CaseritoApp.Chat.Application/Conversaciones/IniciarConversacionCommand.cs` | Gate y liberación perezosa |
| `src/Chat/CaseritoApp.Chat.Application/Conversaciones/IRepositorioConversaciones.cs` | Listar retenidas de un comprador |
| `src/Chat/CaseritoApp.Chat.Application/Mensajes/DtosMensaje.cs` | `Retenida` en el resultado |
| `src/Chat/CaseritoApp.Chat.Application/Mensajes/EnviarMensajeCommand.cs` | Propagar `Retenida` |
| `src/Chat/CaseritoApp.Chat.Infrastructure/Conversaciones/ConsultaConversacionesEfCore.cs` | Invisibilidad para el vendedor |
| `src/Chat/CaseritoApp.Chat.Infrastructure/Conversaciones/RepositorioConversacionesEfCore.cs` | Listar retenidas |
| `src/Host/CaseritoApp.Host/Endpoints/ChatEndpoints.cs` | No publicar `ChatMessageSent` si retenida |
| `src/Host/CaseritoApp.Host/Program.cs` | Registro del adaptador |
| `src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs` y `OrdersEndpoints.cs` | Usar el helper extraído |

---

### Tarea 1: Estado retenido en el dominio

**Archivos**
- Modificar: `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/EstadoConversacion.cs`
- Modificar: `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/Conversacion.cs`
- Modificar: `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/EventosConversacion.cs`
- Test: `tests/CaseritoApp.UnitTests/Chat/ConversacionRetencionTests.cs`

**Interfaces**
- Consume: nada.
- Produce: `EstadoConversacion.RetenidaPorVerificacion`;
  `Conversacion.Crear(Guid avisoId, Guid compradorId, Guid vendedorId, DateTimeOffset creadaEn, bool retenida = false)`;
  `Conversacion.LiberarPorVerificacion(DateTimeOffset ocurrioEn)` que devuelve `Result`.

- [ ] **Paso 1: escribir el test que falla**

Crear `tests/CaseritoApp.UnitTests/Chat/ConversacionRetencionTests.cs`:

```csharp
using CaseritoApp.Chat.Domain.Conversaciones;
using Xunit;

namespace CaseritoApp.UnitTests.Chat;

public sealed class ConversacionRetencionTests
{
    private static readonly Guid _aviso = Guid.NewGuid();
    private static readonly Guid _comprador = Guid.NewGuid();
    private static readonly Guid _vendedor = Guid.NewGuid();
    private static readonly DateTimeOffset _ahora = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    private static Conversacion Retenida() =>
        Conversacion.Crear(_aviso, _comprador, _vendedor, _ahora, retenida: true).Valor;

    private static Conversacion Activa() =>
        Conversacion.Crear(_aviso, _comprador, _vendedor, _ahora).Valor;

    [Fact]
    public void Puede_nacer_retenida()
    {
        Assert.Equal(EstadoConversacion.RetenidaPorVerificacion, Retenida().Estado);
    }

    [Fact]
    public void Por_defecto_nace_activa()
    {
        Assert.Equal(EstadoConversacion.Activa, Activa().Estado);
    }

    [Fact]
    public void Liberar_la_pasa_a_activa()
    {
        var conversacion = Retenida();

        var resultado = conversacion.LiberarPorVerificacion(_ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
    }

    [Fact]
    public void Liberar_dos_veces_es_idempotente()
    {
        var conversacion = Retenida();
        conversacion.LiberarPorVerificacion(_ahora);

        var resultado = conversacion.LiberarPorVerificacion(_ahora);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoConversacion.Activa, conversacion.Estado);
    }

    [Fact]
    public void Liberar_una_conversacion_cerrada_falla()
    {
        var conversacion = Retenida();
        conversacion.LiberarPorVerificacion(_ahora);
        Assert.True(conversacion.CerrarPorParticipante(_comprador, _ahora).EsExito);

        var resultado = conversacion.LiberarPorVerificacion(_ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.TransicionInvalida, resultado.Error.Code);
    }

    [Fact]
    public void El_comprador_puede_escribir_estando_retenida()
    {
        var conversacion = Retenida();

        var resultado = conversacion.CrearMensaje(
            _comprador, Guid.NewGuid(), 1, "hola", _ahora);

        Assert.True(resultado.EsExito);
    }

    [Fact]
    public void El_vendedor_no_puede_escribir_estando_retenida()
    {
        var conversacion = Retenida();

        var resultado = conversacion.CrearMensaje(
            _vendedor, Guid.NewGuid(), 1, "hola", _ahora);

        Assert.False(resultado.EsExito);
        Assert.Equal(ErroresConversacion.NoDisponibleParaEnvio, resultado.Error.Code);
    }
}
```

Si `ErroresConversacion` no define `TransicionInvalida`, usar el código que ya
emplee `CerrarPorParticipante` para una transición no permitida; abrir
`ErroresConversacion.cs` y tomarlo de ahí en lugar de inventar uno nuevo.

- [ ] **Paso 2: ejecutar el test y verlo fallar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~ConversacionRetencionTests
```

Esperado: error de compilación; no existen el valor del enum, el parámetro
`retenida` ni `LiberarPorVerificacion`.

- [ ] **Paso 3: añadir el valor del enum al final**

`EstadoConversacion.cs` queda:

```csharp
namespace CaseritoApp.Chat.Domain.Conversaciones;

public enum EstadoConversacion
{
    Activa,
    Cerrada,
    CerradaPorModeracion,

    // Debe permanecer en último lugar: EF mapea este enum como int por convención
    // y reordenar los valores cambiaría el significado de las filas existentes.
    RetenidaPorVerificacion,
}
```

- [ ] **Paso 4: permitir que nazca retenida**

En `Conversacion.cs`, el constructor privado acepta el estado inicial y `Crear`
recibe el parámetro opcional:

```csharp
    private Conversacion(
        Guid avisoId,
        Guid compradorId,
        Guid vendedorId,
        DateTimeOffset creadaEn,
        EstadoConversacion estadoInicial)
    {
        AvisoId = avisoId;
        CompradorId = compradorId;
        VendedorId = vendedorId;
        CreadaEn = creadaEn;
        UltimaActividadEn = creadaEn;
        Estado = estadoInicial;
    }
```

```csharp
    public static Result<Conversacion> Crear(
        Guid avisoId,
        Guid compradorId,
        Guid vendedorId,
        DateTimeOffset creadaEn,
        bool retenida = false)
```

Y dentro, la construcción pasa a:

```csharp
        var conversacion = new Conversacion(
            avisoId,
            compradorId,
            vendedorId,
            fechaUtc,
            retenida ? EstadoConversacion.RetenidaPorVerificacion : EstadoConversacion.Activa);
```

El resto de `Crear` (validaciones y `ConversacionIniciada`) no cambia.

- [ ] **Paso 5: añadir el evento de liberación**

En `EventosConversacion.cs`, siguiendo la forma de los eventos ya presentes en
ese archivo (sin texto, sin participantes):

```csharp
public sealed record ConversacionLiberada(
    Guid ConversacionId,
    DateTimeOffset OcurridoEn) : IDomainEvent;
```

Si los eventos vecinos usan otra interfaz o incluyen otros campos, copiar esa
forma exactamente.

- [ ] **Paso 6: implementar la liberación**

En `Conversacion.cs`, junto a las demás transiciones:

```csharp
    /// <summary>
    /// Libera una conversación retenida por verificación pendiente. Idempotente si ya está activa;
    /// una conversación cerrada no se reabre por un efecto secundario del KYC.
    /// </summary>
    public Result LiberarPorVerificacion(DateTimeOffset ocurrioEn)
    {
        if (Estado == EstadoConversacion.Activa)
        {
            return Result.Exito();
        }

        if (Estado != EstadoConversacion.RetenidaPorVerificacion)
        {
            return Result.Fallo(new Error(
                ErroresConversacion.TransicionInvalida,
                "La conversación no puede liberarse."));
        }

        var fechaUtc = ocurrioEn.ToUniversalTime();
        Estado = EstadoConversacion.Activa;
        UltimaActividadEn = fechaUtc;
        AgregarEvento(new ConversacionLiberada(Id, fechaUtc));
        return Result.Exito();
    }
```

- [ ] **Paso 7: ampliar la regla de envío**

En `CrearMensaje`, sustituir la guarda de estado (hoy en `Conversacion.cs:156`,
`if (Estado != EstadoConversacion.Activa)`) por:

```csharp
        if (Estado == EstadoConversacion.RetenidaPorVerificacion)
        {
            // Retenida: el vendedor no la ve, así que un envío suyo solo puede venir de un
            // identificador filtrado o de un error.
            if (remitenteId != CompradorId)
            {
                return Result.Fallo<Mensaje>(new Error(
                    ErroresConversacion.NoDisponibleParaEnvio,
                    "La conversación no está disponible para enviar mensajes."));
            }
        }
        else if (Estado != EstadoConversacion.Activa)
        {
            return Result.Fallo<Mensaje>(new Error(
                ErroresConversacion.NoDisponibleParaEnvio,
                "La conversación no está disponible para enviar mensajes."));
        }
```

Usar el nombre real del parámetro del remitente que tenga `CrearMensaje` en la
firma; el resto del método no cambia.

- [ ] **Paso 8: ejecutar el test y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~ConversacionRetencionTests
```

Esperado: 7 tests en verde.

- [ ] **Paso 9: ejecutar los tests vecinos de Chat**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~Chat
```

Esperado: verde. `Conversacion.Crear` tiene el parámetro con valor por defecto,
así que las llamadas existentes siguen compilando.

- [ ] **Paso 10: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src/Chat CaseritoApp/tests/CaseritoApp.UnitTests/Chat/ConversacionRetencionTests.cs
git commit -m "feat(chat): modela la conversacion retenida por verificacion"
```

---

### Tarea 2: Gate al iniciar conversación y liberación perezosa

**Archivos**
- Crear: `src/Chat/CaseritoApp.Chat.Application/Conversaciones/IConsultaVerificacionComprador.cs`
- Crear: `src/Host/CaseritoApp.Host/Chat/ConsultaVerificacionCompradorAdapter.cs`
- Modificar: `src/Chat/CaseritoApp.Chat.Application/Conversaciones/IniciarConversacionCommand.cs`
- Modificar: `src/Host/CaseritoApp.Host/Program.cs`
- Test: `tests/CaseritoApp.UnitTests/Chat/IniciarConversacionCommandHandlerTests.cs` (crear si no existe)

**Interfaces**
- Consume: `Conversacion.Crear(..., bool retenida)` y `LiberarPorVerificacion` de la Tarea 1.
- Produce: `IConsultaVerificacionComprador` con
  `Task<bool> EstaHabilitadoAsync(Guid usuarioId, CancellationToken ct)`, que consume la Tarea 5.

- [ ] **Paso 1: escribir el test que falla**

Crear `tests/CaseritoApp.UnitTests/Chat/IniciarConversacionCommandHandlerTests.cs`.
Los fakes van dentro de la clase de test:

```csharp
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Seguridad;
using CaseritoApp.Chat.Domain.Conversaciones;
using Xunit;

namespace CaseritoApp.UnitTests.Chat;

public sealed class IniciarConversacionCommandHandlerTests
{
    private static readonly Guid _aviso = Guid.NewGuid();
    private static readonly Guid _comprador = Guid.NewGuid();
    private static readonly Guid _vendedor = Guid.NewGuid();

    private sealed class RepoFake(Conversacion? existente) : IRepositorioConversaciones
    {
        public Conversacion? Agregada { get; private set; }

        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) =>
            Task.FromResult(existente);

        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion) => Agregada = conversacion;

        public Task<IReadOnlyList<Conversacion>> ListarRetenidasDeCompradorAsync(
            Guid compradorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Conversacion>>([]);
    }

    private sealed class AvisoFake : IConsultaAvisoContactable
    {
        public Task<ReferenciaAvisoContactable?> ObtenerAsync(Guid avisoId, CancellationToken ct) =>
            Task.FromResult<ReferenciaAvisoContactable?>(
                new ReferenciaAvisoContactable(avisoId, _vendedor));
    }

    private sealed class BloqueosFake : IRepositorioBloqueosUsuario
    {
        public Task<bool> ExisteEntreAsync(Guid a, Guid b, CancellationToken ct) =>
            Task.FromResult(false);
    }

    private sealed class VerificacionFake(bool habilitado) : IConsultaVerificacionComprador
    {
        public Task<bool> EstaHabilitadoAsync(Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(habilitado);
    }

    private static IniciarConversacionCommandHandler Crear(
        RepoFake repo, bool habilitado) =>
        new(repo, new AvisoFake(), TimeProvider.System, new BloqueosFake(),
            new VerificacionFake(habilitado));

    [Fact]
    public async Task Comprador_no_habilitado_obtiene_conversacion_retenida()
    {
        var repo = new RepoFake(null);

        var resultado = await Crear(repo, habilitado: false)
            .Handle(new IniciarConversacionCommand(_comprador, _aviso), default);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoConversacion.RetenidaPorVerificacion, repo.Agregada!.Estado);
    }

    [Fact]
    public async Task Comprador_habilitado_obtiene_conversacion_activa()
    {
        var repo = new RepoFake(null);

        var resultado = await Crear(repo, habilitado: true)
            .Handle(new IniciarConversacionCommand(_comprador, _aviso), default);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoConversacion.Activa, repo.Agregada!.Estado);
    }

    [Fact]
    public async Task Volver_ya_habilitado_libera_la_conversacion_retenida()
    {
        // Red de seguridad: cubre el caso de que el evento UserVerified se haya perdido.
        var retenida = Conversacion.Crear(
            _aviso, _comprador, _vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;
        var repo = new RepoFake(retenida);

        var resultado = await Crear(repo, habilitado: true)
            .Handle(new IniciarConversacionCommand(_comprador, _aviso), default);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoConversacion.Activa, retenida.Estado);
    }

    [Fact]
    public async Task Volver_sin_estar_habilitado_la_deja_retenida()
    {
        var retenida = Conversacion.Crear(
            _aviso, _comprador, _vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;
        var repo = new RepoFake(retenida);

        var resultado = await Crear(repo, habilitado: false)
            .Handle(new IniciarConversacionCommand(_comprador, _aviso), default);

        Assert.True(resultado.EsExito);
        Assert.Equal(EstadoConversacion.RetenidaPorVerificacion, retenida.Estado);
    }
}
```

- [ ] **Paso 2: ejecutar el test y verlo fallar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~IniciarConversacionCommandHandlerTests
```

Esperado: error de compilación; no existe `IConsultaVerificacionComprador` ni el
quinto parámetro del handler.

- [ ] **Paso 3: crear el puerto**

`src/Chat/CaseritoApp.Chat.Application/Conversaciones/IConsultaVerificacionComprador.cs`:

```csharp
namespace CaseritoApp.Chat.Application.Conversaciones;

/// <summary>
/// Estado de verificación de identidad del comprador. Es un puerto porque Chat no puede
/// referenciar Identity: el Host provee el adaptador.
/// </summary>
public interface IConsultaVerificacionComprador
{
    /// <summary>KYC aprobado, o exención por rol de plataforma.</summary>
    public Task<bool> EstaHabilitadoAsync(Guid usuarioId, CancellationToken ct);
}
```

- [ ] **Paso 4: aplicar gate y liberación en el handler**

En `IniciarConversacionCommand.cs`, añadir la dependencia al constructor
primario del handler:

```csharp
public sealed class IniciarConversacionCommandHandler(
    IRepositorioConversaciones repositorio,
    IConsultaAvisoContactable consultaAviso,
    TimeProvider reloj,
    IRepositorioBloqueosUsuario bloqueos,
    IConsultaVerificacionComprador verificacion)
    : ICommandHandler<IniciarConversacionCommand, IniciarConversacionResultadoDto>
```

En la rama de conversación **existente**, justo antes de devolverla —después de
la comprobación de bloqueos—, liberar si procede:

```csharp
            if (existente.Estado == EstadoConversacion.RetenidaPorVerificacion
                && await verificacion.EstaHabilitadoAsync(request.CompradorId, cancellationToken))
            {
                existente.LiberarPorVerificacion(reloj.GetUtcNow());
            }
```

En la rama de conversación **nueva**, sustituir la llamada a `Crear` por:

```csharp
        var habilitado = await verificacion.EstaHabilitadoAsync(
            request.CompradorId, cancellationToken);
        var resultadoCreacion = Conversacion.Crear(
            aviso.AvisoId,
            request.CompradorId,
            aviso.VendedorId,
            reloj.GetUtcNow(),
            retenida: !habilitado);
```

- [ ] **Paso 5: añadir el método al puerto de repositorio**

En `IRepositorioConversaciones.cs`, para que la Tarea 5 lo consuma:

```csharp
    /// <summary>Conversaciones retenidas por verificación de un comprador concreto.</summary>
    public Task<IReadOnlyList<Conversacion>> ListarRetenidasDeCompradorAsync(
        Guid compradorId,
        CancellationToken ct);
```

Implementarlo en `RepositorioConversacionesEfCore.cs`:

```csharp
    public async Task<IReadOnlyList<Conversacion>> ListarRetenidasDeCompradorAsync(
        Guid compradorId,
        CancellationToken ct) =>
        await db.Conversaciones
            .Where(c => c.CompradorId == compradorId
                && c.Estado == EstadoConversacion.RetenidaPorVerificacion)
            .ToListAsync(ct);
```

Sin `AsNoTracking`: estas entidades se modifican y las persiste el UnitOfWork.

- [ ] **Paso 6: crear el adaptador del Host**

`src/Host/CaseritoApp.Host/Chat/ConsultaVerificacionCompradorAdapter.cs`,
gemelo de `Host/Orders/ConsultaVerificacionParticipanteAdapter.cs`:

```csharp
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Identity.Application.Kyc;

namespace CaseritoApp.Host.Chat;

public sealed class ConsultaVerificacionCompradorAdapter(IConsultaVerificacionKyc consulta)
    : IConsultaVerificacionComprador
{
    public Task<bool> EstaHabilitadoAsync(Guid usuarioId, CancellationToken ct) =>
        consulta.EstaHabilitadoParaMarketplaceAsync(usuarioId, ct);
}
```

- [ ] **Paso 7: registrarlo**

En `Program.cs`, junto al registro de
`ConsultaVerificacionParticipanteAdapter` (buscarlo por nombre y copiar el mismo
tiempo de vida):

```csharp
builder.Services.AddScoped<IConsultaVerificacionComprador, ConsultaVerificacionCompradorAdapter>();
```

- [ ] **Paso 8: ejecutar el test y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~IniciarConversacionCommandHandlerTests
```

Esperado: 4 tests en verde.

- [ ] **Paso 9: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src CaseritoApp/tests/CaseritoApp.UnitTests/Chat/IniciarConversacionCommandHandlerTests.cs
git commit -m "feat(chat): retiene la conversacion del comprador sin verificar"
```

---

### Tarea 3: Invisibilidad para el vendedor

**Archivos**
- Modificar: `src/Chat/CaseritoApp.Chat.Infrastructure/Conversaciones/ConsultaConversacionesEfCore.cs`
- Test: `tests/CaseritoApp.IntegrationTests/ChatRetencionTests.cs` (crear)

**Interfaces**
- Consume: `EstadoConversacion.RetenidaPorVerificacion` de la Tarea 1.
- Produce: nada.

El filtro es el mismo en los tres métodos:
`!(c.Estado == EstadoConversacion.RetenidaPorVerificacion && c.VendedorId == usuarioId)`.

`PuedeRecibirTiempoRealAsync` **no se toca**: ya exige
`c.Estado == EstadoConversacion.Activa`, de modo que lo retenido queda excluido
por construcción.

- [ ] **Paso 1: escribir el test que falla**

Crear `tests/CaseritoApp.IntegrationTests/ChatRetencionTests.cs`. Tomar como
modelo la preparación de `ChatPersistenciaTests.cs` —cómo obtiene el
`ChatDbContext` desde el factory y cómo crea conversaciones— y copiar ese
patrón; no inventar una forma nueva de montar el contexto.

```csharp
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

public sealed class ChatRetencionTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task El_vendedor_no_percibe_la_conversacion_retenida()
    {
        var comprador = Guid.NewGuid();
        var vendedor = Guid.NewGuid();
        var aviso = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaConversaciones>();

        var conversacion = Conversacion.Crear(
            aviso, comprador, vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;
        var mensaje = conversacion.CrearMensaje(
            comprador, Guid.NewGuid(), 1, "hola", DateTimeOffset.UtcNow).Valor;
        db.Conversaciones.Add(conversacion);
        db.Mensajes.Add(mensaje);
        await db.SaveChangesAsync();

        // El vendedor no la ve por ninguna vía.
        var listadoVendedor = await consulta.ListarAsync(vendedor, null, 20, default);
        Assert.DoesNotContain(listadoVendedor.Items, c => c.Id == conversacion.Id);
        Assert.Equal(0, await consulta.ContarNoLeidosAsync(vendedor, default));
        Assert.False(await consulta.PuedeAccederAsync(conversacion.Id, vendedor, default));
        Assert.False(await consulta.PuedeRecibirTiempoRealAsync(conversacion.Id, vendedor, default));

        // El comprador sí.
        var listadoComprador = await consulta.ListarAsync(comprador, null, 20, default);
        Assert.Contains(listadoComprador.Items, c => c.Id == conversacion.Id);
        Assert.True(await consulta.PuedeAccederAsync(conversacion.Id, comprador, default));
    }

    [Fact]
    public async Task Tras_liberarla_el_vendedor_la_ve_con_sus_mensajes()
    {
        var comprador = Guid.NewGuid();
        var vendedor = Guid.NewGuid();
        var aviso = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaConversaciones>();

        var conversacion = Conversacion.Crear(
            aviso, comprador, vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;
        var mensaje = conversacion.CrearMensaje(
            comprador, Guid.NewGuid(), 1, "hola", DateTimeOffset.UtcNow).Valor;
        db.Conversaciones.Add(conversacion);
        db.Mensajes.Add(mensaje);
        await db.SaveChangesAsync();

        conversacion.LiberarPorVerificacion(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var listado = await consulta.ListarAsync(vendedor, null, 20, default);
        Assert.Contains(listado.Items, c => c.Id == conversacion.Id);
        Assert.Equal(1, await consulta.ContarNoLeidosAsync(vendedor, default));
    }
}
```

Si el nombre real del `DbSet` de mensajes o de la propiedad `Items` de la página
difiere, tomarlo de `ChatPersistenciaTests.cs`.

- [ ] **Paso 2: ejecutar el test y verlo fallar**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~ChatRetencionTests
```

Requiere Docker. Esperado: falla en el listado y el contador del vendedor, que
hoy no filtran por estado.

- [ ] **Paso 3: filtrar en `PuedeAccederAsync`**

```csharp
    public Task<bool> PuedeAccederAsync(
        Guid conversacionId,
        Guid usuarioId,
        CancellationToken ct) =>
        db.Conversaciones.AsNoTracking().AnyAsync(
            c => c.Id == conversacionId
                && (c.CompradorId == usuarioId || c.VendedorId == usuarioId)
                && !(c.Estado == EstadoConversacion.RetenidaPorVerificacion
                    && c.VendedorId == usuarioId),
            ct);
```

- [ ] **Paso 4: filtrar en `ListarAsync`**

En la consulta base del método:

```csharp
        var consulta = db.Conversaciones.AsNoTracking()
            .Where(c => (c.CompradorId == usuarioId || c.VendedorId == usuarioId)
                && !(c.Estado == EstadoConversacion.RetenidaPorVerificacion
                    && c.VendedorId == usuarioId));
```

- [ ] **Paso 5: filtrar en `ContarNoLeidosAsync`**

```csharp
    public Task<int> ContarNoLeidosAsync(Guid usuarioId, CancellationToken ct) =>
        db.Mensajes.AsNoTracking().CountAsync(
            m => m.RemitenteId != usuarioId
                && db.Conversaciones.Any(c =>
                    c.Id == m.ConversacionId
                    && (c.CompradorId == usuarioId || c.VendedorId == usuarioId)
                    && !(c.Estado == EstadoConversacion.RetenidaPorVerificacion
                        && c.VendedorId == usuarioId)
                    && m.Secuencia > (c.CompradorId == usuarioId
                        ? c.UltimaSecuenciaLeidaComprador
                        : c.UltimaSecuenciaLeidaVendedor)),
            ct);
```

- [ ] **Paso 6: ejecutar el test y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~ChatRetencionTests
```

Esperado: 2 tests en verde.

- [ ] **Paso 7: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src/Chat CaseritoApp/tests/CaseritoApp.IntegrationTests/ChatRetencionTests.cs
git commit -m "feat(chat): oculta al vendedor las conversaciones retenidas"
```

---

### Tarea 4: No notificar mientras esté retenida

**Archivos**
- Modificar: `src/Chat/CaseritoApp.Chat.Application/Mensajes/DtosMensaje.cs`
- Modificar: `src/Chat/CaseritoApp.Chat.Application/Mensajes/EnviarMensajeCommand.cs`
- Modificar: `src/Host/CaseritoApp.Host/Endpoints/ChatEndpoints.cs`
- Test: `tests/CaseritoApp.UnitTests/Chat/EnviarMensajeRetenidaTests.cs` (crear)

**Interfaces**
- Consume: `EstadoConversacion.RetenidaPorVerificacion` de la Tarea 1.
- Produce: `EnviarMensajeResultadoDto` con un cuarto componente `bool Retenida`.

- [ ] **Paso 1: escribir el test que falla**

Crear `tests/CaseritoApp.UnitTests/Chat/EnviarMensajeRetenidaTests.cs`. Copiar
los fakes de repositorio de mensajes y bloqueos del archivo de tests de
`EnviarMensajeCommandHandler` que ya exista en `tests/CaseritoApp.UnitTests/Chat/`;
si no existe ninguno, escribirlos siguiendo el patrón de `RepoFake` de la Tarea 2.

```csharp
    [Fact]
    public async Task Mensaje_en_conversacion_retenida_marca_el_resultado_como_retenido()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), _comprador, _vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;

        var resultado = await CrearHandler(conversacion).Handle(
            new EnviarMensajeCommand(conversacion.Id, _comprador, Guid.NewGuid(), "hola"),
            default);

        Assert.True(resultado.EsExito);
        Assert.True(resultado.Valor.Retenida);
    }

    [Fact]
    public async Task Mensaje_en_conversacion_activa_no_marca_retenido()
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), _comprador, _vendedor, DateTimeOffset.UtcNow).Valor;

        var resultado = await CrearHandler(conversacion).Handle(
            new EnviarMensajeCommand(conversacion.Id, _comprador, Guid.NewGuid(), "hola"),
            default);

        Assert.True(resultado.EsExito);
        Assert.False(resultado.Valor.Retenida);
    }
```

- [ ] **Paso 2: ejecutar el test y verlo fallar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~EnviarMensajeRetenidaTests
```

Esperado: error de compilación; `EnviarMensajeResultadoDto` no tiene `Retenida`.

- [ ] **Paso 3: ampliar el DTO**

En `DtosMensaje.cs`, añadir el componente **al final** para no romper el orden
de los existentes:

```csharp
public sealed record EnviarMensajeResultadoDto(
    MensajeDto Mensaje,
    Guid DestinatarioId,
    bool FueCreado,
    bool Retenida = false);
```

- [ ] **Paso 4: propagarlo desde el handler**

En `EnviarMensajeCommand.cs` hay **dos** puntos que construyen el DTO: el de
idempotencia (mensaje ya existente) y el de creación. En ambos, pasar si la
conversación está retenida:

```csharp
        var retenida = conversacion.Estado == EstadoConversacion.RetenidaPorVerificacion;
```

Calcularlo una vez tras obtener la conversación, y añadirlo como cuarto
argumento en las dos construcciones de `EnviarMensajeResultadoDto`.

- [ ] **Paso 5: no publicar el evento si está retenida**

En `ChatEndpoints.EnviarAsync`, la condición de publicación pasa de
`if (resultado.Valor.FueCreado)` a:

```csharp
            // Conversación retenida: el vendedor no debe percibirla por ninguna vía. No publicar
            // ChatMessageSent corta a la vez SignalR y la notificación push.
            if (resultado.Valor.FueCreado && !resultado.Valor.Retenida)
```

- [ ] **Paso 6: ejecutar el test y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~EnviarMensajeRetenidaTests
```

Esperado: 2 tests en verde.

- [ ] **Paso 7: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src CaseritoApp/tests/CaseritoApp.UnitTests/Chat/EnviarMensajeRetenidaTests.cs
git commit -m "feat(chat): no notifica mensajes de conversaciones retenidas"
```

---

### Tarea 5: Liberación al verificarse

**Archivos**
- Crear: `src/Chat/CaseritoApp.Chat.Application/Conversaciones/LiberarConversacionesAlVerificarHandler.cs`
- Modificar: `src/Chat/CaseritoApp.Chat.Application/Mensajes/IRepositorioMensajes.cs`
- Modificar: `src/Chat/CaseritoApp.Chat.Infrastructure/Mensajes/RepositorioMensajesEfCore.cs`
- Test: `tests/CaseritoApp.UnitTests/Chat/LiberarConversacionesAlVerificarHandlerTests.cs`

**Interfaces**
- Consume: `IRepositorioConversaciones.ListarRetenidasDeCompradorAsync` y
  `Conversacion.LiberarPorVerificacion` de tareas anteriores;
  `UserVerified` de `CaseritoApp.BuildingBlocks.Contracts.Identity`;
  `ChatMessageSent(Guid EventId, DateTimeOffset OcurridoEn, Guid ConversacionId, Guid MensajeId, long Secuencia, Guid RemitenteId, Guid DestinatarioId)`
  de `CaseritoApp.BuildingBlocks.Contracts.Chat`.
- Produce: `IRepositorioMensajes.ObtenerUltimoDeConversacionAsync(Guid conversacionId, CancellationToken ct)`.

El handler publica **un** `ChatMessageSent` por conversación liberada, referido
al último mensaje, para que el vendedor reciba una sola notificación en lugar de
una por mensaje acumulado. Ese evento necesita el id y la secuencia del último
mensaje, que la conversación no guarda: de ahí el método nuevo en el puerto de
mensajes.

- [ ] **Paso 1: escribir el test que falla**

```csharp
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Domain.Conversaciones;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseritoApp.UnitTests.Chat;

public sealed class LiberarConversacionesAlVerificarHandlerTests
{
    private static readonly Guid _comprador = Guid.NewGuid();
    private static readonly Guid _vendedor = Guid.NewGuid();

    private sealed class RepoFake(List<Conversacion> retenidas) : IRepositorioConversaciones
    {
        public Task<Conversacion?> ObtenerPorCompradorAvisoAsync(
            Guid compradorId, Guid avisoId, CancellationToken ct) =>
            Task.FromResult<Conversacion?>(null);

        public Task<Conversacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Conversacion?>(null);

        public void Agregar(Conversacion conversacion) { }

        public Task<IReadOnlyList<Conversacion>> ListarRetenidasDeCompradorAsync(
            Guid compradorId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Conversacion>>(
                compradorId == _comprador ? retenidas : []);
    }

    private sealed class MensajesFake : IRepositorioMensajes
    {
        public Task<Mensaje?> ObtenerAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Mensaje?>(null);

        public Task<Mensaje?> ObtenerPorClaveAsync(
            Guid conversacionId, Guid remitenteId, Guid clave, CancellationToken ct) =>
            Task.FromResult<Mensaje?>(null);

        public Task<long> ReservarSecuenciaAsync(CancellationToken ct) => Task.FromResult(1L);

        public void Agregar(Mensaje mensaje) { }

        // Devuelve el último mensaje realmente creado en la conversación pedida.
        public Task<Mensaje?> ObtenerUltimoDeConversacionAsync(
            Guid conversacionId, CancellationToken ct) =>
            Task.FromResult(Ultimos.GetValueOrDefault(conversacionId));

        public Dictionary<Guid, Mensaje> Ultimos { get; } = [];
    }

    private sealed class PublisherFake : IPublisher
    {
        public List<object> Publicados { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Publicados.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(
            TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Publicados.Add(notification!);
            return Task.CompletedTask;
        }
    }

    private static Conversacion Retenida(MensajesFake mensajes)
    {
        var conversacion = Conversacion.Crear(
            Guid.NewGuid(), _comprador, _vendedor, DateTimeOffset.UtcNow, retenida: true).Valor;
        var mensaje = conversacion.CrearMensaje(
            _comprador, Guid.NewGuid(), 1, "hola", DateTimeOffset.UtcNow).Valor;
        mensajes.Ultimos[conversacion.Id] = mensaje;
        return conversacion;
    }

    [Fact]
    public async Task Libera_todas_las_conversaciones_retenidas_del_comprador()
    {
        var mensajes = new MensajesFake();
        var primera = Retenida(mensajes);
        var segunda = Retenida(mensajes);
        var handler = new LiberarConversacionesAlVerificarHandler(
            new RepoFake([primera, segunda]),
            mensajes,
            new PublisherFake(),
            TimeProvider.System,
            NullLogger<LiberarConversacionesAlVerificarHandler>.Instance);

        await handler.Handle(
            new UserVerified(Guid.NewGuid(), DateTimeOffset.UtcNow, _comprador), default);

        Assert.Equal(EstadoConversacion.Activa, primera.Estado);
        Assert.Equal(EstadoConversacion.Activa, segunda.Estado);
    }

    [Fact]
    public async Task Publica_una_sola_notificacion_por_conversacion()
    {
        var mensajes = new MensajesFake();
        var conversacion = Retenida(mensajes);
        // Dos mensajes acumulados: la notificación debe seguir siendo una sola.
        var segundo = conversacion.CrearMensaje(
            _comprador, Guid.NewGuid(), 2, "sigo aquí", DateTimeOffset.UtcNow).Valor;
        mensajes.Ultimos[conversacion.Id] = segundo;
        var publisher = new PublisherFake();
        var handler = new LiberarConversacionesAlVerificarHandler(
            new RepoFake([conversacion]),
            mensajes,
            publisher,
            TimeProvider.System,
            NullLogger<LiberarConversacionesAlVerificarHandler>.Instance);

        await handler.Handle(
            new UserVerified(Guid.NewGuid(), DateTimeOffset.UtcNow, _comprador), default);

        var evento = Assert.Single(publisher.Publicados.OfType<ChatMessageSent>());
        Assert.Equal(conversacion.Id, evento.ConversacionId);
        Assert.Equal(segundo.Id, evento.MensajeId);
        Assert.Equal(_vendedor, evento.DestinatarioId);
    }

    [Fact]
    public async Task Un_usuario_sin_conversaciones_retenidas_no_falla()
    {
        var publisher = new PublisherFake();
        var handler = new LiberarConversacionesAlVerificarHandler(
            new RepoFake([]),
            new MensajesFake(),
            publisher,
            TimeProvider.System,
            NullLogger<LiberarConversacionesAlVerificarHandler>.Instance);

        await handler.Handle(
            new UserVerified(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid()), default);

        Assert.Empty(publisher.Publicados);
    }
}
```

Añadir a la cabecera del archivo de test:
`using CaseritoApp.BuildingBlocks.Contracts.Chat;`,
`using CaseritoApp.Chat.Application.Mensajes;` y `using MediatR;`.

Comprobar la firma real de `UserVerified` en
`src/BuildingBlocks/CaseritoApp.BuildingBlocks.Contracts/Identity/` y usarla tal
cual; el orden de sus componentes debe coincidir.

- [ ] **Paso 2: ejecutar el test y verlo fallar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~LiberarConversacionesAlVerificarHandlerTests
```

Esperado: error de compilación; el handler no existe.

- [ ] **Paso 3: implementar el handler**

`src/Chat/CaseritoApp.Chat.Application/Conversaciones/LiberarConversacionesAlVerificarHandler.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Contracts.Chat;
using CaseritoApp.BuildingBlocks.Contracts.Identity;
using CaseritoApp.Chat.Application.Mensajes;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Chat.Application.Conversaciones;

/// <summary>
/// Al verificarse un comprador, libera sus conversaciones retenidas para que el vendedor pueda
/// verlas. Es el mecanismo principal; el respaldo está en IniciarConversacionCommand.
/// </summary>
public sealed partial class LiberarConversacionesAlVerificarHandler(
    IRepositorioConversaciones repositorio,
    IRepositorioMensajes mensajes,
    IPublisher publisher,
    TimeProvider reloj,
    ILogger<LiberarConversacionesAlVerificarHandler> logger)
    : INotificationHandler<UserVerified>
{
    public async Task Handle(UserVerified notification, CancellationToken cancellationToken)
    {
        var retenidas = await repositorio.ListarRetenidasDeCompradorAsync(
            notification.UsuarioId, cancellationToken);
        if (retenidas.Count == 0)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        foreach (var conversacion in retenidas)
        {
            if (!conversacion.LiberarPorVerificacion(ahora).EsExito)
            {
                continue;
            }

            // Una sola notificación por conversación, referida al último mensaje: publicar una
            // por cada mensaje acumulado sería una ráfaga de push para el vendedor.
            var ultimo = await mensajes.ObtenerUltimoDeConversacionAsync(
                conversacion.Id, cancellationToken);
            if (ultimo is not null)
            {
                await publisher.Publish(
                    new ChatMessageSent(
                        Guid.NewGuid(),
                        ahora,
                        conversacion.Id,
                        ultimo.Id,
                        ultimo.Secuencia,
                        conversacion.CompradorId,
                        conversacion.VendedorId),
                    cancellationToken);
            }
        }

        RegistrarLiberacion(logger, notification.UsuarioId, retenidas.Count);
    }

    // Auditoría sin PII: solo el usuario y cuántas conversaciones se liberaron.
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Conversaciones liberadas por verificación: usuario={UsuarioId} total={Total}")]
    private static partial void RegistrarLiberacion(ILogger logger, Guid usuarioId, int total);
}
```

Si el nombre del componente de `UserVerified` que lleva el id del usuario no es
`UsuarioId`, usar el real.

- [ ] **Paso 4: añadir el método al puerto de mensajes**

En `IRepositorioMensajes.cs`:

```csharp
    /// <summary>Último mensaje de una conversación por secuencia, o null si no tiene ninguno.</summary>
    public Task<Mensaje?> ObtenerUltimoDeConversacionAsync(
        Guid conversacionId,
        CancellationToken ct);
```

Implementarlo en `RepositorioMensajesEfCore.cs`:

```csharp
    public Task<Mensaje?> ObtenerUltimoDeConversacionAsync(
        Guid conversacionId,
        CancellationToken ct) =>
        db.Mensajes
            .AsNoTracking()
            .Where(m => m.ConversacionId == conversacionId)
            .OrderByDescending(m => m.Secuencia)
            .FirstOrDefaultAsync(ct);
```

- [ ] **Paso 5: ejecutar el test y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~LiberarConversacionesAlVerificarHandlerTests
```

Esperado: 3 tests en verde.

- [ ] **Paso 5: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src/Chat CaseritoApp/tests/CaseritoApp.UnitTests/Chat/LiberarConversacionesAlVerificarHandlerTests.cs
git commit -m "feat(chat): libera las conversaciones retenidas al verificarse"
```

---

### Tarea 6: Unificar el helper de identidad habilitada

Mejora dirigida: el helper está duplicado y este bloque añadiría una tercera
copia si no se unifica.

**Archivos**
- Crear: `src/Host/CaseritoApp.Host/Autorizacion/ClaimsPrincipalExtensions.cs`
- Modificar: `src/Host/CaseritoApp.Host/Endpoints/AvisosEndpoints.cs:263`
- Modificar: `src/Host/CaseritoApp.Host/Endpoints/OrdersEndpoints.cs:286`

**Interfaces**
- Consume: nada.
- Produce: `ClaimsPrincipalExtensions.TieneIdentidadHabilitada(this ClaimsPrincipal usuario)`.

- [ ] **Paso 1: crear el helper**

Abrir primero `AvisosEndpoints.cs:263` y copiar el cuerpo **exacto** del método
existente, para no alterar el comportamiento:

```csharp
using System.Security.Claims;
using CaseritoApp.Identity.Domain.Autorizacion;

namespace CaseritoApp.Host.Autorizacion;

public static class ClaimsPrincipalExtensions
{
    /// <summary>KYC aprobado o exención por rol de plataforma, según el claim del token.</summary>
    public static bool TieneIdentidadHabilitada(this ClaimsPrincipal usuario) =>
        bool.TryParse(usuario.FindFirstValue(ClaimsApp.IdentidadHabilitada), out var habilitada)
        && habilitada;
}
```

- [ ] **Paso 2: sustituir las dos copias**

Eliminar el método privado de `AvisosEndpoints.cs` y el de `OrdersEndpoints.cs`,
añadir `using CaseritoApp.Host.Autorizacion;` en ambos y dejar las llamadas como
`usuario.TieneIdentidadHabilitada()`.

- [ ] **Paso 3: ejecutar los tests afectados**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~Avisos|FullyQualifiedName~Orders"
```

Esperado: verde. Es refactor puro: si algo cambia de comportamiento, el cuerpo
copiado no era idéntico.

- [ ] **Paso 4: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp/src/Host
git commit -m "refactor(host): unifica el helper de identidad habilitada [sin-test] refactor sin cambio de comportamiento, cubierto por los tests existentes"
```

---

### Tarea 7: Flujo extremo a extremo y contrato

**Archivos**
- Modificar: `tests/CaseritoApp.IntegrationTests/ChatRetencionTests.cs`
- Modificar: `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` (regenerado)
- Modificar: `web/src/api/schema.d.ts` (regenerado)

**Interfaces**
- Consume: todo lo anterior.
- Produce: nada.

- [ ] **Paso 1: añadir el test de flujo completo**

Añadir a `ChatRetencionTests.cs`. Para registrar usuarios y obtener tokens,
copiar los helpers de `KycArgosFlujoTests.cs` (`Email`, `Autorizada`,
`RegistrarYLoguearAsync`); para el ARGOS falso, `VerificadorArgosEstatico` del
mismo archivo.

```csharp
    [Fact]
    public async Task Comprador_sin_verificar_escribe_y_el_vendedor_lo_ve_tras_aprobarse_el_kyc()
    {
        using var cliente = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<IVerificadorIdentidadArgos>(_ =>
                new VerificadorArgosEstatico(Result.Exito(
                    new VerificacionFacialResultado(
                        Coinciden: true, SimilitudPercent: 95, MotivoRechazo: null))));
        })).CreateClient();

        // 1. Vendedor verificado con un aviso publicado.
        var emailVendedor = Email("chat-vendedor");
        var tokenVendedor = await RegistrarYLoguearAsync(cliente, emailVendedor, null);
        tokenVendedor = await VerificarYRelogearAsync(cliente, emailVendedor, tokenVendedor, "1234570");
        var avisoId = await PublicarAvisoAsync(cliente, tokenVendedor);

        // 2. Comprador SIN verificar inicia conversación y envía un mensaje.
        var emailComprador = Email("chat-comprador");
        var tokenComprador = await RegistrarYLoguearAsync(cliente, emailComprador, null);

        using var iniciar = Autorizada(HttpMethod.Post, "/api/chat/conversaciones", tokenComprador);
        iniciar.Content = JsonContent.Create(new IniciarConversacionRequest(avisoId));
        var respuestaIniciar = await cliente.SendAsync(iniciar);
        Assert.Equal(HttpStatusCode.OK, respuestaIniciar.StatusCode);
        var iniciada = await respuestaIniciar.Content
            .ReadFromJsonAsync<IniciarConversacionResultadoDto>();
        var conversacionId = iniciada!.Conversacion.Id;

        using var enviar = Autorizada(
            HttpMethod.Post, $"/api/chat/conversaciones/{conversacionId}/mensajes", tokenComprador);
        enviar.Content = JsonContent.Create(
            new EnviarMensajeRequest(Guid.NewGuid(), "hola, sigue disponible?"));
        Assert.Equal(HttpStatusCode.Created, (await cliente.SendAsync(enviar)).StatusCode);

        // 3 y 4. El vendedor no percibe nada a través de la API.
        using var listar = Autorizada(HttpMethod.Get, "/api/chat/conversaciones", tokenVendedor);
        var listado = await (await cliente.SendAsync(listar)).Content.ReadAsStringAsync();
        Assert.DoesNotContain(conversacionId.ToString(), listado, StringComparison.OrdinalIgnoreCase);

        using var noLeidos = Autorizada(HttpMethod.Get, "/api/chat/no-leidos", tokenVendedor);
        var conteo = await (await cliente.SendAsync(noLeidos)).Content.ReadAsStringAsync();
        Assert.Contains("0", conteo, StringComparison.Ordinal);

        // 5. El comprador se verifica: ARGOS aprueba y el KYC se resuelve solo.
        await VerificarYRelogearAsync(cliente, emailComprador, tokenComprador, "1234571");

        // 6 y 7. Ahora el vendedor sí la ve, con el mensaje dentro.
        using var listarTras = Autorizada(HttpMethod.Get, "/api/chat/conversaciones", tokenVendedor);
        var listadoTras = await (await cliente.SendAsync(listarTras)).Content.ReadAsStringAsync();
        Assert.Contains(conversacionId.ToString(), listadoTras, StringComparison.OrdinalIgnoreCase);

        using var mensajes = Autorizada(
            HttpMethod.Get, $"/api/chat/conversaciones/{conversacionId}/mensajes", tokenVendedor);
        var cuerpoMensajes = await (await cliente.SendAsync(mensajes)).Content.ReadAsStringAsync();
        Assert.Contains("sigue disponible", cuerpoMensajes, StringComparison.Ordinal);
    }
```

Este test necesita dos helpers que **no** existen todavía en el archivo y hay que
escribir en él:

- `VerificarYRelogearAsync(HttpClient cliente, string email, string token, string numeroCi)`:
  hace `POST /api/kyc/?numeroCi={numeroCi}&departamentoExpedicion=LaPaz` con el
  formulario multipart (copiar `Formulario()` de `KycArgosFlujoTests.cs`), y
  vuelve a loguear para obtener un token con el claim actualizado. Devuelve el
  token nuevo.
- `PublicarAvisoAsync(HttpClient cliente, string token)`: publica un aviso con el
  vendedor autenticado y devuelve su id. Copiar la forma exacta del cuerpo de
  `POST /api/avisos` desde `AvisosFlujoTests.cs`, que ya lo hace.

Los pasos 3, 4 y 6 son los que dan valor: comprueban lo que el vendedor observa
**a través de la API**. No sustituirlos por consultas directas a la base de
datos.

Si el listado devuelve un envoltorio con cursor, comparar sobre el cuerpo en
crudo como se hace arriba es suficiente y evita acoplar el test a la forma
exacta de la paginación.

- [ ] **Paso 2: ejecutarlo y verlo pasar**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~ChatRetencionTests
```

Esperado: verde. Si el paso 6 falla, el handler de liberación no se está
ejecutando: comprobar que `UserVerified` se publica por el publicador de MediatR
registrado en `Program.cs:73-74` y que el handler está en un ensamblado
escaneado por MediatR.

- [ ] **Paso 3: regenerar contrato y cliente**

Cambia el conjunto de valores posibles de `Estado`.

Desde `CaseritoApp/`:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Testing"; dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj -p:GenerateOpenApi=true
```

Desde `web/`:

```powershell
npm run generate:api
```

- [ ] **Paso 4: ejecutar la suite completa**

Desde `CaseritoApp/`:

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes --exclude-diagnostics CA1502 CA1505 CA1506
```

Desde `web/`:

```powershell
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

- [ ] **Paso 5: revisión final contra el spec**

Comprobar uno a uno los criterios de aceptación de la sección 10 del spec.
Prestar atención especial a:

- el vendedor no percibe la conversación retenida por **ninguna** de las cuatro
  vías (listado, contador, tiempo real, push);
- ningún log incluye texto de mensajes;
- la liberación produce una sola notificación por conversación;
- sin mojibake en los textos nuevos.

- [ ] **Paso 6: verificar y commitear**

```powershell
./verify.ps1 -Changed
```

```bash
git add CaseritoApp web
git commit -m "test(chat): cubre el flujo de retencion y liberacion extremo a extremo"
```

---

## Fuera de alcance

Confirmado en el spec: la UI web (bloque siguiente), la purga de conversaciones
retenidas de usuarios que nunca se verifican, el outbox para el guardado
multi-contexto, y cualquier cambio en los gates de publicar aviso o solicitar
acuerdo.

---

## Cierre (2026-09-23)

Ejecutado en `912a559..a553e8c`, un commit por tarea. Suite completa en verde y
`./verify.ps1 -Changed` verde en cada commit. Sin migraciones: `dotnet ef
migrations has-pending-model-changes` confirma que el modelo no deriva del
snapshot, como se esperaba de añadir un valor al final de un enum mapeado como
`int`.

Cuatro puntos donde **este plan estaba equivocado** y hubo que apartarse de él.
Quedan aquí para que un lector futuro no los repita:

1. **`ErroresConversacion.TransicionInvalida` no existe.** El plan lo usaba en el
   código de `LiberarPorVerificacion` y solo mencionaba el fallback en una nota
   al pie del test. Se usó `NoDisponibleParaEnvio`, que es el código real para
   una transición no permitida.
2. **La plantilla de log rompía el gate anti-PII.** El `usuario={UsuarioId}` que
   proponía el plan choca con `ChatPiiTests`, que prohíbe el fragmento literal
   `Id` en plantillas. Quedó `usuario={Usuario}`.
3. **El helper de la tarea 6 no era el que decía el plan.** El snippet usaba
   `bool.TryParse`; el cuerpo real de `AvisosEndpoints.cs` usa
   `string.Equals(..., "true", OrdinalIgnoreCase)`. Se copió el real.
4. **Dos shapes erróneos en el test extremo a extremo.** `POST /api/chat/conversaciones`
   devuelve `201 Created`, no `200 OK`, y serializa `ConversacionDto`, no
   `IniciarConversacionResultadoDto`.

Y dos efectos que el plan no previó:

- **Dos tests de integración preexistentes se rompieron** (`ChatFlujoTests`,
  `ChatSeguridadConcurrenciaTests`): usaban un comprador sin verificar, así que
  su conversación pasó a nacer retenida y el vendedor dejó de poder escribir en
  ella. Se restauró la precondición con el helper de test
  `CaseritoApiFactoryExtensions.AprobarKycAsync`. Un plan que cambia una regla
  de acceso debería enumerar de antemano los tests que dependen de la regla
  vieja.
- **La regeneración del contrato fue no-op**, porque el enum viaja como número.
  Ver §11.2 del spec: tiene consecuencias para el bloque de UI.

Pendiente heredado: el criterio 5 del spec quedó sin test porque este plan no lo
incluyó. Ver §11.3 del spec.
