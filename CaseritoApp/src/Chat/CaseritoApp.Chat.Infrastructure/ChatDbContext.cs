using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Infrastructure.Conversaciones;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure;

public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public const string Schema = "chat";

    public DbSet<Conversacion> Conversaciones => Set<Conversacion>();

    public DbSet<Mensaje> Mensajes => Set<Mensaje>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        ConfiguracionChat.Configurar(modelBuilder);
    }
}
