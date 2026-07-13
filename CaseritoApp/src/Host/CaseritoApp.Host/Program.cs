using CaseritoApp.BuildingBlocks.Application.Behaviors;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// Se registran MediatR y los behaviors del pipeline de aplicación.
// Aún no se registran DbContexts ni cadena de conexión: el host arranca sin BD.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CaseritoApp.BuildingBlocks.Application.Abstractions.IUnitOfWork>());

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }));

await app.RunAsync();

/// <summary>
/// Clase parcial pública requerida para que <c>WebApplicationFactory&lt;Program&gt;</c>
/// pueda referenciar el host desde el proyecto de pruebas de integración.
/// </summary>
#pragma warning disable S1118 // No se agrega ctor protegido/estático: la clase existe solo como ancla pública para WebApplicationFactory<Program> en tests de integración.
public partial class Program;
#pragma warning restore S1118
