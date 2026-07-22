using CaseritoApp.Chat.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure.Seguridad;

public static class ConfiguracionSeguridadChat
{
    public static void Configurar(ModelBuilder builder)
    {
        builder.Entity<BloqueoUsuario>(e =>
        {
            e.ToTable("BloqueosUsuario");
            e.HasKey(b => b.Id);
            e.Property(b => b.Id).ValueGeneratedNever();
            e.Property(b => b.BloqueadorId).IsRequired();
            e.Property(b => b.BloqueadoId).IsRequired();
            e.Property(b => b.CreadoEn).IsRequired();
            e.Ignore(b => b.EventosDeDominio);

            e.HasIndex(b => new { b.BloqueadorId, b.BloqueadoId }).IsUnique();
            e.HasIndex(b => b.BloqueadorId);
            e.HasIndex(b => b.BloqueadoId);
        });
    }
}
