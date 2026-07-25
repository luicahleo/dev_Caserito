using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using CaseritoApp.Orders.Infrastructure;
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
        builder.UseSetting("AlmacenFotos:RutaBase", Path.Combine(Path.GetTempPath(), $"fotos-test-{Guid.NewGuid():N}"));
        builder.ConfigureServices(servicios =>
        {
            servicios.RemoveAll<DbContextOptions<IdentityDbContext>>();
            servicios.AddDbContext<IdentityDbContext>(o => o.UseSqlServer(_sql.GetConnectionString()));

            servicios.RemoveAll<DbContextOptions<CatalogDbContext>>();
            servicios.AddDbContext<CatalogDbContext>(o => o.UseSqlServer(_sql.GetConnectionString()));

            servicios.RemoveAll<DbContextOptions<ChatDbContext>>();
            servicios.AddDbContext<ChatDbContext>(o => o.UseSqlServer(_sql.GetConnectionString()));

            servicios.RemoveAll<DbContextOptions<OrdersDbContext>>();
            servicios.AddDbContext<OrdersDbContext>(o => o.UseSqlServer(_sql.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.MigrateAsync();

            var dbCatalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await dbCatalog.Database.MigrateAsync();

            var dbChat = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
            await dbChat.Database.MigrateAsync();

            var dbOrders = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            await dbOrders.Database.MigrateAsync();
        }

        await Services.SembrarRolesAsync();
        await Services.SembrarCatalogoAsync();
    }

    public OrdersDbContext CrearOrdersDbContext()
    {
        var opciones = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlServer(_sql.GetConnectionString())
            .Options;
        return new OrdersDbContext(opciones);
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
