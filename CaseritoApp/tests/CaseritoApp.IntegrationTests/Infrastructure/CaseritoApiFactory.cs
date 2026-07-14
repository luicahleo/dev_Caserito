using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;

namespace CaseritoApp.IntegrationTests.Infrastructure;

/// <summary>
/// Fixture de pruebas de integración que arranca la aplicación en entorno <c>Testing</c>
/// contra un SQL Server efímero (Testcontainers), registrando el <see cref="IdentityDbContext"/>
/// que el host no registra por sí mismo cuando no hay cadena de conexión configurada.
/// </summary>
public sealed class CaseritoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <summary>
    /// Clave HS256 fija (mínimo 32 bytes) usada en Testing para que la firma del access JWT (en
    /// login/refresh) y su validación (Bearer, p. ej. /perfil) usen exactamente la misma clave.
    /// </summary>
    public const string JwtKeyDePrueba = "clave-de-prueba-para-tests-de-integracion-32b+";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"{OpcionesJwt.Seccion}:Key", JwtKeyDePrueba);
        builder.ConfigureServices(servicios =>
        {
            servicios.RemoveAll<DbContextOptions<IdentityDbContext>>();
            servicios.AddDbContext<IdentityDbContext>(o => o.UseSqlServer(_sql.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }

    // El WebApplicationFactory base expone DisposeAsync() -> ValueTask (de IAsyncDisposable).
    // IAsyncLifetime de xUnit exige DisposeAsync() -> Task, con firma distinta en el tipo de
    // retorno, por lo que ambos miembros pueden coexistir: este método "new" es el que xUnit
    // invoca (a través de IAsyncLifetime) al finalizar los tests, y delega primero en la
    // limpieza del host (base.DisposeAsync) y luego libera el contenedor de SQL Server.
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }
}
