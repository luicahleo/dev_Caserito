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
            e.Property(v => v.Id).ValueGeneratedNever();

            e.HasMany(v => v.Solicitudes)
                .WithOne()
                .HasForeignKey("VerificacionKycId")
                .OnDelete(DeleteBehavior.Cascade);

            e.Navigation(v => v.Solicitudes).UsePropertyAccessMode(PropertyAccessMode.Field);

            // Token de concurrencia optimista de la raíz (entero incremental, no rowversion: la raíz
            // no tiene columnas escalares propias, así que un rowversion no generaría UPDATE al tocar
            // la raíz). El override de SaveChangesAsync lo incrementa cuando cambia una hija, para que
            // dos operaciones concurrentes sobre el mismo agregado colisionen (doble aprobación / dos
            // Pendiente).
            e.Property<int>("Version").IsConcurrencyToken();
        });

        builder.Entity<SolicitudKyc>(e =>
        {
            e.ToTable("SolicitudesKyc");
            e.HasKey(s => s.Id);
            // El Id lo genera el dominio (Guid.NewGuid() en el constructor), no la BD. Sin esto, EF
            // Core asume por convención que un Guid de PK es "value-generated on add"; al descubrir
            // una hija nueva (con Id ya asignado) añadida a la colección de una raíz YA rastreada
            // (p. ej. reenvío de KYC tras un rechazo, sin pasar por Add() explícito), EF la trata como
            // si ya existiera en la BD y genera un UPDATE en vez de un INSERT, fallando con 0 filas
            // afectadas (DbUpdateConcurrencyException espuria, no relacionada con el token de Version).
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(s => s.TipoDocumento).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(s => s.ReferenciaDocumento).HasMaxLength(200).IsRequired();
            e.Property(s => s.ReferenciaSelfie).HasMaxLength(200).IsRequired();
            e.Property(s => s.MotivoRechazo).HasMaxLength(500);
            e.Property(s => s.ScoreSimilitud); // Score de similitud devuelto por ARGOS; nullable.
            e.HasIndex("VerificacionKycId");
        });

        builder.Entity<DocumentoKycRegistrado>(e =>
        {
            e.ToTable("DocumentosKycRegistrados");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).ValueGeneratedNever();
            e.Property(d => d.HuellaCi).HasMaxLength(64).IsRequired();
            e.Property(d => d.NumeroCiCifrado).HasMaxLength(1000).IsRequired();
            e.Property(d => d.ComplementoCiCifrado).HasMaxLength(1000);
            e.Property(d => d.DepartamentoExpedicion).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasIndex(d => d.HuellaCi).IsUnique();
            e.HasIndex(d => d.UsuarioId).IsUnique();
        });
    }
}
