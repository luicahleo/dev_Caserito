using CaseritoApp.Notifications.Domain.Busquedas;
using CaseritoApp.Notifications.Domain.Notificaciones;
using CaseritoApp.Notifications.Infrastructure.Busquedas;
using CaseritoApp.Notifications.Infrastructure.Notificaciones;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public const string Schema = "notifications";

    public DbSet<Notificacion> Notifications => Set<Notificacion>();

    public DbSet<BusquedaGuardada> BusquedasGuardadas => Set<BusquedaGuardada>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        ConfiguracionNotificacion.Configurar(modelBuilder);
        ConfiguracionBusquedaGuardada.Configurar(modelBuilder);
    }
}
