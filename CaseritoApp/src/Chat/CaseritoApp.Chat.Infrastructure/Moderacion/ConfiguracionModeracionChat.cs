using CaseritoApp.Chat.Domain.Moderacion;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Moderacion;

public static class ConfiguracionModeracionChat
{
    public const string FiltroReportesAbiertos = "[Estado] IN (1, 2)";

    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<ReporteChat>(e =>
        {
            e.ToTable("Reportes");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.ConversacionId).IsRequired();
            e.Property(r => r.ReportanteId).IsRequired();
            e.Property(r => r.TipoObjetivo).IsRequired();
            e.Property(r => r.Categoria).IsRequired();
            e.Property(r => r.Detalle).HasMaxLength(1000).IsUnicode();
            e.Property(r => r.Estado).HasDefaultValue(EstadoReporteChat.Pendiente).IsRequired();
            e.Property(r => r.CreadoEn).IsRequired();
            e.Property<byte[]>("Version").IsRowVersion();
            e.Ignore(r => r.EventosDeDominio);

            e.HasIndex(r => new { r.ReportanteId, r.ConversacionId, r.TipoObjetivo, r.MensajeId })
                .IsUnique()
                .HasFilter(FiltroReportesAbiertos);
        });

        builder.Entity<RegistroModeracionChat>(e =>
        {
            e.ToTable("RegistrosModeracion");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.ReporteId).IsRequired();
            e.Property(r => r.ConversacionId).IsRequired();
            e.Property(r => r.ModeradorId).IsRequired();
            e.Property(r => r.Accion).IsRequired();
            e.Property(r => r.CreadoEn).IsRequired();

            e.HasIndex(r => new { r.ReporteId, r.CreadoEn });
        });
    }
}
