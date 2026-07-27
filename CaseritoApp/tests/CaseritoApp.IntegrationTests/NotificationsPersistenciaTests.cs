using CaseritoApp.IntegrationTests.Infrastructure;
using CaseritoApp.Notifications.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.IntegrationTests;

public sealed class NotificationsPersistenciaTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Migraciones_Notifications_AplicanSinErrores()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await db.Database.MigrateAsync();
        Assert.True(await db.Database.CanConnectAsync());
    }
}
