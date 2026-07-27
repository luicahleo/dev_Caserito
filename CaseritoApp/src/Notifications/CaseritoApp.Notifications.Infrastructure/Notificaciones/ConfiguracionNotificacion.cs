using CaseritoApp.Notifications.Domain.Notificaciones;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure.Notificaciones;

public static class ConfiguracionNotificacion
{
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<Notificacion>(entidad =>
        {
            entidad.ToTable("Notifications");
            entidad.HasKey(n => n.Id);
            entidad.Property(n => n.Id).ValueGeneratedNever();
            entidad.Property(n => n.DestinatarioId).IsRequired();
            entidad.Property(n => n.Tipo)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();
            entidad.Property(n => n.Titulo)
                .HasMaxLength(150)
                .IsRequired();
            entidad.Property(n => n.Mensaje)
                .HasColumnName("Body")
                .HasMaxLength(500)
                .IsRequired();
            entidad.Property(n => n.EntidadRelacionadaId)
                .HasColumnName("RelatedEntityId")
                .IsRequired(false);
            entidad.Property(n => n.Leida)
                .HasColumnName("IsRead")
                .IsRequired();
            entidad.Property(n => n.CreadaEn)
                .HasColumnName("CreatedAt")
                .IsRequired();
            entidad.Property(n => n.Version).IsRowVersion();
            entidad.Ignore(n => n.EventosDeDominio);
            entidad.HasIndex(n => new { n.DestinatarioId, n.CreadaEn, n.Id });
            entidad.HasIndex(n => new { n.DestinatarioId, n.Leida, n.CreadaEn });
        });
    }
}
