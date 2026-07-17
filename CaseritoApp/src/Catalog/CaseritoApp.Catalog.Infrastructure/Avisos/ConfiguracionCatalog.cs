using CaseritoApp.Catalog.Domain.Avisos;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Catalog.Infrastructure.Avisos;

/// <summary>Configuración EF Core de las entidades del contexto Catalog.</summary>
public static class ConfiguracionCatalog
{
    /// <summary>Configura <see cref="Aviso"/>, <see cref="Categoria"/> y <see cref="Ciudad"/>.</summary>
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<Aviso>(e =>
        {
            e.ToTable("Avisos");
            e.HasKey(a => a.Id);
            // Id asignado por el dominio (Entity base): sin value-generation para evitar UPDATE espurio.
            e.Property(a => a.Id).ValueGeneratedNever();
            e.Property(a => a.VendedorId).IsRequired();
            e.Property(a => a.Titulo).HasMaxLength(120).IsRequired();
            e.Property(a => a.Descripcion).HasMaxLength(2000).IsRequired();
            e.Property(a => a.CategoriaId).IsRequired();
            e.Property(a => a.CiudadId).IsRequired();
            e.Property(a => a.Condicion).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(a => a.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(a => a.FechaCreacion).IsRequired();
            e.Property(a => a.FechaActualizacion).IsRequired();

            e.OwnsOne(a => a.Precio, p =>
            {
                p.Property(x => x.Monto).HasColumnName("PrecioMonto").HasColumnType("decimal(18,2)").IsRequired();
                p.Property(x => x.Moneda).HasColumnName("PrecioMoneda").HasConversion<string>().HasMaxLength(3).IsRequired();
            });
            e.Navigation(a => a.Precio).IsRequired();

            e.HasIndex(a => a.VendedorId);
            e.HasIndex(a => a.Estado);
            e.HasIndex(a => new { a.VendedorId, a.Estado });

            // Los eventos de dominio no se persisten (dispatcher diferido).
            e.Ignore(a => a.EventosDeDominio);
        });

        builder.Entity<Categoria>(e =>
        {
            e.ToTable("Categorias");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Nombre).HasMaxLength(80).IsRequired();
            e.Property(c => c.Activa).IsRequired();
            e.Property(c => c.Orden).IsRequired();
        });

        builder.Entity<Ciudad>(e =>
        {
            e.ToTable("Ciudades");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Nombre).HasMaxLength(80).IsRequired();
            e.Property(c => c.Activa).IsRequired();
            e.Property(c => c.Orden).IsRequired();
        });
    }
}
