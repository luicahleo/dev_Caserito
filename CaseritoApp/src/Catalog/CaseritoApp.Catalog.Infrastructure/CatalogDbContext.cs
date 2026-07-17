using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Catalog.Infrastructure.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure;

/// <summary>DbContext del contexto Catalog (schema <c>catalog</c>).</summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    /// <summary>Avisos del marketplace.</summary>
    public DbSet<Aviso> Avisos => Set<Aviso>();

    /// <summary>Categorías de referencia.</summary>
    public DbSet<Categoria> Categorias => Set<Categoria>();

    /// <summary>Ciudades de referencia.</summary>
    public DbSet<Ciudad> Ciudades => Set<Ciudad>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        ConfiguracionCatalog.Configurar(modelBuilder);
    }
}
