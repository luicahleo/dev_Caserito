using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaseritoApp.Reputation.Infrastructure;

public sealed class DesignTimeReputationDbContextFactory
    : IDesignTimeDbContextFactory<ReputationDbContext>
{
    public ReputationDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<ReputationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CaseritoAppDesign;Trusted_Connection=True;")
            .Options;
        return new ReputationDbContext(opciones);
    }
}
