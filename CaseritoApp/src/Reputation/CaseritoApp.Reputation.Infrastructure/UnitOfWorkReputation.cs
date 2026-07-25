using CaseritoApp.BuildingBlocks.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Reputation.Infrastructure;

public sealed class UnitOfWorkReputation(ReputationDbContext db) : IUnitOfWork
{
    public async Task<int> GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            throw new ConflictoUnicidadReputationException(
                "Conflicto de unicidad al persistir Reputation.");
        }
    }
}
