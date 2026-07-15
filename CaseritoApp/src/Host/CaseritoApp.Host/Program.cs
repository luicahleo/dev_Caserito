using CaseritoApp.BuildingBlocks.Application.Behaviors;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.Identity.Infrastructure;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Se registran MediatR y los behaviors del pipeline de aplicación, ampliando el escaneo de
// ensamblados para incluir CaseritoApp.Identity.Application (Query/Command de perfil).
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(
        typeof(CaseritoApp.BuildingBlocks.Application.Abstractions.IUnitOfWork).Assembly,
        typeof(ObtenerPerfilQuery).Assembly));

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

// Validators de FluentValidation del ensamblado de Identity.Application (p. ej. ActualizarPerfilCommandValidator).
builder.Services.AddValidatorsFromAssembly(typeof(ObtenerPerfilQuery).Assembly);

// El DbContext de Identity (y el resto de Identity Core) solo se registra si hay cadena de
// conexión configurada (env, user-secrets o compose). Sin cadena (p. ej. tests de /health),
// el host arranca sin BD.
var cadenaConexion = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AgregarIdentity(builder.Configuration);
builder.Services.AgregarAutenticacionJwt(builder.Configuration, builder.Environment);

var app = builder.Build();

// Migración + seeding de roles automáticos solo en Development y solo si hay cadena de conexión.
if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(cadenaConexion))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }

    await app.Services.SembrarRolesAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapPerfilEndpoints();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

await app.RunAsync();

/// <summary>
/// Clase parcial pública requerida para que <c>WebApplicationFactory&lt;Program&gt;</c>
/// pueda referenciar el host desde el proyecto de pruebas de integración.
/// </summary>
#pragma warning disable S1118 // No se agrega ctor protegido/estático: la clase existe solo como ancla pública para WebApplicationFactory<Program> en tests de integración.
public partial class Program;
#pragma warning restore S1118
