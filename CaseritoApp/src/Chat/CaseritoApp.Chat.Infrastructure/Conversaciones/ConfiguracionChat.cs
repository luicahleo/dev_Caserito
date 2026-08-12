using CaseritoApp.Chat.Domain.Conversaciones;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Conversaciones;

public static class ConfiguracionChat
{
    public const string IndiceConversacionUnica = "UX_Conversaciones_Comprador_Aviso";
    public const string IndiceMensajeIdempotente = "UX_Mensajes_Conversacion_Remitente_Clave";

    public static void Configurar(ModelBuilder builder)
    {
        builder.HasSequence<long>("SecuenciaMensajes", ChatDbContext.Schema)
            .StartsAt(1)
            .IncrementsBy(1);

        builder.Entity<Conversacion>(e =>
        {
            e.ToTable("Conversaciones");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.AvisoId).IsRequired();
            e.Property(c => c.CompradorId).IsRequired();
            e.Property(c => c.VendedorId).IsRequired();
            e.Property(c => c.CreadaEn).IsRequired();
            e.Property(c => c.UltimaActividadEn).IsRequired();
            e.Property(c => c.UltimaSecuencia).IsRequired();
            e.Property(c => c.UltimaSecuenciaEntregadaComprador).IsRequired();
            e.Property(c => c.UltimaSecuenciaEntregadaVendedor).IsRequired();
            e.Property(c => c.UltimaSecuenciaLeidaComprador).IsRequired();
            e.Property(c => c.UltimaSecuenciaLeidaVendedor).IsRequired();
            e.Property<byte[]>("Version").IsRowVersion();
            e.Ignore(c => c.EventosDeDominio);

            e.HasIndex(c => new { c.CompradorId, c.AvisoId })
                .IsUnique()
                .HasDatabaseName(IndiceConversacionUnica);
            e.HasIndex(c => new { c.CompradorId, c.UltimaActividadEn, c.Id });
            e.HasIndex(c => new { c.VendedorId, c.UltimaActividadEn, c.Id });
        });

        builder.Entity<Mensaje>(e =>
        {
            e.ToTable("Mensajes");
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).ValueGeneratedNever();
            e.Property(m => m.ConversacionId).IsRequired();
            e.Property(m => m.RemitenteId).IsRequired();
            e.Property(m => m.ClaveIdempotencia).IsRequired();
            e.Property(m => m.Secuencia).IsRequired();
            e.Property(m => m.Texto).HasMaxLength(2000).IsUnicode().IsRequired();
            e.Property(m => m.EnviadoEn).IsRequired();

            e.HasOne<Conversacion>()
                .WithMany()
                .HasForeignKey(m => m.ConversacionId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => new { m.ConversacionId, m.Secuencia }).IsUnique();
            e.HasIndex(m => new { m.ConversacionId, m.RemitenteId, m.ClaveIdempotencia })
                .IsUnique()
                .HasDatabaseName(IndiceMensajeIdempotente);
        });
    }
}
