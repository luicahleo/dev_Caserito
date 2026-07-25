using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaseritoApp.Orders.Infrastructure;

public sealed class DesignTimeOrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CaseritoApp;Trusted_Connection=True")
            .Options;
        return new OrdersDbContext(opciones);
    }
}
