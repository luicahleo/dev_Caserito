using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure.Kyc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityDbContextBase = Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<
    CaseritoApp.Identity.Infrastructure.ApplicationUser,
    Microsoft.AspNetCore.Identity.IdentityRole<System.Guid>,
    System.Guid>;

namespace CaseritoApp.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContextBase(options)
{
    public const string Schema = "identity";

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<VerificacionKyc> VerificacionesKyc => Set<VerificacionKyc>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        ConfiguracionKyc.Configurar(builder);
    }
}
