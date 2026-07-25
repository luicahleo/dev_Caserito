using CaseritoApp.Orders.Domain.Ordenes;
using CaseritoApp.Orders.Infrastructure.Ordenes;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Orders.Infrastructure;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public const string Schema = "orders";

    public DbSet<Orden> Orders => Set<Orden>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        ConfiguracionOrden.Configurar(modelBuilder);
    }
}
