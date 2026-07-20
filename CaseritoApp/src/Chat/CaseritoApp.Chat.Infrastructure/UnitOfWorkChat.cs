using CaseritoApp.BuildingBlocks.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Chat.Infrastructure;

public sealed class UnitOfWorkChat(ChatDbContext db) : IUnitOfWork
{
    public async Task<int> GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            throw new ConflictoConcurrenciaException(
                "Conflicto de concurrencia al persistir los cambios.", ex);
        }
        catch (DbUpdateException ex) when (EsConflictoUnicidad(ex))
        {
            db.ChangeTracker.Clear();
            throw new ConflictoUnicidadChatException("Conflicto de unicidad al persistir Chat.");
        }
    }

    private static bool EsConflictoUnicidad(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
