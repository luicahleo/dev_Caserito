using CaseritoApp.BuildingBlocks.Application.Behaviors;
using CaseritoApp.Identity.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Se registran MediatR y los behaviors del pipeline de aplicación.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CaseritoApp.BuildingBlocks.Application.Abstractions.IUnitOfWork>());

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

// El DbContext de Identity (y el resto de Identity Core) solo se registra si hay cadena de
// conexión configurada (env, user-secrets o compose). Sin cadena (p. ej. tests de /health),
// el host arranca sin BD.
var cadenaConexion = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AgregarIdentity(builder.Configuration);

var app = builder.Build();

// Migración automática solo en Development y solo si hay cadena de conexión presente.
if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(cadenaConexion))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

await app.RunAsync();

/// <summary>
/// Clase parcial pública requerida para que <c>WebApplicationFactory&lt;Program&gt;</c>
/// pueda referenciar el host desde el proyecto de pruebas de integración.
/// </summary>
#pragma warning disable S1118 // No se agrega ctor protegido/estático: la clase existe solo como ancla pública para WebApplicationFactory<Program> en tests de integración.
public partial class Program;
#pragma warning restore S1118
