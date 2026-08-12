using CaseritoApp.Notifications.Domain.Push;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure.Push;

internal static class ConfiguracionPush
{
    public static void Configurar(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SuscripcionPush>(entidad =>
        {
            entidad.ToTable("PushSubscriptions");
            entidad.HasKey(x => x.Id);
            entidad.Property(x => x.DispositivoId).HasMaxLength(128).IsRequired();
            entidad.Property(x => x.Endpoint).HasMaxLength(2048).IsRequired();
            entidad.Property(x => x.P256dh).HasMaxLength(512).IsRequired();
            entidad.Property(x => x.Auth).HasMaxLength(256).IsRequired();
            entidad.Ignore(x => x.EventosDeDominio);
            entidad.HasIndex(x => new { x.UsuarioId, x.DispositivoId }).IsUnique();
            entidad.HasIndex(x => new { x.UsuarioId, x.Activa });
        });

        modelBuilder.Entity<IntencionPush>(entidad =>
        {
            entidad.ToTable("PushIntents");
            entidad.HasKey(x => x.Id);
            entidad.Ignore(x => x.EventosDeDominio);
            entidad.Ignore(x => x.Procesada);
            entidad.HasIndex(x => x.EventoId).IsUnique();
            entidad.HasIndex(x => new { x.ProcesadaEn, x.DisponibleEn, x.LeaseHasta });
        });
    }
}
