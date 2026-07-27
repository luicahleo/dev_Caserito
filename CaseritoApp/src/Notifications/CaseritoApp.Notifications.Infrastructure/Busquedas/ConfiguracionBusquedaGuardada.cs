using CaseritoApp.Notifications.Domain.Busquedas;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure.Busquedas;

public static class ConfiguracionBusquedaGuardada
{
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<BusquedaGuardada>(entidad =>
        {
            entidad.ToTable("SavedSearches");
            entidad.HasKey(b => b.Id);
            entidad.Property(b => b.Id).ValueGeneratedNever();
            entidad.Property(b => b.UsuarioId).HasColumnName("UserId").IsRequired();
            entidad.Property(b => b.PalabraClave).HasColumnName("Keyword").HasMaxLength(100);
            entidad.Property(b => b.Categoria).HasColumnName("Category").HasMaxLength(100);
            entidad.Property(b => b.Ciudad).HasColumnName("City").HasMaxLength(100);
            entidad.Property(b => b.PrecioMinimo).HasColumnName("MinPrice").HasColumnType("decimal(18,2)");
            entidad.Property(b => b.PrecioMaximo).HasColumnName("MaxPrice").HasColumnType("decimal(18,2)");
            entidad.Property(b => b.EstadoProducto).HasColumnName("EstadoProducto").HasMaxLength(20);
            entidad.Property(b => b.CreadaEn).HasColumnName("CreatedAt").IsRequired();
            entidad.Property(b => b.Version).IsRowVersion();
            entidad.Ignore(b => b.EventosDeDominio);
            entidad.HasIndex(b => b.UsuarioId);
        });
    }
}
