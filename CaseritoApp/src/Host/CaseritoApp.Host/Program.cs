using CaseritoApp.BuildingBlocks.Application.Behaviors;
using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Host.Chat;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Host.OpenApi;
using CaseritoApp.Identity.Application.Perfil;
using CaseritoApp.Identity.Infrastructure;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Se registran MediatR y los behaviors del pipeline de aplicación, ampliando el escaneo de
// ensamblados para incluir CaseritoApp.Identity.Application (Query/Command de perfil) y
// CaseritoApp.Catalog.Application (Query/Command de avisos y catálogo).
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(
        typeof(CaseritoApp.BuildingBlocks.Application.Abstractions.IUnitOfWork).Assembly,
        typeof(ObtenerPerfilQuery).Assembly,
        typeof(CaseritoApp.Catalog.Application.Avisos.CrearAvisoCommand).Assembly));

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

// Validators de FluentValidation de los ensamblados de Identity.Application (p. ej.
// ActualizarPerfilCommandValidator) y Catalog.Application (p. ej. CrearAvisoCommandValidator).
builder.Services.AddValidatorsFromAssembly(typeof(ObtenerPerfilQuery).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(CaseritoApp.Catalog.Application.Avisos.CrearAvisoCommand).Assembly);

// El DbContext de Identity (y el resto de Identity Core) solo se registra si hay cadena de
// conexión configurada (env, user-secrets o compose). Sin cadena (p. ej. tests de /health),
// el host arranca sin BD.
var cadenaConexion = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AgregarIdentity(builder.Configuration, builder.Environment);
builder.Services.AgregarAutenticacionJwt(builder.Configuration, builder.Environment);
builder.Services.AgregarCatalog(builder.Configuration);
builder.Services.AddScoped<IConsultaAvisoContactable, ConsultaAvisoContactableAdapter>();
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

    using (var scopeCatalog = app.Services.CreateScope())
    {
        var dbCatalog = scopeCatalog.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await dbCatalog.Database.MigrateAsync();
    }

    await app.Services.SembrarCatalogoAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapPerfilEndpoints();
app.MapAdminEndpoints();
app.MapKycEndpoints();
app.MapAvisosEndpoints();
app.MapCatalogoEndpoints();
app.MapPublicoEndpoints();
app.MapFotosEndpoints();
app.MapModeracionEndpoints();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

await app.RunAsync();

/// <summary>
/// Clase parcial pública requerida para que <c>WebApplicationFactory&lt;Program&gt;</c>
/// pueda referenciar el host desde el proyecto de pruebas de integración.
/// </summary>
#pragma warning disable S1118 // No se agrega ctor protegido/estático: la clase existe solo como ancla pública para WebApplicationFactory<Program> en tests de integración.
public partial class Program;
#pragma warning restore S1118
