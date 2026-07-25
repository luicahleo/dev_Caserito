using CaseritoApp.Catalog.Domain.Avisos;
using CaseritoApp.Catalog.Domain.Moderacion;
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
            e.Property(a => a.Estado).HasConversion<string>().HasMaxLength(20).IsRequired()
                .IsConcurrencyToken();
            e.Property(a => a.OrdenVentaId).IsConcurrencyToken();
            e.Property(a => a.EstadoModeracion).HasConversion<string>().HasMaxLength(30).IsRequired()
                .HasDefaultValue(EstadoModeracionAviso.Visible).IsConcurrencyToken();
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

            e.HasMany(a => a.Fotos)
             .WithOne()
             .HasForeignKey(f => f.AvisoId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Navigation(a => a.Fotos).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<FotoAviso>(e =>
        {
            e.ToTable("FotosAviso");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).ValueGeneratedNever();
            e.Property(f => f.AvisoId).IsRequired();
            e.Property(f => f.Clave).HasMaxLength(200).IsRequired();
            e.Property(f => f.ContentType).HasMaxLength(50).IsRequired();
            e.Property(f => f.Orden).IsRequired();
            e.HasIndex(f => f.AvisoId);
            e.HasIndex(f => new { f.AvisoId, f.Orden });
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

        builder.Entity<ReporteAviso>(e =>
        {
            e.ToTable("ReportesAviso");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.Motivo).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(r => r.Detalle).HasMaxLength(500);
            e.Property(r => r.Estado).HasConversion<string>().HasMaxLength(20).IsRequired().IsConcurrencyToken();
            e.Property(r => r.FechaCreacion).IsRequired();
            e.HasOne<Aviso>().WithMany().HasForeignKey(r => r.AvisoId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.AvisoId, r.ReportanteId }).IsUnique().HasFilter("[Estado] = N'Pendiente'");
            e.HasIndex(r => new { r.Estado, r.AvisoId });
        });

        builder.Entity<RegistroModeracion>(e =>
        {
            e.ToTable("RegistrosModeracion");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.Accion).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(r => r.Fecha).IsRequired();
            e.HasIndex(r => new { r.AvisoId, r.Fecha });
        });
    }
}
