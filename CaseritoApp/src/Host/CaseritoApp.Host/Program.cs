using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.BuildingBlocks.Application.Behaviors;
using CaseritoApp.BuildingBlocks.Infrastructure.Messaging;
using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Host.Auth;
using CaseritoApp.Host.Chat;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Host.Health;
using CaseritoApp.Host.Notifications;
using CaseritoApp.Host.OpenApi;
using CaseritoApp.Host.Orders;
using CaseritoApp.Host.Reputation;
using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using CaseritoApp.Notifications.Application.Notificaciones;
using CaseritoApp.Notifications.Infrastructure;
using CaseritoApp.Notifications.Infrastructure.PuntosEncuentro;
using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Infrastructure;
using CaseritoApp.Reputation.Application.Resenas;
using CaseritoApp.Reputation.Infrastructure;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Se registran MediatR y los behaviors del pipeline de aplicación, ampliando el escaneo de
// ensamblados para incluir CaseritoApp.Identity.Application (Query/Command de perfil) y
// CaseritoApp.Catalog.Application (Query/Command de avisos y catálogo).
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(
        typeof(CaseritoApp.BuildingBlocks.Application.Abstractions.IUnitOfWork).Assembly,
        typeof(ObtenerPerfilQuery).Assembly,
        typeof(CaseritoApp.Catalog.Application.Avisos.CrearAvisoCommand).Assembly,
        typeof(IniciarConversacionCommand).Assembly,
        typeof(SolicitarOrdenCommand).Assembly,
        typeof(CrearResenaCommand).Assembly,
        typeof(RevocarAccesoTiempoRealHandler).Assembly,
        typeof(CrearNotificacionCommand).Assembly));

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

// Validators de FluentValidation de los ensamblados de Identity.Application (p. ej.
// ActualizarPerfilCommandValidator) y Catalog.Application (p. ej. CrearAvisoCommandValidator).
builder.Services.AddValidatorsFromAssembly(typeof(ObtenerPerfilQuery).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(CaseritoApp.Catalog.Application.Avisos.CrearAvisoCommand).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(IniciarConversacionCommand).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(SolicitarOrdenCommand).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(CrearResenaCommand).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(CrearNotificacionCommand).Assembly);

// El DbContext de Identity (y el resto de Identity Core) solo se registra si hay cadena de
// conexión configurada (env, user-secrets o compose). Sin cadena (p. ej. tests de /health),
// el host arranca sin BD.
var cadenaConexion = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AgregarIdentity(builder.Configuration, builder.Environment);
builder.Services.Replace(
    ServiceDescriptor.Singleton<IPublicadorEventosIntegracion,
        PublicadorEventosIntegracionMediatR>());
builder.Services.AgregarAutenticacionJwt(builder.Configuration, builder.Environment);
builder.Services.AgregarCatalog(builder.Configuration);
builder.Services.AgregarChat(builder.Configuration);
builder.Services.AgregarOrders(builder.Configuration);
builder.Services.AgregarReputation(builder.Configuration);
builder.Services.AgregarNotifications(builder.Configuration, builder.Environment);
builder.Services.AddScoped<IConsultaAvisoContactable, ConsultaAvisoContactableAdapter>();
builder.Services.AddScoped<IConsultaAvisoParaOrden, ConsultaAvisoParaOrdenAdapter>();
builder.Services.AddScoped<IConsultaEmailUsuario, ConsultaEmailUsuarioAdapter>();
builder.Services.AddScoped<IConsultaProductoParaAlerta, ConsultaProductoParaAlertaAdapter>();
builder.Services.AddScoped<IConsultaParticipantesOrden, ConsultaParticipantesOrdenAdapter>();
builder.Services.AddScoped<IConsultaVerificacionParticipante, ConsultaVerificacionParticipanteAdapter>();
builder.Services.AddScoped<IOrquestadorCierreOrden, OrquestadorCierreOrden>();
builder.Services.AddScoped<IConsultaOrdenCalificable, ConsultaOrdenCalificableAdapter>();
builder.Services.AddOptions<OpcionesTiempoRealChat>()
    .Bind(builder.Configuration.GetSection(OpcionesTiempoRealChat.Seccion))
    .Validate(o => o.MaximoConversaciones is > 0 and <= 100)
    .Validate(o => o.MaximoInvocacionesPorMinuto is > 0 and <= 600)
    .ValidateOnStart();
builder.Services.AddSingleton<EstadoSuscripcionesChat>();
builder.Services.AddSingleton<RegistroConexionesChat>();
builder.Services.AddScoped<IRevocadorTiempoRealChat, RevocadorTiempoRealChat>();
builder.Services.AddScoped<IPublicadorMensajesTiempoReal, PublicadorSignalRMensajes>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<DespachadorEntregasTiempoReal>();
}
builder.Services.AddAuthorizationBuilder().AddPolicy(ChatHub.Politica, politica =>
    politica.RequireAuthenticatedUser().RequireAssertion(contexto =>
        Guid.TryParse(
            contexto.User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out var usuarioId)
        && usuarioId != Guid.Empty));

// Policy de correo confirmado: bloquea acciones de riesgo (iniciar KYC, crear/editar avisos)
// a usuarios que aún no confirmaron su email. El claim se emite en el JWT, así que surte
// efecto en el siguiente login o refresh.
builder.Services.AddAuthorizationBuilder().AddPolicy(PoliticasAutorizacion.EmailConfirmado, politica =>
    politica.Requirements.Add(new RequisitoEmailConfirmado()));
builder.Services.AddSingleton<IAuthorizationHandler, EmailConfirmadoHandler>();
builder.Services.AddSignalR(opciones =>
{
    var tiempoReal = builder.Configuration
        .GetSection(OpcionesTiempoRealChat.Seccion)
        .Get<OpcionesTiempoRealChat>() ?? new OpcionesTiempoRealChat();
    opciones.EnableDetailedErrors = false;
    opciones.MaximumReceiveMessageSize = tiempoReal.TamanoMaximoMensajeBytes;
    opciones.StreamBufferCapacity = tiempoReal.CapacidadBuffer;
    opciones.MaximumParallelInvocationsPerClient = 1;
    opciones.HandshakeTimeout = TimeSpan.FromSeconds(tiempoReal.SegundosHandshake);
});
builder.Services.AddHealthChecks()
    .AddCaseritoDbContextChecks(cadenaConexion);

builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy("auth-forgot-password", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("auth-reset-password", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("auth-reenvio-confirmacion", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("chat-iniciar", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("chat-enviar", contexto => RateLimitPartition.GetTokenBucketLimiter(
        Particion(contexto),
        _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 25,
            TokensPerPeriod = 20,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("chat-consultas", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("chat-seguridad-acciones", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("orders-crear", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("orders-acciones", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("orders-consultas", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("reputation-crear", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("reputation-consultas", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("reputation-publico", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    opciones.AddPolicy("busquedas-crear", contexto => RateLimitPartition.GetFixedWindowLimiter(
        Particion(contexto),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
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
});
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<SecuritySchemeTransformer>());

var app = builder.Build();

// El endpoint /openapi/v1.json se expone en Development y en Testing, pero NUNCA en Production
// (no se publica la superficie de endpoints, incluidos los admin). Se incluye Testing porque el
// target de build opt-in (-p:GenerateOpenApi=true, Microsoft.Extensions.ApiDescription.Server)
// arranca el host bajo ASPNETCORE_ENVIRONMENT=Testing para emitir el JSON del contrato: si
// MapOpenApi no se registra en ese entorno, cambia el orden de los endpoints y por lo tanto el
// JSON emitido difiere del committeado. Gatear a Development+Testing preserva el contrato exacto.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
}

// Migración + seeding de roles al arrancar. En Development es automático; fuera de Development
// (p. ej. Production) es opt-in vía "Migraciones:EjecutarAlArranque" (ruta de migración controlada:
// el deploy de prod activa el flag para aplicar el esquema y sembrar los roles del MVP —incluido
// Cliente, sin el cual el registro daría 500—). El seeding es idempotente y corre tras migrar.
var ejecutarMigraciones =
    app.Environment.IsDevelopment()
    || app.Configuration.GetValue<bool>("Migraciones:EjecutarAlArranque");

if (ejecutarMigraciones && !string.IsNullOrWhiteSpace(cadenaConexion))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }

    await app.Services.SembrarRolesAsync();

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

    using (var scopeBootstrap = app.Services.CreateScope())
    {
        var opcionesBootstrap = app.Configuration
            .GetSection("BootstrapPruebas")
            .Get<OpcionesBootstrapUsuariosPrueba>() ?? new OpcionesBootstrapUsuariosPrueba();
        var bootstrap = new BootstrapUsuariosPrueba(
            scopeBootstrap.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>(),
            scopeBootstrap.ServiceProvider.GetRequiredService<ILogger<BootstrapUsuariosPrueba>>());
        await bootstrap.EjecutarAsync(opcionesBootstrap, app.Environment.IsDevelopment());
    }

    using (var scopeCatalog = app.Services.CreateScope())
    {
        var dbCatalog = scopeCatalog.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await dbCatalog.Database.MigrateAsync();
    }

    await app.Services.SembrarCatalogoAsync();

    using (var scopeChat = app.Services.CreateScope())
    {
        var dbChat = scopeChat.ServiceProvider.GetRequiredService<ChatDbContext>();
        await dbChat.Database.MigrateAsync();
    }

    using (var scopeOrders = app.Services.CreateScope())
    {
        var dbOrders = scopeOrders.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbOrders.Database.MigrateAsync();
    }

    using (var scopeReputation = app.Services.CreateScope())
    {
        var dbReputation = scopeReputation.ServiceProvider
            .GetRequiredService<ReputationDbContext>();
        await dbReputation.Database.MigrateAsync();
    }

    using (var scopeNotifications = app.Services.CreateScope())
    {
        var dbNotifications = scopeNotifications.ServiceProvider
            .GetRequiredService<NotificationsDbContext>();
        await dbNotifications.Database.MigrateAsync();
    }

    await app.Services.SembrarPuntosEncuentroSegurosAsync();
}

// nginx termina TLS y llega desde la red bridge de Docker. Se aceptan sus cabeceras porque el
// puerto de la aplicación solo se publica en loopback en el compose de producción.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseStaticFiles();

app.MapAuthEndpoints();
app.MapAuthExternaEndpoints();
app.MapPerfilEndpoints();
app.MapAdminEndpoints();
app.MapKycEndpoints();
app.MapAvisosEndpoints();
app.MapCatalogoEndpoints();
app.MapPublicoEndpoints();
app.MapPerfilesPublicosEndpoints();
app.MapFotosEndpoints();
app.MapModeracionEndpoints();
app.MapModeracionChatEndpoints();
app.MapChatEndpoints();
app.MapOrdersEndpoints();
app.MapReputationEndpoints();
app.MapBusquedasGuardadasEndpoints();
app.MapPuntosEncuentroEndpoints();
app.MapNotificationsEndpoints();
app.MapHub<ChatHub>("/hubs/chat", opciones =>
{
    var tiempoReal = app.Configuration
        .GetSection(OpcionesTiempoRealChat.Seccion)
        .Get<OpcionesTiempoRealChat>() ?? new OpcionesTiempoRealChat();
    opciones.CloseOnAuthenticationExpiration = true;
    opciones.ApplicationMaxBufferSize = tiempoReal.BufferAplicacionBytes;
    opciones.TransportMaxBufferSize = tiempoReal.BufferTransporteBytes;
}).RequireAuthorization(ChatHub.Politica);

app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

static string Particion(HttpContext contexto) =>
    contexto.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
    ?? contexto.Connection.RemoteIpAddress?.ToString()
    ?? "anonimo";

await app.RunAsync();

/// <summary>
/// Clase parcial pública requerida para que <c>WebApplicationFactory&lt;Program&gt;</c>
/// pueda referenciar el host desde el proyecto de pruebas de integración.
/// </summary>
#pragma warning disable S1118 // No se agrega ctor protegido/estático: la clase existe solo como ancla pública para WebApplicationFactory<Program> en tests de integración.
public partial class Program;
#pragma warning restore S1118
