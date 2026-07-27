using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaseritoApp.Notifications.Infrastructure;

public sealed class DesignTimeNotificationsDbContextFactory
    : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CaseritoAppDesign;Trusted_Connection=True;")
            .Options;
        return new NotificationsDbContext(opciones);
    }
}
