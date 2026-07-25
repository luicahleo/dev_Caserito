using CaseritoApp.Reputation.Domain.Resenas;
using CaseritoApp.Reputation.Infrastructure.Resenas;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Reputation.Infrastructure;

public sealed class ReputationDbContext(DbContextOptions<ReputationDbContext> options) : DbContext(options)
{
    public const string Schema = "reputation";

    public DbSet<Resena> Reviews => Set<Resena>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
        ConfiguracionResena.Configurar(modelBuilder);
    }
}
