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

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        MarcarRaicesKycModificadas();
        return base.SaveChangesAsync(cancellationToken);
    }

    // EF no bumpea la versión de la raíz cuando solo cambia una hija: la raíz no entra en el UPDATE
    // y su token no se chequea. Se incrementa el Version de la raíz para forzar ese UPDATE con el
    // chequeo de concurrencia, de modo que dos transacciones concurrentes sobre el mismo agregado
    // colisionen. Incrementar la propiedad la marca como modificada (y a la raíz como Modified).
    private void MarcarRaicesKycModificadas()
    {
        foreach (var hija in ChangeTracker.Entries<SolicitudKyc>().ToList())
        {
            if (hija.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var verificacionId = hija.Property<Guid?>("VerificacionKycId").CurrentValue;
            if (verificacionId is null)
            {
                continue;
            }

            var raiz = ChangeTracker.Entries<VerificacionKyc>()
                .FirstOrDefault(v => v.Entity.Id == verificacionId);

            // Hoy VerificacionKyc no tiene columnas escalares propias, así que la raíz solo pasa a
            // Modified mediante este incremento (por eso basta comprobar Unchanged). Si en el futuro
            // gana estado escalar propio y puede quedar Modified por otra razón, habría que cubrir
            // también ese caso para no dejar el token de concurrencia sin avanzar.
            if (raiz is { State: EntityState.Unchanged })
            {
                var version = raiz.Property<int>("Version");
                version.CurrentValue += 1;
            }
        }
    }
}
