using CaseritoApp.Notifications.Domain.Busquedas;
using CaseritoApp.Notifications.Domain.Notificaciones;
using CaseritoApp.Notifications.Domain.PuntosEncuentro;
using CaseritoApp.Notifications.Domain.Push;
using CaseritoApp.Notifications.Infrastructure.Busquedas;
using CaseritoApp.Notifications.Infrastructure.Notificaciones;
using CaseritoApp.Notifications.Infrastructure.PuntosEncuentro;
using CaseritoApp.Notifications.Infrastructure.Push;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Notifications.Infrastructure;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public const string Schema = "notifications";

    public DbSet<Notificacion> Notifications => Set<Notificacion>();

    public DbSet<BusquedaGuardada> BusquedasGuardadas => Set<BusquedaGuardada>();

    public DbSet<PuntoEncuentroSeguro> PuntosEncuentroSeguros => Set<PuntoEncuentroSeguro>();
    public DbSet<SuscripcionPush> SuscripcionesPush => Set<SuscripcionPush>();
    public DbSet<IntencionPush> IntencionesPush => Set<IntencionPush>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        ConfiguracionNotificacion.Configurar(modelBuilder);
        ConfiguracionBusquedaGuardada.Configurar(modelBuilder);
        ConfiguracionPuntoEncuentroSeguro.Configurar(modelBuilder);
        ConfiguracionPush.Configurar(modelBuilder);
    }
}
