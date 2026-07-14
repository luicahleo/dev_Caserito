using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>
/// Factory de diseño que permite a <c>dotnet ef</c> crear el <see cref="IdentityDbContext"/>
/// sin arrancar el host. La cadena de conexión aquí es solo para el proceso de diseño
/// (generación de migraciones); en tiempo de ejecución el host usa la cadena real via
/// <see cref="DependencyInjection.AgregarIdentity"/>.
/// </summary>
public sealed class DesignTimeIdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=CaseritoDb;User Id=sa;Password=noop;TrustServerCertificate=True")
            .Options;
        return new IdentityDbContext(opciones);
    }
}
