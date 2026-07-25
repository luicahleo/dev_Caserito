using CaseritoApp.Reputation.Domain.Resenas;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Reputation.Infrastructure.Resenas;

public static class ConfiguracionResena
{
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<Resena>(entidad =>
        {
            entidad.ToTable("Reviews");
            entidad.HasKey(resena => resena.Id);
            entidad.Property(resena => resena.Id).ValueGeneratedNever();
            entidad.Property(resena => resena.OrderId).IsRequired();
            entidad.Property(resena => resena.AutorId)
                .HasColumnName("AuthorId")
                .IsRequired();
            entidad.Property(resena => resena.DestinatarioId)
                .HasColumnName("RecipientId")
                .IsRequired();
            entidad.Property(resena => resena.RolAutor)
                .HasColumnName("AuthorRole")
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();
            entidad.Property(resena => resena.Puntuacion)
                .HasColumnName("Rating")
                .IsRequired();
            entidad.Property(resena => resena.Comentario)
                .HasColumnName("Comment")
                .HasMaxLength(500)
                .IsRequired();
            entidad.Property(resena => resena.CreadaEn)
                .HasColumnName("CreatedAt")
                .IsRequired();
            entidad.Property(resena => resena.Version).IsRowVersion();
            entidad.Ignore(resena => resena.EventosDeDominio);
            entidad.HasIndex(resena => new { resena.OrderId, resena.AutorId })
                .IsUnique();
            entidad.HasIndex(resena => new { resena.OrderId, resena.DestinatarioId });
            entidad.HasIndex(resena => new
            {
                resena.DestinatarioId,
                resena.CreadaEn,
                resena.Id,
            });
        });
    }
}
