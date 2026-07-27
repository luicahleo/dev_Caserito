using CaseritoApp.Notifications.Domain.PuntosEncuentro;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure.PuntosEncuentro;

public static class ConfiguracionPuntoEncuentroSeguro
{
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<PuntoEncuentroSeguro>(entidad =>
        {
            entidad.ToTable("SafeMeetingPoints");
            entidad.HasKey(p => p.Id);
            entidad.Property(p => p.Id).ValueGeneratedNever();
            entidad.Property(p => p.Nombre).HasColumnName("Name").HasMaxLength(150).IsRequired();
            entidad.Property(p => p.Ciudad).HasColumnName("City").HasMaxLength(100).IsRequired();
            entidad.Property(p => p.Direccion).HasColumnName("Address").HasMaxLength(250).IsRequired();
            entidad.Property(p => p.Activo).HasColumnName("IsActive").IsRequired();
            entidad.Property(p => p.Version).IsRowVersion();
            entidad.Ignore(p => p.EventosDeDominio);
            entidad.HasIndex(p => new { p.Ciudad, p.Activo });
        });
    }
}
