using CaseritoApp.Chat.Domain.Conversaciones;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.TiempoReal;

internal static class ConfiguracionEntregaTiempoReal
{
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<EntregaTiempoReal>(e =>
        {
            e.ToTable("EntregasTiempoReal");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ConversacionId).IsRequired();
            e.Property(x => x.MensajeId).IsRequired();
            e.Property(x => x.Secuencia).IsRequired();
            e.Property(x => x.CreadaEn).IsRequired();
            e.Property(x => x.Intentos).IsRequired();
            e.Property(x => x.ProximoIntentoEn).IsRequired();

            e.HasOne<Mensaje>()
                .WithMany()
                .HasForeignKey(x => x.MensajeId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.MensajeId).IsUnique();
            e.HasIndex(x => new { x.ProcesadaEn, x.ProximoIntentoEn, x.LeaseHasta });
            e.HasIndex(x => new { x.Secuencia, x.Id });
        });
    }
}
