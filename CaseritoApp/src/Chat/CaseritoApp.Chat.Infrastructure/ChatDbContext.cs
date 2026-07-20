using CaseritoApp.Chat.Domain.Conversaciones;
using CaseritoApp.Chat.Infrastructure.Conversaciones;
using CaseritoApp.Chat.Infrastructure.TiempoReal;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure;

public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public const string Schema = "chat";

    public DbSet<Conversacion> Conversaciones => Set<Conversacion>();

    public DbSet<Mensaje> Mensajes => Set<Mensaje>();

    public DbSet<EntregaTiempoReal> EntregasTiempoReal => Set<EntregaTiempoReal>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepararEntregasTiempoReal();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepararEntregasTiempoReal();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        ConfiguracionChat.Configurar(modelBuilder);
        ConfiguracionEntregaTiempoReal.Configurar(modelBuilder);
    }

    private void PrepararEntregasTiempoReal()
    {
        var mensajesNuevos = ChangeTracker.Entries<Mensaje>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();
        var mensajesConEntrega = EntregasTiempoReal.Local.Select(e => e.MensajeId).ToHashSet();

        foreach (var mensaje in mensajesNuevos.Where(m => !mensajesConEntrega.Contains(m.Id)))
        {
            EntregasTiempoReal.Add(EntregaTiempoReal.Para(mensaje));
        }
    }
}
