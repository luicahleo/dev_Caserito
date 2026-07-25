using CaseritoApp.Orders.Domain.Ordenes;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Orders.Infrastructure.Ordenes;

public static class ConfiguracionOrden
{
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<Orden>(entidad =>
        {
            entidad.ToTable("Orders");
            entidad.HasKey(orden => orden.Id);
            entidad.Property(orden => orden.Id).ValueGeneratedNever();
            entidad.Property(orden => orden.AvisoId).IsRequired();
            entidad.Property(orden => orden.CompradorId).IsRequired();
            entidad.Property(orden => orden.VendedorId).IsRequired();
            entidad.Property(orden => orden.Estado)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entidad.Property(orden => orden.MontoAcordado)
                .HasColumnType("decimal(18,2)")
                .IsRequired();
            entidad.Property(orden => orden.Moneda).HasMaxLength(3).IsRequired();
            entidad.Property(orden => orden.CreadaEn).IsRequired();
            entidad.Property(orden => orden.ActualizadaEn).IsRequired();
            entidad.Property(orden => orden.MarcadaVendidaEn);
            entidad.Property(orden => orden.CompradorConfirmoEn);
            entidad.Property(orden => orden.CompletadaEn);
            entidad.Property(orden => orden.Version).IsRowVersion();
            entidad.Ignore(orden => orden.EventosDeDominio);
            entidad.HasIndex(orden => new { orden.AvisoId, orden.CompradorId })
                .IsUnique()
                .HasFilter("[Estado] <> 'Cancelled'");
            entidad.HasIndex(orden => new { orden.CompradorId, orden.ActualizadaEn });
            entidad.HasIndex(orden => new { orden.VendedorId, orden.ActualizadaEn });
        });
    }
}
