using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Reputation.Infrastructure;

public sealed class ReputationDbContext(DbContextOptions<ReputationDbContext> options) : DbContext(options)
{
    public const string Schema = "reputation";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
