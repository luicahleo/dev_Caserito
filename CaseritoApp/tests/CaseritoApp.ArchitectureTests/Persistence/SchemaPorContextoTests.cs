using CaseritoApp.Catalog.Infrastructure;
using CaseritoApp.Chat.Infrastructure;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Notifications.Infrastructure;
using CaseritoApp.Orders.Infrastructure;
using CaseritoApp.Reputation.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CaseritoApp.ArchitectureTests.Persistence;

public sealed class SchemaPorContextoTests
{
    [Theory]
    [MemberData(nameof(Contextos))]
    public void Cada_contexto_tiene_su_schema(DbContext contexto, string schemaEsperado)
    {
        using (contexto)
        {
            Assert.Equal(schemaEsperado, contexto.Model.GetDefaultSchema());
        }
    }

    public static TheoryData<DbContext, string> Contextos() => new()
    {
        { new IdentityDbContext(Opciones<IdentityDbContext>()), "identity" },
        { new CatalogDbContext(Opciones<CatalogDbContext>()), "catalog" },
        { new ChatDbContext(Opciones<ChatDbContext>()), "chat" },
        { new OrdersDbContext(Opciones<OrdersDbContext>()), "orders" },
        { new ReputationDbContext(Opciones<ReputationDbContext>()), "reputation" },
        { new NotificationsDbContext(Opciones<NotificationsDbContext>()), "notifications" },
    };

    private static DbContextOptions<T> Opciones<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseSqlServer("Server=noop;Database=noop;").Options;
}
