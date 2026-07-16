using CaseritoApp.Identity.Domain.Kyc;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Kyc;

/// <summary>Mapeo EF Core de <see cref="VerificacionKyc"/> y su historial de <see cref="SolicitudKyc"/>.</summary>
public static class ConfiguracionKyc
{
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<VerificacionKyc>(e =>
        {
            e.ToTable("VerificacionesKyc");
            e.HasKey(v => v.Id);

            e.HasMany(v => v.Solicitudes)
                .WithOne()
                .HasForeignKey("VerificacionKycId")
                .OnDelete(DeleteBehavior.Cascade);

            e.Navigation(v => v.Solicitudes).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<SolicitudKyc>(e =>
        {
            e.ToTable("SolicitudesKyc");
            e.HasKey(s => s.Id);
            e.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(s => s.TipoDocumento).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(s => s.ReferenciaDocumento).HasMaxLength(200).IsRequired();
            e.Property(s => s.ReferenciaSelfie).HasMaxLength(200).IsRequired();
            e.Property(s => s.MotivoRechazo).HasMaxLength(500);
            e.HasIndex("VerificacionKycId");
        });
    }
}
