# Fase 6 — Notificaciones, endurecimiento y piloto

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Completar el bounded context Notifications con notificaciones in-app + email, búsquedas guardadas, puntos de encuentro seguros, endurecer la plataforma y dejar lista la configuración del piloto en Cochabamba.

**Architecture:** Se completan los proyectos existentes `Notifications.Domain`, `Notifications.Application` e `Notifications.Infrastructure`. Se consumen eventos de integración existentes (`ChatMessageSent`, `OrderStatusChanged`, `ProductPublished`) mediante un publicador que los despacha como notificaciones MediatR. No hay dependencias directas ni FK cruzadas hacia otros bounded contexts.

**Tech Stack:** .NET 10, EF Core 10, SQL Server, MediatR, FluentValidation, xUnit, React + TypeScript + MUI + TanStack Query, cliente OpenAPI tipado.

## Global Constraints

- Textos de UI, comentarios y nombres de negocio en español; código en inglés siguiendo convenciones del repo.
- Clean Architecture: `Domain` no depende hacia afuera; `Application` solo de `Domain` y `BuildingBlocks`; `Infrastructure` implementa puertos; `Host` compone.
- No crear referencias entre bounded contexts salvo contratos permitidos.
- CQRS-lite: MediatR + Result + FluentValidation; reglas de negocio en dominio.
- Versiones de paquetes exclusivamente en `Directory.Packages.props`.
- Respetar `.editorconfig`, nullable y warnings-as-errors.
- Migraciones por contexto y schema; sin FK cruzada entre contextos.
- Anti-PII: nunca loggear emails, cuerpos de mensajes, IDs sensibles, contenido de reseñas, documentos ni tokens.
- Comandos backend desde `CaseritoApp/`; frontend desde `web/`.

---

## Task 0: Habilitar despacho de eventos de integración por MediatR

Los eventos de integración (`IIntegrationEvent`) hoy solo se loguean. Para que
Notifications pueda suscribir handlers, el publicador debe despacharlos como
notificaciones MediatR.

**Files:**
- Modify: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Contracts/CaseritoApp.BuildingBlocks.Contracts.csproj`
- Modify: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Contracts/IntegrationEvent.cs`
- Create: `CaseritoApp/src/BuildingBlocks/CaseritoApp.BuildingBlocks.Infrastructure/Messaging/PublicadorEventosIntegracionMediatR.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`

**Interfaces:**
- Consumes: `IPublicadorEventosIntegracion` (existente), `IPublisher` (MediatR).
- Produces: `PublicadorEventosIntegracionMediatR` reemplaza a `PublicadorEventosIntegracionLog`.

- [ ] **Step 1: Agregar referencia a MediatR en BuildingBlocks.Contracts**

  ```xml
  <ItemGroup>
    <PackageReference Include="MediatR" />
  </ItemGroup>
  ```

  Asegurarse de que `Directory.Packages.props` ya tenga la versión centralizada de
  `MediatR` (debería estar porque `BuildingBlocks.Domain` ya la usa).

- [ ] **Step 2: Hacer que IIntegrationEvent extienda INotification**

  ```csharp
  using MediatR;

  namespace CaseritoApp.BuildingBlocks.Contracts;

  public interface IIntegrationEvent : INotification
  {
      public Guid EventId { get; }
      public DateTimeOffset OcurridoEn { get; }
  }
  ```

- [ ] **Step 3: Crear el publicador MediatR**

  ```csharp
  using CaseritoApp.BuildingBlocks.Application.Abstractions;
  using CaseritoApp.BuildingBlocks.Contracts;
  using MediatR;
  using Microsoft.Extensions.Logging;

  namespace CaseritoApp.BuildingBlocks.Infrastructure.Messaging;

  public sealed partial class PublicadorEventosIntegracionMediatR(
      IPublisher publisher,
      ILogger<PublicadorEventosIntegracionMediatR> logger)
      : IPublicadorEventosIntegracion
  {
      public async Task PublicarAsync(IIntegrationEvent evento, CancellationToken ct)
      {
          RegistrarEvento(logger, evento.GetType().Name, evento.EventId);
          await publisher.Publish(evento, ct);
      }

      [LoggerMessage(Level = LogLevel.Information, Message = "Evento de integración publicado: tipo={Tipo} eventId={EventId}")]
      private static partial void RegistrarEvento(ILogger logger, string tipo, Guid eventId);
  }
  ```

- [ ] **Step 4: Reemplazar el publicador en Host**

  En `Program.cs`, después de `builder.Services.AgregarIdentity(...)`:

  ```csharp
  builder.Services.Replace(
      ServiceDescriptor.Singleton<IPublicadorEventosIntegracion,
          PublicadorEventosIntegracionMediatR>());
  ```

  Requiere `using CaseritoApp.BuildingBlocks.Infrastructure.Messaging;`.

- [ ] **Step 5: Verificar build**

  Run: `dotnet build CaseritoApp.sln`
  Expected: exit 0, sin warnings nuevos.

- [ ] **Step 6: Commit**

  ```bash
  git add -A
  git commit -m "feat(integration): despacha eventos de integracion via MediatR"
  ```

---

## Task 1: Domain de Notifications — agregado Notificacion

**Files:**
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Domain/Notificaciones/Notificacion.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Domain/Notificaciones/TipoNotificacion.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Domain/Notificaciones/ErroresNotificacion.cs`

**Interfaces:**
- Consumes: `AggregateRoot`, `Result<T>`, `Error` de `BuildingBlocks.Domain`.
- Produces: `Notificacion` con factory `Crear`.

- [ ] **Step 1: Escribir tests de dominio**

  Create: `CaseritoApp/tests/CaseritoApp.UnitTests/Notifications/NotificacionTests.cs`

  ```csharp
  using CaseritoApp.Notifications.Domain.Notificaciones;

  namespace CaseritoApp.UnitTests.Notifications;

  public sealed class NotificacionTests
  {
      [Fact]
      public void Crear_ConDatosValidos_RetornaExito()
      {
          var resultado = Notificacion.Crear(
              Guid.NewGuid(),
              TipoNotificacion.NuevoMensaje,
              "Nuevo mensaje",
              "Tienes un nuevo mensaje de chat.",
              Guid.NewGuid(),
              DateTimeOffset.UtcNow);

          Assert.True(resultado.EsExito);
      }

      [Theory]
      [InlineData(null)]
      [InlineData("")]
      [InlineData("   ")]
      public void Crear_TituloInvalido_RetornaFallo(string? titulo)
      {
          var resultado = Notificacion.Crear(
              Guid.NewGuid(),
              TipoNotificacion.NuevoMensaje,
              titulo!,
              "Mensaje válido de más de diez caracteres.",
              null,
              DateTimeOffset.UtcNow);

          Assert.False(resultado.EsExito);
      }
  }
  ```

- [ ] **Step 2: Run tests to verify they fail**

  Run: `dotnet test CaseritoApp.UnitTests --filter "FullyQualifiedName~NotificacionTests"`
  Expected: FAIL (types not found).

- [ ] **Step 3: Implementar TipoNotificacion**

  ```csharp
  namespace CaseritoApp.Notifications.Domain.Notificaciones;

  public enum TipoNotificacion
  {
      NuevoMensaje,
      CambioEstadoOrden,
      AlertaBusqueda,
  }
  ```

- [ ] **Step 4: Implementar ErroresNotificacion**

  ```csharp
  namespace CaseritoApp.Notifications.Domain.Notificaciones;

  public static class ErroresNotificacion
  {
      public const string Invalida = "notificacion_invalida";
  }
  ```

- [ ] **Step 5: Implementar Notificacion**

  ```csharp
  using CaseritoApp.BuildingBlocks.Domain;

  namespace CaseritoApp.Notifications.Domain.Notificaciones;

  public sealed class Notificacion : AggregateRoot
  {
      private Notificacion()
      {
          Titulo = null!;
          Mensaje = null!;
          Version = null!;
      }

      private Notificacion(
          Guid destinatarioId,
          TipoNotificacion tipo,
          string titulo,
          string mensaje,
          Guid? entidadRelacionadaId,
          DateTimeOffset creadaEn)
      {
          Id = Guid.NewGuid();
          DestinatarioId = destinatarioId;
          Tipo = tipo;
          Titulo = titulo;
          Mensaje = mensaje;
          EntidadRelacionadaId = entidadRelacionadaId;
          Leida = false;
          CreadaEn = creadaEn;
          Version = [];
      }

      public Guid DestinatarioId { get; private set; }
      public TipoNotificacion Tipo { get; private set; }
      public string Titulo { get; private set; }
      public string Mensaje { get; private set; }
      public Guid? EntidadRelacionadaId { get; private set; }
      public bool Leida { get; private set; }
      public DateTimeOffset CreadaEn { get; private set; }
      public byte[] Version { get; private set; }

      public static Result<Notificacion> Crear(
          Guid destinatarioId,
          TipoNotificacion tipo,
          string titulo,
          string mensaje,
          Guid? entidadRelacionadaId,
          DateTimeOffset creadaEn)
      {
          var tituloNormalizado = titulo?.Trim();
          var mensajeNormalizado = mensaje?.Trim();

          if (destinatarioId == Guid.Empty
              || string.IsNullOrWhiteSpace(tituloNormalizado)
              || tituloNormalizado.Length > 150
              || string.IsNullOrWhiteSpace(mensajeNormalizado)
              || mensajeNormalizado.Length > 500)
          {
              return Result.Fallo<Notificacion>(new Error(
                  ErroresNotificacion.Invalida,
                  "La notificación no es válida."));
          }

          return Result.Exito(new Notificacion(
              destinatarioId,
              tipo,
              tituloNormalizado,
              mensajeNormalizado,
              entidadRelacionadaId,
              creadaEn.ToUniversalTime()));
      }

      public void MarcarComoLeida()
      {
          Leida = true;
      }
  }
  ```

- [ ] **Step 6: Run tests to verify they pass**

  Run: `dotnet test CaseritoApp.UnitTests --filter "FullyQualifiedName~NotificacionTests"`
  Expected: PASS.

- [ ] **Step 7: Commit**

  ```bash
  git add -A
  git commit -m "feat(notifications): agregado Notificacion en dominio"
  ```

---

## Task 2: Application de Notifications — crear notificación y consultar bandeja

**Files:**
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/CrearNotificacionCommand.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/ListarNotificacionesQuery.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/ContarNoLeidasQuery.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/MarcarLeidaCommand.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/MarcarTodasLeidasCommand.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/INotificacionRepository.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/DtosNotificacion.cs`

**Interfaces:**
- Consumes: `IUnitOfWork`, `ICommand`, `IQuery`, `Result<T>`.
- Produces: `CrearNotificacionCommand`, `ListarNotificacionesQuery`, `MarcarLeidaCommand`, `MarcarTodasLeidasCommand`, DTOs, `INotificacionRepository`.

- [ ] **Step 1: Definir INotificacionRepository**

  ```csharp
  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public interface INotificacionRepository
  {
      void Agregar(Notificacion notificacion);
      Task<Notificacion?> ObtenerAsync(Guid id, Guid destinatarioId, CancellationToken ct);
      Task<PaginaNotificacionesDto> ListarAsync(
          Guid destinatarioId,
          bool soloNoLeidas,
          int pagina,
          int tamano,
          CancellationToken ct);
      Task<int> ContarNoLeidasAsync(Guid destinatarioId, CancellationToken ct);
      Task<int> MarcarTodasLeidasAsync(Guid destinatarioId, CancellationToken ct);
  }
  ```

- [ ] **Step 2: Definir DTOs**

  ```csharp
  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed record NotificacionDto(
      Guid Id,
      string Tipo,
      string Titulo,
      string Mensaje,
      Guid? EntidadRelacionadaId,
      bool Leida,
      DateTimeOffset CreadaEn);

  public sealed record PaginaNotificacionesDto(
      IReadOnlyList<NotificacionDto> Items,
      int Pagina,
      int Tamano,
      int Total);
  ```

- [ ] **Step 3: Implementar CrearNotificacionCommand**

  ```csharp
  using CaseritoApp.BuildingBlocks.Application.Messaging;
  using CaseritoApp.BuildingBlocks.Domain;
  using CaseritoApp.Notifications.Domain.Notificaciones;
  using FluentValidation;

  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed record CrearNotificacionCommand(
      Guid DestinatarioId,
      TipoNotificacion Tipo,
      string Titulo,
      string Mensaje,
      Guid? EntidadRelacionadaId) : ICommand<Guid>;

  public sealed class CrearNotificacionCommandHandler(
      INotificacionRepository repositorio)
      : ICommandHandler<CrearNotificacionCommand, Guid>
  {
      public Task<Result<Guid>> Handle(
          CrearNotificacionCommand request,
          CancellationToken cancellationToken)
      {
          var resultado = Notificacion.Crear(
              request.DestinatarioId,
              request.Tipo,
              request.Titulo,
              request.Mensaje,
              request.EntidadRelacionadaId,
              DateTimeOffset.UtcNow);

          if (!resultado.EsExito)
          {
              return Task.FromResult(Result.Fallo<Guid>(resultado.Error));
          }

          repositorio.Agregar(resultado.Valor);
          return Task.FromResult(Result.Exito(resultado.Valor.Id));
      }
  }

  public sealed class CrearNotificacionCommandValidator : AbstractValidator<CrearNotificacionCommand>
  {
      public CrearNotificacionCommandValidator()
      {
          RuleFor(c => c.DestinatarioId).NotEmpty();
          RuleFor(c => c.Titulo).NotEmpty().MaximumLength(150);
          RuleFor(c => c.Mensaje).NotEmpty().MaximumLength(500);
      }
  }
  ```

- [ ] **Step 4: Implementar queries y commands de lectura/lectura**

  `ListarNotificacionesQuery`:

  ```csharp
  using CaseritoApp.BuildingBlocks.Application.Messaging;

  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed record ListarNotificacionesQuery(
      Guid DestinatarioId,
      bool SoloNoLeidas,
      int Pagina,
      int Tamano) : IQuery<PaginaNotificacionesDto>;

  public sealed class ListarNotificacionesQueryHandler(
      INotificacionRepository repositorio)
      : IQueryHandler<ListarNotificacionesQuery, PaginaNotificacionesDto>
  {
      public Task<PaginaNotificacionesDto> Handle(
          ListarNotificacionesQuery request,
          CancellationToken cancellationToken)
      {
          return repositorio.ListarAsync(
              request.DestinatarioId,
              request.SoloNoLeidas,
              request.Pagina,
              request.Tamano,
              cancellationToken);
      }
  }
  ```

  `ContarNoLeidasQuery`:

  ```csharp
  using CaseritoApp.BuildingBlocks.Application.Messaging;

  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed record ContarNoLeidasQuery(Guid DestinatarioId) : IQuery<int>;

  public sealed class ContarNoLeidasQueryHandler(
      INotificacionRepository repositorio)
      : IQueryHandler<ContarNoLeidasQuery, int>
  {
      public Task<int> Handle(
          ContarNoLeidasQuery request,
          CancellationToken cancellationToken)
      {
          return repositorio.ContarNoLeidasAsync(request.DestinatarioId, cancellationToken);
      }
  }
  ```

  `MarcarLeidaCommand`:

  ```csharp
  using CaseritoApp.BuildingBlocks.Application.Messaging;
  using CaseritoApp.BuildingBlocks.Domain;

  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed record MarcarLeidaCommand(
      Guid NotificacionId,
      Guid DestinatarioId) : ICommand;

  public sealed class MarcarLeidaCommandHandler(
      INotificacionRepository repositorio)
      : ICommandHandler<MarcarLeidaCommand>
  {
      public async Task<Result> Handle(
          MarcarLeidaCommand request,
          CancellationToken cancellationToken)
      {
          var notificacion = await repositorio.ObtenerAsync(
              request.NotificacionId,
              request.DestinatarioId,
              cancellationToken);

          if (notificacion is null)
          {
              return Result.Fallo(new Error("notificacion_no_disponible", "La notificación no está disponible."));
          }

          notificacion.MarcarComoLeida();
          return Result.Exito();
      }
  }
  ```

  `MarcarTodasLeidasCommand`:

  ```csharp
  using CaseritoApp.BuildingBlocks.Application.Messaging;
  using CaseritoApp.BuildingBlocks.Domain;

  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed record MarcarTodasLeidasCommand(Guid DestinatarioId) : ICommand<int>;

  public sealed class MarcarTodasLeidasCommandHandler(
      INotificacionRepository repositorio)
      : ICommandHandler<MarcarTodasLeidasCommand, int>
  {
      public Task<Result<int>> Handle(
          MarcarTodasLeidasCommand request,
          CancellationToken cancellationToken)
      {
          return repositorio.MarcarTodasLeidasAsync(request.DestinatarioId, cancellationToken)
              .ContinueWith(t => Result.Exito(t.Result), TaskContinuationOptions.ExecuteSynchronously);
      }
  }
  ```

- [ ] **Step 5: Escribir tests de unit para handlers**

  Create: `CaseritoApp/tests/CaseritoApp.UnitTests/Notifications/CrearNotificacionCommandHandlerTests.cs`

  ```csharp
  using CaseritoApp.Notifications.Application.Notificaciones;
  using CaseritoApp.Notifications.Domain.Notificaciones;
  using NSubstitute;

  namespace CaseritoApp.UnitTests.Notifications;

  public sealed class CrearNotificacionCommandHandlerTests
  {
      private readonly INotificacionRepository _repo = Substitute.For<INotificacionRepository>();

      [Fact]
      public async Task Handle_DatosValidos_AgregaNotificacionYRetornaId()
      {
          var handler = new CrearNotificacionCommandHandler(_repo);
          var comando = new CrearNotificacionCommand(
              Guid.NewGuid(),
              TipoNotificacion.NuevoMensaje,
              "Nuevo mensaje",
              "Tienes un nuevo mensaje.",
              Guid.NewGuid());

          var resultado = await handler.Handle(comando, CancellationToken.None);

          Assert.True(resultado.EsExito);
          _repo.Received(1).Agregar(Arg.Any<Notificacion>());
      }
  }
  ```

- [ ] **Step 6: Run tests**

  Run: `dotnet test CaseritoApp.UnitTests --filter "FullyQualifiedName~Notifications"`
  Expected: PASS.

- [ ] **Step 7: Commit**

  ```bash
  git add -A
  git commit -m "feat(notifications): casos de uso de creacion y lectura de notificaciones"
  ```

---

## Task 3: Infrastructure de Notifications — persistencia y email

**Files:**
- Modify: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/NotificationsDbContext.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/Notificaciones/ConfiguracionNotificacion.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/Notificaciones/NotificacionRepositoryEfCore.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/Notificaciones/NotificacionProfile.cs` (o mapping inline)
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/IEmailSender.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/Email/LogEmailSender.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/Email/SmtpEmailSender.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/DependencyInjection.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/DesignTimeNotificationsDbContextFactory.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/UnitOfWorkNotifications.cs`

**Interfaces:**
- Consumes: `INotificacionRepository`, `IEmailSender`, `IUnitOfWork`.
- Produces: `NotificationsDbContext` configurado, repositorio EF Core, adaptadores de email, DI.

- [ ] **Step 1: Configurar entidad Notificacion**

  ```csharp
  using CaseritoApp.Notifications.Domain.Notificaciones;
  using Microsoft.EntityFrameworkCore;

  namespace CaseritoApp.Notifications.Infrastructure.Notificaciones;

  public static class ConfiguracionNotificacion
  {
      public static void Configurar(ModelBuilder builder)
      {
          builder.Entity<Notificacion>(entidad =>
          {
              entidad.ToTable("Notifications");
              entidad.HasKey(n => n.Id);
              entidad.Property(n => n.Id).ValueGeneratedNever();
              entidad.Property(n => n.DestinatarioId).IsRequired();
              entidad.Property(n => n.Tipo)
                  .HasConversion<string>()
                  .HasMaxLength(30)
                  .IsRequired();
              entidad.Property(n => n.Titulo)
                  .HasMaxLength(150)
                  .IsRequired();
              entidad.Property(n => n.Mensaje)
                  .HasColumnName("Body")
                  .HasMaxLength(500)
                  .IsRequired();
              entidad.Property(n => n.EntidadRelacionadaId)
                  .HasColumnName("RelatedEntityId")
                  .IsRequired(false);
              entidad.Property(n => n.Leida)
                  .HasColumnName("IsRead")
                  .IsRequired();
              entidad.Property(n => n.CreadaEn)
                  .HasColumnName("CreatedAt")
                  .IsRequired();
              entidad.Property(n => n.Version).IsRowVersion();
              entidad.Ignore(n => n.EventosDeDominio);
              entidad.HasIndex(n => new { n.DestinatarioId, n.CreadaEn, n.Id });
              entidad.HasIndex(n => new { n.DestinatarioId, n.Leida, n.CreadaEn });
          });
      }
  }
  ```

- [ ] **Step 2: Actualizar NotificationsDbContext**

  ```csharp
  using CaseritoApp.Notifications.Domain.Notificaciones;
  using CaseritoApp.Notifications.Infrastructure.Notificaciones;
  using Microsoft.EntityFrameworkCore;

  namespace CaseritoApp.Notifications.Infrastructure;

  public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
  {
      public const string Schema = "notifications";

      public DbSet<Notificacion> Notifications => Set<Notificacion>();

      protected override void OnModelCreating(ModelBuilder modelBuilder)
      {
          modelBuilder.HasDefaultSchema(Schema);
          base.OnModelCreating(modelBuilder);
          ConfiguracionNotificacion.Configurar(modelBuilder);
      }
  }
  ```

- [ ] **Step 3: Implementar repositorio EF Core**

  ```csharp
  using CaseritoApp.Notifications.Application.Notificaciones;
  using CaseritoApp.Notifications.Domain.Notificaciones;
  using Microsoft.EntityFrameworkCore;

  namespace CaseritoApp.Notifications.Infrastructure.Notificaciones;

  public sealed class NotificacionRepositoryEfCore(NotificationsDbContext db)
      : INotificacionRepository
  {
      public void Agregar(Notificacion notificacion) => db.Notifications.Add(notificacion);

      public Task<Notificacion?> ObtenerAsync(
          Guid id,
          Guid destinatarioId,
          CancellationToken ct)
      {
          return db.Notifications
              .FirstOrDefaultAsync(
                  n => n.Id == id && n.DestinatarioId == destinatarioId,
                  ct);
      }

      public async Task<IReadOnlyList<NotificacionDto>> ListarAsync(
          Guid destinatarioId,
          bool soloNoLeidas,
          int pagina,
          int tamano,
          CancellationToken ct)
      {
          var query = db.Notifications
              .Where(n => n.DestinatarioId == destinatarioId);

          if (soloNoLeidas)
          {
              query = query.Where(n => !n.Leida);
          }

          var total = await query.CountAsync(ct);
          var items = await query
              .OrderByDescending(n => n.CreadaEn)
              .ThenBy(n => n.Id)
              .Skip((pagina - 1) * tamano)
              .Take(tamano)
              .Select(n => new NotificacionDto(
                  n.Id,
                  n.Tipo.ToString(),
                  n.Titulo,
                  n.Mensaje,
                  n.EntidadRelacionadaId,
                  n.Leida,
                  n.CreadaEn))
              .ToListAsync(ct);

          return new PaginaNotificacionesDto(items, pagina, tamano, total);
      }

      public Task<int> ContarNoLeidasAsync(Guid destinatarioId, CancellationToken ct)
      {
          return db.Notifications
              .CountAsync(n => n.DestinatarioId == destinatarioId && !n.Leida, ct);
      }

      public Task<int> MarcarTodasLeidasAsync(Guid destinatarioId, CancellationToken ct)
      {
          return db.Notifications
              .Where(n => n.DestinatarioId == destinatarioId && !n.Leida)
              .ExecuteUpdateAsync(
                  setters => setters.SetProperty(n => n.Leida, true),
                  ct);
      }
  }
  ```

- [ ] **Step 4: Implementar UnitOfWorkNotifications**

  ```csharp
  using CaseritoApp.BuildingBlocks.Application.Abstractions;

  namespace CaseritoApp.Notifications.Infrastructure;

  public sealed class UnitOfWorkNotifications(NotificationsDbContext db) : IUnitOfWork
  {
      public Task<int> GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
  }
  ```

- [ ] **Step 5: Implementar IEmailSender**

  `IEmailSender`:

  ```csharp
  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public interface IEmailSender
  {
      Task EnviarAsync(
          string destinatario,
          string asunto,
          string cuerpoTexto,
          string? cuerpoHtml = null,
          CancellationToken ct = default);
  }
  ```

  `LogEmailSender`:

  ```csharp
  using CaseritoApp.Notifications.Application.Notificaciones;
  using Microsoft.Extensions.Logging;

  namespace CaseritoApp.Notifications.Infrastructure.Email;

  public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger)
      : IEmailSender
  {
      public Task EnviarAsync(
          string destinatario,
          string asunto,
          string cuerpoTexto,
          string? cuerpoHtml = null,
          CancellationToken ct = default)
      {
          logger.LogInformation("Email enviado a destinatario oculto: asunto={Asunto}", asunto);
          return Task.CompletedTask;
      }
  }
  ```

  `SmtpEmailSender` (implementación para piloto):

  ```csharp
  using System.Net;
  using System.Net.Mail;
  using CaseritoApp.Notifications.Application.Notificaciones;
  using Microsoft.Extensions.Options;

  namespace CaseritoApp.Notifications.Infrastructure.Email;

  public sealed class SmtpEmailSender(IOptions<OpcionesEmail> opciones) : IEmailSender
  {
      public async Task EnviarAsync(
          string destinatario,
          string asunto,
          string cuerpoTexto,
          string? cuerpoHtml = null,
          CancellationToken ct = default)
      {
          var opc = opciones.Value;
          using var cliente = new SmtpClient(opc.Host, opc.Port)
          {
              Credentials = new NetworkCredential(opc.Usuario, opc.Password),
              EnableSsl = opc.EnableSsl,
          };
          var mensaje = new MailMessage(opc.Remitente, destinatario, asunto, cuerpoTexto)
          {
              IsBodyHtml = !string.IsNullOrEmpty(cuerpoHtml),
              Body = cuerpoHtml ?? cuerpoTexto,
          };
          await cliente.SendMailAsync(mensaje, ct);
      }
  }

  public sealed class OpcionesEmail
  {
      public const string Seccion = "Email";
      public string Host { get; set; } = string.Empty;
      public int Port { get; set; }
      public string Usuario { get; set; } = string.Empty;
      public string Password { get; set; } = string.Empty;
      public string Remitente { get; set; } = string.Empty;
      public bool EnableSsl { get; set; }
  }
  ```

- [ ] **Step 6: DependencyInjection**

  ```csharp
  using CaseritoApp.BuildingBlocks.Application.Abstractions;
  using CaseritoApp.Notifications.Application.Notificaciones;
  using CaseritoApp.Notifications.Infrastructure.Email;
  using CaseritoApp.Notifications.Infrastructure.Notificaciones;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.DependencyInjection;

  namespace CaseritoApp.Notifications.Infrastructure;

  public static class DependencyInjection
  {
      public static IServiceCollection AgregarNotifications(
          this IServiceCollection servicios,
          IConfiguration config,
          IHostEnvironment entorno)
      {
          var cadena = config.GetConnectionString("DefaultConnection");
          if (!string.IsNullOrWhiteSpace(cadena))
          {
              servicios.AddDbContext<NotificationsDbContext>(
                  opciones => opciones.UseSqlServer(cadena));
          }

          servicios.AddScoped<INotificacionRepository, NotificacionRepositoryEfCore>();
          servicios.AddScoped<UnitOfWorkNotifications>();
          servicios.AddScoped<IUnitOfWork>(
              proveedor => proveedor.GetRequiredService<UnitOfWorkNotifications>());

          servicios.Configure<OpcionesEmail>(config.GetSection(OpcionesEmail.Seccion));

          if (entorno.IsDevelopment() || entorno.IsEnvironment("Testing"))
          {
              servicios.AddScoped<IEmailSender, LogEmailSender>();
          }
          else
          {
              servicios.AddScoped<IEmailSender, SmtpEmailSender>();
          }

          return servicios;
      }
  }
  ```

- [ ] **Step 7: DesignTimeNotificationsDbContextFactory**

  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Design;

  namespace CaseritoApp.Notifications.Infrastructure;

  public sealed class DesignTimeNotificationsDbContextFactory
      : IDesignTimeDbContextFactory<NotificationsDbContext>
  {
      public NotificationsDbContext CreateDbContext(string[] args)
      {
          var opciones = new DbContextOptionsBuilder<NotificationsDbContext>()
              .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CaseritoAppDesign;Trusted_Connection=True;")
              .Options;
          return new NotificationsDbContext(opciones);
      }
  }
  ```

- [ ] **Step 8: Generar migración inicial**

  Run:
  ```bash
  cd CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure
  dotnet ef migrations add NotificationsInicial --startup-project ../../../Host/CaseritoApp.Host
  ```
  Expected: se crea `Migrations/2026..._NotificationsInicial.cs`.

- [ ] **Step 9: Tests de persistencia**

  Create: `CaseritoApp/tests/CaseritoApp.IntegrationTests/NotificationsPersistenciaTests.cs`

  ```csharp
  using CaseritoApp.IntegrationTests.Fixtures;
  using Microsoft.EntityFrameworkCore;

  namespace CaseritoApp.IntegrationTests;

  public sealed class NotificationsPersistenciaTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
  {
      [Fact]
      public async Task Migraciones_Notifications_AplicanSinErrores()
      {
          using var scope = factory.Services.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
          await db.Database.MigrateAsync();
          Assert.True(await db.Database.CanConnectAsync());
      }
  }
  ```

  Nota: requiere que `CaseritoApiFactory` registre `NotificationsDbContext`; se hará en Task 7.

- [ ] **Step 10: Commit**

  ```bash
  git add -A
  git commit -m "feat(notifications): persistencia, email y migracion inicial"
  ```

---

## Task 4: Handlers de eventos de integración para notificaciones

**Files:**
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/IConsultaParticipantesOrden.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/IConsultaProductoParaAlerta.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/IConsultaEmailUsuario.cs`
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Notifications/ConsultaEmailUsuarioAdapter.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/NotificarNuevoMensajeHandler.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/NotificarCambioEstadoOrdenHandler.cs`

**Interfaces:**
- Consumes: `ChatMessageSent`, `OrderStatusChanged`, `ISender`, `IEmailSender`, `CrearNotificacionCommand`.
- Produces: handlers de notificación, puertos `IConsultaParticipantesOrden` e `IConsultaProductoParaAlerta`.

- [ ] **Step 1: Definir puertos**

  ```csharp
  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed record ParticipantesOrden(Guid CompradorId, Guid VendedorId);

  public interface IConsultaParticipantesOrden
  {
      Task<ParticipantesOrden?> ObtenerAsync(Guid orderId, CancellationToken ct);
  }
  ```

  ```csharp
  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed record ProductoParaAlerta(
      Guid ProductId,
      string Titulo,
      string? Categoria,
      string? Ciudad,
      decimal? Precio,
      string? EstadoProducto);

  public interface IConsultaProductoParaAlerta
  {
      Task<ProductoParaAlerta?> ObtenerAsync(Guid productId, CancellationToken ct);
  }
  ```

  ```csharp
  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public interface IConsultaEmailUsuario
  {
      Task<string?> ObtenerAsync(Guid userId, CancellationToken ct);
  }
  ```

- [ ] **Step 2: Implementar NotificarNuevoMensajeHandler**

  ```csharp
  using CaseritoApp.BuildingBlocks.Contracts.Chat;
  using MediatR;

  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed class NotificarNuevoMensajeHandler(
      ISender sender,
      IEmailSender emailSender,
      IConsultaEmailUsuario consultaEmail)
      : INotificationHandler<ChatMessageSent>
  {
      public async Task Handle(ChatMessageSent evento, CancellationToken cancellationToken)
      {
          var titulo = "Nuevo mensaje";
          var mensaje = "Recibiste un nuevo mensaje en una conversación.";

          await sender.Send(
              new CrearNotificacionCommand(
                  evento.DestinatarioId,
                  TipoNotificacion.NuevoMensaje,
                  titulo,
                  mensaje,
                  evento.ConversacionId),
              cancellationToken);

          await EnviarEmailAsync(evento.DestinatarioId, titulo, mensaje, cancellationToken);
      }

      private async Task EnviarEmailAsync(
          Guid destinatarioId,
          string asunto,
          string cuerpo,
          CancellationToken ct)
      {
          try
          {
              var email = await consultaEmail.ObtenerAsync(destinatarioId, ct);
              if (string.IsNullOrWhiteSpace(email))
              {
                  return;
              }

              await emailSender.EnviarAsync(email, asunto, cuerpo, ct: ct);
          }
          catch (Exception)
          {
              // Email no debe fallar la notificación in-app.
              // Loggear genéricamente sin PII.
          }
      }
  }
  ```

- [ ] **Step 3: Implementar NotificarCambioEstadoOrdenHandler**

  ```csharp
  using CaseritoApp.BuildingBlocks.Contracts.Orders;
  using MediatR;

  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed class NotificarCambioEstadoOrdenHandler(
      ISender sender,
      IConsultaParticipantesOrden consulta,
      IEmailSender emailSender,
      IConsultaEmailUsuario consultaEmail)
      : INotificationHandler<OrderStatusChanged>
  {
      public async Task Handle(OrderStatusChanged evento, CancellationToken cancellationToken)
      {
          var participantes = await consulta.ObtenerAsync(evento.OrderId, cancellationToken);
          if (participantes is null)
          {
              return;
          }

          var titulo = "Tu acuerdo cambió de estado";
          var mensaje = $"El acuerdo pasó de {evento.OldStatus} a {evento.NewStatus}.";

          await NotificarAsync(participantes.CompradorId, evento.OrderId, titulo, mensaje, cancellationToken);
          await NotificarAsync(participantes.VendedorId, evento.OrderId, titulo, mensaje, cancellationToken);
      }

      private async Task NotificarAsync(
          Guid destinatarioId,
          Guid orderId,
          string titulo,
          string mensaje,
          CancellationToken ct)
      {
          await sender.Send(
              new CrearNotificacionCommand(
                  destinatarioId,
                  TipoNotificacion.CambioEstadoOrden,
                  titulo,
                  mensaje,
                  orderId),
              ct);

          try
          {
              var email = await consultaEmail.ObtenerAsync(destinatarioId, ct);
              if (!string.IsNullOrWhiteSpace(email))
              {
                  await emailSender.EnviarAsync(email, titulo, mensaje, ct: ct);
              }
          }
          catch (Exception)
          {
              // Email no debe fallar la notificación in-app.
          }
      }
  }
  ```

- [ ] **Step 4: Tests de handlers**

  Create: `CaseritoApp/tests/CaseritoApp.UnitTests/Notifications/NotificarNuevoMensajeHandlerTests.cs`

  ```csharp
  using CaseritoApp.BuildingBlocks.Contracts.Chat;
  using CaseritoApp.Notifications.Application.Notificaciones;
  using MediatR;
  using NSubstitute;

  namespace CaseritoApp.UnitTests.Notifications;

  public sealed class NotificarNuevoMensajeHandlerTests
  {
      private readonly ISender _sender = Substitute.For<ISender>();
      private readonly IEmailSender _email = Substitute.For<IEmailSender>();
      private readonly IConsultaEmailUsuario _consultaEmail = Substitute.For<IConsultaEmailUsuario>();

      [Fact]
      public async Task Handle_CreaNotificacionParaDestinatario()
      {
          var handler = new NotificarNuevoMensajeHandler(_sender, _email, _consultaEmail);
          var evento = new ChatMessageSent(
              Guid.NewGuid(),
              DateTimeOffset.UtcNow,
              Guid.NewGuid(),
              Guid.NewGuid(),
              Guid.NewGuid(),
              Guid.NewGuid());

          await handler.Handle(evento, CancellationToken.None);

          await _sender.Received(1).Send(
              Arg.Is<CrearNotificacionCommand>(c =>
                  c.DestinatarioId == evento.DestinatarioId
                  && c.Tipo == TipoNotificacion.NuevoMensaje
                  && c.EntidadRelacionadaId == evento.ConversacionId),
              Arg.Any<CancellationToken>());
      }
  }
  ```

- [ ] **Step 5: Run tests**

  Run: `dotnet test CaseritoApp.UnitTests --filter "FullyQualifiedName~Notifications"`
  Expected: PASS.

- [ ] **Step 6: Commit**

  ```bash
  git add -A
  git commit -m "feat(notifications): handlers de eventos de integracion"
  ```

---

## Task 5: Endpoints de Notifications

**Files:**
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/NotificationsEndpoints.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`

**Interfaces:**
- Consumes: `ListarNotificacionesQuery`, `ContarNoLeidasQuery`, `MarcarLeidaCommand`, `MarcarTodasLeidasCommand`.

- [ ] **Step 1: Crear NotificationsEndpoints**

  ```csharp
  using System.IdentityModel.Tokens.Jwt;
  using System.Security.Claims;
  using CaseritoApp.Notifications.Application.Notificaciones;
  using MediatR;

  namespace CaseritoApp.Host.Endpoints;

  public static class NotificationsEndpoints
  {
      public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
      {
          var grupo = app.MapGroup("/api/notificaciones").RequireAuthorization();

          grupo.MapGet("/", ListarAsync)
              .RequireRateLimiting("notifications-consultas")
              .Produces<PaginaNotificacionesDto>();

          grupo.MapGet("/no-leidas", ContarNoLeidasAsync)
              .RequireRateLimiting("notifications-conteo")
              .Produces<int>();

          grupo.MapPatch("/{id:guid}/leida", MarcarLeidaAsync)
              .RequireRateLimiting("notifications-acciones")
              .Produces(StatusCodes.Status200OK)
              .ProducesProblem(StatusCodes.Status404NotFound);

          grupo.MapPatch("/marcar-todas-leidas", MarcarTodasLeidasAsync)
              .RequireRateLimiting("notifications-acciones")
              .Produces<int>();

          return app;
      }

      private static async Task<IResult> ListarAsync(
          ISender sender,
          ClaimsPrincipal usuario,
          bool soloNoLeidas = false,
          int pagina = 1,
          int tamano = 20,
          CancellationToken ct = default)
      {
          if (!TryUserId(usuario, out var actorId))
          {
              return Results.Unauthorized();
          }

          var resultado = await sender.Send(
              new ListarNotificacionesQuery(actorId, soloNoLeidas, pagina, tamano),
              ct);
          return Results.Ok(resultado);
      }

      private static async Task<IResult> ContarNoLeidasAsync(
          ISender sender,
          ClaimsPrincipal usuario,
          CancellationToken ct)
      {
          if (!TryUserId(usuario, out var actorId))
          {
              return Results.Unauthorized();
          }

          var total = await sender.Send(new ContarNoLeidasQuery(actorId), ct);
          return Results.Ok(total);
      }

      private static async Task<IResult> MarcarLeidaAsync(
          Guid id,
          ISender sender,
          ClaimsPrincipal usuario,
          CancellationToken ct)
      {
          if (!TryUserId(usuario, out var actorId))
          {
              return Results.Unauthorized();
          }

          var resultado = await sender.Send(new MarcarLeidaCommand(id, actorId), ct);
          return resultado.EsExito
              ? Results.Ok()
              : Results.Problem(
                  title: "notificacion_no_disponible",
                  detail: "La notificación no está disponible.",
                  statusCode: StatusCodes.Status404NotFound);
      }

      private static async Task<IResult> MarcarTodasLeidasAsync(
          ISender sender,
          ClaimsPrincipal usuario,
          CancellationToken ct)
      {
          if (!TryUserId(usuario, out var actorId))
          {
              return Results.Unauthorized();
          }

          var resultado = await sender.Send(new MarcarTodasLeidasCommand(actorId), ct);
          return Results.Ok(resultado.Valor);
      }

      private static bool TryUserId(ClaimsPrincipal usuario, out Guid userId)
      {
          var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
              ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
          return Guid.TryParse(valor, out userId) && userId != Guid.Empty;
      }
  }
  ```

- [ ] **Step 2: Registrar endpoints y rate limiting**

  En `Program.cs`:

  ```csharp
  builder.Services.AgregarNotifications(builder.Configuration, builder.Environment);
  ```

  Agregar dentro del bloque `AddRateLimiter`:

  ```csharp
  opciones.AddPolicy("notifications-consultas", contexto => RateLimitPartition.GetFixedWindowLimiter(
      Particion(contexto),
      _ => new FixedWindowRateLimiterOptions
      {
          PermitLimit = 120,
          Window = TimeSpan.FromMinutes(1),
          QueueLimit = 0,
          AutoReplenishment = true,
      }));
  opciones.AddPolicy("notifications-conteo", contexto => RateLimitPartition.GetFixedWindowLimiter(
      Particion(contexto),
      _ => new FixedWindowRateLimiterOptions
      {
          PermitLimit = 60,
          Window = TimeSpan.FromMinutes(1),
          QueueLimit = 0,
          AutoReplenishment = true,
      }));
  opciones.AddPolicy("notifications-acciones", contexto => RateLimitPartition.GetFixedWindowLimiter(
      Particion(contexto),
      _ => new FixedWindowRateLimiterOptions
      {
          PermitLimit = 30,
          Window = TimeSpan.FromMinutes(1),
          QueueLimit = 0,
          AutoReplenishment = true,
      }));
  ```

  Agregar `app.MapNotificationsEndpoints();` junto a los demás.

  Agregar ensamblado de Notifications.Application en `AddMediatR` y `AddValidatorsFromAssembly`:

  ```csharp
  typeof(CaseritoApp.Notifications.Application.Notificaciones.CrearNotificacionCommand).Assembly,
  ```

  Agregar migración de Notifications en el bloque de arranque:

  ```csharp
  using (var scopeNotifications = app.Services.CreateScope())
  {
      var dbNotifications = scopeNotifications.ServiceProvider
          .GetRequiredService<NotificationsDbContext>();
      await dbNotifications.Database.MigrateAsync();
  }
  ```

- [ ] **Step 3: Implementar puertos en Host**

  Create: `CaseritoApp/src/Host/CaseritoApp.Host/Notifications/ConsultaParticipantesOrdenAdapter.cs`

  ```csharp
  using CaseritoApp.Notifications.Application.Notificaciones;
  using CaseritoApp.Orders.Infrastructure;
  using Microsoft.EntityFrameworkCore;

  namespace CaseritoApp.Host.Notifications;

  public sealed class ConsultaParticipantesOrdenAdapter(OrdersDbContext db)
      : IConsultaParticipantesOrden
  {
      public async Task<ParticipantesOrden?> ObtenerAsync(Guid orderId, CancellationToken ct)
      {
          var orden = await db.Orders
              .AsNoTracking()
              .FirstOrDefaultAsync(o => o.Id == orderId, ct);

          if (orden is null)
          {
              return null;
          }

          return new ParticipantesOrden(orden.CompradorId, orden.VendedorId);
      }
  }
  ```

  Nota: `OrdersDbContext` expone `DbSet<Orden>` como `Orders` y `Orden` tiene
  `CompradorId` y `VendedorId`. Verificar contra el modelo real.

  Register in `Program.cs`:

  ```csharp
  builder.Services.AddScoped<IConsultaParticipantesOrden, ConsultaParticipantesOrdenAdapter>();
  ```

- [ ] **Step 4: Build**

  Run: `dotnet build CaseritoApp.sln`
  Expected: exit 0.

- [ ] **Step 5: Commit**

  ```bash
  git add -A
  git commit -m "feat(notifications): endpoints, rate limiting y registro en host"
  ```

---

## Task 6: Búsquedas guardadas y alertas

**Files:**
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Domain/Busquedas/BusquedaGuardada.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Busquedas/...`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/Busquedas/...`
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/BusquedasGuardadasEndpoints.cs`
- Create handler: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/Notificaciones/NotificarProductoPublicadoHandler.cs`

**Interfaces:**
- Consumes: `ProductPublished`, `IConsultaProductoParaAlerta`, `INotificacionRepository`.
- Produces: `BusquedaGuardada`, commands/queries, alertas.

- [ ] **Step 1: Domain BusquedaGuardada**

  ```csharp
  using CaseritoApp.BuildingBlocks.Domain;

  namespace CaseritoApp.Notifications.Domain.Busquedas;

  public sealed class BusquedaGuardada : AggregateRoot
  {
      private BusquedaGuardada()
      {
          PalabraClave = null;
          Categoria = null;
          Ciudad = null;
          EstadoProducto = null;
          Version = null!;
      }

      private BusquedaGuardada(
          Guid usuarioId,
          string? palabraClave,
          string? categoria,
          string? ciudad,
          decimal? precioMinimo,
          decimal? precioMaximo,
          string? estadoProducto,
          DateTimeOffset creadaEn)
      {
          Id = Guid.NewGuid();
          UsuarioId = usuarioId;
          PalabraClave = palabraClave;
          Categoria = categoria;
          Ciudad = ciudad;
          PrecioMinimo = precioMinimo;
          PrecioMaximo = precioMaximo;
          EstadoProducto = estadoProducto;
          CreadaEn = creadaEn;
          Version = [];
      }

      public Guid UsuarioId { get; private set; }
      public string? PalabraClave { get; private set; }
      public string? Categoria { get; private set; }
      public string? Ciudad { get; private set; }
      public decimal? PrecioMinimo { get; private set; }
      public decimal? PrecioMaximo { get; private set; }
      public string? EstadoProducto { get; private set; }
      public DateTimeOffset CreadaEn { get; private set; }
      public byte[] Version { get; private set; }

      public static Result<BusquedaGuardada> Crear(
          Guid usuarioId,
          string? palabraClave,
          string? categoria,
          string? ciudad,
          decimal? precioMinimo,
          decimal? precioMaximo,
          string? estadoProducto,
          DateTimeOffset creadaEn)
      {
          if (usuarioId == Guid.Empty
              || string.IsNullOrWhiteSpace(palabraClave)
                  && string.IsNullOrWhiteSpace(categoria)
                  && string.IsNullOrWhiteSpace(ciudad)
              || (precioMinimo.HasValue && precioMaximo.HasValue && precioMinimo > precioMaximo))
          {
              return Result.Fallo<BusquedaGuardada>(new Error(
                  "busqueda_guardada_invalida",
                  "La búsqueda guardada no es válida."));
          }

          return Result.Exito(new BusquedaGuardada(
              usuarioId,
              palabraClave?.Trim().ToLowerInvariant(),
              categoria?.Trim().ToLowerInvariant(),
              ciudad?.Trim().ToLowerInvariant(),
              precioMinimo,
              precioMaximo,
              estadoProducto?.Trim().ToLowerInvariant(),
              creadaEn.ToUniversalTime()));
      }
  }
  ```

- [ ] **Step 2: Application Busquedas**

  Definir `IBusquedaGuardadaRepository`:

  ```csharp
  using CaseritoApp.Notifications.Domain.Busquedas;

  namespace CaseritoApp.Notifications.Application.Busquedas;

  public interface IBusquedaGuardadaRepository
  {
      void Agregar(BusquedaGuardada busqueda);
      Task<BusquedaGuardada?> ObtenerAsync(Guid id, Guid usuarioId, CancellationToken ct);
      Task<IReadOnlyList<BusquedaGuardada>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken ct);
      Task<int> ContarPorUsuarioAsync(Guid usuarioId, CancellationToken ct);
      void Eliminar(BusquedaGuardada busqueda);
      Task<IReadOnlyList<BusquedaGuardada>> ListarCoincidenciasAsync(
          string? tituloProducto,
          string? categoriaProducto,
          string? ciudadProducto,
          decimal? precioProducto,
          string? estadoProducto,
          CancellationToken ct);
  }
  ```

  Commands/queries similares a notificaciones. Crear:

  - `CrearBusquedaGuardadaCommand` (limitar a 20 búsquedas por usuario)
  - `ListarBusquedasGuardadasQuery`
  - `EliminarBusquedaGuardadaCommand`

  DTO `BusquedaGuardadaDto`.

- [ ] **Step 3: Handler NotificarProductoPublicadoHandler**

  ```csharp
  using CaseritoApp.BuildingBlocks.Contracts.Catalog;
  using CaseritoApp.Notifications.Application.Busquedas;
  using CaseritoApp.Notifications.Application.Notificaciones;
  using MediatR;
  using Microsoft.EntityFrameworkCore;

  namespace CaseritoApp.Notifications.Application.Notificaciones;

  public sealed class NotificarProductoPublicadoHandler(
      ISender sender,
      IBusquedaGuardadaRepository busquedas,
      IConsultaProductoParaAlerta consulta,
      IEmailSender emailSender,
      IConsultaEmailUsuario consultaEmail)
      : INotificationHandler<ProductPublished>
  {
      public async Task Handle(ProductPublished evento, CancellationToken cancellationToken)
      {
          var producto = await consulta.ObtenerAsync(evento.ProductId, cancellationToken);
          if (producto is null)
          {
              return;
          }

          var coincidentes = await busquedas.ListarCoincidenciasAsync(
              producto.Titulo,
              producto.Categoria,
              producto.Ciudad,
              producto.Precio,
              producto.EstadoProducto,
              cancellationToken);

          foreach (var busqueda in coincidentes)
          {
              await sender.Send(
                  new CrearNotificacionCommand(
                      busqueda.UsuarioId,
                      TipoNotificacion.AlertaBusqueda,
                      "Nuevo aviso que puede interesarte",
                      "Se publicó un aviso que coincide con tu búsqueda.",
                      evento.ProductId),
                  cancellationToken);

              await EnviarEmailAsync(
                  busqueda.UsuarioId,
                  "Nuevo aviso que puede interesarte",
                  "Se publicó un aviso que coincide con una de tus búsquedas guardadas.",
                  cancellationToken);
          }
      }

      private async Task EnviarEmailAsync(
          Guid destinatarioId,
          string asunto,
          string cuerpo,
          CancellationToken ct)
      {
          try
          {
              var email = await consultaEmail.ObtenerAsync(destinatarioId, ct);
              if (!string.IsNullOrWhiteSpace(email))
              {
                  await emailSender.EnviarAsync(email, asunto, cuerpo, ct: ct);
              }
          }
          catch (Exception)
          {
              // Email no debe fallar la alerta in-app.
          }
      }
  }
  ```

  Nota: el texto del mensaje no incluye título ni precio del aviso para evitar
  PII/variabilidad; el cliente resuelve el detalle.

- [ ] **Step 4: Infrastructure Busquedas**

  Configuración EF Core para `BusquedaGuardada` en tabla `SavedSearches` según spec.
  Repositorio con `ListarCoincidenciasAsync` que ejecuta query SQL o LINQ.

- [ ] **Step 5: Endpoints BusquedasGuardadas**

  Grupo `/api/busquedas-guardadas` autenticado: GET, POST, DELETE.

- [ ] **Step 6: Implementar IConsultaProductoParaAlerta en Host**

  Create: `CaseritoApp/src/Host/CaseritoApp.Host/Notifications/ConsultaProductoParaAlertaAdapter.cs`

  ```csharp
  using CaseritoApp.Catalog.Infrastructure;
  using CaseritoApp.Notifications.Application.Notificaciones;
  using Microsoft.EntityFrameworkCore;

  namespace CaseritoApp.Host.Notifications;

  public sealed class ConsultaProductoParaAlertaAdapter(CatalogDbContext db)
      : IConsultaProductoParaAlerta
  {
      public async Task<ProductoParaAlerta?> ObtenerAsync(Guid productId, CancellationToken ct)
      {
          var aviso = await db.Avisos
              .AsNoTracking()
              .FirstOrDefaultAsync(a => a.Id == productId, ct);

          if (aviso is null)
          {
              return null;
          }

          var categoria = await db.Categorias
              .AsNoTracking()
              .FirstOrDefaultAsync(c => c.Id == aviso.CategoriaId, ct);
          var ciudad = await db.Ciudades
              .AsNoTracking()
              .FirstOrDefaultAsync(c => c.Id == aviso.CiudadId, ct);

          return new ProductoParaAlerta(
              aviso.Id,
              aviso.Titulo,
              categoria?.Nombre,
              ciudad?.Nombre,
              aviso.Precio?.Monto,
              aviso.Condicion.ToString());
      }
  }
  ```

  Ajustar nombres de propiedades según el modelo real de Catalog.

  Register:

  ```csharp
  builder.Services.AddScoped<IConsultaProductoParaAlerta, ConsultaProductoParaAlertaAdapter>();
  ```

- [ ] **Step 7 (extra): Implementar IConsultaEmailUsuario en Host**

  Create: `CaseritoApp/src/Host/CaseritoApp.Host/Notifications/ConsultaEmailUsuarioAdapter.cs`

  ```csharp
  using CaseritoApp.Notifications.Application.Notificaciones;
  using Microsoft.AspNetCore.Identity;

  namespace CaseritoApp.Host.Notifications;

  public sealed class ConsultaEmailUsuarioAdapter(UserManager<ApplicationUser> userManager)
      : IConsultaEmailUsuario
  {
      public async Task<string?> ObtenerAsync(Guid userId, CancellationToken ct)
      {
          var usuario = await userManager.FindByIdAsync(userId.ToString());
          return usuario?.Email;
      }
  }
  ```

  Register:

  ```csharp
  builder.Services.AddScoped<IConsultaEmailUsuario, ConsultaEmailUsuarioAdapter>();
  ```

- [ ] **Step 8: Migración**

  Add migration `BusquedasGuardadas` en Notifications.Infrastructure.

- [ ] **Step 8: Tests**

  Unit tests para `BusquedaGuardada.Crear`, handler de alertas, repository.
  Integration tests para endpoints.

- [ ] **Step 9: Commit**

  ```bash
  git add -A
  git commit -m "feat(notifications): busquedas guardadas y alertas de productos"
  ```

---

## Task 7: Puntos de encuentro seguros

**Files:**
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Domain/PuntosEncuentro/PuntoEncuentroSeguro.cs`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Application/PuntosEncuentro/...`
- Create: `CaseritoApp/src/Notifications/CaseritoApp.Notifications.Infrastructure/PuntosEncuentro/...`
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PuntosEncuentroEndpoints.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs` (seed)

**Interfaces:**
- Consumes: endpoint público.
- Produces: listado de puntos activos por ciudad.

- [ ] **Step 1: Domain PuntoEncuentroSeguro**

  Entity simple con validaciones mínimas.

- [ ] **Step 2: Application**

  `ListarPuntosEncuentroQuery(ciudad)`.

- [ ] **Step 3: Infrastructure**

  Configuración y repositorio; migración.

- [ ] **Step 4: Endpoints públicos**

  `/api/publico/puntos-encuentro?ciudad=Cochabamba`.

- [ ] **Step 5: Seed de Cochabamba**

  En `Program.cs`, al arrancar con `Migraciones:EjecutarAlArranque` o en un
  hosted service/seed idempotente, insertar 3–5 puntos de encuentro seguros de
  Cochabamba si no existen.

- [ ] **Step 6: Tests**

  Integration test: listado público filtra por ciudad y activos.

- [ ] **Step 7: Commit**

  ```bash
  git add -A
  git commit -m "feat(notifications): puntos de encuentro seguros"
  ```

---

## Task 8: Endurecimiento

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Health/DbHealthCheck.cs`
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Config/SeguridadChecklist.md`
- Modify: tests existentes.

**Interfaces:**
- Consumes: `HealthChecksBuilder`.
- Produces: `/health` con chequeo de BD.

- [ ] **Step 1: Agregar health check de base de datos**

  ```csharp
  builder.Services.AddHealthChecks()
      .AddDbContextCheck<IdentityDbContext>("identity")
      .AddDbContextCheck<CatalogDbContext>("catalog")
      .AddDbContextCheck<ChatDbContext>("chat")
      .AddDbContextCheck<OrdersDbContext>("orders")
      .AddDbContextCheck<ReputationDbContext>("reputation")
      .AddDbContextCheck<NotificationsDbContext>("notifications");
  ```

  Reemplazar endpoint `/health`:

  ```csharp
  app.MapHealthChecks("/health");
  ```

  Requiere paquete `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`;
  agregar a `CaseritoApp.Host.csproj` y `Directory.Packages.props`.

- [ ] **Step 2: Ejecutar checklist de seguridad**

  Crear `CaseritoApp/Documentacion/checklist-seguridad-piloto.md` con items del
  spec y marcar cada uno. Incluye:

  - Revisar todos los endpoints tienen `RequireAuthorization` o `AllowAnonymous` explícito.
  - Confirmar rate limiting en endpoints sensibles.
  - Verificar CORS y headers de seguridad.
  - Confirmar que no se loggea PII.
  - Ejecutar `dotnet list package --vulnerable`.
  - Revisar que secretos no estén hardcodeados.

- [ ] **Step 3: Tests de flujos críticos**

  Crear o extender `CaseritoApp/tests/CaseritoApp.IntegrationTests/FlujoCriticoTests.cs`:

  - registro → login → KYC aprobado → publicar aviso → iniciar chat → crear orden → completar orden → calificar.

- [ ] **Step 4: Estrategia de backup**

  Documentar en `CaseritoApp/Documentacion/estrategia-backup-piloto.md`:

  - Backup full de SQL Server cada 24h.
  - Backup de logs cada hora.
  - Retención mínima 7 días.
  - Procedimiento de restauración.
  - Backup de blobs de imágenes mediante políticas del proveedor.

- [ ] **Step 5: Commit**

  ```bash
  git add -A
  git commit -m "feat(hardening): health checks, checklist seguridad y documentacion de backup"
  ```

---

## Task 9: Frontend — API cliente y componentes

**Files:**
- Create: `web/src/api/notificaciones.ts`
- Create: `web/src/api/busquedasGuardadas.ts`
- Create: `web/src/api/puntosEncuentro.ts`
- Create: `web/src/notificaciones/NotificacionesBadge.tsx`
- Create: `web/src/notificaciones/NotificacionesDropdown.tsx`
- Create: `web/src/routes/NotificacionesPage.tsx`
- Create: `web/src/routes/BusquedasGuardadasPage.tsx`
- Modify: `web/src/app/AppLayout.tsx`
- Modify: `web/src/app/router.tsx`

**Interfaces:**
- Consumes: endpoints `/api/notificaciones`, `/api/busquedas-guardadas`, `/api/publico/puntos-encuentro`.
- Produces: UI de notificaciones, búsquedas guardadas y puntos de encuentro.

- [ ] **Step 1: Generar tipos OpenAPI**

  Run: `npm run generate:api` (desde `web/`).
  Expected: `src/api/schema.d.ts` actualizado sin errores.

- [ ] **Step 2: Crear API clients**

  `web/src/api/notificaciones.ts`:

  ```typescript
  import type { components } from './schema';
  import { api, desempaquetar } from './http';

  export type Notificacion = components['schemas']['NotificacionDto'];
  export type PaginaNotificaciones = components['schemas']['PaginaNotificacionesDto'];

  export async function listarNotificaciones(
    soloNoLeidas = false,
    pagina = 1,
    tamano = 20,
  ): Promise<PaginaNotificaciones> {
    return desempaquetar(
      await api.GET('/api/notificaciones', {
        params: { query: { soloNoLeidas, pagina, tamano } },
      }),
    );
  }

  export async function contarNoLeidas(): Promise<number> {
    return desempaquetar(await api.GET('/api/notificaciones/no-leidas'));
  }

  export async function marcarLeida(id: string): Promise<void> {
    await api.PATCH('/api/notificaciones/{id}/leida', { params: { path: { id } } });
  }

  export async function marcarTodasLeidas(): Promise<number> {
    return desempaquetar(await api.PATCH('/api/notificaciones/marcar-todas-leidas'));
  }
  ```

  Ajustar nombres de schemas según OpenAPI generado.

- [ ] **Step 3: Implementar NotificacionesBadge con polling**

  ```tsx
  import { Badge, IconButton } from '@mui/material';
  import NotificationsIcon from '@mui/icons-material/Notifications';
  import { useQuery } from '@tanstack/react-query';
  import { contarNoLeidas } from '../api/notificaciones';

  export function NotificacionesBadge({ onClick }: { onClick: () => void }) {
    const { data: total = 0 } = useQuery({
      queryKey: ['notificaciones', 'no-leidas'],
      queryFn: contarNoLeidas,
      refetchInterval: 60000,
    });

    return (
      <IconButton color="inherit" onClick={onClick}>
        <Badge badgeContent={total} color="error">
          <NotificationsIcon />
        </Badge>
      </IconButton>
    );
  }
  ```

- [ ] **Step 4: Implementar dropdown y página de notificaciones**

  Dropdown con últimas 5 notificaciones + enlace a "Ver todas".
  Página con lista paginada y botón "Marcar todas como leídas".

- [ ] **Step 5: Integrar en AppLayout**

  Añadir `<NotificacionesBadge />` en el header junto a `<ContadorChat />`.

- [ ] **Step 6: Página de búsquedas guardadas**

  Formulario para crear alerta, listado con eliminar.

- [ ] **Step 7: Mostrar puntos de encuentro**

  En `DetalleAvisoPage` y/o `DetalleAcuerdoPage`, mostrar lista de puntos de
  encuentro seguros de la ciudad del aviso.

- [ ] **Step 8: Tests frontend**

  Crear tests para `NotificacionesBadge`, `NotificacionesPage` y
  `BusquedasGuardadasPage`.

- [ ] **Step 9: Typecheck, lint, build**

  Run:
  ```bash
  npm run typecheck
  npm run lint
  npm run test -- --run
  npm run build
  ```
  Expected: todo verde.

- [ ] **Step 10: Commit**

  ```bash
  git add -A
  git commit -m "feat(web): UI de notificaciones, busquedas guardadas y puntos de encuentro"
  ```

---

## Task 10: Registro de Notifications en fábrica de pruebas

**Files:**
- Modify: `CaseritoApp/tests/CaseritoApp.IntegrationTests/Fixtures/CaseritoApiFactory.cs` (o equivalente)

**Interfaces:**
- Consumes: `NotificationsDbContext`, `AgregarNotifications`.

- [ ] **Step 1: Localizar la fábrica**

  Find file con `CaseritoApiFactory`. Usualmente en
  `CaseritoApp/tests/CaseritoApp.IntegrationTests/Fixtures/`.

- [ ] **Step 2: Registrar NotificationsDbContext en tests**

  Asegurar que la fábrica:

  - Llame `services.AgregarNotifications(configuration, Environment)`.
  - Reemplace `NotificationsDbContext` con `AddDbContext` apuntando al contenedor de Testcontainers (igual que los otros contextos).

- [ ] **Step 3: Commit**

  ```bash
  git add -A
  git commit -m "test(integration): registra NotificationsDbContext en ApiFactory"
  ```

---

## Task 11: Integración y verificación final

- [ ] **Step 1: Suite de unit tests**

  Run: `dotnet test CaseritoApp.UnitTests`
  Expected: PASS.

- [ ] **Step 2: Suite de integration tests**

  Run: `dotnet test CaseritoApp.IntegrationTests`
  Expected: PASS (requiere Docker).

- [ ] **Step 3: Suite de architecture tests**

  Run: `dotnet test CaseritoApp.ArchitectureTests`
  Expected: PASS.

- [ ] **Step 4: Format check**

  Run: `dotnet format CaseritoApp.sln --verify-no-changes`
  Expected: sin cambios.

- [ ] **Step 5: Frontend build**

  Run desde `web/`:
  ```bash
  npm run typecheck && npm run lint && npm run test -- --run && npm run build
  ```
  Expected: todo verde.

- [ ] **Step 6: Commit final**

  ```bash
  git add -A
  git commit -m "chore(fase-6): integracion completa y verificacion"
  ```

---

## Spec coverage

| Requisito del spec | Tarea que lo implementa |
|---|---|
| Bounded context Notifications | Task 1, 2, 3, 10 |
| Consumo de `ChatMessageSent` | Task 0, 4 |
| Consumo de `OrderStatusChanged` | Task 0, 4, 5 |
| Consumo de `ProductPublished` para alertas | Task 0, 6 |
| Notificaciones in-app + email | Task 2, 3, 4 |
| Bandeja, conteo, marcar leídas | Task 2, 5 |
| Búsquedas guardadas | Task 6 |
| Puntos de encuentro seguros | Task 7 |
| Endurecimiento (health, seguridad, backup) | Task 8 |
| Frontend notificaciones/búsquedas/puntos | Task 9 |
| No dependencias directas entre contextos | Task 4, 6 (puertos por Host) |
| Anti-PII | Todo el plan; revisión en Task 8 |

## Placeholder scan

- Sin "TBD", "TODO", "implement later".
- Los adaptadores que consultan Orders/Catalog usan nombres de propiedad
  asumidos; se debe verificar contra el modelo real y ajustar.
- `IEmailSender` recibe el email ya resuelto; los handlers usan
  `IConsultaEmailUsuario` para obtenerlo desde Identity sin crear dependencia
  directa.
