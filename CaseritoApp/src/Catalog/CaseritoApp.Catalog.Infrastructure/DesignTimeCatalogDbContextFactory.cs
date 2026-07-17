using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaseritoApp.Catalog.Infrastructure;

/// <summary>
/// Factory de diseño para que <c>dotnet ef</c> cree el <see cref="CatalogDbContext"/> sin arrancar
/// el host. La cadena es solo para el proceso de diseño (generación de migraciones).
/// </summary>
public sealed class DesignTimeCatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=CaseritoDb;User Id=sa;Password=noop;TrustServerCertificate=True")
            .Options;
        return new CatalogDbContext(opciones);
    }
}
